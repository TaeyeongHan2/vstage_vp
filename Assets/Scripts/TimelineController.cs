using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

public class TimelineController : NetworkBehaviour
{
    [Header("키 설정")] public KeyCode toggleKey = KeyCode.M;
        

    public PlayableDirector timeline;
    
    void Start()
    {
        timeline.Stop();
    }
    
    private void Update()
    {
        if (HasStateAuthority && Input.GetKeyDown(toggleKey))
        {
            StarTimelineRPC(Runner.Tick);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void StarTimelineRPC(int networkTick)
    {
        timeline.Play();
        Debug.Log("Timeline 시작!");
    }
}