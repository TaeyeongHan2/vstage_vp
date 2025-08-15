using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.Playables;

public class PerformanceController : NetworkBehaviour
{
    [Header("AI 음성 송신 트리거 타임(초)")] 
    public float aiSendTriggerTime = 33f;
    
    [Header("AI 텍스트 표시 타이밍(초)")]
    public float aiDisplayTime = 39f;    // 화면에 띄울 시점
   
    // [Header("가사·텍스트 데이터")]
    // public CueData cueData;  

    [Networked] public int ShowStartNetworkTick { get; set; }      // Tick 값은 int
    private bool isShowStartedLocally = false;
    
    //클라이언트가 AI 서버에 송신 요청시 필요한 flag
    private bool aiSendRequestDone = false;
    private bool aiDisplayDone = false;
    private int nextCueIndex = 0;
    
    [SerializeField] private WebSocketVoiceClient _webSocketVoiceClient;
    [SerializeField] private TMP_PRO tmpPro;

    private bool isSpawnReady = false;
    private bool isSpacePressed;
    
    [SerializeField] private PlayableDirector timeline;   // ← 타임라인 상태 확인용(인스펙터에서 할당)
    private bool timelineStartCheckLogged = false;        // ← 5초 체크 로그 1회용

    
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

        // Host만 공연 시작 입력 받음(공연 시작은 호스트만 트리거)
        if (HasStateAuthority && !isShowStartedLocally && isSpacePressed)
        {
            isSpacePressed = false;

            Debug.Log("[호스트] Space 입력, RPC 호출");
            // 올바른 Tick 획득
            StartShowRPC(Runner.Tick);
        }

        // 공연 시작신호 받았으면 경과시간 출력
        if (isShowStartedLocally)
        {
            int elapsedTicks = Runner.Tick - ShowStartNetworkTick;
            float elapsedSec = elapsedTicks * Runner.DeltaTime;
            Debug.Log($"쇼 시작 후 경과시간: {elapsedSec:N2}초");
            
            // ✅ (추가) 공연 시작 후 5초 시점에 타임라인 실행 상태 확인 로그
            if (!timelineStartCheckLogged && elapsedSec >= 5f)
            {
                timelineStartCheckLogged = true;

                // 이 시점에 로컬 타임라인이 실행 중인지(동기 시작됐는지) 확인
                bool isTimelinePlaying = (timeline != null && timeline.state == PlayState.Playing);

                // 참고용: 이 기기에서 예상되는 타임라인 경과(초) = (현재Tick - 타임라인 시작Tick) * dt
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

            // 36초에 RPC로 AI 송신 요청
            if (!aiSendRequestDone && elapsedSec >= aiSendTriggerTime)
            {
                aiSendRequestDone = true;
                Debug.Log("AI 송신 트리거 RPC 전송!");
                RequestAISendRPC();
            }

            // 2) 39초에 AI 텍스트 표시 RPC
            if (!aiDisplayDone && elapsedSec >= aiDisplayTime)
            {
                aiDisplayDone = true;
                Debug.Log(aiSendRequestDone);
                Debug.Log( $"{elapsedSec >= aiSendTriggerTime}");
                Debug.Log("AI 텍스트 표시 RPC 전송!");
                DisplayAITextRPC();
            }

            // Cue 순차 처리
            // if (HasStateAuthority && nextCueIndex < cueData.cues.Count)
            // {
            //     var cue = cueData.cues[nextCueIndex];
            //     if (elapsedSec >= cue.time)
            //     {
            //         TriggerCueRPC(nextCueIndex);
            //         nextCueIndex++;
            //     }
            // }
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
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RequestAISendRPC()
    {
        if (!isSpawnReady) return;
        
        Debug.Log("관객 및 호스트: AI 서버에 음성 송신 요청 트리거 수신!");
        //36초의 RPC의 실제 AI 송신 실행은 관객만 실행
         if (!HasStateAuthority) 
         {
             if (_webSocketVoiceClient&& _webSocketVoiceClient.IsTriggerConnected)
             {
                 Debug.Log("RequestAISendRPC");
                 _webSocketVoiceClient.SendGaugeSignal();
             }
             else
             {
                 Debug.LogWarning("관객: VoiceClient 준비 안됨, 송신 실패");
             }
         }
        
       
        // if (_webSocketVoiceClient&& _webSocketVoiceClient.IsTriggerConnected)
        // {
        //     Debug.Log("RequestAISendRPC");
        //     _webSocketVoiceClient.SendGaugeSignal();
        // }
        // else
        // {
        //     Debug.LogWarning("관객: VoiceClient 준비 안됨, 송신 실패");
        // }
        
    }
    
    // Host→All RPC로, 모든 클라이언트가 39초에 이 함수 실행
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void DisplayAITextRPC()
    {
        if (!isSpawnReady) return;
        Debug.Log("[All] AI 텍스트 표시 트리거 수신");
        // TMP_PRO 컴포넌트를 찾아서 UpdateText() 호출
        
        if (tmpPro)
            tmpPro.UpdateText();
        else
            Debug.LogWarning("TMP_PRO를 찾을 수 없습니다.");
    }
    
    // [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    // void TriggerCueRPC(int cueIndex)
    // {
    //     var cue = cueData.cues[cueIndex];
    //     Debug.Log($"[Cue] {cue.time}s → {cue.text}");
    //     // TODO: 실제 UI 표시
    //     // 예: UIManager.Instance.ShowLyric(cue.text);
    // }
}