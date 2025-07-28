using Fusion;
using UnityEngine;

public class PerformanceController : NetworkBehaviour
{
    [Networked] public int ShowStartNetworkTick { get; set; }      // Tick 값은 int
    private bool isShowStartedLocally = false;

    private void Update()
    {
        // Host만 공연 시작 입력 받음
        if (HasStateAuthority && !isShowStartedLocally && Input.GetKeyDown(KeyCode.Space))
        {
            // 올바른 Tick 획득
            StartShowRPC(Runner.Tick);
        }

        // 공연 시작신호 받았으면 경과시간 출력
        if (isShowStartedLocally)
        {
            int elapsedTicks = Runner.Tick - ShowStartNetworkTick;
            float elapsedSec = elapsedTicks * Runner.DeltaTime;
            Debug.Log($"쇼 시작 후 경과시간: {elapsedSec:N2}초");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void StartShowRPC(int networkTick)
    {
        ShowStartNetworkTick = networkTick;
        isShowStartedLocally = true;
        Debug.Log("관객 - StartShowRPC 호출됨! networkTick:" + networkTick);
    }
}