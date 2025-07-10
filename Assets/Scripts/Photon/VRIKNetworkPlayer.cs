using UnityEngine;
using Fusion;
using RootMotion.FinalIK;

namespace VStage.Networking
{
    /// <summary>
    /// Host 전용 VRIK 네트워킹 플레이어
    /// Host가 VR 트래커로 아바타를 조작하고, 모든 플레이어가 관전합니다.
    /// </summary>
    public class VRIKNetworkPlayer : NetworkBehaviour
    {
        [Header("VRIK Configuration")]
        [SerializeField] private VRIK vrik;
        [SerializeField] private Animator animator;
        
        [Header("Facial Tracking")]
        [SerializeField] private SimpleShinanoFacialTracking facialTracking;
        
        // VR Target Objects는 이름으로 자동 검색됩니다
        private GameObject headTarget;
        private GameObject leftHandTarget;
        private GameObject rightHandTarget;
        private GameObject waistTarget;
        private GameObject leftFootTarget;
        private GameObject rightFootTarget;
        
        // Host 아바타 본 포즈 동기화 (Client들이 수신)
        [Networked, Capacity(100)] public NetworkArray<Quaternion> BoneRotations => default;
        [Networked] public Vector3 RootPosition { get; set; }
        [Networked] public Quaternion RootRotation { get; set; }
        [Networked] public bool IsDataInitialized { get; set; }
        
        // 페이셜 트래킹 데이터 동기화 (6개 주요 표정)
        [Networked] public float FacialJaw { get; set; }
        [Networked] public float FacialSmile { get; set; }
        [Networked] public float FacialWide { get; set; }
        [Networked] public float FacialO { get; set; }
        [Networked] public float FacialSad { get; set; }
        [Networked] public float FacialTongue { get; set; }
        
        // 본 레퍼런스 캐시
        private Transform[] boneReferences;
        private int boneCount;
        private bool isHost;
        private bool hasReceivedValidData = false;
        
        // 초기 본 상태 저장 (T-pose 방지용)
        private Vector3[] initialBonePositions;
        private Quaternion[] initialBoneRotations;
        
        // 디버깅용 변수들
        private float lastDebugTime = 0f;
        private int dataUpdateCount = 0;
        
        public override void Spawned()
        {
            Debug.Log($"VRIKNetworkPlayer Spawned: Object={Object.name}, HasInputAuthority={Object.HasInputAuthority}, IsValid={Object.IsValid}");
            
            // 기본 컴포넌트 유효성 검사
            if (vrik == null)
            {
                vrik = GetComponent<VRIK>();
                if (vrik == null)
                {
                    Debug.LogError("CRITICAL: VRIK component not found! This script requires VRIK component.");
                    return;
                }
            }
            
            Debug.Log($"VRIK found: {vrik != null}, Solver initiated: {vrik.solver.initiated}, References filled: {vrik.references.isFilled}");
            
            // Host인지 Client인지 판단
            isHost = Object.HasInputAuthority;
            
            // Animator 컴포넌트 자동 검색
            if (animator == null) animator = GetComponent<Animator>();
            Debug.Log($"Animator found: {animator != null}, Enabled: {(animator != null ? animator.enabled : false)}");
            
            // Facial Tracking 컴포넌트 자동 검색
            if (facialTracking == null) facialTracking = GetComponentInChildren<SimpleShinanoFacialTracking>();
            Debug.Log($"Facial Tracking found: {facialTracking != null}, Enabled: {(facialTracking != null ? facialTracking.enabled : false)}");
            
            if (isHost)
            {
                Debug.Log("=== HOST INITIALIZATION START ===");
                
                // Host: VR Target을 이름으로 자동 검색하고 설정
                FindVRTargets();
                SetupVRIKTargets();
                CacheBoneReferences();
                SaveInitialBoneStates();
                
                name = "VRIKNetworkPlayer (Host - VR Controlled)";
                Debug.Log($"=== HOST INITIALIZATION COMPLETE === Bones cached: {boneCount}");
            }
            else
            {
                Debug.Log("=== CLIENT INITIALIZATION START ===");
                
                // Client: Host의 본 데이터를 받아서 아바타 동기화
                Debug.Log($"VRIK enabled before: {vrik.enabled}");
                vrik.enabled = false; // VRIK 비활성화, 직접 본 제어
                Debug.Log($"VRIK enabled after: {vrik.enabled}");
                
                if (animator != null) 
                {
                    Debug.Log($"Animator enabled before: {animator.enabled}");
                    animator.enabled = false; // Animator도 비활성화하여 충돌 방지
                    Debug.Log($"Animator enabled after: {animator.enabled}");
                }
                
                // Facial Tracking도 비활성화 (네트워크 데이터를 직접 받음)
                if (facialTracking != null)
                {
                    Debug.Log($"Facial Tracking enabled before: {facialTracking.enabled}");
                    facialTracking.enabled = false; // 페이셜 트래킹 비활성화, 네트워크 데이터 사용
                    Debug.Log($"Facial Tracking enabled after: {facialTracking.enabled}");
                }
                
                CacheBoneReferences();
                SaveInitialBoneStates();
                
                name = "VRIKNetworkPlayer (Client - Spectating)";
                Debug.Log($"=== CLIENT INITIALIZATION COMPLETE === Bones cached: {boneCount}");
            }
        }
        
        private void SaveInitialBoneStates()
        {
            if (boneReferences == null) 
            {
                Debug.LogWarning("SaveInitialBoneStates: boneReferences is null!");
                return;
            }
            
            Debug.Log($"=== SAVING INITIAL BONE STATES === Count: {boneCount}");
            
            initialBonePositions = new Vector3[boneCount];
            initialBoneRotations = new Quaternion[boneCount];
            
            for (int i = 0; i < boneCount; i++)
            {
                if (boneReferences[i] != null)
                {
                    initialBonePositions[i] = boneReferences[i].position;
                    initialBoneRotations[i] = boneReferences[i].rotation;
                    Debug.Log($"Initial Bone[{i}] ({boneReferences[i].name}): Pos={initialBonePositions[i]}, Rot={initialBoneRotations[i].eulerAngles}");
                }
                else
                {
                    Debug.LogError($"Initial Bone[{i}] is null!");
                }
            }
            
            Debug.Log("=== INITIAL BONE STATES SAVED ===");
        }
        
        private void FindVRTargets()
        {
            Debug.Log("=== FINDING VR TARGETS START ===");
            
            // 이름으로 VR Target GameObject들을 자동 검색
            headTarget = GameObject.Find("Head Target");
            leftHandTarget = GameObject.Find("Left Hand Target");
            rightHandTarget = GameObject.Find("Right Hand Target");
            waistTarget = GameObject.Find("Waist Target");
            leftFootTarget = GameObject.Find("Left Foot Target");
            rightFootTarget = GameObject.Find("Right Foot Target");
            
            // 상세 검색 결과 로깅
            Debug.Log($"Head Target: {(headTarget != null ? $"Found at {headTarget.transform.position}" : "NOT FOUND")}");
            Debug.Log($"Left Hand Target: {(leftHandTarget != null ? $"Found at {leftHandTarget.transform.position}" : "NOT FOUND")}");
            Debug.Log($"Right Hand Target: {(rightHandTarget != null ? $"Found at {rightHandTarget.transform.position}" : "NOT FOUND")}");
            Debug.Log($"Waist Target: {(waistTarget != null ? $"Found at {waistTarget.transform.position}" : "NOT FOUND")}");
            Debug.Log($"Left Foot Target: {(leftFootTarget != null ? $"Found at {leftFootTarget.transform.position}" : "NOT FOUND")}");
            Debug.Log($"Right Foot Target: {(rightFootTarget != null ? $"Found at {rightFootTarget.transform.position}" : "NOT FOUND")}");
            
            int foundTargets = 0;
            if (headTarget != null) foundTargets++;
            if (leftHandTarget != null) foundTargets++;
            if (rightHandTarget != null) foundTargets++;
            if (waistTarget != null) foundTargets++;
            if (leftFootTarget != null) foundTargets++;
            if (rightFootTarget != null) foundTargets++;
            
            Debug.Log($"=== VR TARGETS SEARCH COMPLETE === Found: {foundTargets}/6 targets");
            
            if (foundTargets == 0)
            {
                Debug.LogError("CRITICAL: NO VR TARGETS FOUND! VR tracking will not work!");
                Debug.LogError("Please ensure VR Target GameObjects exist with exact names: 'Head Target', 'Left Hand Target', 'Right Hand Target', 'Waist Target', 'Left Foot Target', 'Right Foot Target'");
            }
            else if (foundTargets < 3)
            {
                Debug.LogWarning($"WARNING: Only {foundTargets} VR targets found. Tracking may be limited.");
            }
        }

        private void SetupVRIKTargets()
        {
            // VR Target이 없을 경우 VRIK 기본 설정 사용
            bool hasAnyTarget = false;
            
            // Spine stiffness 설정
            vrik.solver.spine.bodyPosStiffness = 1f;
            vrik.solver.spine.bodyRotStiffness = 1f;
            Debug.Log("Spine stiffness set: bodyPosStiffness=1, bodyRotStiffness=1");
            
            if (headTarget != null)
            {
                vrik.solver.spine.headTarget = headTarget.transform;
                hasAnyTarget = true;
            }
            
            if (leftHandTarget != null)
            {
                vrik.solver.leftArm.target = leftHandTarget.transform;
                vrik.solver.leftArm.positionWeight = 1f;
                vrik.solver.leftArm.rotationWeight = 1f;
                hasAnyTarget = true;
            }
            
            if (rightHandTarget != null)
            {
                vrik.solver.rightArm.target = rightHandTarget.transform;
                vrik.solver.rightArm.positionWeight = 1f;
                vrik.solver.rightArm.rotationWeight = 1f;
                hasAnyTarget = true;
            }
            
            if (waistTarget != null)
            {
                vrik.solver.spine.pelvisTarget = waistTarget.transform;
                vrik.solver.spine.pelvisPositionWeight = 0.5f;
                vrik.solver.spine.pelvisRotationWeight = 0f;
                hasAnyTarget = true;
            }
            
            if (leftFootTarget != null)
            {
                vrik.solver.leftLeg.target = leftFootTarget.transform;
                vrik.solver.leftLeg.positionWeight = 1f;
                vrik.solver.leftLeg.rotationWeight = 0f;
                hasAnyTarget = true;
            }
            
            if (rightFootTarget != null)
            {
                vrik.solver.rightLeg.target = rightFootTarget.transform;
                vrik.solver.rightLeg.positionWeight = 1f;
                vrik.solver.rightLeg.rotationWeight = 0f;
                hasAnyTarget = true;
            }
            
            // // 오른쪽 다리 bendGoal에 waist target 할당 (무릎 구부림 방향 제어)
            // if (waistTarget != null)
            // {
            //     vrik.solver.rightLeg.bendGoal = waistTarget.transform;
            //     vrik.solver.rightLeg.bendGoalWeight = 0.5f;
            //     Debug.Log($"Right leg bendGoal assigned to waist target: {waistTarget.name}");
        
            //  // 왼쪽쪽 다리 bendGoal에 waist target 할당 (무릎 구부림 방향 제어)
            //     vrik.solver.leftLeg.bendGoal = waistTarget.transform;
            //     vrik.solver.leftLeg.bendGoalWeight = 0.5f;
            //     Debug.Log($"Right leg bendGoal assigned to waist target: {waistTarget.name}");
            // }
            if (!hasAnyTarget)
            {
                Debug.LogWarning("No VR Targets found! VRIK will use default behavior. This might cause network synchronization issues.");
            }
        }
        
        private void CacheBoneReferences()
        {
            Debug.Log("=== CACHING BONE REFERENCES START ===");
            
            var refs = vrik.references;
            var tempBones = new System.Collections.Generic.List<Transform>();
            
            // 표준 휴머노이드 본들을 수집
            if (refs.root != null) { tempBones.Add(refs.root); Debug.Log($"Added root: {refs.root.name}"); }
            if (refs.pelvis != null) { tempBones.Add(refs.pelvis); Debug.Log($"Added pelvis: {refs.pelvis.name}"); }
            if (refs.spine != null) { tempBones.Add(refs.spine); Debug.Log($"Added spine: {refs.spine.name}"); }
            if (refs.chest != null) { tempBones.Add(refs.chest); Debug.Log($"Added chest: {refs.chest.name}"); }
            if (refs.neck != null) { tempBones.Add(refs.neck); Debug.Log($"Added neck: {refs.neck.name}"); }
            if (refs.head != null) { tempBones.Add(refs.head); Debug.Log($"Added head: {refs.head.name}"); }
            
            // 팔 본들
            if (refs.leftShoulder != null) { tempBones.Add(refs.leftShoulder); Debug.Log($"Added leftShoulder: {refs.leftShoulder.name}"); }
            if (refs.leftUpperArm != null) { tempBones.Add(refs.leftUpperArm); Debug.Log($"Added leftUpperArm: {refs.leftUpperArm.name}"); }
            if (refs.leftForearm != null) { tempBones.Add(refs.leftForearm); Debug.Log($"Added leftForearm: {refs.leftForearm.name}"); }
            if (refs.leftHand != null) { tempBones.Add(refs.leftHand); Debug.Log($"Added leftHand: {refs.leftHand.name}"); }
            if (refs.rightShoulder != null) { tempBones.Add(refs.rightShoulder); Debug.Log($"Added rightShoulder: {refs.rightShoulder.name}"); }
            if (refs.rightUpperArm != null) { tempBones.Add(refs.rightUpperArm); Debug.Log($"Added rightUpperArm: {refs.rightUpperArm.name}"); }
            if (refs.rightForearm != null) { tempBones.Add(refs.rightForearm); Debug.Log($"Added rightForearm: {refs.rightForearm.name}"); }
            if (refs.rightHand != null) { tempBones.Add(refs.rightHand); Debug.Log($"Added rightHand: {refs.rightHand.name}"); }
            
            // 다리 본들
            if (refs.leftThigh != null) { tempBones.Add(refs.leftThigh); Debug.Log($"Added leftThigh: {refs.leftThigh.name}"); }
            if (refs.leftCalf != null) { tempBones.Add(refs.leftCalf); Debug.Log($"Added leftCalf: {refs.leftCalf.name}"); }
            if (refs.leftFoot != null) { tempBones.Add(refs.leftFoot); Debug.Log($"Added leftFoot: {refs.leftFoot.name}"); }
            if (refs.rightThigh != null) { tempBones.Add(refs.rightThigh); Debug.Log($"Added rightThigh: {refs.rightThigh.name}"); }
            if (refs.rightCalf != null) { tempBones.Add(refs.rightCalf); Debug.Log($"Added rightCalf: {refs.rightCalf.name}"); }
            if (refs.rightFoot != null) { tempBones.Add(refs.rightFoot); Debug.Log($"Added rightFoot: {refs.rightFoot.name}"); }
            
            boneReferences = tempBones.ToArray();
            boneCount = boneReferences.Length;
            
            Debug.Log($"=== BONE CACHING COMPLETE === Total bones: {boneCount}, NetworkArray capacity: {BoneRotations.Length}");
            
            // 본 목록 출력
            for (int i = 0; i < boneCount; i++)
            {
                Debug.Log($"Bone[{i}]: {boneReferences[i].name} at {boneReferences[i].position}");
            }
        }
        
        // Network Object 상태 체크
        private bool IsNetworkValid()
        {
            if (Object == null)
            {
                Debug.LogError("Network Object is null!");
                return false;
            }
            
            if (!Object.IsValid)
            {
                Debug.LogError("Network Object is not valid!");
                return false;
            }
            
            return true;
        }
        
        public override void FixedUpdateNetwork()
        {
            if (!IsNetworkValid()) return;
            
            if (isHost)
            {
                // Host: VR 본 데이터를 네트워크로 송신
                UpdateHostData();
            }
        }
        
        // Client는 Update에서 처리하여 더 부드러운 동기화
        void Update()
        {
            if (!isHost && IsNetworkValid())
            {
                // Client: Host 본 데이터를 수신해서 아바타에 적용
                UpdateClientData();
            }
        }
        
        private void UpdateHostData()
        {
            // 데이터 초기화 플래그 설정
            IsDataInitialized = true;
            
            bool rootSent = false;
            int bonesSent = 0;
            
            // Root position/rotation 송신
            if (boneCount > 0 && boneReferences[0] != null)
            {
                Vector3 newRootPos = boneReferences[0].position;
                Quaternion newRootRot = boneReferences[0].rotation;
                
                RootPosition = newRootPos;
                RootRotation = newRootRot;
                rootSent = true;
                
                // 상세 디버그: Root 송신 데이터
                if (dataUpdateCount % 60 == 0) // 1초마다
                {
                    Debug.Log($"Host Root Send: Pos={newRootPos}, Rot={newRootRot.eulerAngles}");
                }
            }
            else
            {
                if (dataUpdateCount % 120 == 0) // 2초마다
                {
                    Debug.LogError($"Host Root Error: boneCount={boneCount}, root={boneReferences?[0]?.name ?? "null"}");
                }
            }
            
            // 모든 본 회전 데이터 송신
            for (int i = 0; i < boneCount && i < BoneRotations.Length; i++)
            {
                if (boneReferences[i] != null)
                {
                    Quaternion boneRot = boneReferences[i].rotation;
                    BoneRotations.Set(i, boneRot);
                    bonesSent++;
                    
                    // 상세 디버그: 특정 본 송신 데이터 (첫 5개만)
                    if (i < 5 && dataUpdateCount % 120 == 0) // 2초마다
                    {
                        Debug.Log($"Host Bone[{i}] ({boneReferences[i].name}) Send: {boneRot.eulerAngles}");
                    }
                }
                else
                {
                    if (dataUpdateCount % 300 == 0) // 5초마다
                    {
                        Debug.LogError($"Host Bone[{i}] is null during send!");
                    }
                }
            }
            
            // 디버깅: 주기적으로 호스트 데이터 송신 상태 로그
            dataUpdateCount++;
            if (Time.time - lastDebugTime > 3f) // 3초마다
            {
                lastDebugTime = Time.time;
                Debug.Log($"Host Update #{dataUpdateCount}: RootSent={rootSent}, BonesSent={bonesSent}/{boneCount}");
                Debug.Log($"Host Send Status: IsDataInit={IsDataInitialized}, Position={RootPosition}, Rotation={RootRotation.eulerAngles}");
                
                if (!rootSent)
                {
                    Debug.LogError("Host: ROOT DATA NOT SENT! Check root bone reference.");
                }
                if (bonesSent == 0)
                {
                    Debug.LogError("Host: NO BONE DATA SENT! Check bone references.");
                }
                else if (bonesSent < boneCount)
                {
                    Debug.LogWarning($"Host: PARTIAL bone data sent ({bonesSent}/{boneCount}). Some bones may be null.");
                }
                
                // NetworkArray 오버플로우 체크
                if (boneCount > BoneRotations.Length)
                {
                    Debug.LogError($"Host: NetworkArray OVERFLOW! Need {boneCount} but capacity is {BoneRotations.Length}");
                }
                
                dataUpdateCount = 0;
            }
            
            // 페이셜 트래킹 데이터 송신
            UpdateFacialData();
        }
        
        private void UpdateFacialData()
        {
            if (facialTracking != null && facialTracking.IsFacialTrackingActive())
            {
                // 호스트에서 페이셜 데이터 읽기
                facialTracking.GetFacialData(out float jaw, out float smile, out float wide, out float o, out float sad, out float tongue);
                
                // 네트워크로 전송
                FacialJaw = jaw;
                FacialSmile = smile;
                FacialWide = wide;
                FacialO = o;
                FacialSad = sad;
                FacialTongue = tongue;
                
                // 상세 디버그: 페이셜 데이터 송신 (5초마다)
                if (dataUpdateCount % 300 == 0)
                {
                    Debug.Log($"Host Facial Send: Jaw={jaw:F2}, Smile={smile:F2}, Wide={wide:F2}, O={o:F2}, Sad={sad:F2}, Tongue={tongue:F2}");
                }
            }
            else
            {
                // 페이셜 트래킹이 비활성화된 경우 모든 값을 0으로 설정
                FacialJaw = FacialSmile = FacialWide = FacialO = FacialSad = FacialTongue = 0f;
                
                if (dataUpdateCount % 600 == 0) // 10초마다
                {
                    Debug.LogWarning("Host: Facial tracking inactive - sending zero values");
                }
            }
        }
        
        private void UpdateClientData()
        {
            // 네트워크 데이터가 초기화되지 않았다면 대기
            if (!IsDataInitialized)
            {
                // 디버깅: 데이터 초기화 대기 상태 로그
                if (Time.time - lastDebugTime > 2f)
                {
                    lastDebugTime = Time.time;
                    Debug.LogWarning($"Client: Waiting for host data initialization... IsDataInitialized={IsDataInitialized}");
                }
                return;
            }
            
            // 첫 번째 유효한 데이터 수신 시 로그
            if (!hasReceivedValidData)
            {
                hasReceivedValidData = true;
                Debug.Log($"Client: Received first valid network data from host! RootPos={RootPosition}, RootRot={RootRotation}");
            }
            
            bool rootApplied = false;
            // Root position/rotation 직접 적용 (유효성 검증)
            if (boneCount > 0 && boneReferences[0] != null)
            {
                // Root 데이터가 기본값이 아니고 유효한 범위 내에 있는 경우에만 적용
                if (IsValidPosition(RootPosition) && IsValidRotation(RootRotation))
                {
                    Vector3 oldPos = boneReferences[0].position;
                    Quaternion oldRot = boneReferences[0].rotation;
                    
                    // 직접 적용
                    boneReferences[0].position = RootPosition;
                    boneReferences[0].rotation = RootRotation;
                    rootApplied = true;
                    
                    // 상세 디버그: Root 변화 로그
                    if (dataUpdateCount % 60 == 0) // 1초마다 (60 FPS 기준)
                    {
                        Debug.Log($"Client Root Applied: Pos {oldPos} -> {RootPosition}, Rot {oldRot.eulerAngles} -> {RootRotation.eulerAngles}");
                    }
                }
                else
                {
                    if (dataUpdateCount % 120 == 0) // 2초마다
                    {
                        Debug.LogWarning($"Client Root Invalid: Pos={RootPosition} (Valid={IsValidPosition(RootPosition)}), Rot={RootRotation} (Valid={IsValidRotation(RootRotation)})");
                    }
                }
            }
            
            // 모든 본 회전 데이터 직접 적용 (유효성 검증)
            int validBonesCount = 0;
            int invalidBonesCount = 0;
            for (int i = 0; i < boneCount && i < BoneRotations.Length; i++)
            {
                if (boneReferences[i] != null)
                {
                    Quaternion networkRotation = BoneRotations[i];
                    // 기본값이 아니고 유효한 회전 데이터인 경우에만 적용
                    if (IsValidRotation(networkRotation))
                    {
                        // 직접 적용
                        boneReferences[i].rotation = networkRotation;
                        validBonesCount++;
                        
                        // 상세 디버그: 특정 본의 변화 로그 (첫 5개 본만)
                        if (i < 5 && dataUpdateCount % 120 == 0)
                        {
                            Debug.Log($"Client Bone[{i}] ({boneReferences[i].name}) Applied: {networkRotation.eulerAngles}");
                        }
                    }
                    else
                    {
                        invalidBonesCount++;
                        // 상세 디버그: 유효하지 않은 본 데이터
                        if (i < 5 && dataUpdateCount % 180 == 0) // 3초마다
                        {
                            Debug.LogWarning($"Client Bone[{i}] ({boneReferences[i].name}) Invalid: {networkRotation}");
                        }
                    }
                }
                else
                {
                    if (dataUpdateCount % 300 == 0) // 5초마다
                    {
                        Debug.LogError($"Client Bone[{i}] is null!");
                    }
                }
            }
            
            // 디버깅: 주기적으로 클라이언트 데이터 수신 상태 로그
            dataUpdateCount++;
            if (Time.time - lastDebugTime > 3f) // 3초마다
            {
                lastDebugTime = Time.time;
                Debug.Log($"Client Update #{dataUpdateCount}: Valid={validBonesCount}, Invalid={invalidBonesCount}, Total={boneCount}, RootApplied={rootApplied}");
                Debug.Log($"Client Network State: IsDataInit={IsDataInitialized}, HasValidData={hasReceivedValidData}, RootPos={RootPosition}");
                
                if (validBonesCount == 0 && invalidBonesCount == 0)
                {
                    Debug.LogError("Client: NO BONE DATA AT ALL! Check network connection and host setup.");
                }
                else if (validBonesCount == 0)
                {
                    Debug.LogError($"Client: NO VALID bone data! All {invalidBonesCount} bones have invalid rotations.");
                }
                else if (validBonesCount < boneCount / 2)
                {
                    Debug.LogWarning($"Client: Low valid bone ratio ({validBonesCount}/{boneCount}). Possible network issues.");
                }
                
                dataUpdateCount = 0;
            }
            
            // 페이셜 트래킹 데이터 적용
            ApplyFacialData();
        }
        
        private void ApplyFacialData()
        {
            if (facialTracking != null)
            {
                // 클라이언트에서 받은 페이셜 데이터 적용
                facialTracking.SetFacialData(FacialJaw, FacialSmile, FacialWide, FacialO, FacialSad, FacialTongue);
                
                // 상세 디버그: 페이셜 데이터 수신 (5초마다)
                if (dataUpdateCount % 300 == 0)
                {
                    Debug.Log($"Client Facial Applied: Jaw={FacialJaw:F2}, Smile={FacialSmile:F2}, Wide={FacialWide:F2}, O={FacialO:F2}, Sad={FacialSad:F2}, Tongue={FacialTongue:F2}");
                }
                
                // 활성 표정 개수 체크
                int activeFacials = 0;
                if (FacialJaw > 0.01f) activeFacials++;
                if (FacialSmile > 0.01f) activeFacials++;
                if (FacialWide > 0.01f) activeFacials++;
                if (FacialO > 0.01f) activeFacials++;
                if (FacialSad > 0.01f) activeFacials++;
                if (FacialTongue > 0.01f) activeFacials++;
                
                if (dataUpdateCount % 600 == 0) // 10초마다
                {
                    Debug.Log($"Client Facial Status: {activeFacials}/6 expressions active");
                    if (activeFacials == 0)
                    {
                        Debug.LogWarning("Client: No facial expressions detected - check host facial tracking");
                    }
                }
            }
            else
            {
                if (dataUpdateCount % 600 == 0) // 10초마다
                {
                    Debug.LogWarning("Client: No facial tracking component found");
                }
            }
        }
        
        // 위치 데이터 유효성 검증 (더 관대하게)
        private bool IsValidPosition(Vector3 position)
        {
            // NaN, Infinity 체크와 합리적인 범위 체크
            bool isValid = !float.IsNaN(position.x) && !float.IsNaN(position.y) && !float.IsNaN(position.z) &&
                          !float.IsInfinity(position.x) && !float.IsInfinity(position.y) && !float.IsInfinity(position.z) &&
                          position.magnitude < 10000f; // 10km 이내 (매우 관대한 범위)
            
            if (!isValid && dataUpdateCount % 120 == 0)
            {
                Debug.LogWarning($"Invalid Position: {position}, Magnitude: {position.magnitude}");
            }
            
            return isValid;
        }
        
        // 회전 데이터 유효성 검증 (더 관대하게)
        private bool IsValidRotation(Quaternion rotation)
        {
            // NaN 체크와 기본적인 쿼터니언 유효성만 체크 (identity 제외)
            bool isValid = !float.IsNaN(rotation.x) && !float.IsNaN(rotation.y) && !float.IsNaN(rotation.z) && !float.IsNaN(rotation.w) &&
                          !float.IsInfinity(rotation.x) && !float.IsInfinity(rotation.y) && !float.IsInfinity(rotation.z) && !float.IsInfinity(rotation.w);
            
            // 쿼터니언 정규화 체크 (좀 더 관대하게)
            if (isValid)
            {
                float magnitude = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
                isValid = magnitude > 0.5f && magnitude < 2.0f; // 정규화된 쿼터니언은 1에 가까워야 함
            }
            
            if (!isValid && dataUpdateCount % 120 == 0)
            {
                Debug.LogWarning($"Invalid Rotation: {rotation}, Magnitude: {(rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w)}");
            }
            
            return isValid;
        }
        
        // 디버깅용 메서드
        [ContextMenu("Debug Network Data")]
        private void DebugNetworkData()
        {
            Debug.Log("=== NETWORK DEBUG INFO ===");
            Debug.Log($"IsHost: {isHost}");
            Debug.Log($"IsDataInitialized: {IsDataInitialized}");
            Debug.Log($"HasReceivedValidData: {hasReceivedValidData}");
            Debug.Log($"Network Object Valid: {IsNetworkValid()}");
            Debug.Log($"Network Object HasInputAuthority: {(Object != null ? Object.HasInputAuthority : false)}");
            
            Debug.Log($"=== COMPONENT STATUS ===");
            Debug.Log($"VRIK: {(vrik != null ? $"Found, Enabled={vrik.enabled}" : "NULL")}");
            Debug.Log($"Animator: {(animator != null ? $"Found, Enabled={animator.enabled}" : "NULL")}");
            
            Debug.Log($"=== BONE REFERENCES ===");
            Debug.Log($"BoneCount: {boneCount}");
            Debug.Log($"NetworkArray Capacity: {BoneRotations.Length}");
            Debug.Log($"NetworkArray vs BoneCount: {(boneCount <= BoneRotations.Length ? "OK" : "OVERFLOW!")}");
            
            if (boneReferences != null)
            {
                for (int i = 0; i < Mathf.Min(10, boneCount); i++) // 첫 10개만 표시
                {
                    if (boneReferences[i] != null)
                    {
                        Debug.Log($"Bone[{i}]: {boneReferences[i].name} at {boneReferences[i].position}, rot={boneReferences[i].rotation.eulerAngles}");
                    }
                    else
                    {
                        Debug.LogError($"Bone[{i}]: NULL");
                    }
                }
            }
            
            Debug.Log($"=== NETWORK DATA ===");
            Debug.Log($"RootPosition: {RootPosition} (Valid: {IsValidPosition(RootPosition)})");
            Debug.Log($"RootRotation: {RootRotation} (Valid: {IsValidRotation(RootRotation)})");
            
            Debug.Log($"=== FACIAL TRACKING DATA ===");
            Debug.Log($"Facial Component: {(facialTracking != null ? $"Found, Enabled={facialTracking.enabled}" : "NULL")}");
            Debug.Log($"Facial Jaw: {FacialJaw:F3}");
            Debug.Log($"Facial Smile: {FacialSmile:F3}");
            Debug.Log($"Facial Wide: {FacialWide:F3}");
            Debug.Log($"Facial O: {FacialO:F3}");
            Debug.Log($"Facial Sad: {FacialSad:F3}");
            Debug.Log($"Facial Tongue: {FacialTongue:F3}");
            
            if (isHost)
            {
                Debug.Log("=== HOST VR TARGETS ===");
                Debug.Log($"Head Target: {(headTarget != null ? headTarget.name : "NULL")}");
                Debug.Log($"Left Hand Target: {(leftHandTarget != null ? leftHandTarget.name : "NULL")}");
                Debug.Log($"Right Hand Target: {(rightHandTarget != null ? rightHandTarget.name : "NULL")}");
                Debug.Log($"Waist Target: {(waistTarget != null ? waistTarget.name : "NULL")}");
                Debug.Log($"Left Foot Target: {(leftFootTarget != null ? leftFootTarget.name : "NULL")}");
                Debug.Log($"Right Foot Target: {(rightFootTarget != null ? rightFootTarget.name : "NULL")}");
                
                if (facialTracking != null)
                {
                    Debug.Log($"=== HOST FACIAL STATUS ===");
                    Debug.Log($"Facial Tracking Active: {facialTracking.IsFacialTrackingActive()}");
                }
            }
            
            Debug.Log("=== END DEBUG INFO ===");
        }
        
        // 에러 상황에서 자동 복구 시도
        [ContextMenu("Attempt Auto Recovery")]
        private void AttemptAutoRecovery()
        {
            Debug.Log("=== ATTEMPTING AUTO RECOVERY ===");
            
            if (!isHost && !hasReceivedValidData)
            {
                Debug.Log("Client: Resetting data reception state...");
                hasReceivedValidData = false;
                dataUpdateCount = 0;
                lastDebugTime = 0f;
            }
            
            if (isHost)
            {
                Debug.Log("Host: Re-finding VR targets...");
                FindVRTargets();
                SetupVRIKTargets();
                
                // 페이셜 트래킹 컴포넌트 재검색
                if (facialTracking == null)
                {
                    Debug.Log("Host: Re-finding facial tracking component...");
                    facialTracking = GetComponentInChildren<SimpleShinanoFacialTracking>();
                    if (facialTracking != null)
                    {
                        Debug.Log($"Host: Facial tracking component found: {facialTracking.name}");
                    }
                }
            }
            else
            {
                // 클라이언트: 페이셜 컴포넌트 재검색 및 비활성화
                if (facialTracking == null)
                {
                    Debug.Log("Client: Re-finding facial tracking component...");
                    facialTracking = GetComponentInChildren<SimpleShinanoFacialTracking>();
                    if (facialTracking != null)
                    {
                        facialTracking.enabled = false; // 클라이언트에서는 비활성화
                        Debug.Log($"Client: Facial tracking component found and disabled: {facialTracking.name}");
                    }
                }
            }
            
            Debug.Log("=== AUTO RECOVERY COMPLETE ===");
        }
    }
}