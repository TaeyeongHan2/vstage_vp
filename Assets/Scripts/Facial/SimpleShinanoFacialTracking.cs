using UnityEngine;
using Fusion;
// VIVE 하드웨어 사용하지 않음 - 클라이언트용으로 RPC만 사용
//using UnityEngine.XR.OpenXR;
//using VIVE.OpenXR.FacialTracking;

/// <summary>
/// 클라이언트 전용 페이셜 애니메이션 컨트롤러
/// RPC를 통해 Host로부터 페이셜 데이터를 받아서 블렌드셰이프 애니메이션 재생
/// VIVE 하드웨어 직접 사용하지 않고 네트워크 데이터만 처리
/// </summary>
public class SimpleShinanoFacialTracking : NetworkBehaviour
{
    [Header("필수 설정")]
    /// <summary>
    /// 페이셜 애니메이션을 적용할 SkinnedMeshRenderer
    /// 아바타의 얼굴 메시여야 하며, 블렌드셰이프가 설정되어 있어야 함
    /// </summary>
    public SkinnedMeshRenderer targetMesh;
    
    [Header("조정값")]
    /// <summary>
    /// 페이셜 애니메이션의 강도 배율 (0.0 ~ 2.0)
    /// 1.0이 기본값이며, 더 과장된 표정을 원하면 높게 설정
    /// </summary>
    [Range(0f, 2f)] public float intensity = 1.0f;
    
    /// <summary>
    /// 페이셜 애니메이션의 부드러움 정도 (0.0 ~ 1.0)
    /// 0에 가까우면 즉시 반응, 1에 가까우면 천천히 변화
    /// </summary>
    [Range(0f, 1f)] public float smoothing = 0.1f;
    
    [Header("디버그")]
    /// <summary>
    /// 디버그 정보를 화면에 표시할지 여부
    /// true로 설정하면 OnGUI를 통해 현재 페이셜 값들이 표시됨
    /// </summary>
    public bool showDebug = false;
    
    // VIVE 하드웨어 관련 변수들 (클라이언트에서는 사용하지 않음)
    // private ViveFacialTracking facialTracking;  // 제거됨
    // private float[] lipData;  // 제거됨
    
    // 블렌드셰이프 인덱스 캐시 - 성능 최적화를 위해 미리 저장
    /// <summary>입 벌리기 블렌드셰이프 인덱스 ("mouth_a1")</summary>
    private int jawOpenIndex = -1;
    /// <summary>미소 블렌드셰이프 인덱스 ("mouth_smile")</summary>
    private int smileIndex = -1;
    /// <summary>입 넓히기 블렌드셰이프 인덱스 ("mouth_wide")</summary>
    private int mouthWideIndex = -1;
    /// <summary>입술 오므리기 블렌드셰이프 인덱스 ("mouth_o1")</summary>
    private int mouthOIndex = -1;
    /// <summary>슬픈 표정 블렌드셰이프 인덱스 ("mouth_sad")</summary>
    private int sadIndex = -1;
    /// <summary>혀 내밀기 블렌드셰이프 인덱스 ("tongue_pero")</summary>
    private int tongueIndex = -1;
    
    // 스무딩용 값들 - 부드러운 애니메이션을 위한 보간값 저장
    /// <summary>현재 턱 열림 스무딩 값 (0.0 ~ 1.0)</summary>
    private float smoothJaw = 0f;
    /// <summary>현재 미소 스무딩 값 (0.0 ~ 1.0)</summary>
    private float smoothSmile = 0f;
    /// <summary>현재 입 넓힘 스무딩 값 (0.0 ~ 1.0)</summary>
    private float smoothWide = 0f;
    /// <summary>현재 입술 오므림 스무딩 값 (0.0 ~ 1.0)</summary>
    private float smoothO = 0f;
    /// <summary>현재 슬픈 표정 스무딩 값 (0.0 ~ 1.0)</summary>
    private float smoothSad = 0f;
    /// <summary>현재 혀 내밀기 스무딩 값 (0.0 ~ 1.0)</summary>
    private float smoothTongue = 0f;
    
    /// <summary>
    /// 컴포넌트 초기화 - 타겟 메시 검색 및 블렌드셰이프 인덱스 캐싱
    /// 클라이언트용이므로 VIVE 하드웨어 초기화는 건너뜀
    /// </summary>
    void Start()
    {
        // VIVE 하드웨어 초기화 코드 제거 (클라이언트는 RPC로만 동작)
        /*
        facialTracking = OpenXRSettings.Instance?.GetFeature<ViveFacialTracking>();
        
        if (facialTracking == null || !facialTracking.enabled)
        {
            Debug.LogError("VIVE Facial Tracking이 활성화되지 않았습니다!");
            enabled = false;
            return;
        }
        */
        
        // 타겟 메시 자동 찾기
        if (targetMesh == null)
        {
            // "Body"라는 이름의 메시를 우선적으로 검색
            var meshes = GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var mesh in meshes)
            {
                if (mesh.name == "Body")
                {
                    targetMesh = mesh;
                    break;
                }
            }
            
            // "Body" 메시를 못 찾으면 첫 번째 SkinnedMeshRenderer 사용
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
        
        // VIVE 립 데이터 배열 초기화 제거 (클라이언트에서는 불필요)
        // lipData = new float[(int)XrLipExpressionHTC.XR_LIP_EXPRESSION_MAX_ENUM_HTC];
        
        // 블렌드셰이프 인덱스 캐시 (성능 최적화)
        CacheBlendshapeIndices();
        
        Debug.Log("SimpleShinanoFacialTracking (Client RPC Mode) 초기화 완료!");
    }
    
    /// <summary>
    /// 대상 메시의 블렌드셰이프 인덱스를 미리 캐시하는 메서드
    /// 매번 이름으로 검색하지 않고 인덱스로 빠르게 접근하기 위함
    /// </summary>
    void CacheBlendshapeIndices()
    {
        if (targetMesh?.sharedMesh == null) return;
        
        // 블렌드셰이프 이름으로 인덱스 찾기 및 캐시
        for (int i = 0; i < targetMesh.sharedMesh.blendShapeCount; i++)
        {
            string name = targetMesh.sharedMesh.GetBlendShapeName(i);
            
            switch (name)
            {
                case "mouth_a1":        // 입 벌리기 (턱 움직임)
                    jawOpenIndex = i;
                    break;
                case "mouth_smile":     // 미소 (입꼬리 올리기)
                    smileIndex = i;
                    break;
                case "mouth_wide":      // 입 넓히기 (놀란 표정)
                    mouthWideIndex = i;
                    break;
                case "mouth_o1":        // 입술 오므리기 (O 모양)
                    mouthOIndex = i;
                    break;
                case "mouth_sad":       // 슬픈 표정 (입꼬리 내리기)
                    sadIndex = i;
                    break;
                case "tongue_pero":     // 혀 내밀기
                    tongueIndex = i;
                    break;
            }
        }
        
        if (showDebug)
        {
            Debug.Log($"블렌드셰이프 인덱스 캐시 완료 - Jaw: {jawOpenIndex}, Smile: {smileIndex}, Wide: {mouthWideIndex}, O: {mouthOIndex}, Sad: {sadIndex}, Tongue: {tongueIndex}");
        }
    }
    
    /// <summary>
    /// 매 프레임 업데이트 - 클라이언트에서는 스무딩 처리만 수행
    /// 실제 페이셜 데이터는 RPC를 통해서만 받음
    /// </summary>
    void Update()
    {
        if (targetMesh == null) return;
        
        // VIVE 하드웨어에서 데이터 읽기 제거 (클라이언트는 RPC로만 동작)
        /*
        // VIVE 페이셜 데이터 가져오기
        bool success = facialTracking.GetFacialExpressions(
            XrFacialTrackingTypeHTC.XR_FACIAL_TRACKING_TYPE_LIP_DEFAULT_HTC,
            out lipData
        );
        
        if (!success || lipData == null) return;
        
        // 주요 표정들 처리
        ProcessFacialExpressions();
        */
        
        // 클라이언트에서는 RPC로 받은 데이터의 스무딩만 처리
        // 실제 블렌드셰이프 적용은 RPC 메서드에서 수행
        ApplySmoothingToBlendshapes();
    }
    
    /// <summary>
    /// 스무딩된 값들을 실제 블렌드셰이프에 적용하는 메서드
    /// 부드러운 페이셜 애니메이션을 위해 보간된 값들을 사용
    /// </summary>
    private void ApplySmoothingToBlendshapes()
    {
        // 각 표정별로 스무딩된 값을 블렌드셰이프에 적용 (0~100 범위로 변환)
        if (jawOpenIndex >= 0)
            targetMesh.SetBlendShapeWeight(jawOpenIndex, smoothJaw * 100f);
        
        if (smileIndex >= 0)
            targetMesh.SetBlendShapeWeight(smileIndex, smoothSmile * 100f);
        
        if (mouthWideIndex >= 0)
            targetMesh.SetBlendShapeWeight(mouthWideIndex, smoothWide * 100f);
        
        if (mouthOIndex >= 0)
            targetMesh.SetBlendShapeWeight(mouthOIndex, smoothO * 100f);
        
        if (sadIndex >= 0)
            targetMesh.SetBlendShapeWeight(sadIndex, smoothSad * 100f);
        
        if (tongueIndex >= 0)
            targetMesh.SetBlendShapeWeight(tongueIndex, smoothTongue * 100f);
    }
    
    /// <summary>
    /// VIVE 하드웨어에서 페이셜 데이터를 처리하는 메서드 (클라이언트에서는 사용하지 않음)
    /// 원본 코드 보존을 위해 주석 처리
    /// </summary>
    /*
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
    */
    
    /// <summary>
    /// Host에서 클라이언트들로 페이셜 데이터를 전송하는 RPC 메서드
    /// Host만 호출 가능하며, 모든 클라이언트가 동일한 페이셜 애니메이션을 재생
    /// </summary>
    /// <param name="jaw">턱 열림 값 (0.0 ~ 1.0)</param>
    /// <param name="smile">미소 값 (0.0 ~ 1.0)</param>
    /// <param name="wide">입 넓힘 값 (0.0 ~ 1.0)</param>
    /// <param name="o">입술 오므림 값 (0.0 ~ 1.0)</param>
    /// <param name="sad">슬픈 표정 값 (0.0 ~ 1.0)</param>
    /// <param name="tongue">혀 내밀기 값 (0.0 ~ 1.0)</param>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_UpdateFacialExpression(float jaw, float smile, float wide, float o, float sad, float tongue)
    {
        // 받은 RPC 데이터를 스무딩 변수에 보간 적용
        // intensity를 곱해서 표정 강도 조절
        smoothJaw = Mathf.Lerp(smoothJaw, Mathf.Clamp01(jaw) * intensity, smoothing);
        smoothSmile = Mathf.Lerp(smoothSmile, Mathf.Clamp01(smile) * intensity, smoothing);
        smoothWide = Mathf.Lerp(smoothWide, Mathf.Clamp01(wide) * intensity, smoothing);
        smoothO = Mathf.Lerp(smoothO, Mathf.Clamp01(o) * intensity, smoothing);
        smoothSad = Mathf.Lerp(smoothSad, Mathf.Clamp01(sad) * intensity, smoothing);
        smoothTongue = Mathf.Lerp(smoothTongue, Mathf.Clamp01(tongue) * intensity, smoothing);
        
        // 디버그 로그 출력 (활성 표정만)
        if (showDebug)
        {
            if (jaw > 0.01f) Debug.Log($"RPC Jaw: {jaw:F2} -> {smoothJaw:F2}");
            if (smile > 0.01f) Debug.Log($"RPC Smile: {smile:F2} -> {smoothSmile:F2}");
            if (wide > 0.01f) Debug.Log($"RPC Wide: {wide:F2} -> {smoothWide:F2}");
            if (o > 0.01f) Debug.Log($"RPC O: {o:F2} -> {smoothO:F2}");
            if (sad > 0.01f) Debug.Log($"RPC Sad: {sad:F2} -> {smoothSad:F2}");
            if (tongue > 0.01f) Debug.Log($"RPC Tongue: {tongue:F2} -> {smoothTongue:F2}");
        }
    }
    
    /// <summary>
    /// 개별 표정을 즉시 설정하는 RPC 메서드
    /// 특정 표정만 빠르게 변경하고 싶을 때 사용
    /// </summary>
    /// <param name="expressionType">표정 타입 (0=jaw, 1=smile, 2=wide, 3=o, 4=sad, 5=tongue)</param>
    /// <param name="value">표정 값 (0.0 ~ 1.0)</param>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SetSingleExpression(int expressionType, float value)
    {
        float clampedValue = Mathf.Clamp01(value) * intensity;
        
        switch (expressionType)
        {
            case 0: // Jaw
                smoothJaw = Mathf.Lerp(smoothJaw, clampedValue, smoothing);
                break;
            case 1: // Smile
                smoothSmile = Mathf.Lerp(smoothSmile, clampedValue, smoothing);
                break;
            case 2: // Wide
                smoothWide = Mathf.Lerp(smoothWide, clampedValue, smoothing);
                break;
            case 3: // O
                smoothO = Mathf.Lerp(smoothO, clampedValue, smoothing);
                break;
            case 4: // Sad
                smoothSad = Mathf.Lerp(smoothSad, clampedValue, smoothing);
                break;
            case 5: // Tongue
                smoothTongue = Mathf.Lerp(smoothTongue, clampedValue, smoothing);
                break;
        }
        
        if (showDebug && value > 0.01f)
        {
            string[] expressionNames = { "Jaw", "Smile", "Wide", "O", "Sad", "Tongue" };
            if (expressionType >= 0 && expressionType < expressionNames.Length)
            {
                Debug.Log($"RPC Single {expressionNames[expressionType]}: {value:F2} -> {clampedValue:F2}");
            }
        }
    }
    
    /// <summary>
    /// 모든 페이셜 표정을 즉시 리셋하는 RPC 메서드
    /// 표정을 기본 상태로 되돌릴 때 사용
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ResetAllExpressions()
    {
        // 모든 스무딩 값을 0으로 리셋
        smoothJaw = 0f;
        smoothSmile = 0f;
        smoothWide = 0f;
        smoothO = 0f;
        smoothSad = 0f;
        smoothTongue = 0f;
        
        if (showDebug)
        {
            Debug.Log("RPC: All facial expressions reset to 0");
        }
    }
    
    /// <summary>
    /// 수동으로 표정 설정하는 함수 (테스트용)
    /// Inspector나 다른 스크립트에서 직접 호출 가능
    /// </summary>
    /// <param name="expressionName">표정 이름 (jaw, smile, wide, o, sad, tongue)</param>
    /// <param name="value">표정 값 (0.0 ~ 1.0)</param>
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
    
    /// <summary>
    /// 간단한 GUI 디버깅 창
    /// showDebug가 true일 때 화면에 현재 페이셜 값들을 표시
    /// </summary>
    void OnGUI()
    {
        if (!showDebug) return;
        
        int y = 10;
        GUI.Box(new Rect(10, y, 250, 170), "");
        
        y += 5;
        GUI.Label(new Rect(15, y, 240, 20), "=== 페이셜 트래킹 (RPC 클라이언트) ===");
        y += 25;
        
        GUI.Label(new Rect(15, y, 240, 20), $"입 벌림: {(smoothJaw * 100f):F0}%");
        y += 20;
        GUI.Label(new Rect(15, y, 240, 20), $"미소: {(smoothSmile * 100f):F0}%");
        y += 20;
        GUI.Label(new Rect(15, y, 240, 20), $"입 넓힘: {(smoothWide * 100f):F0}%");
        y += 20;
        GUI.Label(new Rect(15, y, 240, 20), $"O 모양: {(smoothO * 100f):F0}%");
        y += 20;
        GUI.Label(new Rect(15, y, 240, 20), $"슬픔: {(smoothSad * 100f):F0}%");
        y += 20;
        GUI.Label(new Rect(15, y, 240, 20), $"혀: {(smoothTongue * 100f):F0}%");
        y += 25;
        
        // 네트워크 상태 정보
        string networkStatus = Object != null && Object.IsValid ? 
            $"네트워크: 연결됨 (Authority: {Object.HasStateAuthority})" : 
            "네트워크: 연결 안됨";
        GUI.Label(new Rect(15, y, 240, 20), networkStatus);
    }
    
    // === 네트워킹용 데이터 접근 메서드들 (기존 코드 호환성 유지) ===
    
    /// <summary>
    /// 현재 페이셜 데이터를 가져옵니다 (네트워킹용)
    /// Host에서 현재 스무딩된 값들을 읽어올 때 사용
    /// </summary>
    /// <param name="jaw">턱 열림 값 출력</param>
    /// <param name="smile">미소 값 출력</param>
    /// <param name="wide">입 넓힘 값 출력</param>
    /// <param name="o">입술 오므림 값 출력</param>
    /// <param name="sad">슬픈 표정 값 출력</param>
    /// <param name="tongue">혀 내밀기 값 출력</param>
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
    /// RPC 대신 직접 호출로 페이셜 데이터를 설정할 때 사용
    /// 스무딩 없이 즉시 적용됨
    /// </summary>
    /// <param name="jaw">턱 열림 값 (0.0 ~ 1.0)</param>
    /// <param name="smile">미소 값 (0.0 ~ 1.0)</param>
    /// <param name="wide">입 넓힘 값 (0.0 ~ 1.0)</param>
    /// <param name="o">입술 오므림 값 (0.0 ~ 1.0)</param>
    /// <param name="sad">슬픈 표정 값 (0.0 ~ 1.0)</param>
    /// <param name="tongue">혀 내밀기 값 (0.0 ~ 1.0)</param>
    public void SetFacialData(float jaw, float smile, float wide, float o, float sad, float tongue)
    {
        if (targetMesh == null) return;
        
        // 직접 블렌드셰이프에 적용 (스무딩 없이 즉시 반영)
        if (jawOpenIndex >= 0) targetMesh.SetBlendShapeWeight(jawOpenIndex, Mathf.Clamp01(jaw) * 100f);
        if (smileIndex >= 0) targetMesh.SetBlendShapeWeight(smileIndex, Mathf.Clamp01(smile) * 100f);
        if (mouthWideIndex >= 0) targetMesh.SetBlendShapeWeight(mouthWideIndex, Mathf.Clamp01(wide) * 100f);
        if (mouthOIndex >= 0) targetMesh.SetBlendShapeWeight(mouthOIndex, Mathf.Clamp01(o) * 100f);
        if (sadIndex >= 0) targetMesh.SetBlendShapeWeight(sadIndex, Mathf.Clamp01(sad) * 100f);
        if (tongueIndex >= 0) targetMesh.SetBlendShapeWeight(tongueIndex, Mathf.Clamp01(tongue) * 100f);
        
        // 스무딩 값도 업데이트 (일관성 유지)
        smoothJaw = Mathf.Clamp01(jaw);
        smoothSmile = Mathf.Clamp01(smile);
        smoothWide = Mathf.Clamp01(wide);
        smoothO = Mathf.Clamp01(o);
        smoothSad = Mathf.Clamp01(sad);
        smoothTongue = Mathf.Clamp01(tongue);
    }
    
    /// <summary>
    /// 페이셜 트래킹이 활성화되어 있는지 확인
    /// 클라이언트 모드에서는 컴포넌트 활성화 상태만 확인
    /// </summary>
    /// <returns>컴포넌트가 활성화되어 있으면 true</returns>
    public bool IsFacialTrackingActive()
    {
        // 클라이언트 모드에서는 VIVE 하드웨어가 아닌 컴포넌트 활성화 상태만 확인
        return enabled && targetMesh != null;
        
        // 원본 코드 (VIVE 하드웨어 확인)
        // return facialTracking != null && facialTracking.enabled && enabled;
    }
    
    /// <summary>
    /// Host에서 호출하여 현재 페이셜 데이터를 모든 클라이언트에게 전송
    /// VRIKNetworkPlayer나 다른 Host 컴포넌트에서 주기적으로 호출
    /// </summary>
    public void SendFacialDataToClients()
    {
        // Host (StateAuthority)만 RPC 전송 가능
        if (Object != null && Object.HasStateAuthority)
        {
            // 현재 스무딩된 값들을 모든 클라이언트에게 전송
            RPC_UpdateFacialExpression(smoothJaw, smoothSmile, smoothWide, smoothO, smoothSad, smoothTongue);
        }
    }
} 