using UnityEngine;
using UnityEngine.XR.OpenXR;
using VIVE.OpenXR.FacialTracking;
using Fusion;

public class SimpleShinanoFacialTracking_Host : NetworkBehaviour
{
    [Header("필수 설정")]
    public SkinnedMeshRenderer targetMesh;
    
    [Header("조정값")]
    [Range(0f, 2f)] public float intensity = 1.0f;
    [Range(0f, 1f)] public float smoothing = 0.1f;
    
    [Header("네트워크 설정")]
    [Range(0.01f, 0.5f)] public float networkSendRate = 0.1f; // RPC 전송 간격 (초)
    [Range(0.001f, 0.1f)] public float changeThreshold = 0.01f; // 변화 감지 임계값
    
    [Header("디버그")]
    public bool showDebug = false;
    public bool enableErrorRecovery = true; // 오류 복구 활성화
    
    // VIVE 페이셜 트래킹
    private ViveFacialTracking facialTracking;
    private float[] lipData;
    
    // 블렌드셰이프 인덱스 캐시
    private int jawOpenIndex = -1;
    private int smileIndex = -1;
    private int mouthWideIndex = -1;
    private int mouthOIndex = -1;
    private int sadIndex = -1;
    private int tongueIndex = -1;
    
    // 스무딩용 값들
    private float smoothJaw = 0f;
    private float smoothSmile = 0f;
    private float smoothWide = 0f;
    private float smoothO = 0f;
    private float smoothSad = 0f;
    private float smoothTongue = 0f;
    
    // 네트워크 동기화용 변수들
    private float lastNetworkSendTime = 0f;
    private float lastJaw = 0f;
    private float lastSmile = 0f;
    private float lastWide = 0f;
    private float lastO = 0f;
    private float lastSad = 0f;
    private float lastTongue = 0f;
    
    // 오류 처리용 변수들
    private bool facialTrackingValid = false;
    private float lastErrorTime = 0f;
    private int consecutiveErrors = 0;
    private const float ERROR_RECOVERY_DELAY = 5f; // 5초 후 재시도
    private const int MAX_CONSECUTIVE_ERRORS = 10; // 최대 연속 오류 횟수
    
    void Start()
    {
        // VIVE 페이셜 트래킹 초기화
        facialTracking = OpenXRSettings.Instance?.GetFeature<ViveFacialTracking>();
        
        if (facialTracking == null || !facialTracking.enabled)
        {
            Debug.LogError("VIVE Facial Tracking이 활성화되지 않았습니다!");
            enabled = false;
            return;
        }
        
        // 타겟 메시 자동 찾기
        if (targetMesh == null)
        {
            // Body라는 이름의 메시를 먼저 찾기
            var meshes = GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var mesh in meshes)
            {
                if (mesh.name == "Body")
                {
                    targetMesh = mesh;
                    break;
                }
            }
            
            // 못 찾으면 첫 번째 메시 사용
            if (targetMesh == null && meshes.Length > 0)
            {
                targetMesh = meshes[0];
            }
        }
        
        if (targetMesh == null)
        {
            Debug.LogError("SkinnedMeshRenderer를 찾을 수 없습니다!");
            enabled = false;
            return;
        }
        
        // 립 데이터 배열 초기화
        lipData = new float[(int)XrLipExpressionHTC.XR_LIP_EXPRESSION_MAX_ENUM_HTC];
        
        // 블렌드셰이프 인덱스 캐시
        CacheBlendshapeIndices();
        
        Debug.Log("SimpleShinanoFacialTracking_Host 초기화 완료!");
    }
    
    void CacheBlendshapeIndices()
    {
        if (targetMesh?.sharedMesh == null) return;
        
        // 블렌드셰이프 이름으로 인덱스 찾기
        for (int i = 0; i < targetMesh.sharedMesh.blendShapeCount; i++)
        {
            string name = targetMesh.sharedMesh.GetBlendShapeName(i);
            
            switch (name)
            {
                case "mouth_a1":
                    jawOpenIndex = i;
                    break;
                case "mouth_smile":
                    smileIndex = i;
                    break;
                case "mouth_wide":
                    mouthWideIndex = i;
                    break;
                case "mouth_o1":
                    mouthOIndex = i;
                    break;
                case "mouth_sad":
                    sadIndex = i;
                    break;
                case "tongue_pero":
                    tongueIndex = i;
                    break;
            }
        }
        
        if (showDebug)
        {
            Debug.Log($"블렌드셰이프 인덱스 - Jaw: {jawOpenIndex}, Smile: {smileIndex}, Wide: {mouthWideIndex}, O: {mouthOIndex}, Sad: {sadIndex}, Tongue: {tongueIndex}");
        }
    }
    
    void Update()
    {
        if (targetMesh == null) return;
        
        // 호스트에서만 페이셜 트래킹 실행
        if (HasStateAuthority && facialTracking != null)
        {
            // 연속 오류가 너무 많으면 페이셜 트래킹 비활성화
            if (consecutiveErrors >= MAX_CONSECUTIVE_ERRORS)
            {
                if (showDebug)
                    Debug.LogWarning($"페이셜 트래킹 비활성화: 연속 오류 {consecutiveErrors}회");
                return;
            }
            
            // 오류 복구 대기 중인지 체크
            if (!facialTrackingValid && enableErrorRecovery)
            {
                if (Time.time - lastErrorTime < ERROR_RECOVERY_DELAY)
                {
                    return; // 아직 대기 시간
                }
                // 재시도 시간이 되었으므로 다시 시도
            }
            
            // 페이셜 트래킹 데이터 안전하게 가져오기
            if (TryGetFacialData())
            {
                // 로컬에서 표정 처리
                ProcessFacialExpressions();
                
                // 네트워크 전송 체크
                CheckAndSendFacialData();
            }
        }
    }
    
    /// <summary>
    /// 안전하게 페이셜 트래킹 데이터를 가져오기 (오류 처리 포함)
    /// </summary>
    bool TryGetFacialData()
    {
        try
        {
            // VIVE 페이셜 데이터 가져오기
            bool success = facialTracking.GetFacialExpressions(
                XrFacialTrackingTypeHTC.XR_FACIAL_TRACKING_TYPE_LIP_DEFAULT_HTC,
                out lipData
            );
            
            if (success && lipData != null)
            {
                // 성공하면 오류 카운터 리셋
                consecutiveErrors = 0;
                facialTrackingValid = true;
                return true;
            }
            else
            {
                // 페이셜 데이터 가져오기 실패
                HandleFacialTrackingError("페이셜 데이터 가져오기 실패");
                return false;
            }
        }
        catch (System.Exception ex)
        {
            // XR_ERROR_SESSION_LOST 등의 예외 처리
            HandleFacialTrackingError($"페이셜 트래킹 예외: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 페이셜 트래킹 오류 처리
    /// </summary>
    void HandleFacialTrackingError(string errorMessage)
    {
        consecutiveErrors++;
        facialTrackingValid = false;
        lastErrorTime = Time.time;
        
        if (showDebug)
        {
            Debug.LogWarning($"[페이셜 트래킹 오류] {errorMessage} (연속 오류: {consecutiveErrors}/{MAX_CONSECUTIVE_ERRORS})");
        }
        
        // 최대 오류 횟수 도달시 경고
        if (consecutiveErrors >= MAX_CONSECUTIVE_ERRORS)
        {
            Debug.LogError($"페이셜 트래킹이 비활성화되었습니다. 연속 오류 {consecutiveErrors}회 발생.");
        }
        else if (enableErrorRecovery)
        {
            if (showDebug)
            {
                Debug.Log($"{ERROR_RECOVERY_DELAY}초 후 페이셜 트래킹 재시도...");
            }
        }
    }
    
    void ProcessFacialExpressions()
    {
        // 입 벌리기 (Jaw Open)
        if (jawOpenIndex >= 0)
        {
            float jawValue = lipData[(int)XrLipExpressionHTC.XR_LIP_EXPRESSION_JAW_OPEN_HTC];
            smoothJaw = Mathf.Lerp(smoothJaw, jawValue * intensity, smoothing);
            targetMesh.SetBlendShapeWeight(jawOpenIndex, smoothJaw * 100f);
            
            if (showDebug && smoothJaw > 0.01f)
                Debug.Log($"Jaw Open: {smoothJaw:F2}");
        }
        
        // 웃음 (Smile - 좌우 합산)
        if (smileIndex >= 0)
        {
            float smileLeft = lipData[(int)XrLipExpressionHTC.XR_LIP_EXPRESSION_MOUTH_RAISER_LEFT_HTC];
            float smileRight = lipData[(int)XrLipExpressionHTC.XR_LIP_EXPRESSION_MOUTH_RAISER_RIGHT_HTC];
            float smileValue = Mathf.Max(smileLeft, smileRight);
            
            smoothSmile = Mathf.Lerp(smoothSmile, smileValue * intensity, smoothing);
            targetMesh.SetBlendShapeWeight(smileIndex, smoothSmile * 100f);
            
            if (showDebug && smoothSmile > 0.01f)
                Debug.Log($"Smile: {smoothSmile:F2}");
        }
        
        // 입 넓히기 (Wide Mouth)
        if (mouthWideIndex >= 0)
        {
            float wideLeft = lipData[(int)XrLipExpressionHTC.XR_LIP_EXPRESSION_MOUTH_STRETCHER_LEFT_HTC];
            float wideRight = lipData[(int)XrLipExpressionHTC.XR_LIP_EXPRESSION_MOUTH_STRETCHER_RIGHT_HTC];
            float wideValue = Mathf.Max(wideLeft, wideRight);
            
            smoothWide = Mathf.Lerp(smoothWide, wideValue * intensity * 0.8f, smoothing);
            targetMesh.SetBlendShapeWeight(mouthWideIndex, smoothWide * 100f);
            
            if (showDebug && smoothWide > 0.01f)
                Debug.Log($"Wide: {smoothWide:F2}");
        }
        
        // O모양 입 (Pout)
        if (mouthOIndex >= 0)
        {
            float oValue = lipData[(int)XrLipExpressionHTC.XR_LIP_EXPRESSION_MOUTH_POUT_HTC];
            smoothO = Mathf.Lerp(smoothO, oValue * intensity, smoothing);
            targetMesh.SetBlendShapeWeight(mouthOIndex, smoothO * 100f);
            
            if (showDebug && smoothO > 0.01f)
                Debug.Log($"O Shape: {smoothO:F2}");
        }
        
        // 슬픈 표정 (Sad - 아래쪽 입술 움직임)
        if (sadIndex >= 0)
        {
            float sadLeft = lipData[(int)XrLipExpressionHTC.XR_LIP_EXPRESSION_MOUTH_LOWER_DOWNLEFT_HTC];
            float sadRight = lipData[(int)XrLipExpressionHTC.XR_LIP_EXPRESSION_MOUTH_LOWER_DOWNRIGHT_HTC];
            float sadValue = Mathf.Max(sadLeft, sadRight);
            
            smoothSad = Mathf.Lerp(smoothSad, sadValue * intensity * 0.7f, smoothing);
            targetMesh.SetBlendShapeWeight(sadIndex, smoothSad * 100f);
            
            if (showDebug && smoothSad > 0.01f)
                Debug.Log($"Sad: {smoothSad:F2}");
        }
        
        // 혀 내밀기 (Tongue Out)
        if (tongueIndex >= 0)
        {
            float tongueValue = lipData[(int)XrLipExpressionHTC.XR_LIP_EXPRESSION_TONGUE_LONGSTEP1_HTC];
            smoothTongue = Mathf.Lerp(smoothTongue, tongueValue * intensity, smoothing);
            targetMesh.SetBlendShapeWeight(tongueIndex, smoothTongue * 100f);
            
            if (showDebug && smoothTongue > 0.01f)
                Debug.Log($"Tongue: {smoothTongue:F2}");
        }
    }
    
    /// <summary>
    /// 네트워크 전송 조건을 체크하고 필요시 RPC 호출
    /// </summary>
    void CheckAndSendFacialData()
    {
        float currentTime = Time.time;
        
        // 시간 간격 체크
        if (currentTime - lastNetworkSendTime < networkSendRate) return;
        
        // 변화량 체크 (하나라도 임계값 이상 변했으면 전송)
        bool hasSignificantChange = 
            Mathf.Abs(smoothJaw - lastJaw) > changeThreshold ||
            Mathf.Abs(smoothSmile - lastSmile) > changeThreshold ||
            Mathf.Abs(smoothWide - lastWide) > changeThreshold ||
            Mathf.Abs(smoothO - lastO) > changeThreshold ||
            Mathf.Abs(smoothSad - lastSad) > changeThreshold ||
            Mathf.Abs(smoothTongue - lastTongue) > changeThreshold;
        
        if (hasSignificantChange)
        {
            // 클라이언트용 RPC 호출 (메서드명 변경)
            ReceiveFacialDataRPC(smoothJaw, smoothSmile, smoothWide, smoothO, smoothSad, smoothTongue);
            
            // 마지막 전송 시간과 값들 업데이트
            lastNetworkSendTime = currentTime;
            lastJaw = smoothJaw;
            lastSmile = smoothSmile;
            lastWide = smoothWide;
            lastO = smoothO;
            lastSad = smoothSad;
            lastTongue = smoothTongue;
            
            if (showDebug)
            {
                Debug.Log($"[호스트] 페이셜 데이터 전송 - Jaw: {smoothJaw:F2}, Smile: {smoothSmile:F2}");
            }
        }
    }
    
    // 수동으로 표정 설정하는 함수 (테스트용)
    public void SetExpression(string expressionName, float value)
    {
        int index = -1;
        
        switch (expressionName.ToLower())
        {
            case "jaw":
            case "mouth_a1":
                index = jawOpenIndex;
                break;
            case "smile":
            case "mouth_smile":
                index = smileIndex;
                break;
            case "wide":
            case "mouth_wide":
                index = mouthWideIndex;
                break;
            case "o":
            case "mouth_o1":
                index = mouthOIndex;
                break;
            case "sad":
            case "mouth_sad":
                index = sadIndex;
                break;
            case "tongue":
            case "tongue_pero":
                index = tongueIndex;
                break;
        }
        
        if (index >= 0 && targetMesh != null)
        {
            targetMesh.SetBlendShapeWeight(index, Mathf.Clamp01(value) * 100f);
        }
    }
    
    // 간단한 GUI 디버깅
    void OnGUI()
    {
        if (!showDebug) return;
        
        int y = 10;
        GUI.Box(new Rect(10, y, 200, 180), "");
        
        y += 5;
        GUI.Label(new Rect(15, y, 190, 20), "=== 호스트 (송신) ===");
        y += 25;
        
        // 상태 표시
        if (HasStateAuthority)
        {
            string status = facialTrackingValid ? "정상" : "오류";
            GUI.color = facialTrackingValid ? Color.green : Color.red;
            GUI.Label(new Rect(15, y, 190, 20), $"상태: {status}");
            GUI.color = Color.white;
            y += 20;
            
            if (consecutiveErrors > 0)
            {
                GUI.color = Color.yellow;
                GUI.Label(new Rect(15, y, 190, 20), $"오류: {consecutiveErrors}회");
                GUI.color = Color.white;
                y += 20;
            }
        }
        else
        {
            GUI.Label(new Rect(15, y, 190, 20), "클라이언트 (수신 모드)");
            y += 20;
        }
        
        GUI.Label(new Rect(15, y, 190, 20), $"입 벌림: {(smoothJaw * 100f):F0}%");
        y += 20;
        GUI.Label(new Rect(15, y, 190, 20), $"미소: {(smoothSmile * 100f):F0}%");
        y += 20;
        GUI.Label(new Rect(15, y, 190, 20), $"입 넓힘: {(smoothWide * 100f):F0}%");
        y += 20;
        GUI.Label(new Rect(15, y, 190, 20), $"O 모양: {(smoothO * 100f):F0}%");
        y += 20;
        GUI.Label(new Rect(15, y, 190, 20), $"슬픔: {(smoothSad * 100f):F0}%");
        y += 20;
        GUI.Label(new Rect(15, y, 190, 20), $"혀: {(smoothTongue * 100f):F0}%");
    }
    
    // === 네트워킹용 데이터 접근 메서드들 ===
    
    /// <summary>
    /// 현재 페이셜 데이터를 가져옵니다 (네트워킹용)
    /// </summary>
    public void GetFacialData(out float jaw, out float smile, out float wide, out float o, out float sad, out float tongue)
    {
        jaw = smoothJaw;
        smile = smoothSmile;
        wide = smoothWide;
        o = smoothO;
        sad = smoothSad;
        tongue = smoothTongue;
    }
    
    /// <summary>
    /// 페이셜 데이터를 직접 설정합니다 (클라이언트용)
    /// </summary>
    public void SetFacialData(float jaw, float smile, float wide, float o, float sad, float tongue)
    {
        if (targetMesh == null) return;
        
        // 직접 블렌드셰이프에 적용 (스무딩 없이)
        if (jawOpenIndex >= 0) targetMesh.SetBlendShapeWeight(jawOpenIndex, jaw * 100f);
        if (smileIndex >= 0) targetMesh.SetBlendShapeWeight(smileIndex, smile * 100f);
        if (mouthWideIndex >= 0) targetMesh.SetBlendShapeWeight(mouthWideIndex, wide * 100f);
        if (mouthOIndex >= 0) targetMesh.SetBlendShapeWeight(mouthOIndex, o * 100f);
        if (sadIndex >= 0) targetMesh.SetBlendShapeWeight(sadIndex, sad * 100f);
        if (tongueIndex >= 0) targetMesh.SetBlendShapeWeight(tongueIndex, tongue * 100f);
    }
    
    /// <summary>
    /// 페이셜 트래킹이 활성화되어 있는지 확인
    /// </summary>
    public bool IsFacialTrackingActive()
    {
        return facialTracking != null && facialTracking.enabled && enabled && facialTrackingValid;
    }
    
    /// <summary>
    /// 페이셜 트래킹 오류 상태 확인
    /// </summary>
    public bool HasFacialTrackingErrors()
    {
        return consecutiveErrors > 0;
    }
    
    /// <summary>
    /// 페이셜 트래킹 오류 통계 가져오기
    /// </summary>
    public (int errors, bool isValid, float lastErrorTime) GetFacialTrackingStatus()
    {
        return (consecutiveErrors, facialTrackingValid, lastErrorTime);
    }
    
    /// <summary>
    /// 페이셜 트래킹 오류 상태 리셋 (수동 복구)
    /// </summary>
    public void ResetFacialTrackingErrors()
    {
        consecutiveErrors = 0;
        facialTrackingValid = false;
        lastErrorTime = 0f;
        
        if (showDebug)
        {
            Debug.Log("페이셜 트래킹 오류 상태가 리셋되었습니다.");
        }
    }
    
    /// <summary>
    /// 클라이언트들에게 페이셜 데이터 전송 (RPC)
    /// 클라이언트 스크립트와 호환되도록 메서드명 통일
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void ReceiveFacialDataRPC(float jaw, float smile, float wide, float o, float sad, float tongue)
    {
        // 호스트는 이미 로컬에서 적용했으므로 클라이언트만 적용
        // 클라이언트 스크립트에서 HasStateAuthority 체크를 하므로 여기서는 모든 클라이언트에게 전송
        if (showDebug && HasStateAuthority)
        {
            Debug.Log($"[호스트] RPC 전송 완료 - Jaw: {jaw:F2}, Smile: {smile:F2}");
        }
    }
}