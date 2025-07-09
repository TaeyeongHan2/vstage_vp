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
        bool isTracked = _handTrackingEvents.handIsTracked;
        if (!isTracked) return false;

        bool shapeOrPoseDetected = false;
        if (_handShape != null)
        {
            shapeOrPoseDetected = _handShape.CheckConditions(eventArgs);
        }
        else if (_handPose != null)
        {
            shapeOrPoseDetected = _handPose.CheckConditions(eventArgs);
        }
        
        Debug.Log($"[제스처 분석] 1. 손 추적 성공. / 2. 에셋 조건 일치: {shapeOrPoseDetected}");

        // 에셋 조건이 실패했을 때만 상세 디버그 로그 출력
        if (!shapeOrPoseDetected && Time.frameCount % 10 == 0) // 로그가 너무 많지 않게 조절
        {
            var palm = eventArgs.hand.GetJoint(XRHandJointID.Palm);
            if (palm.TryGetPose(out Pose palmPose))
            {
                float dotUp = Vector3.Dot(palmPose.up, Vector3.up);
                Debug.Log($"[실시간 손바닥 방향] up-vector: {palmPose.up.ToString("F2")}, dotUp: {dotUp:F2}, rotation: {palmPose.rotation.eulerAngles.ToString("F1")}");
            }
        }

        if (!shapeOrPoseDetected) return false;
        
        // 에셋 조건이 통과했을 경우, 추가적인 'requirePalmUp' 로직 (필요 시 사용)
        if (!_requirePalmUp)
        {
            return true; 
        }
        
        var palmForCheck = eventArgs.hand.GetJoint(XRHandJointID.Palm);
        if (palmForCheck.TryGetPose(out Pose palmPoseForCheck))
        {
            float dotUp = Vector3.Dot(palmPoseForCheck.up, Vector3.up);
            if (dotUp >= _palmUpTolerance)
            {
                return true;
            }
        }

        return false;
    }
    
    private string GetGestureName()
    {
        if (_handShapeOrPose != null)
            return _handShapeOrPose.name;
        return "Unknown";
    }
    #endregion
}