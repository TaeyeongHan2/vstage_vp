using UnityEngine;
using RootMotion.FinalIK;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace RootMotion.Demos
{
    /// <summary>
    /// 완전 자동화된 VR IK 캘리브레이션 시스템
    /// - 매직 넘버를 제거하고 모든 수치를 설정 가능한 변수로 분리
    /// - 사용자의 신체 측정부터 VRIK 적용까지 완전 자동화
    /// - 높은 정확도와 유연성을 제공하는 캘리브레이션 시스템
    /// 
    /// 주요 기능:
    /// 1. 자동 바닥 높이 감지
    /// 2. T-포즈 검증 및 신체 측정
    /// 3. 아바타와 사용자 비율 분석
    /// 4. 최적 스케일 계산 및 적용
    /// </summary>
    public class FullyAutomatedVRCalibration : MonoBehaviour
    {
        #region VRIK 및 트래커 설정
        
        [Header("VRIK 설정")]
        [Tooltip("아바타의 VRIK 컴포넌트 - 캘리브레이션 대상")]
        public VRIK ik;

        [Header("VR 트래커 할당")]
        [Tooltip("HMD (Vive Pro 2 헤드셋) - 머리 위치 및 방향 추적")]
        public Transform hmdTracker;
        
        [Tooltip("왼손 컨트롤러 - 왼팔 위치 추적")]
        public Transform leftControllerTracker;
        
        [Tooltip("오른손 컨트롤러 - 오른팔 위치 추적")]
        public Transform rightControllerTracker;
        
        [Tooltip("허리/골반 트래커 - 몸통 중심 위치 추적 (선택사항)")]
        public Transform waistTracker;
        
        [Tooltip("왼발 트래커 - 왼다리 위치 추적 (선택사항)")]
        public Transform leftFootTracker;
        
        [Tooltip("오른발 트래커 - 오른다리 위치 추적 (선택사항)")]
        public Transform rightFootTracker;

        #endregion

        #region 자동 측정 설정

        [Header("자동 측정 설정")]
        [Tooltip("바닥 높이를 발 트래커 위치로부터 자동 감지할지 여부")]
        public bool autoDetectFloor = true;
        
        [SerializeField, Tooltip("자동으로 감지된 바닥 높이 (Y축 기준)")]
        private float detectedFloorLevel = 0f;
        
        [Tooltip("정확도 향상을 위한 측정 샘플 수 - 많을수록 정확하지만 시간이 오래 걸림")]
        [Range(3, 10)]
        public int measurementSamples = 5;
        
        [Tooltip("각 측정 간격 (초) - 너무 짧으면 불안정, 너무 길면 사용자가 지침")]
        [Range(0.1f, 1f)]
        public float measurementInterval = 0.3f;

        #endregion

        #region 인체 측정 파라미터

        [Header("인체 측정 파라미터")]
        [Tooltip("어깨에서 HMD(머리)까지의 거리 - 목 길이 포함")]
        [Range(0.1f, 0.4f)]
        public float shoulderToHeadDistance = 0.2f;
        
        [Tooltip("어깨 너비의 절반 - 중심에서 한쪽 어깨까지의 거리")]
        [Range(0.1f, 0.3f)]
        public float shoulderWidth = 0.18f;
        
        [Tooltip("전체 키에서 허리 높이의 비율 (허리 트래커가 없을 때 추정용)")]
        [Range(0.4f, 0.7f)]
        public float waistHeightRatio = 0.55f;
        
        [Tooltip("바닥 감지 시 여유 공간 - 신발 두께나 측정 오차 고려")]
        [Range(0.01f, 0.2f)]
        public float floorDetectionMargin = 0.05f;

        #endregion

        #region T-포즈 검증 기준

        [Header("T-포즈 검증 기준")]
        [Tooltip("팔 각도 허용 범위 - 엄격 기준 (수평에서 벗어날 수 있는 각도)")]
        [Range(10f, 60f)]
        public float armAngleStrictThreshold = 45f;
        
        [Tooltip("팔 각도 허용 범위 - 관대 기준 (완벽하지 않아도 허용)")]
        [Range(30f, 90f)]
        public float armAngleLooseThreshold = 60f;
        
        [Tooltip("척추(몸통) 각도 허용 범위 - 엄격 기준 (수직에서 벗어날 수 있는 각도)")]
        [Range(5f, 45f)]
        public float spineAngleStrictThreshold = 30f;
        
        [Tooltip("척추(몸통) 각도 허용 범위 - 관대 기준")]
        [Range(20f, 60f)]
        public float spineAngleLooseThreshold = 45f;
        
        [Tooltip("양발 간격 최소값 - 너무 좁으면 불안정")]
        [Range(0.1f, 0.5f)]
        public float feetDistanceMin = 0.2f;
        
        [Tooltip("양발 간격 최대값 - 이상적 범위 (어깨너비 정도)")]
        [Range(0.5f, 1.5f)]
        public float feetDistanceMaxIdeal = 1.0f;
        
        [Tooltip("양발 간격 최대값 - 허용 가능한 범위")]
        [Range(1.0f, 2.0f)]
        public float feetDistanceMaxAllowed = 1.5f;

        #endregion

        #region 측정 정확도 기준

        [Header("측정 정확도 기준")]
        [Tooltip("T-포즈 검증 최소 점수 - 이 점수 이하면 측정하지 않음")]
        [Range(30f, 90f)]
        public float tPoseMinScore = 50f;
        
        [Tooltip("개별 측정 데이터의 최소 정확도 - 이 값 이하면 재측정")]
        [Range(50f, 95f)]
        public float measurementMinAccuracy = 70f;
        
        [Tooltip("전체 캘리브레이션의 최소 정확도 - 이 값 이하면 캘리브레이션 실패")]
        [Range(50f, 95f)]
        public float calibrationMinAccuracy = 70f;

        #endregion

        #region 인체 비율 검증 범위

        [Header("인체 비율 검증 범위")]
        [Tooltip("팔길이/키 비율의 최소값 - 일반적인 인체 비율 범위")]
        [Range(0.15f, 0.35f)]
        public float armToHeightRatioMin = 0.25f;
        
        [Tooltip("팔길이/키 비율의 최대값")]
        [Range(0.35f, 0.65f)]
        public float armToHeightRatioMax = 0.50f;
        
        [Tooltip("다리길이/키 비율의 최소값")]
        [Range(0.3f, 0.5f)]
        public float legToHeightRatioMin = 0.40f;
        
        [Tooltip("다리길이/키 비율의 최대값")]
        [Range(0.5f, 0.8f)]
        public float legToHeightRatioMax = 0.65f;

        #endregion

        #region 절대값 검증 범위

        [Header("절대값 검증 범위")]
        [Tooltip("허용되는 최소 키 - VR 환경에서 실제적인 범위")]
        [Range(0.5f, 1.5f)]
        public float minHeight = 1.0f;
        
        [Tooltip("허용되는 최대 키")]
        [Range(1.8f, 3.0f)]
        public float maxHeight = 2.5f;
        
        [Tooltip("허용되는 최소 팔 길이")]
        [Range(0.2f, 0.5f)]
        public float minArmLength = 0.3f;
        
        [Tooltip("허용되는 최대 팔 길이")]
        [Range(0.8f, 1.5f)]
        public float maxArmLength = 1.2f;

        #endregion

        #region 키 차이 보정 비율

        [Header("키 차이 보정 비율")]
        [Tooltip("머리 타겟 높이 조정 비율 - 사용자와 아바타 키 차이를 보정")]
        [Range(0f, 0.5f)]
        public float headHeightCompensationRatio = 0.1f;
        
        [Tooltip("허리 타겟 높이 조정 비율")]
        [Range(0f, 0.3f)]
        public float pelvisHeightCompensationRatio = 0.05f;
        
        [Tooltip("발 타겟 높이 조정 비율")]
        [Range(0f, 0.1f)]
        public float footHeightCompensationRatio = 0.02f;

        #endregion

        #region 스케일 계산 가중치

        [Header("스케일 계산 가중치")]
        [Tooltip("키 비율이 최종 스케일에 미치는 영향도")]
        [Range(0f, 1f)]
        public float heightRatioWeight = 0.6f;
        
        [Tooltip("팔 비율이 최종 스케일에 미치는 영향도")]
        [Range(0f, 1f)]
        public float armRatioWeight = 0.2f;
        
        [Tooltip("다리 비율이 최종 스케일에 미치는 영향도")]
        [Range(0f, 1f)]
        public float legRatioWeight = 0.2f;
        
        [Tooltip("허용되는 최소 스케일 배율 - 너무 작으면 부자연스러움")]
        [Range(0.3f, 0.8f)]
        public float minScale = 0.5f;
        
        [Tooltip("허용되는 최대 스케일 배율 - 너무 크면 부자연스러움")]
        [Range(1.2f, 3.0f)]
        public float maxScale = 2.0f;

        #endregion

        #region 측정 정확도 페널티

        [Header("측정 정확도 페널티")]
        [Tooltip("인체 비율을 벗어났을 때 적용되는 정확도 감점")]
        [Range(5f, 30f)]
        public float ratioViolationPenalty = 15f;
        
        [Tooltip("절대값 범위를 벗어났을 때 적용되는 정확도 감점")]
        [Range(10f, 40f)]
        public float absoluteValueViolationPenalty = 20f;
        
        [Tooltip("팔길이 범위를 벗어났을 때 적용되는 정확도 감점")]
        [Range(5f, 25f)]
        public float armLengthViolationPenalty = 10f;

        #endregion

        #region 타이밍 설정

        [Header("타이밍 설정")]
        [Tooltip("캘리브레이션 시작 전 카운트다운 시간")]
        [Range(1, 10)]
        public int countdownDuration = 3;
        
        [Tooltip("자세 안정성 확인을 위한 대기 시간")]
        [Range(0.1f, 1f)]
        public float poseStabilityCheckTime = 0.2f;
        
        [Tooltip("자세가 불안정할 때 재시도 전 대기 시간")]
        [Range(0.3f, 2f)]
        public float poseInstabilityWaitTime = 0.5f;
        
        [Tooltip("T-포즈 실패 시 재시도 전 대기 시간")]
        [Range(0.5f, 3f)]
        public float tPoseFailureWaitTime = 1f;

        #endregion

        #region 캘리브레이션 설정 및 상태

        [Header("캘리브레이션 설정")]
        [Tooltip("VRIK 캘리브레이션의 상세 설정 - 트래커 방향, 오프셋 등")]
        public VRIKCalibrator.Settings calibrationSettings = new VRIKCalibrator.Settings();

        [Header("캘리브레이션 상태")]
        [SerializeField, Tooltip("현재 캘리브레이션 프로세스의 진행 상태")]
        public CalibrationState currentState = CalibrationState.Ready;
        
        [SerializeField, Tooltip("측정된 데이터의 정확도 (%단위)")]
        public float measurementAccuracy = 0f;
        
        [SerializeField, Tooltip("계산된 최종 스케일 배율")]
        public float finalScale = 1f;
        
        [Tooltip("VRIK에서 사용하는 캘리브레이션 데이터")]
        public VRIKCalibrator.CalibrationData calibrationData = new VRIKCalibrator.CalibrationData();

        #endregion

        #region 측정 결과 (읽기 전용)

        [Header("측정 결과 (자동 계산됨)")]
        [SerializeField, Tooltip("측정된 사용자의 실제 키")]
        public float userHeight;
        
        [SerializeField, Tooltip("측정된 사용자의 팔 길이 (어깨에서 손목까지)")]
        public float userArmLength;
        
        [SerializeField, Tooltip("측정된 사용자의 다리 길이 (허리에서 발목까지)")]
        public float userLegLength;
        
        [SerializeField, Tooltip("계산된 아바타의 키")]
        public float avatarHeight;
        
        [SerializeField, Tooltip("계산된 아바타의 팔 길이")]
        public float avatarArmLength;
        
        [SerializeField, Tooltip("계산된 아바타의 다리 길이")]
        public float avatarLegLength;

        #endregion

        #region 상태 열거형 및 데이터 클래스
        
        /// <summary>
        /// 캘리브레이션 프로세스의 현재 상태를 나타내는 열거형
        /// </summary>
        public enum CalibrationState
        {
            Ready,              // 준비 상태 - 캘리브레이션 시작 가능
            Countdown,          // 카운트다운 중 - 사용자에게 준비 시간 제공
            DetectingFloor,     // 바닥 높이 감지 중
            MeasuringUser,      // 사용자 신체 측정 중 - T-포즈 필요
            CalculatingAvatar,  // 아바타 신체 측정값 계산 중
            ComputingScale,     // 최적 스케일 비율 계산 중
            ApplyingCalibration,// VRIK 캘리브레이션 적용 중
            Completed,          // 캘리브레이션 완료
            Error,              // 오류 발생
            Failed              // 캘리브레이션 실패
        }

        /// <summary>
        /// 개별 측정 데이터를 저장하는 클래스
        /// - 여러 샘플을 평균내어 정확도를 높이기 위해 사용
        /// </summary>
        [System.Serializable]
        public class MeasurementData
        {
            public float height;        // 측정된 키
            public float armLength;     // 측정된 팔 길이
            public float legLength;     // 측정된 다리 길이
            public float shoulderWidth; // 측정된 어깨 너비
            public float waistHeight;   // 측정된 허리 높이
            public float accuracy;      // 이 측정의 정확도
            public float timestamp;     // 측정 시간
        }

        #endregion

        #region Private 변수들
        
        // 측정 샘플들을 저장하는 리스트
        // 문제점: List<T>는 참조 타입이므로 GC 압박을 줄 수 있음
        // 개선 방향: CircularBuffer나 ArrayPool 사용 고려
        private List<MeasurementData> measurementSamples_list = new List<MeasurementData>();
        
        // 현재 실행 중인 캘리브레이션 코루틴
        // 문제점: 여러 코루틴이 동시에 실행될 위험성 있음
        // 개선 방향: 코루틴 실행 전 이전 코루틴 확실히 정리
        private Coroutine calibrationCoroutine;
        
        // 캘리브레이션 진행 중 여부
        public bool calibrationInProgress = false;
        
        // UI 표시용 카운트다운 타이머
        public int countdownTimer = 0;

        #endregion

        #region Unity 생명주기
        
        /// <summary>
        /// 시작 시 초기화 함수
        /// </summary>
        void Start()
        {
            // 캘리브레이션 기본 설정 초기화
            InitializeCalibrationSettings();
            
            // 설정값들의 유효성 검증
            ValidateSettings();
        }

        /// <summary>
        /// 매 프레임 업데이트 함수
        /// 문제점: 매 프레임마다 키 입력 체크는 성능상 비효율적
        /// 개선 방향: 이벤트 기반 입력 시스템 또는 InputSystem 사용 고려
        /// </summary>
        void Update()
        {
            // 스페이스바로 완전 자동 캘리브레이션 시작
            if (Input.GetKeyDown(KeyCode.Space) && !calibrationInProgress)
            {
                StartFullAutoCalibration();
            }

            // ESC로 캘리브레이션 중단
            if (Input.GetKeyDown(KeyCode.Escape) && calibrationInProgress)
            {
                StopCalibration();
            }

            // 캘리브레이션 완료 후 실시간 스케일 조정 기능 제거됨
            // 이전 버전에서는 여기서 실시간 조정을 했으나, 안정성을 위해 제거

            // R 키로 전체 리셋
            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetCalibration();
            }
        }

        #endregion

        #region 설정 검증
        
        /// <summary>
        /// 사용자가 설정한 값들의 유효성을 검증
        /// - 논리적 오류나 잘못된 설정을 미리 감지
        /// </summary>
        void ValidateSettings()
        {
            // 스케일 계산 가중치들의 합이 1인지 확인
            float totalWeight = heightRatioWeight + armRatioWeight + legRatioWeight;
            if (Mathf.Abs(totalWeight - 1f) > 0.01f)
            {
                Debug.LogWarning($"[캘리브레이션 경고] 스케일 가중치 합계가 1이 아닙니다! 현재: {totalWeight:F3}");
                // 문제점: 경고만 출력하고 자동 보정하지 않음
                // 개선 방향: 자동으로 정규화하거나 기본값으로 재설정
            }

            // 인체 비율 범위의 논리적 일관성 검증
            if (armToHeightRatioMin >= armToHeightRatioMax)
            {
                Debug.LogError("[캘리브레이션 오류] 팔/키 비율 최소값이 최대값보다 크거나 같습니다!");
            }

            if (legToHeightRatioMin >= legToHeightRatioMax)
            {
                Debug.LogError("[캘리브레이션 오류] 다리/키 비율 최소값이 최대값보다 크거나 같습니다!");
            }

            // 추가 검증: 절대값 범위도 확인
            if (minHeight >= maxHeight)
            {
                Debug.LogError("[캘리브레이션 오류] 최소 키가 최대 키보다 크거나 같습니다!");
            }

            if (minArmLength >= maxArmLength)
            {
                Debug.LogError("[캘리브레이션 오류] 최소 팔길이가 최대 팔길이보다 크거나 같습니다!");
            }
        }

        #endregion

        #region 캘리브레이션 시작/중단
        
        /// <summary>
        /// 완전 자동 캘리브레이션 프로세스 시작
        /// - 사용자 개입 없이 모든 과정을 자동으로 수행
        /// </summary>
        public void StartFullAutoCalibration()
        {
            // 중복 실행 방지
            if (calibrationInProgress)
            {
                Debug.LogWarning("[캘리브레이션] 이미 캘리브레이션이 진행 중입니다.");
                return;
            }

            Debug.Log("[캘리브레이션] 완전 자동 VR 캘리브레이션을 시작합니다!");
            Debug.Log($"[캘리브레이션] {countdownDuration}초 후 시작됩니다. T-포즈를 준비해주세요!");
            
            // 이전 코루틴이 있다면 정리 (안전장치)
            if (calibrationCoroutine != null)
            {
                StopCoroutine(calibrationCoroutine);
            }
            
            // 새로운 캘리브레이션 코루틴 시작
            calibrationCoroutine = StartCoroutine(FullCalibrationProcess());
        }

        #endregion

        #region 메인 캘리브레이션 프로세스
        
                /// <summary>
        /// 완전 자동 캘리브레이션의 메인 프로세스
        /// - 6단계의 순차적 진행
        /// - 각 단계별 오류 처리 및 검증
        /// 
        /// 주의: C#에서는 try-catch 블록 안에서 yield return을 사용할 수 없으므로
        /// 각 단계별로 예외 처리를 분리하여 구현
        /// </summary>
        IEnumerator FullCalibrationProcess()
        {
            calibrationInProgress = true;
            currentState = CalibrationState.Countdown;
            string errorMessage = null;
            
            // === 카운트다운 단계 ===
            Debug.Log("[단계 0] 캘리브레이션 준비 중...");
            for (int i = countdownDuration; i > 0; i--)
            {
                countdownTimer = i;
                Debug.Log($"[카운트다운] {i}초 후 시작... T-포즈를 준비하세요!");
                yield return new WaitForSeconds(1f);
            }
            countdownTimer = 0;
            Debug.Log("[카운트다운] 캘리브레이션 시작!");
            
            // === 1단계: 트래커 연결 확인 ===
            currentState = CalibrationState.Ready;
            Debug.Log("[단계 1] 트래커 연결 상태 확인 중...");
            
            try
            {
                if (!ValidateTrackers())
                {
                    errorMessage = "필수 트래커가 연결되지 않음";
                }
            }
            catch (System.Exception e)
            {
                errorMessage = $"트래커 검증 중 오류: {e.Message}";
            }
            
            if (errorMessage != null) goto HandleError;
            yield return new WaitForSeconds(0.5f);

            // === 2단계: 바닥 높이 자동 감지 ===
            currentState = CalibrationState.DetectingFloor;
            Debug.Log("[단계 2] 바닥 높이 자동 감지 중...");
            
            try
            {
                DetectFloorLevel();
            }
            catch (System.Exception e)
            {
                errorMessage = $"바닥 감지 중 오류: {e.Message}";
            }
            
            if (errorMessage != null) goto HandleError;
            yield return new WaitForSeconds(0.5f);

            // === 3단계: 사용자 신체 자동 측정 ===
            currentState = CalibrationState.MeasuringUser;
            Debug.Log("[단계 3] 사용자 신체 자동 측정 시작 - T-포즈를 취해주세요!");
            
            yield return StartCoroutine(AutoMeasureUserBody());
            
            // 측정 정확도 검증
            if (measurementAccuracy < calibrationMinAccuracy)
            {
                errorMessage = $"측정 정확도가 너무 낮습니다 ({measurementAccuracy:F1}%). 최소 기준: {calibrationMinAccuracy}%";
                goto HandleError;
            }

            // === 4단계: 아바타 측정값 계산 ===
            currentState = CalibrationState.CalculatingAvatar;
            Debug.Log("[단계 4] 아바타 신체 측정값 계산 중...");
            
            try
            {
                CalculateAvatarMeasurements();
            }
            catch (System.Exception e)
            {
                errorMessage = $"아바타 측정 중 오류: {e.Message}";
            }
            
            if (errorMessage != null) goto HandleError;
            yield return new WaitForSeconds(0.5f);

            // === 5단계: 스케일 계산 ===
            currentState = CalibrationState.ComputingScale;
            Debug.Log("[단계 5] 최적 스케일 비율 계산 중...");
            
            try
            {
                CalculateOptimalScale();
            }
            catch (System.Exception e)
            {
                errorMessage = $"스케일 계산 중 오류: {e.Message}";
            }
            
            if (errorMessage != null) goto HandleError;
            yield return new WaitForSeconds(0.5f);

            // === 6단계: VRIK 캘리브레이션 적용 ===
            currentState = CalibrationState.ApplyingCalibration;
            Debug.Log("[단계 6] VRIK 캘리브레이션 적용 중...");
            
            try
            {
                ApplyVRIKCalibration();
            }
            catch (System.Exception e)
            {
                errorMessage = $"VRIK 캘리브레이션 적용 중 오류: {e.Message}";
            }
            
            if (errorMessage != null) goto HandleError;
            yield return new WaitForSeconds(1f);

            // === 완료 ===
            currentState = CalibrationState.Completed;
            Debug.Log("[완료] 완전 자동 캘리브레이션이 성공적으로 완료되었습니다!");
            
            LogFinalResults();
            
            // 정리 작업
            calibrationInProgress = false;
            countdownTimer = 0;
            calibrationCoroutine = null;
            yield break;

            // === 오류 처리 레이블 ===
            HandleError:
            Debug.LogError($"[캘리브레이션 오류] {errorMessage}");
            currentState = CalibrationState.Error;
            calibrationInProgress = false;
            countdownTimer = 0;
            calibrationCoroutine = null;
        }

        #endregion

        #region 사용자 신체 측정
        
        /// <summary>
        /// 사용자 신체를 자동으로 측정하는 프로세스
        /// - 여러 번 측정하여 평균값 계산
        /// - T-포즈 유효성 검사 및 자세 안정성 확인
        /// </summary>
        IEnumerator AutoMeasureUserBody()
        {
            // 이전 측정 데이터 초기화
            measurementSamples_list.Clear();
            int successfulMeasurements = 0;
            int maxAttempts = measurementSamples * 3; // 최대 시도 횟수 (실패 고려)
            int attempts = 0;

            Debug.Log($"[측정 시작] 목표: {measurementSamples}개 샘플, 최대 시도: {maxAttempts}회");

            // 충분한 샘플을 얻을 때까지 반복
            while (successfulMeasurements < measurementSamples && attempts < maxAttempts)
            {
                attempts++;
                Debug.Log($"[측정 시도] {attempts}/{maxAttempts} (성공: {successfulMeasurements}/{measurementSamples})");

                // T-포즈 유효성 검사
                if (!ValidateTPose())
                {
                    Debug.LogWarning("[측정 실패] T-포즈가 올바르지 않습니다. 자세를 다시 취해주세요.");
                    yield return new WaitForSeconds(tPoseFailureWaitTime);
                    continue;
                }

                // 자세 안정성 확인 (움직임 감지)
                yield return new WaitForSeconds(poseStabilityCheckTime);
                if (!CheckPoseStability())
                {
                    Debug.LogWarning("[측정 실패] 자세가 불안정합니다. 움직이지 마세요.");
                    yield return new WaitForSeconds(poseInstabilityWaitTime);
                    continue;
                }

                // 실제 신체 측정 수행
                MeasurementData measurement = PerformSingleMeasurement();
                
                // 측정 정확도 검증
                if (measurement.accuracy > measurementMinAccuracy)
                {
                    measurementSamples_list.Add(measurement);
                    successfulMeasurements++;
                    Debug.Log($"[측정 성공] ({successfulMeasurements}/{measurementSamples}) - 정확도: {measurement.accuracy:F1}%");
                }
                else
                {
                    Debug.LogWarning($"[측정 실패] 정확도 부족 ({measurement.accuracy:F1}%) - 재시도 (기준: {measurementMinAccuracy}%)");
                }

                // 다음 측정까지 대기
                yield return new WaitForSeconds(measurementInterval);
            }

            // 측정 결과 검증
            if (successfulMeasurements < measurementSamples)
            {
                Debug.LogWarning($"[측정 경고] 충분한 측정 데이터를 얻지 못했습니다. ({successfulMeasurements}/{measurementSamples})");
                // 문제점: 부족한 데이터로도 진행함
                // 개선 방향: 최소 필요한 샘플 수 설정하여 실패 처리 고려
            }

            // 최종 사용자 측정값 계산 (가중 평균)
            CalculateFinalUserMeasurements();
        }

        #endregion

        #region 개별 측정 함수들
        
        /// <summary>
        /// 한 번의 신체 측정을 수행
        /// - 현재 트래커 위치를 기반으로 신체 치수 계산
        /// - 측정 정확도도 함께 계산
        /// </summary>
        MeasurementData PerformSingleMeasurement()
        {
            MeasurementData measurement = new MeasurementData();
            measurement.timestamp = Time.time;

            // === 키 측정 (HMD 높이 기준) ===
            measurement.height = hmdTracker.position.y - detectedFloorLevel;

            // === 팔 길이 측정 (양팔 평균) ===
            float leftArmLength = 0f;
            float rightArmLength = 0f;

            // 왼팔 길이 계산 (어깨 추정 위치에서 컨트롤러까지)
            if (leftControllerTracker != null)
            {
                Vector3 leftShoulder = hmdTracker.position + Vector3.down * shoulderToHeadDistance + Vector3.left * shoulderWidth;
                leftArmLength = Vector3.Distance(leftShoulder, leftControllerTracker.position);
            }

            // 오른팔 길이 계산
            if (rightControllerTracker != null)
            {
                Vector3 rightShoulder = hmdTracker.position + Vector3.down * shoulderToHeadDistance + Vector3.right * shoulderWidth;
                rightArmLength = Vector3.Distance(rightShoulder, rightControllerTracker.position);
            }

            // 양팔 평균 (한쪽만 있으면 그 값 사용)
            if (leftArmLength > 0 && rightArmLength > 0)
            measurement.armLength = (leftArmLength + rightArmLength) * 0.5f;
            else if (leftArmLength > 0)
                measurement.armLength = leftArmLength;
            else if (rightArmLength > 0)
                measurement.armLength = rightArmLength;
            else
                measurement.armLength = 0f; // 팔 측정 불가

            // === 다리 길이 측정 ===
            if (waistTracker != null && leftFootTracker != null)
            {
                // 허리 트래커가 있으면 정확한 다리 길이 측정 가능
                measurement.legLength = waistTracker.position.y - leftFootTracker.position.y;
                measurement.waistHeight = waistTracker.position.y - detectedFloorLevel;
            }
            else if (leftFootTracker != null)
            {
                // 허리 트래커가 없으면 키의 비율로 허리 높이 추정
                measurement.waistHeight = measurement.height * waistHeightRatio;
                measurement.legLength = measurement.waistHeight - (leftFootTracker.position.y - detectedFloorLevel);
            }
            else
            {
                // 발 트래커도 없으면 다리 길이 측정 불가
                measurement.legLength = 0f;
                measurement.waistHeight = measurement.height * waistHeightRatio; // 추정값
            }

            // === 어깨 너비 측정 ===
            if (leftControllerTracker != null && rightControllerTracker != null)
            {
                measurement.shoulderWidth = Vector3.Distance(leftControllerTracker.position, rightControllerTracker.position);
            }
            else
            {
                measurement.shoulderWidth = shoulderWidth * 2f; // 기본값 사용
            }

            // === 측정 정확도 계산 ===
            measurement.accuracy = CalculateMeasurementAccuracy(measurement);

            return measurement;
        }

        /// <summary>
        /// T-포즈의 유효성을 검사
        /// - 팔이 수평인지, 몸이 똑바른지, 발 위치가 적절한지 확인
        /// - 점수 기반 시스템으로 정확도 평가
        /// </summary>
        bool ValidateTPose()
        {
            float tPoseScore = 0f;

            // === 1. 팔이 수평인지 확인 (40점 만점) ===
            if (leftControllerTracker != null && rightControllerTracker != null)
            {
                // HMD 위치를 기준으로 한 팔 벡터 계산
                Vector3 leftArm = leftControllerTracker.position - hmdTracker.position;
                Vector3 rightArm = rightControllerTracker.position - hmdTracker.position;

                // 각 팔이 이상적인 방향(좌우)과 얼마나 일치하는지 계산
                float leftAngle = Vector3.Angle(leftArm.normalized, Vector3.left);
                float rightAngle = Vector3.Angle(rightArm.normalized, Vector3.right);

                Debug.Log($"[T-포즈 검증] 팔 각도 - 왼팔: {leftAngle:F1}°, 오른팔: {rightAngle:F1}°");

                // 점수 부여 (엄격 기준 vs 관대 기준)
                if (leftAngle < armAngleStrictThreshold && rightAngle < armAngleStrictThreshold)
                    tPoseScore += 40f; // 완벽한 수평
                else if (leftAngle < armAngleLooseThreshold && rightAngle < armAngleLooseThreshold)
                    tPoseScore += 20f; // 어느 정도 수평
                // else 0점 (팔이 너무 처져 있음)
            }
            else
            {
                Debug.LogWarning("[T-포즈 검증] 양손 컨트롤러가 필요합니다.");
                // 문제점: 컨트롤러가 없으면 팔 검증 불가
                // 개선 방향: 핸드 트래킹이나 다른 방법 고려
            }

            // === 2. 몸이 똑바로 서 있는지 확인 (30점 만점) ===
            if (waistTracker != null)
            {
                // 허리에서 머리로 향하는 벡터 (척추 방향)
                Vector3 spine = hmdTracker.position - waistTracker.position;
                float spineAngle = Vector3.Angle(spine, Vector3.up);
                
                Debug.Log($"[T-포즈 검증] 척추 각도: {spineAngle:F1}° (수직 기준)");

                // 점수 부여
                if (spineAngle < spineAngleStrictThreshold)
                    tPoseScore += 30f; // 완전히 똑바름
                else if (spineAngle < spineAngleLooseThreshold)
                    tPoseScore += 15f; // 약간 기울어져 있음
                // else 0점 (너무 기울어져 있음)
            }
            else
            {
                // 허리 트래커가 없으면 기본 점수 부여 (관대하게 처리)
                tPoseScore += 20f;
                Debug.Log("[T-포즈 검증] 허리 트래커 없음 - 기본 점수 부여");
            }

            // === 3. 양발 위치 확인 (30점 만점) ===
            if (leftFootTracker != null && rightFootTracker != null)
            {
                Vector3 feetVector = rightFootTracker.position - leftFootTracker.position;
                float feetDistance = feetVector.magnitude;
                
                Debug.Log($"[T-포즈 검증] 발 간격: {feetDistance:F2}m");
                
                // 적절한 발 간격인지 확인 (어깨너비 정도가 이상적)
                if (feetDistance > feetDistanceMin && feetDistance < feetDistanceMaxIdeal)
                    tPoseScore += 30f; // 이상적인 간격
                else if (feetDistance > (feetDistanceMin * 0.5f) && feetDistance < feetDistanceMaxAllowed)
                    tPoseScore += 15f; // 허용 가능한 간격
                // else 0점 (너무 좁거나 너무 넓음)
            }
            else
            {
                // 발 트래커가 없으면 기본 점수 부여
                tPoseScore += 20f;
                Debug.Log("[T-포즈 검증] 발 트래커 없음 - 기본 점수 부여");
            }

            Debug.Log($"[T-포즈 검증] 총 점수: {tPoseScore:F1}% (기준: {tPoseMinScore}%)");

            return tPoseScore >= tPoseMinScore;
        }

        /// <summary>
        /// 자세의 안정성을 확인
        /// 문제점: 현재는 단순히 true만 반환하는 더미 함수
        /// 개선 방향: 실제로 연속된 프레임에서 위치 변화량을 측정하여 움직임 감지
        /// </summary>
        bool CheckPoseStability()
        {
            // TODO: 실제 구현 필요
            // - 이전 프레임들의 트래커 위치를 저장
            // - 현재 위치와 비교하여 움직임 정도 계산
            // - 임계값 이하일 때만 안정적으로 판단
            
            return true; // 임시로 항상 안정적이라고 가정
        }

        #endregion

        #region 측정 정확도 계산
        
        /// <summary>
        /// 측정된 데이터의 정확도를 계산
        /// - 인체 비율, 절대값 범위 등을 검증하여 점수 산출
        /// - 비정상적인 측정값에 대해 페널티 적용
        /// </summary>
        float CalculateMeasurementAccuracy(MeasurementData measurement)
        {
            float accuracy = 100f; // 만점에서 시작하여 문제 발견 시 감점

            Debug.Log($"[정확도 계산] 측정값 - 키: {measurement.height:F2}m, 팔: {measurement.armLength:F2}m, 다리: {measurement.legLength:F2}m");

            // === 1. 인체 비율 검사 ===
            
            // 팔길이/키 비율 검증
            if (measurement.armLength > 0 && measurement.height > 0)
            {
                float armRatio = measurement.armLength / measurement.height;
                Debug.Log($"[정확도 계산] 팔/키 비율: {armRatio:F3} (정상 범위: {armToHeightRatioMin:F2}-{armToHeightRatioMax:F2})");
                
                if (armRatio < armToHeightRatioMin || armRatio > armToHeightRatioMax)
                {
                    accuracy -= ratioViolationPenalty;
                    Debug.LogWarning($"[정확도 감점] 팔/키 비율 이상 - {ratioViolationPenalty}점 감점");
                }
            }

            // 다리길이/키 비율 검증
            if (measurement.legLength > 0 && measurement.height > 0)
            {
                float legRatio = measurement.legLength / measurement.height;
                Debug.Log($"[정확도 계산] 다리/키 비율: {legRatio:F3} (정상 범위: {legToHeightRatioMin:F2}-{legToHeightRatioMax:F2})");
                
                if (legRatio < legToHeightRatioMin || legRatio > legToHeightRatioMax)
                {
                    accuracy -= ratioViolationPenalty;
                    Debug.LogWarning($"[정확도 감점] 다리/키 비율 이상 - {ratioViolationPenalty}점 감점");
                }
            }

            // === 2. 절대값 검사 ===
            
            // 키 범위 검증
            if (measurement.height < minHeight || measurement.height > maxHeight)
            {
                accuracy -= absoluteValueViolationPenalty;
                Debug.LogWarning($"[정확도 감점] 키 범위 벗어남: {measurement.height:F2}m (정상: {minHeight:F1}-{maxHeight:F1}m) - {absoluteValueViolationPenalty}점 감점");
            }
            
            // 팔길이 범위 검증
            if (measurement.armLength > 0 && (measurement.armLength < minArmLength || measurement.armLength > maxArmLength))
            {
                accuracy -= armLengthViolationPenalty;
                Debug.LogWarning($"[정확도 감점] 팔길이 범위 벗어남: {measurement.armLength:F2}m (정상: {minArmLength:F1}-{maxArmLength:F1}m) - {armLengthViolationPenalty}점 감점");
            }

            // === 3. 추가 검증 (필요 시) ===
            // TODO: 어깨 너비, 머리 크기 등 추가 검증 가능

            Debug.Log($"[정확도 계산] 최종 정확도: {accuracy:F1}%");

            // 0-100% 범위로 제한
            return Mathf.Clamp(accuracy, 0f, 100f);
        }

        #endregion

        #region 최종 측정값 계산
        
        /// <summary>
        /// 여러 샘플의 측정값을 정확도 가중 평균으로 최종 값 계산
        /// - 정확도가 높은 측정값에 더 높은 가중치 부여
        /// - 전체 측정 정확도도 함께 계산
        /// </summary>
        void CalculateFinalUserMeasurements()
        {
            if (measurementSamples_list.Count == 0) return;

            float totalWeight = 0f;
            userHeight = 0f;
            userArmLength = 0f;
            userLegLength = 0f;

            // === 정확도 기반 가중 평균 계산 ===
            foreach (var sample in measurementSamples_list)
            {
                // 정확도를 가중치로 사용 (0~1 범위)
                float weight = sample.accuracy / 100f;
                totalWeight += weight;

                // 각 측정값에 가중치 적용하여 누적
                userHeight += sample.height * weight;
                userArmLength += sample.armLength * weight;
                userLegLength += sample.legLength * weight;
            }

            // === 가중 평균으로 최종값 계산 ===
            if (totalWeight > 0)
            {
                userHeight /= totalWeight;
                userArmLength /= totalWeight;
                userLegLength /= totalWeight;

                // === 전체 측정 정확도 계산 (단순 평균) ===
                measurementAccuracy = 0f;
                foreach (var sample in measurementSamples_list)
                {
                    measurementAccuracy += sample.accuracy;
                }
                measurementAccuracy /= measurementSamples_list.Count;
            }
            else
            {
                Debug.LogError("[측정 오류] 총 가중치가 0입니다. 모든 측정의 정확도가 0%입니다.");
                measurementAccuracy = 0f;
            }

            Debug.Log($"[측정 완료] 사용자 최종 측정값 - 키: {userHeight:F2}m, 팔: {userArmLength:F2}m, 다리: {userLegLength:F2}m (전체 정확도: {measurementAccuracy:F1}%)");
        }

        #endregion

        #region 아바타 측정
        
        /// <summary>
        /// 아바타의 신체 치수를 계산
        /// - VRIK의 본 구조를 분석하여 실제 아바타 크기 측정
        /// - 사용자 측정값과 비교를 위한 기준값 제공
        /// </summary>
        void CalculateAvatarMeasurements()
        {
            if (ik?.references == null)
            {
                throw new System.Exception("VRIK 컴포넌트 또는 본 참조가 설정되지 않았습니다.");
            }

            // === 아바타 키 계산 ===
            // 머리 본에서 루트 본까지의 높이 차이
            if (ik.references.head != null && ik.references.root != null)
            {
            avatarHeight = ik.references.head.position.y - ik.references.root.position.y;
            }
            else
            {
                throw new System.Exception("아바타의 머리 또는 루트 본이 설정되지 않았습니다.");
            }

            // === 아바타 팔 길이 계산 ===
            // 상완 + 전완 길이의 합
            if (ik.references.leftUpperArm && ik.references.leftForearm && ik.references.leftHand)
            {
                float upperArm = Vector3.Distance(ik.references.leftUpperArm.position, ik.references.leftForearm.position);
                float forearm = Vector3.Distance(ik.references.leftForearm.position, ik.references.leftHand.position);
                avatarArmLength = upperArm + forearm;
            }
            else
            {
                Debug.LogWarning("[아바타 측정] 왼팔 본이 완전하지 않습니다. 팔 길이를 0으로 설정합니다.");
                avatarArmLength = 0f;
            }

            // === 아바타 다리 길이 계산 ===
            // 허벅지 + 종아리 길이의 합
            if (ik.references.leftThigh && ik.references.leftCalf && ik.references.leftFoot)
            {
                float thigh = Vector3.Distance(ik.references.leftThigh.position, ik.references.leftCalf.position);
                float calf = Vector3.Distance(ik.references.leftCalf.position, ik.references.leftFoot.position);
                avatarLegLength = thigh + calf;
            }
            else
            {
                Debug.LogWarning("[아바타 측정] 왼다리 본이 완전하지 않습니다. 다리 길이를 0으로 설정합니다.");
                avatarLegLength = 0f;
            }

            Debug.Log($"[아바타 측정] 완료 - 키: {avatarHeight:F2}m, 팔: {avatarArmLength:F2}m, 다리: {avatarLegLength:F2}m");

            // === 측정값 유효성 검증 ===
            if (avatarHeight <= 0)
            {
                throw new System.Exception($"아바타 키가 유효하지 않습니다: {avatarHeight:F2}m");
            }
            
            // 문제점: 팔이나 다리 길이가 0이어도 진행됨
            // 개선 방향: 필수 측정값에 대한 더 엄격한 검증 필요
        }

        #endregion

        #region 스케일 계산
        
        /// <summary>
        /// 사용자와 아바타의 비율을 분석하여 최적 스케일 계산
        /// - 키, 팔, 다리 비율을 가중 평균으로 종합
        /// - 설정된 범위 내로 스케일 제한
        /// </summary>
        void CalculateOptimalScale()
        {
            if (avatarHeight <= 0)
            {
                throw new System.Exception("아바타 키가 유효하지 않아 스케일을 계산할 수 없습니다.");
            }

            // === 각 부위별 비율 계산 ===
            
            // 키 비율 (기본)
            float heightRatio = userHeight / avatarHeight;
            
            // 팔 비율 (없으면 키 비율 사용)
            float armRatio = avatarArmLength > 0 ? userArmLength / avatarArmLength : heightRatio;
            
            // 다리 비율 (없으면 키 비율 사용)
            float legRatio = avatarLegLength > 0 ? userLegLength / avatarLegLength : heightRatio;

            Debug.Log($"[스케일 계산] 개별 비율 - 키: {heightRatio:F3}, 팔: {armRatio:F3}, 다리: {legRatio:F3}");

            // === 가중 평균으로 최종 스케일 계산 ===
            finalScale = (heightRatio * heightRatioWeight) + 
                        (armRatio * armRatioWeight) + 
                        (legRatio * legRatioWeight);
            
            Debug.Log($"[스케일 계산] 가중 평균 전: {finalScale:F3} (가중치: 키{heightRatioWeight:F1} + 팔{armRatioWeight:F1} + 다리{legRatioWeight:F1})");
            
            // === 설정된 범위로 스케일 제한 ===
            float originalScale = finalScale;
            finalScale = Mathf.Clamp(finalScale, minScale, maxScale);

            if (originalScale != finalScale)
            {
                Debug.LogWarning($"[스케일 제한] {originalScale:F3} → {finalScale:F3} (범위: {minScale:F1}-{maxScale:F1})");
            }

            // === 캘리브레이션 설정 ===
            // 아바타 크기는 원본 유지하고, VRIK 타겟 위치로만 보정
            calibrationSettings.scaleMlp = 1.0f;

            Debug.Log($"[스케일 계산] 완료 - 최종 스케일: {finalScale:F3}");
            Debug.Log($"[스케일 계산] 아바타 크기는 원본 유지 (scaleMlp = 1.0), VRIK 타겟 위치로 보정");
        }

        #endregion

        #region VRIK 캘리브레이션 적용
        
        /// <summary>
        /// 계산된 스케일을 바탕으로 VRIK 캘리브레이션 적용
        /// - 아바타 크기는 변경하지 않고 VRIK 타겟 위치만 조정
        /// - 사용자와 아바타의 키 차이를 보정
        /// </summary>
        void ApplyVRIKCalibration()
        {
            try
            {
                // === 기본 VRIK 캘리브레이션 수행 ===
                // 아바타 크기 변경 없이 트래커 위치 매핑
            calibrationData = VRIKCalibrator.Calibrate(
                ik,
                calibrationSettings,
                hmdTracker,
                waistTracker,
                leftControllerTracker,
                rightControllerTracker,
                leftFootTracker,
                rightFootTracker
            );

                // === 키 차이 보정 적용 ===
                // 사용자와 아바타의 키 차이를 VRIK 타겟 위치 조정으로 해결
            ApplyHeightCompensation();

                Debug.Log("[VRIK 적용] 캘리브레이션 완료! (아바타 크기 변경 없음)");
        }
            catch (System.Exception e)
            {
                throw new System.Exception($"VRIK 캘리브레이션 적용 실패: {e.Message}");
            }
        }

        /// <summary>
        /// 사용자와 아바타의 키 차이를 VRIK 타겟 위치 조정으로 보정
        /// - 머리, 허리, 발 타겟의 위치를 미세 조정
        /// - 각 부위별로 설정된 보정 비율 적용
        /// </summary>
        void ApplyHeightCompensation()
        {
            // 키 차이가 없으면 보정 불필요
            if (Mathf.Approximately(finalScale, 1.0f))
            {
                Debug.Log("[키 차이 보정] 보정이 필요하지 않습니다. (스케일 ≈ 1.0)");
                return;
            }

            float heightDifference = userHeight - avatarHeight;
            
            Debug.Log($"[키 차이 보정] 시작 - 사용자: {userHeight:F2}m, 아바타: {avatarHeight:F2}m, 차이: {heightDifference:F2}m");

            // === 1. HMD (머리) 타겟 높이 조정 ===
            if (ik.solver.spine.headTarget != null)
            {
                Vector3 currentPos = ik.solver.spine.headTarget.localPosition;
                float headAdjustment = heightDifference * headHeightCompensationRatio;
                ik.solver.spine.headTarget.localPosition = new Vector3(currentPos.x, currentPos.y + headAdjustment, currentPos.z);
                Debug.Log($"[키 차이 보정] 머리 타겟 조정: +{headAdjustment:F3}m ({headHeightCompensationRatio:F1}%)");
            }
            else
            {
                Debug.LogWarning("[키 차이 보정] 머리 타겟이 설정되지 않았습니다.");
            }

            // === 2. 허리 트래커 타겟 높이 조정 ===
            if (waistTracker != null && ik.solver.spine.pelvisTarget != null)
            {
                Vector3 currentPos = ik.solver.spine.pelvisTarget.localPosition;
                float pelvisAdjustment = heightDifference * pelvisHeightCompensationRatio;
                ik.solver.spine.pelvisTarget.localPosition = new Vector3(currentPos.x, currentPos.y + pelvisAdjustment, currentPos.z);
                Debug.Log($"[키 차이 보정] 허리 타겟 조정: +{pelvisAdjustment:F3}m ({pelvisHeightCompensationRatio:F1}%)");
            }

            // === 3. 발 트래커 타겟 높이 조정 ===
            // 바닥에 발이 제대로 닿도록 미세 조정
            float footAdjustment = heightDifference * footHeightCompensationRatio;
            
            // 왼발 조정
            if (leftFootTracker != null && ik.solver.leftLeg.target != null)
            {
                Vector3 currentPos = ik.solver.leftLeg.target.localPosition;
                ik.solver.leftLeg.target.localPosition = new Vector3(currentPos.x, currentPos.y - footAdjustment, currentPos.z);
            }
            
            // 오른발 조정
            if (rightFootTracker != null && ik.solver.rightLeg.target != null)
            {
                Vector3 currentPos = ik.solver.rightLeg.target.localPosition;
                ik.solver.rightLeg.target.localPosition = new Vector3(currentPos.x, currentPos.y - footAdjustment, currentPos.z);
            }

            if (footAdjustment != 0)
            {
                Debug.Log($"[키 차이 보정] 발 타겟 조정: -{footAdjustment:F3}m ({footHeightCompensationRatio:F1}%)");
            }

            Debug.Log("[키 차이 보정] 완료 - 아바타 크기는 원본 유지, VRIK 타겟만 조정됨");
        }

        #endregion

        #region 보조 기능들
        
        /// <summary>
        /// 바닥 높이를 자동으로 감지
        /// - 발 트래커 중 가장 낮은 위치를 기준으로 설정
        /// - 여유 공간을 고려하여 약간 낮게 설정
        /// </summary>
        void DetectFloorLevel()
        {
            if (!autoDetectFloor)
            {
                Debug.Log("[바닥 감지] 자동 감지가 비활성화되어 있습니다.");
                return;
            }

            if (leftFootTracker != null && rightFootTracker != null)
            {
                // 양발 중 더 낮은 위치를 바닥으로 간주
                float leftFootY = leftFootTracker.position.y;
                float rightFootY = rightFootTracker.position.y;
                detectedFloorLevel = Mathf.Min(leftFootY, rightFootY) - floorDetectionMargin;
                
                Debug.Log($"[바닥 감지] 완료 - 높이: {detectedFloorLevel:F3}m (왼발: {leftFootY:F3}m, 오른발: {rightFootY:F3}m, 여유: {floorDetectionMargin:F3}m)");
            }
            else if (leftFootTracker != null)
            {
                // 왼발만 있으면 왼발 기준
                detectedFloorLevel = leftFootTracker.position.y - floorDetectionMargin;
                Debug.Log($"[바닥 감지] 왼발만 사용 - 높이: {detectedFloorLevel:F3}m");
            }
            else if (rightFootTracker != null)
            {
                // 오른발만 있으면 오른발 기준
                detectedFloorLevel = rightFootTracker.position.y - floorDetectionMargin;
                Debug.Log($"[바닥 감지] 오른발만 사용 - 높이: {detectedFloorLevel:F3}m");
            }
            else
            {
                // 발 트래커가 없으면 기본값 유지
                Debug.LogWarning("[바닥 감지] 발 트래커가 없어 바닥을 감지할 수 없습니다. 기본값 사용: {detectedFloorLevel:F3}m");
            }
        }

        /// <summary>
        /// 트래커 연결 상태를 검증
        /// - 필수 트래커들이 제대로 연결되어 있는지 확인
        /// - 최소 요구 사항: HMD + (양손 컨트롤러 또는 양발 트래커)
        /// </summary>
        bool ValidateTrackers()
        {
            // === HMD는 필수 ===
            if (hmdTracker == null)
            {
                Debug.LogError("[트래커 검증] HMD가 연결되지 않았습니다!");
                return false;
            }

            // === 컨트롤러 상태 확인 ===
            bool hasControllers = leftControllerTracker != null && rightControllerTracker != null;
            bool hasFeet = leftFootTracker != null && rightFootTracker != null;

            // === 최소 요구사항 확인 ===
            // 양손 컨트롤러 또는 양발 트래커 중 하나는 있어야 함
            if (!hasControllers && !hasFeet)
            {
                Debug.LogError("[트래커 검증] 양손 컨트롤러 또는 양발 트래커 중 하나는 필요합니다!");
                Debug.LogError("[트래커 검증] 현재 상태 - 왼손: " + (leftControllerTracker != null ? "O" : "X") + 
                             ", 오른손: " + (rightControllerTracker != null ? "O" : "X") +
                             ", 왼발: " + (leftFootTracker != null ? "O" : "X") + 
                             ", 오른발: " + (rightFootTracker != null ? "O" : "X"));
                return false;
            }

            // === 연결 상태 로그 ===
            Debug.Log($"[트래커 검증] 완료 - HMD: O, 컨트롤러: {(hasControllers ? "O" : "X")}, " +
                     $"허리: {(waistTracker != null ? "O" : "X")}, 발: {(hasFeet ? "O" : "X")}");

            // 문제점: 트래커가 연결되어 있어도 실제로 동작하는지는 확인하지 않음
            // 개선 방향: 트래커의 위치/회전 변화를 감지하여 실제 동작 여부 확인
            
            return true;
        }

        /// <summary>
        /// VRIK 캘리브레이션의 기본 설정을 초기화
        /// - Vive 트래커의 축 방향 설정
        /// - 각종 오프셋 및 가중치 설정
        /// </summary>
        void InitializeCalibrationSettings()
        {
            // === Vive 트래커 기본 축 설정 ===
            // 트래커의 forward(앞)와 up(위) 방향 정의
            calibrationSettings.headTrackerForward = Vector3.forward;
            calibrationSettings.headTrackerUp = Vector3.up;
            calibrationSettings.handTrackerForward = Vector3.forward;
            calibrationSettings.handTrackerUp = Vector3.up;
            calibrationSettings.footTrackerForward = Vector3.forward;
            calibrationSettings.footTrackerUp = Vector3.up;

            // === 기본 오프셋 설정 ===
            calibrationSettings.headOffset = Vector3.zero;        // 머리 위치 오프셋
            calibrationSettings.handOffset = Vector3.zero;        // 손 위치 오프셋
            calibrationSettings.footForwardOffset = 0.08f;        // 발 전후 오프셋
            calibrationSettings.footInwardOffset = 0.04f;         // 발 좌우 오프셋
            calibrationSettings.footHeadingOffset = 0f;           // 발 회전 오프셋

            // === VRIK 가중치 설정 ===
            calibrationSettings.pelvisPositionWeight = 1.0f;      // 골반 위치 가중치
            calibrationSettings.pelvisRotationWeight = 1.0f;      // 골반 회전 가중치

            Debug.Log("[설정 초기화] VRIK 캘리브레이션 기본 설정이 완료되었습니다.");
        }

        #endregion

        #region 캘리브레이션 제어
        
        /// <summary>
        /// 진행 중인 캘리브레이션을 중단
        /// - 안전하게 코루틴 정리 및 상태 리셋
        /// </summary>
        public void StopCalibration()
        {
            // 실행 중인 코루틴 중단
            if (calibrationCoroutine != null)
            {
                StopCoroutine(calibrationCoroutine);
                calibrationCoroutine = null;
            }
            
            // 상태 리셋
            calibrationInProgress = false;
            currentState = CalibrationState.Ready;
            countdownTimer = 0;
            
            Debug.Log("[캘리브레이션] 사용자에 의해 중단되었습니다.");
        }

        /// <summary>
        /// 캘리브레이션 관련 모든 데이터를 리셋
        /// - 측정값, 설정, 아바타 스케일 등 초기화
        /// </summary>
        public void ResetCalibration()
        {
            // 진행 중인 캘리브레이션 중단
            StopCalibration();
            
            // 상태 초기화
            currentState = CalibrationState.Ready;
            measurementAccuracy = 0f;
            finalScale = 1f;
            
            // 측정 데이터 정리
            measurementSamples_list.Clear();
            
            // 사용자 측정값 초기화
            userHeight = 0f;
            userArmLength = 0f;
            userLegLength = 0f;
            
            // 아바타 측정값 초기화
            avatarHeight = 0f;
            avatarArmLength = 0f;
            avatarLegLength = 0f;
            
            // 아바타 스케일 원래대로 복원
            if (ik?.references?.root != null)
            {
                ik.references.root.localScale = Vector3.one;
            }

            Debug.Log("[캘리브레이션] 모든 데이터가 리셋되었습니다.");
        }

        #endregion

        #region 로그 및 결과 출력
        
        /// <summary>
        /// 캘리브레이션 완료 후 최종 결과를 상세히 로그로 출력
        /// - 측정값, 계산 결과, 설정값 등 종합 정보 제공
        /// </summary>
        void LogFinalResults()
        {
            Debug.Log("========================================");
            Debug.Log("=== 완전 자동 캘리브레이션 완료 ===");
            Debug.Log("========================================");
            
            // 사용자 측정 결과
            Debug.Log($"[사용자 측정] 키: {userHeight:F2}m, 팔길이: {userArmLength:F2}m, 다리길이: {userLegLength:F2}m");
            
            // 아바타 측정 결과
            Debug.Log($"[아바타 측정] 키: {avatarHeight:F2}m, 팔길이: {avatarArmLength:F2}m, 다리길이: {avatarLegLength:F2}m");
            
            // 비율 계산 결과
            if (avatarHeight > 0)
            {
                Debug.Log($"[크기 비율] 키: {(userHeight/avatarHeight):F3}, " +
                         $"팔: {(avatarArmLength > 0 ? userArmLength/avatarArmLength : 0):F3}, " +
                         $"다리: {(avatarLegLength > 0 ? userLegLength/avatarLegLength : 0):F3}");
            }
            
            // 최종 스케일 및 정확도
            Debug.Log($"[최종 결과] 스케일: {finalScale:F3}, 측정 정확도: {measurementAccuracy:F1}%");
            
            // VRIK 데이터
            Debug.Log($"[VRIK 데이터] 캘리브레이션 스케일: {calibrationData.scale:F3}");
            
            // 보정 설정
            Debug.Log($"[보정 비율] 머리: {headHeightCompensationRatio:F1}%, " +
                     $"허리: {pelvisHeightCompensationRatio:F1}%, " +
                     $"발: {footHeightCompensationRatio:F1}%");
            
            // 측정 품질 정보
            Debug.Log($"[측정 품질] 성공한 샘플: {measurementSamples_list.Count}/{measurementSamples}개");
            
            Debug.Log("========================================");
        }

        #endregion

        #region UI 및 디스플레이
        
        /// <summary>
        /// 즉시 모드 GUI로 캘리브레이션 상태 및 정보 표시
        /// 문제점: OnGUI()는 매 프레임 호출되어 성능에 영향을 줄 수 있음
        /// 개선 방향: uGUI 캔버스로 교체하거나 업데이트 빈도 제한 고려
        /// </summary>
        void OnGUI()
        {
            // GUI 영역 설정 (화면 왼쪽 상단)
            GUILayout.BeginArea(new Rect(10, 10, 600, 500));
            
            // === 제목 ===
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            GUILayout.Label("개선된 완전 자동 VR 캘리브레이션 (매직 넘버 제거)", titleStyle);
            
            GUILayout.Space(10);

            // === 현재 상태 표시 ===
            GUIStyle statusStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            string statusText = GetStatusText(currentState);
            GUILayout.Label($"상태: {statusText}", statusStyle);

            GUILayout.Space(5);

            // === 진행 상황별 상세 정보 ===
            if (calibrationInProgress)
            {
                if (currentState == CalibrationState.Countdown)
                {
                    // 카운트다운 화면 (큰 숫자로 표시)
                    GUIStyle countdownStyle = new GUIStyle(GUI.skin.label) 
                    { 
                        fontSize = 48, 
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    };
                    GUILayout.Label($"{countdownTimer}", countdownStyle);
                    
                    GUILayout.Space(10);
                    
                    // T-포즈 안내
                    GUIStyle instructionStyle = new GUIStyle(GUI.skin.label) 
                    { 
                        fontSize = 16, 
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    };
                    GUILayout.Label("T-포즈를 준비하세요!", instructionStyle);
                    GUILayout.Label("• 팔을 양옆으로 수평하게", instructionStyle);
                    GUILayout.Label("• 똑바로 서기", instructionStyle);
                    GUILayout.Label("• 발은 어깨너비로", instructionStyle);
                }
                else
                {
                    // 기타 진행 상황
                    GUILayout.Label("진행 중... 움직이지 마세요!");
                    if (currentState == CalibrationState.MeasuringUser)
                    {
                        GUILayout.Label("T-포즈를 정확히 취해주세요:");
                        GUILayout.Label("• 팔을 양옆으로 수평하게");
                        GUILayout.Label("• 똑바로 서기");
                        GUILayout.Label("• 발은 어깨너비로");
                        
                        // 현재 측정 진행도 표시
                        if (measurementSamples_list.Count > 0)
                        {
                            GUILayout.Label($"측정 진행도: {measurementSamples_list.Count}/{measurementSamples}");
                        }
                    }
                }
            }

            GUILayout.Space(10);

            // === 설정값 표시 (캘리브레이션 진행 중이 아닐 때만) ===
            if (!calibrationInProgress)
            {
                GUIStyle boldLabelStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                GUILayout.Label("=== 현재 설정값 ===", boldLabelStyle);
                GUILayout.Label($"측정 샘플 수: {measurementSamples}회");
                GUILayout.Label($"T-포즈 최소 점수: {tPoseMinScore:F0}%");
                GUILayout.Label($"측정 최소 정확도: {measurementMinAccuracy:F0}%");
                GUILayout.Label($"보정 비율 - 머리: {headHeightCompensationRatio:F1}%, " +
                               $"허리: {pelvisHeightCompensationRatio:F1}%, " +
                               $"발: {footHeightCompensationRatio:F1}%");
            }

            // === 측정 결과 (완료된 경우) ===
            if (currentState == CalibrationState.Completed)
            {
                GUILayout.Space(10);
                GUIStyle boldLabelStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                GUILayout.Label("=== 측정 결과 ===", boldLabelStyle);
                GUILayout.Label($"사용자 키: {userHeight:F2}m");
                GUILayout.Label($"사용자 팔길이: {userArmLength:F2}m");
                GUILayout.Label($"측정 정확도: {measurementAccuracy:F1}%");
                GUILayout.Label($"최종 스케일: {finalScale:F3}");
                
                // 아바타 비교 정보
                if (avatarHeight > 0)
                {
                    GUILayout.Label($"아바타 키: {avatarHeight:F2}m (비율: {(userHeight/avatarHeight):F3})");
                }
            }

            // === 오류 정보 (오류 상태일 때) ===
            if (currentState == CalibrationState.Error || currentState == CalibrationState.Failed)
            {
            GUILayout.Space(10);
                GUIStyle errorStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                GUILayout.Label("=== 오류 발생 ===", errorStyle);
                GUILayout.Label("콘솔 로그를 확인해주세요.");
                GUILayout.Label("R 키를 눌러 리셋 후 다시 시도하세요.");
            }

            GUILayout.Space(10);

            // === 컨트롤 안내 ===
            GUIStyle boldLabelStyle2 = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            GUILayout.Label("=== 컨트롤 ===", boldLabelStyle2);
            if (!calibrationInProgress)
            {
                GUILayout.Label("SPACE - 자동 캘리브레이션 시작");
            }
            else
            {
                GUILayout.Label("ESC - 캘리브레이션 중단");
            }
            GUILayout.Label("R - 전체 리셋");

            GUILayout.EndArea();
        }

        /// <summary>
        /// 캘리브레이션 상태를 한국어 텍스트로 변환
        /// </summary>
        string GetStatusText(CalibrationState state)
        {
            switch (state)
            {
                case CalibrationState.Ready: return "대기 중";
                case CalibrationState.Countdown: return "카운트다운 중";
                case CalibrationState.DetectingFloor: return "바닥 감지 중";
                case CalibrationState.MeasuringUser: return "사용자 측정 중";
                case CalibrationState.CalculatingAvatar: return "아바타 분석 중";
                case CalibrationState.ComputingScale: return "스케일 계산 중";
                case CalibrationState.ApplyingCalibration: return "캘리브레이션 적용 중";
                case CalibrationState.Completed: return "완료";
                case CalibrationState.Error: return "오류";
                case CalibrationState.Failed: return "실패";
                default: return "알 수 없음";
            }
        }

        #endregion
    }
}