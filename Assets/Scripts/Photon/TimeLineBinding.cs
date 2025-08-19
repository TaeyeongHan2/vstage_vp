using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.Linq;
using System.Collections;
using RootMotion.FinalIK;
using Fusion;

// NetworkBehaviour를 상속받아 Fusion 네트워크 기능 사용
public class NetworkedTimeLineBinding : NetworkBehaviour
{
    private GameObject vtuberParentObject;
    private VRIK vrIK;
    
    public PlayableDirector director;
    private GameObject vtuberObject;
    private Coroutine stopCoroutine;
    private bool isAutoBindingCompleted = false;
    
    // 네트워크 동기화를 위한 변수들
    [Networked] public NetworkBool IsTimelinePlaying { get; set; }
    [Networked] public float TimelineStartTick { get; set; }
    [Networked] public Vector3 SyncedStartPosition { get; set; }
    [Networked] public Quaternion SyncedStartRotation { get; set; }
    
    // 로컬 Transform 저장
    private Vector3 playStartPosition;
    private Quaternion playStartRotation;
    private Vector3 playStartScale;
    
    // 네트워크 러너 참조
    private NetworkRunner runner;
    
    public override void Spawned()
    {
        base.Spawned();
        
        // NetworkRunner 참조 저장
        runner = Runner;
        
        // Timeline 초기 설정
        if (director != null)
        {
            director.playOnAwake = false;
            director.extrapolationMode = DirectorWrapMode.Hold;
            director.timeUpdateMode = DirectorUpdateMode.GameTime;
        }
        else
        {
            Debug.LogError("PlayableDirector가 할당되지 않았습니다!");
            return;
        }
        
        // 10초 후 자동으로 Vtuber 바인딩
        StartCoroutine(AutoBindVtuberAfterDelay(10f));
    }
    
    private IEnumerator AutoBindVtuberAfterDelay(float delay)
    {
        Debug.Log($"게임 시작 - {delay}초 후 Vtuber 자동 바인딩 예정");
        
        yield return new WaitForSeconds(delay);
        
        // Vtuber 오브젝트 찾기
        vtuberObject = GameObject.FindWithTag("Vtuber");
        
        if (vtuberObject != null)
        {
            // Animator 컴포넌트 확인 및 설정
            Animator animator = vtuberObject.GetComponent<Animator>();
            if (animator == null)
            {
                animator = vtuberObject.AddComponent<Animator>();
                Debug.Log("Animator 컴포넌트가 추가되었습니다.");
            }
            
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            
            // Timeline 트랙 바인딩
            bool bindingSuccess = BindObject("Dance Animation Track", vtuberObject);
            
            if (bindingSuccess)
            {
                isAutoBindingCompleted = true;
                Debug.Log("✓ Vtuber 오브젝트가 Timeline에 성공적으로 바인딩되었습니다.");
            }
            else
            {
                Debug.LogError("Timeline 트랙 바인딩에 실패했습니다.");
            }
        }
        else
        {
            Debug.LogWarning("Vtuber 태그를 가진 오브젝트를 찾을 수 없습니다.");
        }
        
        vtuberParentObject = GameObject.FindWithTag("VtuberParent");
        
        if (vtuberParentObject != null)
        {
            vrIK = vtuberParentObject.GetComponentInChildren<VRIK>();
            
            if (vrIK != null)
            {
                Debug.Log($"VRIK 찾음: {vrIK.gameObject.name}");
            }
        }
    }
    
    // RPC를 통해 모든 클라이언트에서 Timeline 시작
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_StartTimeline(Vector3 startPos, Quaternion startRot)
    {
        Debug.Log($"[{(HasStateAuthority ? "HOST" : "CLIENT")}] Timeline 시작 RPC 수신");
        
        // 시작 위치 설정
        playStartPosition = startPos;
        playStartRotation = startRot;
        
        // 로컬에서 Timeline 시작
        StartTimelineLocal();
    }
    
    // RPC를 통해 모든 클라이언트에서 Timeline 정지
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_StopTimeline()
    {
        Debug.Log($"[{(HasStateAuthority ? "HOST" : "CLIENT")}] Timeline 정지 RPC 수신");
        StopTimelineLocal();
    }
    
    // 로컬 Timeline 시작 로직
    private void StartTimelineLocal()
    {
        if (stopCoroutine != null)
        {
            StopCoroutine(stopCoroutine);
            stopCoroutine = null;
        }
        
        if (vrIK != null)
            vrIK.enabled = false;
        
        // Timeline 활성화 및 초기화
        director.enabled = true;
        director.time = 0;
        
        // Animator의 Root Transform 오프셋 설정
        if (vtuberObject != null)
        {
            var animator = vtuberObject.GetComponent<Animator>();
            if (animator != null)
            {
                animator.rootPosition = playStartPosition;
                animator.rootRotation = playStartRotation;
            }
        }
        
        director.Evaluate();
        director.Play();
        
        Debug.Log($"▶ [{(HasStateAuthority ? "HOST" : "CLIENT")}] Timeline 재생 시작");
        Debug.Log($"  시작 위치: {playStartPosition}");
        Debug.Log($"  시작 회전: {playStartRotation.eulerAngles}");
        
        // 12초 후 자동 정지
        stopCoroutine = StartCoroutine(StopTimelineAfterDelay(12f));
    }
    
    // 로컬 Timeline 정지 로직
    private void StopTimelineLocal()
    {
        if (stopCoroutine != null)
        {
            StopCoroutine(stopCoroutine);
            stopCoroutine = null;
        }
        
        director.Stop();
        director.time = 0;
        director.enabled = false;
        
        if (vrIK != null)
            vrIK.enabled = true;
        
        Debug.Log($"■ [{(HasStateAuthority ? "HOST" : "CLIENT")}] Timeline 정지");
    }
    
    // Host만 호출하는 Timeline 토글 메서드
    public void ToggleTimelineNetworked()
    {
        // Host만 실행 가능
        if (!HasStateAuthority)
        {
            Debug.LogWarning("Timeline 제어는 Host만 가능합니다.");
            return;
        }
        
        if (!director.enabled || director.state != PlayState.Playing)
        {
            // 현재 Transform 저장
            SaveCurrentTransformAsStart();
            
            // 네트워크 상태 업데이트
            IsTimelinePlaying = true;
            TimelineStartTick = runner.Tick;
            SyncedStartPosition = playStartPosition;
            SyncedStartRotation = playStartRotation;
            
            // RPC로 모든 클라이언트에 시작 명령 전송
            RPC_StartTimeline(playStartPosition, playStartRotation);
        }
        else
        {
            // 네트워크 상태 업데이트
            IsTimelinePlaying = false;
            
            // RPC로 모든 클라이언트에 정지 명령 전송
            RPC_StopTimeline();
        }
    }
    
    private void SaveCurrentTransformAsStart()
    {
        if (vtuberObject != null)
        {
            playStartPosition = vtuberObject.transform.position;
            playStartRotation = vtuberObject.transform.rotation;
            playStartScale = vtuberObject.transform.localScale;
            Debug.Log($"재생 시작 Transform 저장 - 위치: {playStartPosition}, 회전: {playStartRotation.eulerAngles}");
        }
    }
    
    private IEnumerator StopTimelineAfterDelay(float delay)
    {
        float elapsedTime = 0;
        while (elapsedTime < delay)
        {
            elapsedTime += 1f;
            yield return new WaitForSeconds(1f);
            
            if (vtuberObject != null && elapsedTime % 3 == 0)
            {
                Debug.Log($"  재생 {elapsedTime:F0}초 - 현재 위치: {vtuberObject.transform.position}");
            }
        }
        
        // Host인 경우 네트워크로 정지 명령 전송
        if (HasStateAuthority)
        {
            IsTimelinePlaying = false;
            RPC_StopTimeline();
        }
        
        stopCoroutine = null;
    }
    
    public override void FixedUpdateNetwork()
    {
        // 네트워크 상태가 변경되었을 때 동기화
        // Late join한 클라이언트를 위한 처리
        if (!HasStateAuthority && IsTimelinePlaying)
        {
            // Timeline이 재생 중이어야 하는데 로컬에서 재생 중이 아닌 경우
            if (!director.enabled || director.state != PlayState.Playing)
            {
                // 동기화된 시작 위치 적용
                playStartPosition = SyncedStartPosition;
                playStartRotation = SyncedStartRotation;
                
                // Timeline 시작
                StartTimelineLocal();
                
                // 경과 시간 계산하여 Timeline 시간 동기화
                float elapsedTime = (runner.Tick - TimelineStartTick) * runner.DeltaTime;
                if (elapsedTime > 0 && elapsedTime < director.duration)
                {
                    director.time = elapsedTime;
                    Debug.Log($"Late Join 동기화: Timeline 시간을 {elapsedTime:F2}초로 설정");
                }
            }
        }
        else if (!HasStateAuthority && !IsTimelinePlaying)
        {
            // Timeline이 정지되어야 하는데 재생 중인 경우
            if (director.enabled && director.state == PlayState.Playing)
            {
                StopTimelineLocal();
            }
        }
    }
    
    void Update()
    {
        // Host만 S키 입력 처리
        if (HasStateAuthority && Input.GetKeyDown(KeyCode.S))
        {
            if (!isAutoBindingCompleted)
            {
                Debug.LogWarning("아직 자동 바인딩이 완료되지 않았습니다. 10초를 기다려주세요.");
                
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    Debug.Log("Shift+S: 강제 바인딩 시도...");
                    StartCoroutine(AutoBindVtuberAfterDelay(0f));
                }
            }
            else
            {
                ToggleTimelineNetworked();
            }
        }
        
        // 디버그 키는 모든 클라이언트에서 사용 가능
        if (Input.GetKeyDown(KeyCode.D))
        {
            DebugTimelineStatus();
        }
        
        if (Input.GetKeyDown(KeyCode.T))
        {
            if (vtuberObject != null)
            {
                Debug.Log($"=== [{(HasStateAuthority ? "HOST" : "CLIENT")}] 현재 Transform ===");
                Debug.Log($"위치: {vtuberObject.transform.position}");
                Debug.Log($"회전: {vtuberObject.transform.rotation.eulerAngles}");
                Debug.Log($"스케일: {vtuberObject.transform.localScale}");
            }
        }
    }
    
    private void DebugTimelineStatus()
    {
        Debug.Log($"=== [{(HasStateAuthority ? "HOST" : "CLIENT")}] Timeline 상태 ===");
        Debug.Log($"Director 활성화: {director.enabled}");
        Debug.Log($"재생 상태: {director.state}");
        Debug.Log($"현재 시간: {director.time:F2}초 / 전체: {director.duration:F2}초");
        Debug.Log($"자동 바인딩 완료: {isAutoBindingCompleted}");
        Debug.Log($"네트워크 재생 상태: {IsTimelinePlaying}");
        Debug.Log($"네트워크 시작 Tick: {TimelineStartTick}");
        
        if (vtuberObject != null)
        {
            Debug.Log($"Vtuber 현재 위치: {vtuberObject.transform.position}");
            Debug.Log($"Vtuber 현재 회전: {vtuberObject.transform.rotation.eulerAngles}");
        }
    }
    
    public bool BindObject(string trackName, GameObject target)
    {
        if (director == null || director.playableAsset == null)
        {
            Debug.LogError("Director 또는 PlayableAsset이 null입니다.");
            return false;
        }
        
        var timeline = director.playableAsset as TimelineAsset;
        if (timeline == null)
        {
            Debug.LogError("Timeline Asset을 가져올 수 없습니다.");
            return false;
        }
        
        var track = timeline.GetOutputTracks().FirstOrDefault(t => t.name == trackName);
        
        if (track != null)
        {
            if (track is AnimationTrack animTrack)
            {
                var animator = target.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = target.AddComponent<Animator>();
                }
                
                animTrack.trackOffset = TrackOffset.ApplySceneOffsets;
                director.SetGenericBinding(track, animator);
                
                var binding = director.GetGenericBinding(track);
                if (binding != null)
                {
                    Debug.Log($"✓ Animation Track '{trackName}' 바인딩 성공: {binding}");
                    return true;
                }
                else
                {
                    Debug.LogError($"바인딩 실패: {trackName}");
                    return false;
                }
            }
            else if (track is ActivationTrack)
            {
                director.SetGenericBinding(track, target);
                Debug.Log($"✓ Activation Track '{trackName}' 바인딩 성공");
                return true;
            }
            else
            {
                director.SetGenericBinding(track, target);
                Debug.Log($"✓ Track '{trackName}' 바인딩 성공 (Type: {track.GetType().Name})");
                return true;
            }
        }
        else
        {
            Debug.LogError($"트랙 '{trackName}'을 찾을 수 없습니다!");
            return false;
        }
    }
    
    // 헬퍼 메서드들
    public bool IsLocalTimelinePlaying()
    {
        return director.enabled && director.state == PlayState.Playing;
    }
    
    public double GetCurrentTime()
    {
        return director.time;
    }
    
    public void SetTime(double time)
    {
        director.time = time;
        director.Evaluate();
    }
}