using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;

public class StaticHandGesture : MonoBehaviour, IHandGesture
{
    #region Events
    public UnityEvent GesturePerformed;
    public UnityEvent GestureEnded;
    #endregion
    
    #region Fields
    [SerializeField] private XRHandTrackingEvents _handTrackingEvents;
    [SerializeField] private XRHandShape _handShape; 
    [SerializeField] private Transform _targetTransform;
    [SerializeField] private float _minimumHoldTime = 0.2f;
    [SerializeField] private float _gestureDetectionInterval = 0.1f;
    
    private bool _wasDetected;
    private bool _performedTriggered;
    private float _timeOfLastConditionCheck;
    private float _holdStartTime;
    
    private bool _isUpdateHandGestureDetectedFrame 
        => !isActiveAndEnabled ||
           Time.timeSinceLevelLoad < _timeOfLastConditionCheck + _gestureDetectionInterval;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        if (_targetTransform == null)
        {
            Debug.LogError("[StaticHandGesture] Target Transform이 할당되지 않았습니다!");
        }
        
        Debug.Log("[StaticHandGesture] 1단계 테스트: 거리만 체크 (Shape 무시)");
    }
    
    private void OnEnable() => _handTrackingEvents.jointsUpdated.AddListener(OnJointsUpdated);
    private void OnDisable() => _handTrackingEvents.jointsUpdated.RemoveListener(OnJointsUpdated);
    #endregion

    #region Private Methods
    private void OnJointsUpdated(XRHandJointsUpdatedEventArgs eventArgs)
    {
        if (_isUpdateHandGestureDetectedFrame) return;

        var detected = IsDetected(eventArgs);

        if (!_wasDetected && detected)
        {
            _holdStartTime = Time.timeSinceLevelLoad;
            Debug.Log("🟢 [제스처] 감지 시작!");
        }
        else if (_wasDetected && !detected)
        {
            _performedTriggered = false;
            GestureEnded?.Invoke();
            Debug.Log("🔴 [제스처] 종료!");
        }

        _wasDetected = detected;

        if (!_performedTriggered && detected)
        {
            var holdTimer = Time.timeSinceLevelLoad - _holdStartTime;
            if (holdTimer > _minimumHoldTime)
            {
                GesturePerformed?.Invoke();
                _performedTriggered = true;
                Debug.Log($"✅ [제스처] 수행! (홀드 시간: {holdTimer:F2}초)");
            }
        }

        _timeOfLastConditionCheck = Time.timeSinceLevelLoad;
    }

    private bool IsDetected(XRHandJointsUpdatedEventArgs eventArgs)
    {
        // 핸드 트래킹 확인
        if (!_handTrackingEvents.handIsTracked)
        {
            Debug.Log("❌ [핸드트래킹] 비활성화");
            return false;
        }

        // 거리 체크만 수행 (10cm ~ 35cm)
        var joint = eventArgs.hand.GetJoint(XRHandJointID.MiddleMetacarpal);

        if (joint.TryGetPose(out Pose p))
        {
            float dist = Vector3.Distance(p.position, _targetTransform.position);
            bool distanceOk = dist >= 0.10f && dist <= 0.35f;
            
            string status = distanceOk ? "✅ OK" : "❌ NG";
            Debug.Log($"📏 [거리] {dist:F3}m → {status} (범위: 10-35cm)");
            
            return distanceOk;
        }
        else
        {
            Debug.Log("❌ [거리] 중지 기저관절 위치를 가져올 수 없음");
            return false;
        }
    }
    #endregion
}