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
            
        // 초기 설정 디버그 로그
        if (_handShape != null)
            Debug.Log($"HandGesture: HandShape '{_handShape.name}' 설정됨");
        if (_handPose != null)
            Debug.Log($"HandGesture: HandPose '{_handPose.name}' 설정됨");
    }
    
    private void Update()
    {
        if (_handTrackingEvents != null)
        {
            Debug.Log($"handIsTracked222: {_handTrackingEvents.subsystem.leftHand.isTracked}");
            Debug.Log($"handIsTracked111: {_handTrackingEvents.handIsTracked}");
            Debug.Log($"subsystem: {_handTrackingEvents.subsystem}");
            Debug.Log($"running: {_handTrackingEvents.subsystem?.running}");
        }
    }
    
    private void OnEnable() 
    {
        if (_handTrackingEvents != null)
        {
            _handTrackingEvents.jointsUpdated.AddListener(OnJointsUpdated);
            Debug.Log($"HandGesture: jointsUpdated 리스너 등록됨");
        }
        else
        {
            Debug.LogError("HandGesture: _handTrackingEvents가 null입니다!");
        }
    }
    
    // private void OnEnable()
    // {
    //     if (_handTrackingEvents == null)
    //     {
    //         Debug.LogError("HandGesture: _handTrackingEvents가 null입니다!");
    //         return;
    //     }
    //
    //     _handTrackingEvents.trackingAcquired.AddListener(() =>
    //         Debug.Log("[Gesture] TrackingAcquired fired → handIsTracked should now be TRUE"));
    //
    //     _handTrackingEvents.trackingLost.AddListener(() =>
    //         Debug.Log("[Gesture] TrackingLost fired → handIsTracked should now be FALSE"));
    //
    //     // 기존 jointsUpdated 리스너
    //     _handTrackingEvents.jointsUpdated.AddListener(OnJointsUpdated);
    // }


    private void OnDisable() 
    {
        if (_handTrackingEvents != null)
        {
            _handTrackingEvents.jointsUpdated.RemoveListener(OnJointsUpdated);
            Debug.Log($"HandGesture: jointsUpdated 리스너 해제됨");
        }
        else
        {
            Debug.Log($"HandGesture: jointsUpdated 리스너 해제 실패");
        }
    }
    #endregion

    #region Private Methods
    public void OnJointsUpdated(XRHandJointsUpdatedEventArgs eventArgs) 
    {
        Debug.Log("OnJointsUpdated 들어옴");
        if (_isUpdateHandGestureDetectedFrame) 
        {
            // 너무 자주 출력되지 않도록 가끔씩만 로그
            if (Time.frameCount % 300 == 0) // 약 5초마다 한 번
                Debug.Log($"HandGesture: 감지 간격 대기 중... (프레임: {Time.frameCount})");
            return;
        }

        var detected = IsDetected(eventArgs);
        
        // 핸드 트래킹 상태 로그 (가끔씩)
        if (Time.frameCount % 300 == 0)
        {
            Debug.Log($"HandGesture: 핸드 트래킹 상태 - handIsTracked: {_handTrackingEvents.handIsTracked}, detected: {detected}");
        }

        if (!_wasDetected && detected)
        {
            _holdStartTime = Time.timeSinceLevelLoad;
            // 제스처가 처음 감지되었을 때 로그
            Debug.Log($"HandGesture: {GetGestureName()} 🟢제스처 감지 시작!");
        }
        else if (_wasDetected && !detected)
        {
            _performedTriggered = false;
            // 제스처가 끝났을 때 로그
            Debug.Log($"HandGesture: {GetGestureName()} 🔴제스처 감지 종료");
            GestureEnded?.Invoke();
        }

        _wasDetected = detected;

        if (!_performedTriggered && detected)
        {
            var holdTimer = Time.timeSinceLevelLoad - _holdStartTime;
            Debug.Log($"HandGesture: {GetGestureName()} 제스처 유지 중... (시간: {holdTimer:F2}초 / 필요: {_minimumHoldTime:F2}초)");
            
            if (holdTimer > _minimumHoldTime)
            {
                // 제스처가 최소 시간 이상 유지되어 수행되었을 때 로그
                Debug.Log($"HandGesture: {GetGestureName()} 제스처 수행됨! (유지 시간: {holdTimer:F2}초)");
                GesturePerformed?.Invoke();
                _performedTriggered = true;
            }
        }

        _timeOfLastConditionCheck = Time.timeSinceLevelLoad;
    }

    private bool IsDetected(XRHandJointsUpdatedEventArgs eventArgs)
    {
        bool handShapeDetected = false;
        bool handPoseDetected = false;
        
        if (_handShape != null)
        {
            handShapeDetected = _handShape.CheckConditions(eventArgs);
            // 가끔씩 HandShape 감지 상태 로그
            if (Time.frameCount % 300 == 0)
                Debug.Log($"HandGesture: HandShape '{_handShape.name}' 감지 상태: {handShapeDetected}");
        }
        
        if (_handPose != null)
        {
            handPoseDetected = _handPose.CheckConditions(eventArgs);
            // 가끔씩 HandPose 감지 상태 로그
            if (Time.frameCount % 300 == 0)
                Debug.Log($"HandGesture: HandPose '{_handPose.name}' 감지 상태: {handPoseDetected}");
        }
        
        bool finalResult = _handTrackingEvents.handIsTracked && (handShapeDetected || handPoseDetected);
        
        return finalResult;
    }
    
    // 현재 설정된 제스처의 이름을 반환하는 헬퍼 메서드
    private string GetGestureName()
    {
        if (_handShape != null)
            return _handShape.name;
        if (_handPose != null)
            return _handPose.name;
        return "Unknown";
    }
    #endregion
}