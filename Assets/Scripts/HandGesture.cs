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
        return _handTrackingEvents.handIsTracked &&
               _handShape != null && _handShape.CheckConditions(eventArgs) ||
               _handPose != null && _handPose.CheckConditions(eventArgs);
    }
    #endregion
}
