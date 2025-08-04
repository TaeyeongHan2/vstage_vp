using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class PerformanceController : NetworkBehaviour
{
    [Header("AI 음성 송신 트리거 타임(초)")] 
    public float aiSendTriggerTime = 36f;
    
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
    
    public override void Spawned()
    {
        ShowStartNetworkTick = 0;
    }
    
    public override void Render()
    {
        // Host만 공연 시작 입력 받음(공연 시작은 호스트만 트리거)
        if (HasStateAuthority && !isShowStartedLocally && Input.GetKeyDown(KeyCode.Space))
        {
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
        Debug.Log($"[호스트/클라이언트] StartShowRPC 호출됨: Tick: {networkTick}");
        ShowStartNetworkTick = networkTick;
        isShowStartedLocally = true;
        aiSendRequestDone = false;
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RequestAISendRPC()
    {
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
    }
    
    // Host→All RPC로, 모든 클라이언트가 39초에 이 함수 실행
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void DisplayAITextRPC()
    {
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