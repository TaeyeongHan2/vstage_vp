using UnityEngine;
using Fusion;
using RootMotion.FinalIK;

namespace VStage.Networking
{
    /// <summary>
    /// Host 전용 VRIK 네트워킹 플레이어
    /// Host가 VR 트래커로 아바타를 조작하고, 모든 플레이어가 관전합니다.
    /// 
    /// 주요 기능:
    /// - Host: VR 트래커 데이터를 받아 VRIK로 아바타 제어 후 네트워크로 전송
    /// - Client: Host의 본 데이터를 받아서 동일한 아바타 포즈 재현
    /// - 실시간 본 회전 데이터 동기화 (최대 100개 본 지원)
    /// - 페이셜 트래킹 데이터 동기화 (6가지 주요 표정)
    /// - VR 트래커 자동 검색 및 VRIK 설정
    /// </summary>
    public class VRIKNetworkPlayer : NetworkBehaviour
    {
        [Header("VRIK Configuration")] 
        /// <summary>
        /// Final IK의 VRIK 컴포넌트 - VR 트래커 데이터로 전신 IK 계산
        /// Host에서만 활성화되며, Client에서는 비활성화하고 직접 본 제어 사용
        /// </summary>
        [SerializeField]
        private VRIK vrik;

        /// <summary>
        /// 아바타의 Animator 컴포넌트
        /// Client에서는 비활성화하여 네트워크 본 데이터와 충돌 방지
        /// </summary>
        [SerializeField] private Animator animator;

        [Header("Facial Tracking")]
        /// <summary>
        /// 페이셜 트래킹 컴포넌트 (현재 주석 처리됨)
        /// Host의 얼굴 표정을 읽어 네트워크로 전송하는 역할
        /// </summary>
        //[SerializeField] private SimpleShinanoFacialTracking facialTracking;

        // VR Target Objects는 이름으로 자동 검색됩니다
        /// <summary>
        /// VR 헤드셋 트래커 타겟 - 머리 위치/회전 추적
        /// GameObject.Find("Head Target")로 자동 검색
        /// </summary>
        private GameObject headTarget;

        /// <summary>
        /// VR 왼손 컨트롤러 트래커 타겟 - 왼손 위치/회전 추적
        /// GameObject.Find("Left Hand Target")로 자동 검색
        /// </summary>
        private GameObject leftHandTarget;
        
        /// <summary>
        /// VR 오른손 컨트롤러 트래커 타겟 - 오른손 위치/회전 추적
        /// GameObject.Find("Right Hand Target")로 자동 검색
        /// </summary>
        private GameObject rightHandTarget;
        
        /// <summary>
        /// VR 허리 트래커 타겟 - 골반 위치 추적 (옵션)
        /// GameObject.Find("Waist Target")로 자동 검색
        /// </summary>
        private GameObject waistTarget;
        
        /// <summary>
        /// VR 왼발 트래커 타겟 - 왼발 위치 추적 (옵션)
        /// GameObject.Find("Left Foot Target")로 자동 검색
        /// </summary>
        private GameObject leftFootTarget;
        
        /// <summary>
        /// VR 오른발 트래커 타겟 - 오른발 위치 추적 (옵션)
        /// GameObject.Find("Right Foot Target")로 자동 검색
        /// </summary>
        private GameObject rightFootTarget;

        // Host 아바타 본 포즈 동기화 (Client들이 수신)
        /// <summary>
        /// 네트워크로 동기화되는 모든 본의 회전 데이터 배열
        /// 최대 100개 본까지 지원, Host가 송신하고 Client들이 수신
        /// </summary>
        [Networked, Capacity(100)] public NetworkArray<Quaternion> BoneRotations => default;
        
        /// <summary>
        /// 아바타 루트 본의 월드 위치 (네트워크 동기화)
        /// 전체 아바타의 위치를 결정하는 기준점
        /// </summary>
        [Networked] public Vector3 RootPosition { get; set; }
        
        /// <summary>
        /// 아바타 루트 본의 월드 회전 (네트워크 동기화)
        /// 전체 아바타의 방향을 결정하는 기준
        /// </summary>
        [Networked] public Quaternion RootRotation { get; set; }
        
        /// <summary>
        /// 네트워크 데이터가 초기화되었는지 확인하는 플래그
        /// Host가 true로 설정하면 Client들이 데이터 수신 시작
        /// </summary>
        [Networked] public bool IsDataInitialized { get; set; }

        // 페이셜 트래킹 데이터 동기화 (6개 주요 표정)
        /// <summary>
        /// 턱 움직임 표정 값 (0.0 ~ 1.0)
        /// 입 벌리기, 턱 내리기 등의 표정
        /// </summary>
        [Networked] public float FacialJaw { get; set; }
        
        /// <summary>
        /// 미소 표정 값 (0.0 ~ 1.0)
        /// 입꼬리 올리기, 웃는 표정
        /// </summary>
        [Networked] public float FacialSmile { get; set; }
        
        /// <summary>
        /// 입 벌리기 표정 값 (0.0 ~ 1.0)
        /// 놀란 표정, 입 크게 벌리기
        /// </summary>
        [Networked] public float FacialWide { get; set; }
        
        /// <summary>
        /// 입술 오므리기 표정 값 (0.0 ~ 1.0)
        /// 'O' 모양 입 만들기
        /// </summary>
        [Networked] public float FacialO { get; set; }
        
        /// <summary>
        /// 슬픈 표정 값 (0.0 ~ 1.0)
        /// 입꼬리 내리기, 우울한 표정
        /// </summary>
        [Networked] public float FacialSad { get; set; }
        
        /// <summary>
        /// 혀 내밀기 표정 값 (0.0 ~ 1.0)
        /// 혀를 내미는 장난스러운 표정
        /// </summary>
        [Networked] public float FacialTongue { get; set; }

        // 본 레퍼런스 캐시
        /// <summary>
        /// 아바타의 모든 주요 본(뼈대) Transform 참조 배열
        /// 휴머노이드 리그의 표준 본들을 순서대로 저장
        /// </summary>
        private Transform[] boneReferences;
        
        /// <summary>
        /// 캐시된 본의 총 개수
        /// boneReferences 배열의 유효한 요소 수
        /// </summary>
        private int boneCount;
        
        /// <summary>
        /// 현재 인스턴스가 Host인지 확인하는 플래그
        /// Host는 VR 데이터를 송신하고, Client는 수신만 함
        /// </summary>
        private bool isHost;
        
        /// <summary>
        /// Client가 Host로부터 유효한 데이터를 최초로 받았는지 확인
        /// 초기 동기화 완료 여부를 추적
        /// </summary>
        private bool hasReceivedValidData = false;

        // 초기 본 상태 저장 (T-pose 방지용)
        /// <summary>
        /// 각 본의 초기 위치 상태 저장
        /// T-pose나 이상한 포즈로 되돌아가는 것을 방지하는 용도
        /// </summary>
        private Vector3[] initialBonePositions;
        
        /// <summary>
        /// 각 본의 초기 회전 상태 저장
        /// 기본 포즈로 복원할 때 사용하는 참조값
        /// </summary>
        private Quaternion[] initialBoneRotations;

        // 디버깅용 변수들
        /// <summary>
        /// 마지막 디버그 로그 출력 시간
        /// 주기적인 디버그 메시지 출력 간격 제어
        /// </summary>
        private float lastDebugTime = 0f;
        
        /// <summary>
        /// 데이터 업데이트 횟수 카운터
        /// 성능 모니터링 및 주기적 로그 출력에 사용
        /// </summary>
        private int dataUpdateCount = 0;

        /// <summary>
        /// 네트워크 오브젝트가 스폰될 때 호출되는 Fusion 콜백
        /// Host와 Client 역할에 따라 초기화 과정이 분기됨
        /// </summary>
        public override void Spawned()
        {
            Debug.Log(
                $"VRIKNetworkPlayer Spawned: Object={Object.name}, HasInputAuthority={Object.HasInputAuthority}, IsValid={Object.IsValid}");

            // 기본 컴포넌트 유효성 검사
            if (vrik == null)
            {
                // VRIK 컴포넌트 자동 검색
                vrik = GetComponent<VRIK>();
                if (vrik == null)
                {
                    Debug.LogError("CRITICAL: VRIK component not found! This script requires VRIK component.");
                    return;
                }
            }

            Debug.Log(
                $"VRIK found: {vrik != null}, Solver initiated: {vrik.solver.initiated}, References filled: {vrik.references.isFilled}");

            // Host인지 Client인지 판단 (Input Authority 기준)
            isHost = Object.HasInputAuthority;

            // Animator 컴포넌트 자동 검색
            if (animator == null) animator = GetComponent<Animator>();
            Debug.Log($"Animator found: {animator != null}, Enabled: {(animator != null ? animator.enabled : false)}");

            // Facial Tracking 컴포넌트 자동 검색 (현재 주석 처리)
            // if (facialTracking == null) facialTracking = GetComponentInChildren<SimpleShinanoFacialTracking>();
            // Debug.Log($"Facial Tracking found: {facialTracking != null}, Enabled: {(facialTracking != null ? facialTracking.enabled : false)}");
            //
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
                // if (facialTracking != null)
                // {
                //     Debug.Log($"Facial Tracking enabled before: {facialTracking.enabled}");
                //     facialTracking.enabled = false; // 페이셜 트래킹 비활성화, 네트워크 데이터 사용
                //     Debug.Log($"Facial Tracking enabled after: {facialTracking.enabled}");
                // }

                CacheBoneReferences();
                SaveInitialBoneStates();

                name = "VRIKNetworkPlayer (Client - Spectating)";
                Debug.Log($"=== CLIENT INITIALIZATION COMPLETE === Bones cached: {boneCount}");
            }
        }

        /// <summary>
        /// 아바타의 초기 본 상태를 저장하는 메서드
        /// T-pose나 이상한 포즈로 복원되는 것을 방지하기 위해 사용
        /// 각 본의 초기 위치와 회전을 별도 배열에 백업
        /// </summary>
        private void SaveInitialBoneStates()
        {
            if (boneReferences == null)
            {
                Debug.LogWarning("SaveInitialBoneStates: boneReferences is null!");
                return;
            }

            Debug.Log($"=== SAVING INITIAL BONE STATES === Count: {boneCount}");

            // 초기 상태 저장용 배열 생성
            initialBonePositions = new Vector3[boneCount];
            initialBoneRotations = new Quaternion[boneCount];

            // 모든 본의 현재 상태를 초기값으로 저장
            for (int i = 0; i < boneCount; i++)
            {
                if (boneReferences[i] != null)
                {
                    initialBonePositions[i] = boneReferences[i].position;
                    initialBoneRotations[i] = boneReferences[i].rotation;
                    Debug.Log(
                        $"Initial Bone[{i}] ({boneReferences[i].name}): Pos={initialBonePositions[i]}, Rot={initialBoneRotations[i].eulerAngles}");
                }
                else
                {
                    Debug.LogError($"Initial Bone[{i}] is null!");
                }
            }

            Debug.Log("=== INITIAL BONE STATES SAVED ===");
        }

        /// <summary>
        /// VR 트래커 타겟 GameObject들을 이름으로 자동 검색하는 메서드
        /// Host에서만 호출되며, 6개의 주요 VR 트래커를 찾음:
        /// - Head Target: 헤드셋
        /// - Left/Right Hand Target: 양손 컨트롤러
        /// - Waist Target: 허리 트래커 (옵션)
        /// - Left/Right Foot Target: 양발 트래커 (옵션)
        /// </summary>
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
            Debug.Log(
                $"Head Target: {(headTarget != null ? $"Found at {headTarget.transform.position}" : "NOT FOUND")}");
            Debug.Log(
                $"Left Hand Target: {(leftHandTarget != null ? $"Found at {leftHandTarget.transform.position}" : "NOT FOUND")}");
            Debug.Log(
                $"Right Hand Target: {(rightHandTarget != null ? $"Found at {rightHandTarget.transform.position}" : "NOT FOUND")}");
            Debug.Log(
                $"Waist Target: {(waistTarget != null ? $"Found at {waistTarget.transform.position}" : "NOT FOUND")}");
            Debug.Log(
                $"Left Foot Target: {(leftFootTarget != null ? $"Found at {leftFootTarget.transform.position}" : "NOT FOUND")}");
            Debug.Log(
                $"Right Foot Target: {(rightFootTarget != null ? $"Found at {rightFootTarget.transform.position}" : "NOT FOUND")}");

            // 찾은 타겟 개수 계산
            int foundTargets = 0;
            if (headTarget != null) foundTargets++;
            if (leftHandTarget != null) foundTargets++;
            if (rightHandTarget != null) foundTargets++;
            if (waistTarget != null) foundTargets++;
            if (leftFootTarget != null) foundTargets++;
            if (rightFootTarget != null) foundTargets++;

            Debug.Log($"=== VR TARGETS SEARCH COMPLETE === Found: {foundTargets}/6 targets");

            // 타겟이 전혀 없으면 오류, 3개 미만이면 경고
            if (foundTargets == 0)
            {
                Debug.LogError("CRITICAL: NO VR TARGETS FOUND! VR tracking will not work!");
                Debug.LogError(
                    "Please ensure VR Target GameObjects exist with exact names: 'Head Target', 'Left Hand Target', 'Right Hand Target', 'Waist Target', 'Left Foot Target', 'Right Foot Target'");
            }
            else if (foundTargets < 3)
            {
                Debug.LogWarning($"WARNING: Only {foundTargets} VR targets found. Tracking may be limited.");
            }
        }

        /// <summary>
        /// 찾은 VR 타겟들을 VRIK solver에 연결하고 가중치를 설정하는 메서드
        /// 각 신체 부위별로 적절한 IK 가중치와 강성을 설정하여 자연스러운 움직임 구현
        /// </summary>
        private void SetupVRIKTargets()
        {
            // VR Target이 없을 경우 VRIK 기본 설정 사용
            bool hasAnyTarget = false;

            // Spine stiffness 설정 - 척추의 강성도 조절
            vrik.solver.spine.bodyPosStiffness = 1f;  // 몸통 위치 강성 최대
            vrik.solver.spine.bodyRotStiffness = 1f;  // 몸통 회전 강성 최대
            Debug.Log("Spine stiffness set: bodyPosStiffness=1, bodyRotStiffness=1");

            // 머리 타겟 설정 (헤드셋)
            if (headTarget != null)
            {
                vrik.solver.spine.headTarget = headTarget.transform;
                hasAnyTarget = true;
            }

            // 왼손 타겟 설정 (왼손 컨트롤러)
            if (leftHandTarget != null)
            {
                vrik.solver.leftArm.target = leftHandTarget.transform;
                vrik.solver.leftArm.positionWeight = 1f;  // 위치 추적 가중치 최대
                vrik.solver.leftArm.rotationWeight = 1f;  // 회전 추적 가중치 최대
                hasAnyTarget = true;
            }

            // 오른손 타겟 설정 (오른손 컨트롤러)
            if (rightHandTarget != null)
            {
                vrik.solver.rightArm.target = rightHandTarget.transform;
                vrik.solver.rightArm.positionWeight = 1f;  // 위치 추적 가중치 최대
                vrik.solver.rightArm.rotationWeight = 1f;  // 회전 추적 가중치 최대
                hasAnyTarget = true;
            }

            // 허리 타겟 설정 (허리 트래커)
            if (waistTarget != null)
            {
                vrik.solver.spine.pelvisTarget = waistTarget.transform;
                vrik.solver.spine.pelvisPositionWeight = 0.5f;  // 골반 위치 가중치 중간값
                vrik.solver.spine.pelvisRotationWeight = 0f;    // 골반 회전은 비활성화
                hasAnyTarget = true;
            }

            // 왼발 타겟 설정 (왼발 트래커)
            if (leftFootTarget != null)
            {
                vrik.solver.leftLeg.target = leftFootTarget.transform;
                vrik.solver.leftLeg.positionWeight = 1f;  // 위치 추적 가중치 최대
                vrik.solver.leftLeg.rotationWeight = 0f;  // 발 회전은 비활성화 (자연스러운 움직임)
                hasAnyTarget = true;
            }

            // 오른발 타겟 설정 (오른발 트래커)
            if (rightFootTarget != null)
            {
                vrik.solver.rightLeg.target = rightFootTarget.transform;
                vrik.solver.rightLeg.positionWeight = 1f;  // 위치 추적 가중치 최대
                vrik.solver.rightLeg.rotationWeight = 0f;  // 발 회전은 비활성화 (자연스러운 움직임)
                hasAnyTarget = true;
            }

            // 무릎 굽힘 방향 제어 설정 (주석 처리됨)
            // 허리 트래커를 무릎 굽힘의 기준점으로 사용하여 자연스러운 다리 움직임 구현
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
                Debug.LogWarning(
                    "No VR Targets found! VRIK will use default behavior. This might cause network synchronization issues.");
            }
        }

        /// <summary>
        /// 아바타의 모든 주요 본(뼈대) 참조를 캐시하는 메서드
        /// VRIK references에서 휴머노이드 표준 본들을 수집하여 배열로 저장
        /// 네트워크 동기화 시 빠른 접근을 위해 미리 참조를 저장해둠
        /// </summary>
        private void CacheBoneReferences()
        {
            Debug.Log("=== CACHING BONE REFERENCES START ===");

            var refs = vrik.references;
            var tempBones = new System.Collections.Generic.List<Transform>();

            // 표준 휴머노이드 본들을 수집
            // 루트 본 (전체 아바타의 기준점)
            if (refs.root != null)
            {
                tempBones.Add(refs.root);
                Debug.Log($"Added root: {refs.root.name}");
            }

            // 골반 본 (하체의 기준점)
            if (refs.pelvis != null)
            {
                tempBones.Add(refs.pelvis);
                Debug.Log($"Added pelvis: {refs.pelvis.name}");
            }

            // 척추 본 (등뼈)
            if (refs.spine != null)
            {
                tempBones.Add(refs.spine);
                Debug.Log($"Added spine: {refs.spine.name}");
            }

            // 가슴 본 (상체)
            if (refs.chest != null)
            {
                tempBones.Add(refs.chest);
                Debug.Log($"Added chest: {refs.chest.name}");
            }

            // 목 본
            if (refs.neck != null)
            {
                tempBones.Add(refs.neck);
                Debug.Log($"Added neck: {refs.neck.name}");
            }

            // 머리 본
            if (refs.head != null)
            {
                tempBones.Add(refs.head);
                Debug.Log($"Added head: {refs.head.name}");
            }

            // 팔 본들 - 왼쪽 어깨부터 손까지
            if (refs.leftShoulder != null)
            {
                tempBones.Add(refs.leftShoulder);
                Debug.Log($"Added leftShoulder: {refs.leftShoulder.name}");
            }

            if (refs.leftUpperArm != null)
            {
                tempBones.Add(refs.leftUpperArm);
                Debug.Log($"Added leftUpperArm: {refs.leftUpperArm.name}");
            }

            if (refs.leftForearm != null)
            {
                tempBones.Add(refs.leftForearm);
                Debug.Log($"Added leftForearm: {refs.leftForearm.name}");
            }

            if (refs.leftHand != null)
            {
                tempBones.Add(refs.leftHand);
                Debug.Log($"Added leftHand: {refs.leftHand.name}");
            }

            // 팔 본들 - 오른쪽 어깨부터 손까지
            if (refs.rightShoulder != null)
            {
                tempBones.Add(refs.rightShoulder);
                Debug.Log($"Added rightShoulder: {refs.rightShoulder.name}");
            }

            if (refs.rightUpperArm != null)
            {
                tempBones.Add(refs.rightUpperArm);
                Debug.Log($"Added rightUpperArm: {refs.rightUpperArm.name}");
            }

            if (refs.rightForearm != null)
            {
                tempBones.Add(refs.rightForearm);
                Debug.Log($"Added rightForearm: {refs.rightForearm.name}");
            }

            if (refs.rightHand != null)
            {
                tempBones.Add(refs.rightHand);
                Debug.Log($"Added rightHand: {refs.rightHand.name}");
            }

            // 다리 본들 - 왼쪽 허벅지부터 발까지
            if (refs.leftThigh != null)
            {
                tempBones.Add(refs.leftThigh);
                Debug.Log($"Added leftThigh: {refs.leftThigh.name}");
            }

            if (refs.leftCalf != null)
            {
                tempBones.Add(refs.leftCalf);
                Debug.Log($"Added leftCalf: {refs.leftCalf.name}");
            }

            if (refs.leftFoot != null)
            {
                tempBones.Add(refs.leftFoot);
                Debug.Log($"Added leftFoot: {refs.leftFoot.name}");
            }

            // 다리 본들 - 오른쪽 허벅지부터 발까지
            if (refs.rightThigh != null)
            {
                tempBones.Add(refs.rightThigh);
                Debug.Log($"Added rightThigh: {refs.rightThigh.name}");
            }

            if (refs.rightCalf != null)
            {
                tempBones.Add(refs.rightCalf);
                Debug.Log($"Added rightCalf: {refs.rightCalf.name}");
            }

            if (refs.rightFoot != null)
            {
                tempBones.Add(refs.rightFoot);
                Debug.Log($"Added rightFoot: {refs.rightFoot.name}");
            }

            // 최종 배열로 변환 및 정보 저장
            boneReferences = tempBones.ToArray();
            boneCount = boneReferences.Length;

            Debug.Log(
                $"=== BONE CACHING COMPLETE === Total bones: {boneCount}, NetworkArray capacity: {BoneRotations.Length}");

            // 본 목록 출력 (디버깅용)
            for (int i = 0; i < boneCount; i++)
            {
                Debug.Log($"Bone[{i}]: {boneReferences[i].name} at {boneReferences[i].position}");
            }
        }

        /// <summary>
        /// Network Object의 유효성을 검사하는 헬퍼 메서드
        /// 네트워크 통신 전에 오브젝트 상태를 확인하여 오류 방지
        /// </summary>
        /// <returns>네트워크 오브젝트가 유효하면 true, 아니면 false</returns>
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

        /// <summary>
        /// Fusion의 고정 업데이트 네트워크 콜백 (서버 틱 레이트에 맞춤)
        /// Host에서만 실행되며, VR 본 데이터를 네트워크로 송신
        /// 정확한 물리 시뮬레이션과 동기화를 위해 FixedUpdate 사용
        /// </summary>
        public override void FixedUpdateNetwork()
        {
            if (!IsNetworkValid()) return;

            if (isHost)
            {
                // Host: VR 본 데이터를 네트워크로 송신
                UpdateHostData();
            }
        }

        /// <summary>
        /// Unity의 Update 콜백 - 매 프레임마다 호출
        /// Client에서만 실행되며, Host로부터 받은 본 데이터를 아바타에 적용
        /// 부드러운 시각적 동기화를 위해 Update 사용 (FixedUpdate보다 빈번)
        /// </summary>
        void Update()
        {
            if (!isHost && IsNetworkValid())
            {
                // Client: Host 본 데이터를 수신해서 아바타에 적용
                UpdateClientData();
            }
        }

        /// <summary>
        /// Host에서 VR 트래커 데이터를 읽어 네트워크로 송신하는 메서드
        /// VRIK 계산 결과인 모든 본의 회전값과 루트 위치/회전을 전송
        /// 실시간으로 계속 호출되므로 성능 최적화가 중요
        /// </summary>
        private void UpdateHostData()
        {
            // 데이터 초기화 플래그 설정 (Client들이 수신 시작할 수 있도록)
            IsDataInitialized = true;

            bool rootSent = false;
            int bonesSent = 0;

            // Root position/rotation 송신 (아바타 전체의 기준점)
            if (boneCount > 0 && boneReferences[0] != null)
            {
                Vector3 newRootPos = boneReferences[0].position;
                Quaternion newRootRot = boneReferences[0].rotation;

                RootPosition = newRootPos;
                RootRotation = newRootRot;
                rootSent = true;

                // 상세 디버그: Root 송신 데이터 (1초마다)
                if (dataUpdateCount % 60 == 0)
                {
                    Debug.Log($"Host Root Send: Pos={newRootPos}, Rot={newRootRot.eulerAngles}");
                }
            }
            else
            {
                if (dataUpdateCount % 120 == 0) // 2초마다
                {
                    Debug.LogError(
                        $"Host Root Error: boneCount={boneCount}, root={boneReferences?[0]?.name ?? "null"}");
                }
            }

            // 모든 본 회전 데이터 송신 (NetworkArray 용량 내에서)
            for (int i = 0; i < boneCount && i < BoneRotations.Length; i++)
            {
                if (boneReferences[i] != null)
                {
                    Quaternion boneRot = boneReferences[i].rotation;
                    BoneRotations.Set(i, boneRot);
                    bonesSent++;

                    // 상세 디버그: 특정 본 송신 데이터 (첫 5개만, 2초마다)
                    if (i < 5 && dataUpdateCount % 120 == 0)
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
                Debug.Log(
                    $"Host Send Status: IsDataInit={IsDataInitialized}, Position={RootPosition}, Rotation={RootRotation.eulerAngles}");

                // 오류 상황 감지 및 로그
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
                    Debug.LogWarning(
                        $"Host: PARTIAL bone data sent ({bonesSent}/{boneCount}). Some bones may be null.");
                }

                // NetworkArray 오버플로우 체크
                if (boneCount > BoneRotations.Length)
                {
                    Debug.LogError(
                        $"Host: NetworkArray OVERFLOW! Need {boneCount} but capacity is {BoneRotations.Length}");
                }

                dataUpdateCount = 0;
            }

            // 페이셜 트래킹 데이터 송신 (현재 주석 처리)
            //UpdateFacialData();
        }

        /// <summary>
        /// Host에서 페이셜 트래킹 데이터를 읽어 네트워크로 송신하는 메서드 (현재 주석 처리)
        /// 6가지 주요 표정(턱, 미소, 입벌림, 입오므림, 슬픔, 혀내밀기)을 전송
        /// 실제 구현 시 주석을 해제하여 사용
        /// </summary>
        // private void UpdateFacialData()
        // {
        //     if (facialTracking != null && facialTracking.IsFacialTrackingActive())
        //     {
        //         // 호스트에서 페이셜 데이터 읽기
        //         facialTracking.GetFacialData(out float jaw, out float smile, out float wide, out float o, out float sad, out float tongue);
        //         
        //         // 네트워크로 전송
        //         FacialJaw = jaw;
        //         FacialSmile = smile;
        //         FacialWide = wide;
        //         FacialO = o;
        //         FacialSad = sad;
        //         FacialTongue = tongue;
        //         
        //         // 상세 디버그: 페이셜 데이터 송신 (5초마다)
        //         if (dataUpdateCount % 300 == 0)
        //         {
        //             Debug.Log($"Host Facial Send: Jaw={jaw:F2}, Smile={smile:F2}, Wide={wide:F2}, O={o:F2}, Sad={sad:F2}, Tongue={tongue:F2}");
        //         }
        //     }
        //     else
        //     {
        //         // 페이셜 트래킹이 비활성화된 경우 모든 값을 0으로 설정
        //         FacialJaw = FacialSmile = FacialWide = FacialO = FacialSad = FacialTongue = 0f;
        //         
        //         if (dataUpdateCount % 600 == 0) // 10초마다
        //         {
        //             Debug.LogWarning("Host: Facial tracking inactive - sending zero values");
        //         }
        //     }
        // }

        /// <summary>
        /// Client에서 Host로부터 받은 본 데이터를 아바타에 적용하는 메서드
        /// 네트워크 지연과 패킷 손실을 고려한 유효성 검증 포함
        /// 부드러운 동기화를 위해 매 프레임 호출됨
        /// </summary>
        private void UpdateClientData()
        {
            // 네트워크 데이터가 초기화되지 않았다면 대기
            if (!IsDataInitialized)
            {
                // 디버깅: 데이터 초기화 대기 상태 로그
                if (Time.time - lastDebugTime > 2f)
                {
                    lastDebugTime = Time.time;
                    Debug.LogWarning(
                        $"Client: Waiting for host data initialization... IsDataInitialized={IsDataInitialized}");
                }

                return;
            }

            // 첫 번째 유효한 데이터 수신 시 로그
            if (!hasReceivedValidData)
            {
                hasReceivedValidData = true;
                Debug.Log(
                    $"Client: Received first valid network data from host! RootPos={RootPosition}, RootRot={RootRotation}");
            }

            bool rootApplied = false;
            // Root position/rotation 직접 적용 (유효성 검증 후)
            if (boneCount > 0 && boneReferences[0] != null)
            {
                // Root 데이터가 기본값이 아니고 유효한 범위 내에 있는 경우에만 적용
                if (IsValidPosition(RootPosition) && IsValidRotation(RootRotation))
                {
                    Vector3 oldPos = boneReferences[0].position;
                    Quaternion oldRot = boneReferences[0].rotation;

                    // 직접 적용 (VRIK 없이 본을 직접 제어)
                    boneReferences[0].position = RootPosition;
                    boneReferences[0].rotation = RootRotation;
                    rootApplied = true;

                    // 상세 디버그: Root 변화 로그 (1초마다)
                    if (dataUpdateCount % 60 == 0)
                    {
                        Debug.Log(
                            $"Client Root Applied: Pos {oldPos} -> {RootPosition}, Rot {oldRot.eulerAngles} -> {RootRotation.eulerAngles}");
                    }
                }
                else
                {
                    if (dataUpdateCount % 120 == 0) // 2초마다
                    {
                        Debug.LogWarning(
                            $"Client Root Invalid: Pos={RootPosition} (Valid={IsValidPosition(RootPosition)}), Rot={RootRotation} (Valid={IsValidRotation(RootRotation)})");
                    }
                }
            }

            // 모든 본 회전 데이터 직접 적용 (유효성 검증 후)
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
                        // 직접 적용 (Animator 없이 Transform을 직접 제어)
                        boneReferences[i].rotation = networkRotation;
                        validBonesCount++;

                        // 상세 디버그: 특정 본의 변화 로그 (첫 5개 본만, 2초마다)
                        if (i < 5 && dataUpdateCount % 120 == 0)
                        {
                            Debug.Log(
                                $"Client Bone[{i}] ({boneReferences[i].name}) Applied: {networkRotation.eulerAngles}");
                        }
                    }
                    else
                    {
                        invalidBonesCount++;
                        // 상세 디버그: 유효하지 않은 본 데이터 (3초마다)
                        if (i < 5 && dataUpdateCount % 180 == 0)
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
                Debug.Log(
                    $"Client Update #{dataUpdateCount}: Valid={validBonesCount}, Invalid={invalidBonesCount}, Total={boneCount}, RootApplied={rootApplied}");
                Debug.Log(
                    $"Client Network State: IsDataInit={IsDataInitialized}, HasValidData={hasReceivedValidData}, RootPos={RootPosition}");

                // 문제 상황 감지 및 경고
                if (validBonesCount == 0 && invalidBonesCount == 0)
                {
                    Debug.LogError("Client: NO BONE DATA AT ALL! Check network connection and host setup.");
                }
                else if (validBonesCount == 0)
                {
                    Debug.LogError(
                        $"Client: NO VALID bone data! All {invalidBonesCount} bones have invalid rotations.");
                }
                else if (validBonesCount < boneCount / 2)
                {
                    Debug.LogWarning(
                        $"Client: Low valid bone ratio ({validBonesCount}/{boneCount}). Possible network issues.");
                }

                dataUpdateCount = 0;
            }

            // 페이셜 트래킹 데이터 적용 (현재 주석 처리)
            //ApplyFacialData();
        }

        /// <summary>
        /// Client에서 Host로부터 받은 페이셜 트래킹 데이터를 적용하는 메서드 (현재 주석 처리)
        /// 6가지 주요 표정을 아바타에 실시간으로 반영
        /// 실제 구현 시 주석을 해제하여 사용
        /// </summary>
        // private void ApplyFacialData()
        // {
        //     if (facialTracking != null)
        //     {
        //         // 클라이언트에서 받은 페이셜 데이터 적용
        //         facialTracking.SetFacialData(FacialJaw, FacialSmile, FacialWide, FacialO, FacialSad, FacialTongue);
        //         
        //         // 상세 디버그: 페이셜 데이터 수신 (5초마다)
        //         if (dataUpdateCount % 300 == 0)
        //         {
        //             Debug.Log($"Client Facial Applied: Jaw={FacialJaw:F2}, Smile={FacialSmile:F2}, Wide={FacialWide:F2}, O={FacialO:F2}, Sad={FacialSad:F2}, Tongue={FacialTongue:F2}");
        //         }
        //         
        //         // 활성 표정 개수 체크
        //         int activeFacials = 0;
        //         if (FacialJaw > 0.01f) activeFacials++;
        //         if (FacialSmile > 0.01f) activeFacials++;
        //         if (FacialWide > 0.01f) activeFacials++;
        //         if (FacialO > 0.01f) activeFacials++;
        //         if (FacialSad > 0.01f) activeFacials++;
        //         if (FacialTongue > 0.01f) activeFacials++;
        //         
        //         if (dataUpdateCount % 600 == 0) // 10초마다
        //         {
        //             Debug.Log($"Client Facial Status: {activeFacials}/6 expressions active");
        //             if (activeFacials == 0)
        //             {
        //                 Debug.LogWarning("Client: No facial expressions detected - check host facial tracking");
        //             }
        //         }
        //     }
        //     else
        //     {
        //         if (dataUpdateCount % 600 == 0) // 10초마다
        //         {
        //             Debug.LogWarning("Client: No facial tracking component found");
        //         }
        //     }
        // }

        /// <summary>
        /// 위치 데이터의 유효성을 검증하는 메서드
        /// NaN, Infinity 값과 비현실적인 좌표값을 필터링
        /// 네트워크 오류나 계산 오류로 인한 이상값 방지
        /// </summary>
        /// <param name="position">검증할 위치 벡터</param>
        /// <returns>유효한 위치이면 true, 아니면 false</returns>
        private bool IsValidPosition(Vector3 position)
        {
            // NaN, Infinity 체크와 합리적인 범위 체크
            bool isValid = !float.IsNaN(position.x) && !float.IsNaN(position.y) && !float.IsNaN(position.z) &&
                           !float.IsInfinity(position.x) && !float.IsInfinity(position.y) &&
                           !float.IsInfinity(position.z) &&
                           position.magnitude < 10000f; // 10km 이내 (매우 관대한 범위)

            if (!isValid && dataUpdateCount % 120 == 0)
            {
                Debug.LogWarning($"Invalid Position: {position}, Magnitude: {position.magnitude}");
            }

            return isValid;
        }

        /// <summary>
        /// 회전 데이터(쿼터니언)의 유효성을 검증하는 메서드
        /// NaN, Infinity 값과 비정규화된 쿼터니언을 필터링
        /// 잘못된 회전으로 인한 아바타 변형 방지
        /// </summary>
        /// <param name="rotation">검증할 회전 쿼터니언</param>
        /// <returns>유효한 회전이면 true, 아니면 false</returns>
        private bool IsValidRotation(Quaternion rotation)
        {
            // NaN 체크와 기본적인 쿼터니언 유효성만 체크 (identity 제외)
            bool isValid = !float.IsNaN(rotation.x) && !float.IsNaN(rotation.y) && !float.IsNaN(rotation.z) &&
                           !float.IsNaN(rotation.w) &&
                           !float.IsInfinity(rotation.x) && !float.IsInfinity(rotation.y) &&
                           !float.IsInfinity(rotation.z) && !float.IsInfinity(rotation.w);

            // 쿼터니언 정규화 체크 (좀 더 관대하게)
            if (isValid)
            {
                float magnitude = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z +
                                  rotation.w * rotation.w;
                isValid = magnitude > 0.5f && magnitude < 2.0f; // 정규화된 쿼터니언은 1에 가까워야 함
            }

            if (!isValid && dataUpdateCount % 120 == 0)
            {
                Debug.LogWarning(
                    $"Invalid Rotation: {rotation}, Magnitude: {(rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w)}");
            }

            return isValid;
        }

        /// <summary>
        /// 개발자용 디버깅 메서드 - Unity Inspector의 Context Menu에서 실행 가능
        /// 현재 네트워크 상태, 컴포넌트 상태, 본 정보 등을 종합적으로 출력
        /// 문제 진단 시 유용한 모든 정보를 한 번에 확인 가능
        /// </summary>
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
                        Debug.Log(
                            $"Bone[{i}]: {boneReferences[i].name} at {boneReferences[i].position}, rot={boneReferences[i].rotation.eulerAngles}");
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
            Debug.Log(
                $"Jaw={FacialJaw:F2}, Smile={FacialSmile:F2}, Wide={FacialWide:F2}, O={FacialO:F2}, Sad={FacialSad:F2}, Tongue={FacialTongue:F2}");

            if (isHost)
            {
                Debug.Log("=== HOST VR TARGETS ===");
                Debug.Log($"Head Target: {(headTarget != null ? headTarget.name : "NULL")}");
                Debug.Log($"Left Hand Target: {(leftHandTarget != null ? leftHandTarget.name : "NULL")}");
                Debug.Log($"Right Hand Target: {(rightHandTarget != null ? rightHandTarget.name : "NULL")}");
                Debug.Log($"Waist Target: {(waistTarget != null ? waistTarget.name : "NULL")}");
                Debug.Log($"Left Foot Target: {(leftFootTarget != null ? leftFootTarget.name : "NULL")}");
                Debug.Log($"Right Foot Target: {(rightFootTarget != null ? rightFootTarget.name : "NULL")}");
            }

            Debug.Log("=== END DEBUG INFO ===");
        }

        /// <summary>
        /// 에러 상황에서 자동 복구를 시도하는 메서드 - Unity Inspector의 Context Menu에서 실행 가능
        /// 네트워크 동기화 문제나 VR 타겟 연결 문제 발생 시 수동으로 복구 시도
        /// 런타임 중 문제 해결을 위한 응급 처치 기능
        /// </summary>
        [ContextMenu("Attempt Auto Recovery")]
        private void AttemptAutoRecovery()
        {
            Debug.Log("=== ATTEMPTING AUTO RECOVERY ===");

            // Client의 데이터 수신 상태 리셋
            if (!isHost && !hasReceivedValidData)
            {
                Debug.Log("Client: Resetting data reception state...");
                hasReceivedValidData = false;
                dataUpdateCount = 0;
                lastDebugTime = 0f;
            }

            // Host의 VR 타겟 재검색 및 재설정
            if (isHost)
            {
                Debug.Log("Host: Re-finding VR targets...");
                FindVRTargets();
                SetupVRIKTargets();
            }
        }
    }
}