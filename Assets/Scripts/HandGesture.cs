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
    [Header("Gesture Asset")]
    [SerializeField] private ScriptableObject _handShapeOrPose;

    [Header("Configuration")]
    [SerializeField] private XRHandTrackingEvents _handTrackingEvents;
    [SerializeField] private float _minimumHoldTime = 0.2f;
    [SerializeField] private float _gestureDetectionInterval = 0.1f;

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
        if (_handShapeOrPose == null)
        {
            Debug.LogError("HandGesture: A valid XRHandShape or XRHandPose must be assigned!", this);
            return;
        }

        _handShape = _handShapeOrPose as XRHandShape;
        _handPose = _handShapeOrPose as XRHandPose;

        if (_handShape == null && _handPose == null)
            Debug.LogError($"HandGesture: The assigned asset '{_handShapeOrPose.name}' is not a valid XRHandShape or XRHandPose.", this);
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
        if (_isUpdateHandGestureDetectedFrame || _handShapeOrPose == null) 
        {
            return;
        }

        var detected = IsDetected(eventArgs);

        if (!_wasDetected && detected)
        {
            _holdStartTime = Time.timeSinceLevelLoad;
        }
        else if (_wasDetected && !detected)
        {
            if (_performedTriggered)
            {
                Debug.Log($"[제스처] '{_handShapeOrPose.name}' 제스처 종료됨.");
                GestureEnded?.Invoke();
            }
            _performedTriggered = false;
        }

        _wasDetected = detected;

        if (!_performedTriggered && detected)
        {
            var holdTimer = Time.timeSinceLevelLoad - _holdStartTime;
            if (holdTimer > _minimumHoldTime)
            {
                Debug.Log($"[제스처] '{_handShapeOrPose.name}' 제스처 수행됨! (유지 시간: {holdTimer:F2}초)");
                GesturePerformed?.Invoke();
                _performedTriggered = true;
            }
        }

        _timeOfLastConditionCheck = Time.timeSinceLevelLoad;
    }

    private bool IsDetected(XRHandJointsUpdatedEventArgs eventArgs)
    {
        if (!_handTrackingEvents.handIsTracked)
        {
            return false;
        }

        bool isConditionMet = false;
        if (_handShape != null)
        {
            isConditionMet = _handShape.CheckConditions(eventArgs);
        }
        else if (_handPose != null)
        {
            isConditionMet = _handPose.CheckConditions(eventArgs);
        }

        // 매 30프레임마다 (약 0.5초) 로그를 출력하여 콘솔이 너무 복잡해지는 것을 방지합니다.
        if (Time.frameCount % 30 == 0)
        {
            Debug.Log($"[제스처 실시간 체크] 에셋: '{_handShapeOrPose.name}' / 현재 손 모양 일치 여부: {isConditionMet}");
        }

        return isConditionMet;
    }
    #endregion
}