using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;

[RequireComponent(typeof(XRHandTrackingEvents))]
public class HandJointsDebugger : MonoBehaviour
{
    XRHandTrackingEvents _events;

    void Awake()
    {
        _events = GetComponent<XRHandTrackingEvents>();
        Debug.Assert(_events != null, "[HandJointsDebugger] XRHandTrackingEvents 컴포넌트가 없습니다!");

        // 런타임에 리스너 등록
        _events.jointsUpdated.AddListener(OnJointsUpdated);
        _events.poseUpdated.AddListener(OnPoseUpdated);
        _events.trackingChanged.AddListener(b => Debug.Log($"[TrackingChanged] isTracked={b}"));
    }

    void OnDestroy()
    {
        // 메모리 누수 방지
        _events.jointsUpdated.RemoveListener(OnJointsUpdated);
        _events.poseUpdated.RemoveListener(OnPoseUpdated);
    }

    void OnJointsUpdated(XRHandJointsUpdatedEventArgs args)
    {
        Debug.Log($"[JointsUpdated] {args.hand.handedness}");
    }

    void OnPoseUpdated(Pose pose)
    {
        Debug.Log($"[PoseUpdated] pos={pose.position:F3}, rot={pose.rotation.eulerAngles:F1}");
    }
}