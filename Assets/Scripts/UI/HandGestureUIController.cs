using UnityEngine;
using UnityEngine.XR.Hands;

public class HandGestureUIController : MonoBehaviour
{
    [Header("Gesture (StaticHandGesture)")]
    [SerializeField] private StaticHandGesture gesture;        

    [Header("Hand Tracking")]
    [SerializeField] private XRHandSubsystem handSubsystem;    
    [SerializeField] private bool useLeftHand = false;          // 왼손(true) 또는 오른손(false) 선택
    [SerializeField] private XRHandJointID attachJoint = XRHandJointID.Palm;

    [Header("UI")]
    [SerializeField] private GameObject uiPrefab;              
    [SerializeField] private Vector3 positionOffset = Vector3.up * 0.1f;
    private GameObject uiInstance;

    public void OnEnable()
    {
        gesture.GesturePerformed.AddListener(ShowUI);
        gesture.GestureEnded.AddListener(HideUI);
    }

    public void OnDisable()
    {
        gesture.GesturePerformed.RemoveListener(ShowUI);
        gesture.GestureEnded.RemoveListener(HideUI);
    }

    private void ShowUI()
    {
        if (uiInstance == null)
            uiInstance = Instantiate(uiPrefab);
        uiInstance.SetActive(true);
        UpdateUITransform();
    }

    private void HideUI()
    {
        if (uiInstance != null)
            uiInstance.SetActive(false);
    }

    private void Update()
    {
        if (uiInstance != null && uiInstance.activeSelf)
            UpdateUITransform();
    }

    private void UpdateUITransform()
    {
        // ① 사용할 손 선택
        XRHand hand = useLeftHand ? handSubsystem.leftHand : handSubsystem.rightHand;

        // ② 손 추적 중이고, 지정 관절 Pose 획득 가능할 때
        if (hand.isTracked 
            && hand.GetJoint(attachJoint).TryGetPose(out Pose pose))
        {
            uiInstance.transform.position = pose.position + positionOffset;
            uiInstance.transform.rotation = Quaternion.LookRotation(
                uiInstance.transform.position - Camera.main.transform.position);
        }
    }
}