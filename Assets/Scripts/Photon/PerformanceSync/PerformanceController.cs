using Fusion;
using UnityEngine;

public class PerformanceController : NetworkBehaviour
{
    [Header("AI 음성 송신 트리거 타임(초)")] 
    public float aiSendTriggerTime = 100f;
    
    [Networked] public int ShowStartNetworkTick { get; set; }      // Tick 값은 int
    private bool isShowStartedLocally = false;
    
    //클라이언트가 AI 서버에 송신 요청시 필요한 flag
    private bool aiSendRequestDone = false;
    
    private void Update()
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
            var voiceClient = FindObjectOfType<WebSocketVoiceClient>();
            if (voiceClient != null && voiceClient.IsTriggerConnected)
                voiceClient.SendGaugeSignal();
            else
                Debug.LogWarning("관객: VoiceClient 준비 안됨, 송신 실패");
        }
    }
}