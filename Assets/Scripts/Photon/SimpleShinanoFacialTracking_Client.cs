using UnityEngine;
using Fusion;

/// <summary>
/// 클라이언트 전용 페이셜 트래킹 수신기
/// 호스트로부터 RPC를 통해 페이셜 데이터를 받아서 블렌드셰이프에 적용
/// </summary>
public class SimpleShinanoFacialTracking_Client : NetworkBehaviour
{
    [Header("필수 설정")]
    public SkinnedMeshRenderer targetMesh;
    
    [Header("조정값")]
    [Range(0f, 2f)] public float intensity = 1.0f;
    [Range(0f, 1f)] public float smoothing = 0.1f;
    
    [Header("디버그")]
    public bool showDebug = false;
    
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
    
    // 수신된 타겟 값들
    private float targetJaw = 0f;
    private float targetSmile = 0f;
    private float targetWide = 0f;
    private float targetO = 0f;
    private float targetSad = 0f;
    private float targetTongue = 0f;
    
    void Start()
    {
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
        
        // 블렌드셰이프 인덱스 캐시
        CacheBlendshapeIndices();
        
        Debug.Log("SimpleShinanoFacialTracking_Client 초기화 완료!");
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
        
        // 스무딩을 통해 타겟 값들로 부드럽게 이동
        ApplySmoothingAndBlendshapes();
    }
    
    void ApplySmoothingAndBlendshapes()
    {
        float deltaTime = Time.deltaTime;
        float lerpSpeed = smoothing > 0 ? deltaTime / smoothing : 1f;
        
        // 스무딩 적용
        smoothJaw = Mathf.Lerp(smoothJaw, targetJaw, lerpSpeed);
        smoothSmile = Mathf.Lerp(smoothSmile, targetSmile, lerpSpeed);
        smoothWide = Mathf.Lerp(smoothWide, targetWide, lerpSpeed);
        smoothO = Mathf.Lerp(smoothO, targetO, lerpSpeed);
        smoothSad = Mathf.Lerp(smoothSad, targetSad, lerpSpeed);
        smoothTongue = Mathf.Lerp(smoothTongue, targetTongue, lerpSpeed);
        
        // 블렌드셰이프에 적용
        if (jawOpenIndex >= 0) 
            targetMesh.SetBlendShapeWeight(jawOpenIndex, smoothJaw * intensity * 100f);
        
        if (smileIndex >= 0) 
            targetMesh.SetBlendShapeWeight(smileIndex, smoothSmile * intensity * 100f);
        
        if (mouthWideIndex >= 0) 
            targetMesh.SetBlendShapeWeight(mouthWideIndex, smoothWide * intensity * 100f);
        
        if (mouthOIndex >= 0) 
            targetMesh.SetBlendShapeWeight(mouthOIndex, smoothO * intensity * 100f);
        
        if (sadIndex >= 0) 
            targetMesh.SetBlendShapeWeight(sadIndex, smoothSad * intensity * 100f);
        
        if (tongueIndex >= 0) 
            targetMesh.SetBlendShapeWeight(tongueIndex, smoothTongue * intensity * 100f);
    }
    
    /// <summary>
    /// 호스트로부터 페이셜 데이터 수신 (RPC)
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void ReceiveFacialDataRPC(float jaw, float smile, float wide, float o, float sad, float tongue)
    {
        // 호스트가 아닌 클라이언트만 데이터 적용
        if (!HasStateAuthority)
        {
            // 타겟 값들 업데이트 (스무딩은 Update에서 처리)
            targetJaw = jaw;
            targetSmile = smile;
            targetWide = wide;
            targetO = o;
            targetSad = sad;
            targetTongue = tongue;
            
            if (showDebug)
            {
                Debug.Log($"[클라이언트] 페이셜 데이터 수신 - Jaw: {jaw:F2}, Smile: {smile:F2}, Wide: {wide:F2}");
            }
        }
    }
    
    /// <summary>
    /// 수동으로 표정 설정하는 함수 (테스트용)
    /// </summary>
    public void SetExpression(string expressionName, float value)
    {
        int index = -1;
        
        switch (expressionName.ToLower())
        {
            case "jaw":
            case "mouth_a1":
                index = jawOpenIndex;
                targetJaw = value;
                break;
            case "smile":
            case "mouth_smile":
                index = smileIndex;
                targetSmile = value;
                break;
            case "wide":
            case "mouth_wide":
                index = mouthWideIndex;
                targetWide = value;
                break;
            case "o":
            case "mouth_o1":
                index = mouthOIndex;
                targetO = value;
                break;
            case "sad":
            case "mouth_sad":
                index = sadIndex;
                targetSad = value;
                break;
            case "tongue":
            case "tongue_pero":
                index = tongueIndex;
                targetTongue = value;
                break;
        }
        
        if (index >= 0 && targetMesh != null)
        {
            targetMesh.SetBlendShapeWeight(index, Mathf.Clamp01(value) * intensity * 100f);
        }
    }
    
    /// <summary>
    /// 즉시 페이셜 데이터를 설정 (스무딩 없이)
    /// </summary>
    public void SetFacialDataImmediate(float jaw, float smile, float wide, float o, float sad, float tongue)
    {
        targetJaw = smoothJaw = jaw;
        targetSmile = smoothSmile = smile;
        targetWide = smoothWide = wide;
        targetO = smoothO = o;
        targetSad = smoothSad = sad;
        targetTongue = smoothTongue = tongue;
        
        // 즉시 적용
        ApplySmoothingAndBlendshapes();
    }
    
    /// <summary>
    /// 모든 표정을 리셋
    /// </summary>
    public void ResetAllExpressions()
    {
        SetFacialDataImmediate(0f, 0f, 0f, 0f, 0f, 0f);
    }
    
    /// <summary>
    /// 현재 페이셜 데이터 가져오기
    /// </summary>
    public void GetCurrentFacialData(out float jaw, out float smile, out float wide, out float o, out float sad, out float tongue)
    {
        jaw = smoothJaw;
        smile = smoothSmile;
        wide = smoothWide;
        o = smoothO;
        sad = smoothSad;
        tongue = smoothTongue;
    }
    
    // 간단한 GUI 디버깅
    void OnGUI()
    {
        if (!showDebug) return;
        
        int y = 10;
        GUI.Box(new Rect(10, y, 200, 160), "");
        
        y += 5;
        GUI.Label(new Rect(15, y, 190, 20), "=== 클라이언트 (수신) ===");
        y += 25;
        
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
        
        // 테스트 버튼들
        y += 30;
        if (GUI.Button(new Rect(15, y, 80, 20), "리셋"))
        {
            ResetAllExpressions();
        }
        if (GUI.Button(new Rect(105, y, 80, 20), "테스트"))
        {
            // 간단한 테스트 표정
            SetFacialDataImmediate(0.3f, 0.5f, 0.2f, 0f, 0f, 0f);
        }
    }
}