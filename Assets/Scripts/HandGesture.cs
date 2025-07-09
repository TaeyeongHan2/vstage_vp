using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;

public class HandGesture : MonoBehaviour, IHandGesture
{
    #region Events
    public UnityEvent GesturePerformed;
    public UnityEvent GestureEnded;
    #endregion
    
    #region Fields
    [SerializeField] private XRHandTrackingEvents _handTrackingEvents;
    [SerializeField] private ScriptableObject _handShapeOrPose;
    [SerializeField] private Transform _targetTransform;
    [SerializeField] private float _minimumHoldTime = 0.2f;
    [SerializeField] private float _gestureDetectionInterval = 0.1f;
    [Tooltip("If true, the gesture will only be detected if the palm is facing upwards.")]
    [SerializeField] private bool _requirePalmUp = false;
    [Tooltip("How close to 'perfectly up' the palm must be. 1 is perfect, 0.8 is a good starting value.")]
    [SerializeField] private float _palmUpTolerance = 0.8f;

    private XRHandShape _handShape;
    private XRHandPose _handPose;
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
        _handShape = _handShapeOrPose as XRHandShape;
        _handPose = _handShapeOrPose as XRHandPose;
        if (_handPose != null && _handPose.relativeOrientation != null)
            _handPose.relativeOrientation.targetTransform = _targetTransform;
            
        if (_handShape == null && _handPose == null)
            Debug.LogError("HandGesture: A valid XRHandShape or XRHandPose must be assigned!", this);
    }
    
    private void OnEnable() 
    {
        if (_handTrackingEvents != null)
        {
            _handTrackingEvents.jointsUpdated.AddListener(OnJointsUpdated);
        }
        else
        {
            Debug.LogError("HandGesture: _handTrackingEvents is not assigned!", this);
        }
    }

    private void OnDisable() 
    {
        if (_handTrackingEvents != null)
        {
            _handTrackingEvents.jointsUpdated.RemoveListener(OnJointsUpdated);
        }
    }
    #endregion

    #region Private Methods
    public void OnJointsUpdated(XRHandJointsUpdatedEventArgs eventArgs) 
    {
        if (_isUpdateHandGestureDetectedFrame) 
        {
            return;
        }

        var detected = IsDetected(eventArgs);

        if (!_wasDetected && detected)
        {
            _holdStartTime = Time.timeSinceLevelLoad;
            GestureEnded?.Invoke(); // Clear previous gesture state if any
        }
        else if (_wasDetected && !detected)
        {
            _performedTriggered = false;
            GestureEnded?.Invoke();
        }

        _wasDetected = detected;

        if (!_performedTriggered && detected)
        {
            var holdTimer = Time.timeSinceLevelLoad - _holdStartTime;
            if (holdTimer > _minimumHoldTime)
            {
                GesturePerformed?.Invoke();
                _performedTriggered = true;
            }
        }

        _timeOfLastConditionCheck = Time.timeSinceLevelLoad;
    }

    private bool IsDetected(XRHandJointsUpdatedEventArgs eventArgs)
    {
        // 1. 손 추적 상태 확인
        bool isTracked = _handTrackingEvents.handIsTracked;
        if (!isTracked)
        {
            // 추적되지 않으면 바로 종료
            if (Time.frameCount % 180 == 0) // 로그가 너무 많이 쌓이지 않도록 조절
                Debug.Log("[제스처 분석] 1. 손 추적 실패. 감지를 중단합니다.");
            return false;
        }
        Debug.Log("[제스처 분석] 1. 손 추적 성공.");

        // 2. 손 모양 또는 포즈 확인
        bool shapeOrPoseDetected = false;
        if (_handShape != null)
        {
            shapeOrPoseDetected = _handShape.CheckConditions(eventArgs);
        }
        else if (_handPose != null)
        {
            shapeOrPoseDetected = _handPose.CheckConditions(eventArgs);
        }
        Debug.Log($"[제스처 분석] 2. 손 모양 일치: {shapeOrPoseDetected}");

        if (!shapeOrPoseDetected)
        {
            return false; // 모양이 다르면 바로 종료
        }

        // 3. 손바닥 방향 체크 확인
        if (!_requirePalmUp)
        {
            Debug.Log("[제스처 분석] 3. 손바닥 방향 체크 비활성화. >> 최종 성공.");
            return true; // 방향 체크가 필요 없으면 성공
        }
        
        Debug.Log("[제스처 분석] 3. 손바닥 방향 체크 활성화됨.");

        // 4. 손바닥 방향 계산
        var palm = eventArgs.hand.GetJoint(XRHandJointID.Palm);
        if (palm.TryGetPose(out Pose palmPose))
        {
            float dotUp = Vector3.Dot(palmPose.up, Vector3.up);
            bool isPalmUp = dotUp >= _palmUpTolerance;
            Debug.Log($"[제스처 분석] 4. 손바닥 방향 값(Dot Product): {dotUp:F2} (기준: {_palmUpTolerance} 이상) → 결과: {isPalmUp}");

            if (isPalmUp)
            {
                Debug.Log("[제스처 분석] >> 최종 성공.");
                return true; // 모양과 방향이 모두 정확
            }
        }
        else
        {
            Debug.Log("[제스처 분석] 4. 손바닥 관절(Palm) 위치를 가져올 수 없음.");
        }

        Debug.Log("[제스처 분석] >> 최종 실패 (손바닥 방향 불일치).");
        return false; // 모양은 맞았지만, 방향이 틀림
    }
    
    private string GetGestureName()
    {
        if (_handShapeOrPose != null)
            return _handShapeOrPose.name;
        return "Unknown";
    }
    #endregion
}