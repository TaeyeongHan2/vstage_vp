using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Hands;

public class RiseStickMicRecord : MonoBehaviour
{
    [Header("Hand Tracking")]
    public XRHandSubsystem handSubsystem;       
    public XRHandJointID attachJoint = XRHandJointID.Wrist;
    
    [Header("Target Object")]
    public GameObject targetPrefab;              
    private GameObject targetInstance;

    [Header("Debug")]
    public bool showDebugInfo = true;

    private Pose validPose;
    
    void Awake()
    {
        // 타겟 프리팹 인스턴스화 후 비활성화
        if (targetPrefab != null)
        {
            targetInstance = Instantiate(targetPrefab,transform);
            targetInstance.SetActive(false);
            Debug.Log("타겟 오브젝트 생성 완료");
        }
        else
        {
            Debug.LogError("Target Prefab이 설정되지 않았습니다!");
        }
    }

    void Start()
    {
        StartCoroutine(InitializeHandSubsystem());
    }

    private IEnumerator InitializeHandSubsystem()
    {
        Debug.Log("Hand Subsystem 초기화 시작...");
        
        float timeout = 30f;
        float elapsed = 0f;
        
        while (handSubsystem == null && elapsed < timeout)
        {
            var subsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            
            if (subsystems.Count > 0)
            {
                handSubsystem = subsystems[0];
                Debug.Log("✅ Hand Subsystem 초기화 완료");
                
                // Hand Subsystem이 실제로 실행 중인지 확인
                if (!handSubsystem.running)
                {
                    Debug.Log("Hand Subsystem을 시작하는 중...");
                    handSubsystem.Start();
                }
                
                break;
            }
            
            elapsed += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }
        
        if (handSubsystem == null)
        {
            Debug.LogError("Hand Subsystem 초기화 실패");
        }
    }

    void Update()
    {
        if (handSubsystem == null || targetInstance == null)
            return;

        var hand = handSubsystem.rightHand;
        Debug.Log("1. 오른쪽 손 감지 완료 및 정보 가져옴");

        // 손 추적 확인
        if (hand.isTracked)
        {
            Debug.Log("2. 손이 추적되고 있음");
            
            // 여러 Joint를 시도해보는 방식
            Pose validPose;
            if (TryGetValidHandPose(hand, out validPose))
            {
                Debug.Log("3. 유효한 포즈 발견!");
                
                // 오브젝트 활성화 및 위치 업데이트
                if (!targetInstance.activeSelf)
                {
                    targetInstance.SetActive(true);
                    Debug.Log("4. 손 감지됨 - 오브젝트 활성화");
                }
                
                
                // 손 위치를 따라다니기
                targetInstance.transform.position = hand.rootPose.position;
                targetInstance.transform.rotation = hand.rootPose.rotation;
                
                // 디버그 정보 (1초에 한 번만 출력)
                if (showDebugInfo && Time.frameCount % 60 == 0)
                {
                    Debug.Log($"✋ Hand Position: {hand.rootPose.position:F2}");
                }
            }
            else
            {
                Debug.LogWarning("모든 Joint에서 유효한 pose를 가져올 수 없습니다");
                HandleHandLost();
            }
        }
        else
        {
            Debug.Log("손이 추적되지 않음");
            HandleHandLost();
        }
    }

    // 여러 Joint를 시도해서 유효한 Pose를 찾는 메서드
    private bool TryGetValidHandPose(XRHand hand, out Pose pose)
    {
        pose = Pose.identity;
        
        // 시도할 Joint들의 우선순위 목록
        XRHandJointID[] jointsToTry = new XRHandJointID[]
        {
            attachJoint,           
            XRHandJointID.Wrist,   
            XRHandJointID.Palm,    
            XRHandJointID.IndexTip, 
            XRHandJointID.MiddleTip 
        };
        
        foreach (var jointID in jointsToTry)
        {
            try
            {
                //hand.rootPose
                XRHandJoint joint = hand.GetJoint(jointID);
                Debug.Log($"Joint {jointID} 시도 중...");
                
                if (joint.TryGetPose(out pose))
                {
                    if (IsValidPose(pose))
                    {
                        Debug.Log($"Joint {jointID}에서 유효한 pose 발견: {pose.position:F2}");
                        return true;
                    }
                    else
                    {
                        Debug.Log($"Joint {jointID}의 pose가 유효하지 않음: {pose.position:F2}");
                    }
                }
                else
                {
                    Debug.Log($"Joint {jointID}에서 pose를 가져올 수 없음");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Joint {jointID} 에러: {e.Message}");
            }
        }
        
        return false;
    }

    private void HandleHandLost()
    {
        if (targetInstance != null && targetInstance.activeSelf)
        {
            targetInstance.SetActive(false);
            Debug.Log("손 추적 끊어짐 - 오브젝트 숨김");
        }
    }

    // 더 관대한 Pose 유효성 검사
    private bool IsValidPose(Pose pose)
    {
        // NaN이나 Infinity 체크
        if (float.IsNaN(pose.position.x) || float.IsNaN(pose.position.y) || float.IsNaN(pose.position.z))
        {
            Debug.Log("Pose 무효: NaN 값 포함");
            return false;
        }
            
        if (float.IsInfinity(pose.position.x) || float.IsInfinity(pose.position.y) || float.IsInfinity(pose.position.z))
        {
            Debug.Log("Pose 무효: Infinity 값 포함");
            return false;
        }
        
        // 원점 체크는 더 관대하게 (0에 가까운 값도 허용)
        if (Vector3.Distance(pose.position, Vector3.zero) < 0.001f)
        {
            Debug.Log("Pose 무효: 원점에 너무 가까움");
            return false;
        }
        
        // 손의 위치가 너무 멀리 있는지 체크 (10미터 이내)
        if (Vector3.Distance(pose.position, Vector3.zero) > 10f)
        {
            Debug.Log("Pose 무효: 위치가 너무 멀음");
            return false;
        }
            
        return true;
    }

    // 디버그용 GUI
    void OnGUI()
    {
        if (!showDebugInfo || (!Application.isEditor && !Debug.isDebugBuild))
            return;
            
        GUILayout.BeginArea(new Rect(10, 10, 400, 200));
        GUILayout.Box("Hand Tracking Debug Info");
        
        GUILayout.Label($"Hand Subsystem: {(handSubsystem != null ? "Connected" : "Not Found")}");
        
        if (handSubsystem != null)
        {
            GUILayout.Label($"Subsystem Running: {handSubsystem.running}");
            
            var hand = handSubsystem.rightHand;
            GUILayout.Label($"Hand Tracked: {(hand.isTracked ? "Yes" : "No")}");
            
            if (hand.isTracked)
            {
                // 여러 Joint 상태 표시
                foreach (var jointID in new XRHandJointID[] { XRHandJointID.Wrist, XRHandJointID.Palm, XRHandJointID.IndexTip })
                {
                    XRHandJoint joint = hand.GetJoint(jointID);
                    bool canGetPose = joint.TryGetPose(out Pose pose);
                    GUILayout.Label($"{jointID}: {(canGetPose ? "✅" : "❌")}");
                }
            }
        }
        
        GUILayout.Label($"Target Object: {(targetInstance?.activeSelf == true ? "Active" : "Inactive")}");
        
        GUILayout.EndArea();
    }

    // 손 관절 변경 (Inspector에서 실시간 테스트용)
    [ContextMenu("Change to Wrist")]
    void ChangeToWrist()
    {
        attachJoint = XRHandJointID.Wrist;
        Debug.Log("관절 변경: Wrist");
    }
    
    [ContextMenu("Change to Palm")]
    void ChangeToPalm()
    {
        attachJoint = XRHandJointID.Palm;
        Debug.Log("관절 변경: Palm");
    }
    
    [ContextMenu("Change to Index Tip")]
    void ChangeToIndexTip()
    {
        attachJoint = XRHandJointID.IndexTip;
        Debug.Log("관절 변경: Index Tip");
    }
}