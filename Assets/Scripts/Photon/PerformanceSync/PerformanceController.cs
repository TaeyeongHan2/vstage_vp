using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.Playables;

public class PerformanceController : NetworkBehaviour
{
    [Header("AI 음성 송신 트리거 타임(초)")] 
    public float aiSendTriggerTime = 33f;
    
    [Header("AI 텍스트 표시 타이밍(초)")]
    public float aiDisplayTime = 34f;

    [Networked] public int ShowStartNetworkTick { get; set; }
    private bool isShowStartedLocally = false;
    
    private bool aiSendRequestDone = false;
    private bool aiDisplayDone = false;
    private int nextCueIndex = 0;
    
    [SerializeField] private WebSocketVoiceClient _webSocketVoiceClient;
    [SerializeField] private TMP_PRO tmpPro;

    private bool isSpawnReady = false;
    private bool isSpacePressed;
    
    [SerializeField] private PlayableDirector timeline;
    private bool timelineStartCheckLogged = false;

    public override void Spawned()
    {
        base.Spawned();
        isSpawnReady = true;
        ShowStartNetworkTick = 0;
        
        Debug.Log($"[{nameof(PerformanceController)}] 생성 완료");
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isSpacePressed = true;
        }
    }

    public override void Render()
    {
        if (!isSpawnReady) return;

        // Host만 공연 시작 입력 받음
        if (HasStateAuthority && !isShowStartedLocally && isSpacePressed)
        {
            isSpacePressed = false;
            Debug.Log("[호스트] Space 입력, RPC 호출");
            StartShowRPC(Runner.Tick);
        }

        // 공연 시작신호 받았으면 경과시간 출력
        if (isShowStartedLocally)
        {
            int elapsedTicks = Runner.Tick - ShowStartNetworkTick;
            float elapsedSec = elapsedTicks * Runner.DeltaTime;
            Debug.Log($"쇼 시작 후 경과시간: {elapsedSec:N2}초");
            
            // 공연 시작 후 5초 시점에 타임라인 실행 상태 확인 로그
            if (!timelineStartCheckLogged && elapsedSec >= 5f)
            {
                timelineStartCheckLogged = true;

                bool isTimelinePlaying = (timeline != null && timeline.state == PlayState.Playing);
                int delayTicks = Mathf.CeilToInt(5f / Runner.DeltaTime);
                int timelineStartTick = ShowStartNetworkTick + delayTicks;
                double expectedTimelineElapsed = Mathf.Max(0, Runner.Tick - timelineStartTick) * Runner.DeltaTime;
                double actualTime = (timeline != null) ? timeline.time : -1.0;

                Debug.Log(
                    $"[Check@5s] Timeline Playing={isTimelinePlaying} | " +
                    $"expectedElapsed={expectedTimelineElapsed:F3}s | actualTimeline.time={actualTime:F3}s | " +
                    $"nowTick={Runner.Tick}, startTick={ShowStartNetworkTick}, startDelayTicks={delayTicks}"
                );
            }

            // 33초에 AI 서버에 키워드 분석 요청 (호스트만 gauge_full 신호)
            if (!aiSendRequestDone && elapsedSec >= aiSendTriggerTime && HasStateAuthority)
            {
                aiSendRequestDone = true;
                Debug.Log("33초 도달 - Host가 AI 서버에 키워드 분석 요청!");
                
                if (_webSocketVoiceClient && _webSocketVoiceClient.IsTriggerConnected)
                {
                    _webSocketVoiceClient.SendGaugeSignal();
                    Debug.Log("[Host] gauge_full 신호 전송 완료 - AI 서버가 키워드 분석 시작");
                }
                else
                {
                    Debug.LogWarning("Host: AI 서버 Trigger 연결 안됨, 키워드 요청 실패");
                }
            }

            // 39초에 AI 텍스트 표시 RPC (Host가 모든 클라이언트에게 전송)
            if (!aiDisplayDone && elapsedSec >= aiDisplayTime && HasStateAuthority)
            {
                aiDisplayDone = true;
                Debug.Log("AI 텍스트 표시 RPC 전송!");
                
                // 39초에 AI 데이터를 다시 한 번 모든 클라이언트에게 전송
                if (AIResponseStore.Instance != null && 
                    AIResponseStore.Instance.LatestKeywords.Count > 0)
                {
                    string[] keywords = AIResponseStore.Instance.LatestKeywords.ToArray();
                    string[] emotions = AIResponseStore.Instance.LatestEmotions.ToArray();
                    
                    Debug.Log($"[Host] 39초 전송할 데이터 - Keywords: {string.Join(", ", keywords)}");
                    Debug.Log($"[Host] 39초 전송할 데이터 - Emotions: {string.Join(", ", emotions)}");
                    
                    SendAIDataToAllClientsRPC(keywords, emotions);
                    Debug.Log("[Host] 39초에 AI 데이터 재전송 완료");
                }
                else
                {
                    Debug.LogWarning("[Host] 39초 시점에 AIResponseStore가 null이거나 키워드가 비어있음!");
                    if (AIResponseStore.Instance == null)
                        Debug.LogError("[Host] AIResponseStore.Instance가 null!");
                    else
                        Debug.LogWarning($"[Host] 키워드 개수: {AIResponseStore.Instance.LatestKeywords.Count}");
                }
                
                DisplayAITextRPC();
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void StartShowRPC(int networkTick)
    {
        if (!isSpawnReady)
        {
            Debug.LogWarning("Spawned 전 RPC 수신: 동작 보류");
            return;
        }
        Debug.Log($"[호스트/클라이언트] StartShowRPC 호출됨: Tick: {networkTick}");
        ShowStartNetworkTick = networkTick;
        isShowStartedLocally = true;
        aiSendRequestDone = false;
    }
    
    // Host가 AI 데이터를 모든 클라이언트에게 하나씩 전송하는 RPC
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void SendSingleAIDataRPC(int index, string keyword, string emotion)
    {
        string deviceType = HasStateAuthority ? "Host" : "Client";
        
        if (!isSpawnReady) 
        {
            Debug.LogWarning($"[{deviceType}] SendSingleAIDataRPC - isSpawnReady가 false, 처리 중단");
            return;
        }
        
        Debug.Log($"[{deviceType}] AI 데이터 수신 [{index}] - Keyword: {keyword}, Emotion: {emotion}");
        
        if (AIResponseStore.Instance != null)
        {
            // 인덱스에 맞게 데이터 저장 (리스트 크기 확장)
            while (AIResponseStore.Instance.LatestKeywords.Count <= index)
            {
                AIResponseStore.Instance.LatestKeywords.Add("");
            }
            while (AIResponseStore.Instance.LatestEmotions.Count <= index)
            {
                AIResponseStore.Instance.LatestEmotions.Add("");
            }
            
            // 해당 인덱스에 데이터 저장
            AIResponseStore.Instance.LatestKeywords[index] = keyword;
            AIResponseStore.Instance.LatestEmotions[index] = emotion;
            
            Debug.Log($"[{deviceType}] 인덱스 [{index}] 저장 완료 - {keyword}/{emotion}");
        }
        else
        {
            Debug.LogError($"[{deviceType}] AIResponseStore.Instance가 null입니다!");
        }
    }

    // 기존 SendAIDataToAllClientsRPC를 하나씩 전송하는 방식으로 변경
    public void SendAIDataToAllClientsRPC(string[] keywords, string[] emotions)
    {
        if (!HasStateAuthority) return;
        
        Debug.Log($"[Host] AI 데이터를 하나씩 전송 시작 - Keywords: {keywords.Length}개, Emotions: {emotions.Length}개");
        
        // 최대 20개까지 하나씩 전송
        int maxCount = Mathf.Min(20, Mathf.Min(keywords.Length, emotions.Length));
        
        for (int i = 0; i < maxCount; i++)
        {
            string keyword = i < keywords.Length ? keywords[i] : "";
            string emotion = i < emotions.Length ? emotions[i] : "";
            
            SendSingleAIDataRPC(i, keyword, emotion);
            Debug.Log($"[Host] [{i}] 전송: {keyword} / {emotion}");
        }
        
        Debug.Log($"[Host] AI 데이터 하나씩 전송 완료 - 총 {maxCount}개");
    }

    // Host→All RPC로, 모든 클라이언트가 39초에 이 함수 실행
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void DisplayAITextRPC()
    {
        if (!isSpawnReady) return;
        Debug.Log("[All] AI 텍스트 표시 트리거 수신");
        
        if (tmpPro)
            tmpPro.UpdateText();
        else
            Debug.LogWarning("TMP_PRO를 찾을 수 없습니다.");
    }
}