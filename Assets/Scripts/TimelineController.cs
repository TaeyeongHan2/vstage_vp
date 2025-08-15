using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

public class TimelineController : NetworkBehaviour
{
    // [Header("키 설정")] public KeyCode toggleKey = KeyCode.M;
    
    [Header("Timeline 시작 지연(초)")]
    public float startDelaySeconds = 5f;

    [Header("참조")]
    public PerformanceController perf;
    public PlayableDirector timeline;
    
    private bool started;
    private int targetTick; // Timeline을 시작해야 하는 네트워크 Tick(모두 동일)
    
    public override void Spawned()
    {
        base.Spawned();
        started = false;
        targetTick = 0;
        if (timeline) timeline.Stop();
    }

    public override void Render()
    {
        if (started || timeline == null || perf == null) return;

        // 공연이 아직 시작 안했으면 대기
        if (perf.ShowStartNetworkTick <= 0) return;

        // 목표 Tick 1회 계산
        if (targetTick == 0)
        {
            // 5초 → Tick으로 변환
            int delayTicks = Mathf.CeilToInt(startDelaySeconds / Runner.DeltaTime);
            targetTick = perf.ShowStartNetworkTick + delayTicks;
        }

        // 지금 Tick이 목표 Tick을 지났는지 체크
        int ticksLate = Runner.Tick - targetTick;
        if (ticksLate >= 0)
        {
            // 지연 보정: 이미 지난 틱만큼 타임라인 시간을 앞당겨 정확히 맞춤
            double elapsedSec = ticksLate * Runner.DeltaTime;
            
            timeline.time = elapsedSec;   // 0이면 정확히 동시에 시작, 양수면 보정 시작
            timeline.Play();
            started = true;
            Debug.Log($"Timeline started with offset: {elapsedSec:F3}s (ticksLate={ticksLate})");
            Debug.Log($"[Timeline] 시작됨 | elapsedSec={elapsedSec:F3}s | Runner.Tick={Runner.Tick}");
        }
    }
    
    // void Start()
    // {
    //     timeline.Stop();
    // }
    
    // private void Update()
    // {
    //     if (HasStateAuthority && Input.GetKeyDown(toggleKey))
    //     {
    //         StarTimelineRPC(Runner.Tick);
    //     }
    // }

    // [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    // public void StarTimelineRPC(int networkTick)
    // {
    //     timeline.Play();
    //     Debug.Log("Timeline 시작!");
    // }
}