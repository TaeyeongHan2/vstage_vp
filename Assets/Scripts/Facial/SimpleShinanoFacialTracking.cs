using UnityEngine;
using Fusion;

/// <summary>
/// Apple Vision Pro 클라이언트용 페이셜 트래킹 컴포넌트
/// 네트워크에서 받은 블렌드셰이프 데이터를 아바타에 적용
/// </summary>
public class SimpleShinanoFacialTracking : NetworkBehaviour
{
    [Header("필수 설정")]
    public SkinnedMeshRenderer targetMesh;
    
    [Header("조정값")]
    [Range(0f, 2f)] public float intensity = 1.0f;
    [Range(0f, 1f)] public float smoothing = 0.1f;
    
    [Header("디버그")]
    public bool showDebug = false;
    
    [Header("네트워크 페이셜 데이터")]
    [Networked] public float NetworkJaw { get; set; }
    [Networked] public float NetworkSmile { get; set; }
    [Networked] public float NetworkWide { get; set; }
    [Networked] public float NetworkO { get; set; }
    [Networked] public float NetworkSad { get; set; }
    [Networked] public float NetworkTongue { get; set; }
    
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
    
    // 이전 네트워크 값들 (변화 감지용)
    private float prevNetworkJaw = 0f;
    private float prevNetworkSmile = 0f;
    private float prevNetworkWide = 0f;
    private float prevNetworkO = 0f;
    private float prevNetworkSad = 0f;
    private float prevNetworkTongue = 0f;

    public override void Spawned()
    {
        InitializeComponent();
    }
    
    void Start()
    {
        // NetworkBehaviour가 아직 초기화되지 않았을 수 있으므로 Start에서도 호출
        if (targetMesh == null)
        {
            InitializeComponent();
        }
    }
    
    void InitializeComponent()
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
        
        Debug.Log("SimpleShinanoFacialTracking (Apple Vision Pro 클라이언트용) 초기화 완료!");
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
        
        // 네트워크 데이터가 변경되었는지 확인하고 적용
        bool dataChanged = CheckNetworkDataChanged();
        if (dataChanged)
        {
            ApplyNetworkFacialData();
        }
        
        // 스무딩 적용
        ApplySmoothedBlendshapes();
    }
    
    bool CheckNetworkDataChanged()
    {
        bool changed = false;
        
        if (Mathf.Abs(NetworkJaw - prevNetworkJaw) > 0.001f) changed = true;
        if (Mathf.Abs(NetworkSmile - prevNetworkSmile) > 0.001f) changed = true;
        if (Mathf.Abs(NetworkWide - prevNetworkWide) > 0.001f) changed = true;
        if (Mathf.Abs(NetworkO - prevNetworkO) > 0.001f) changed = true;
        if (Mathf.Abs(NetworkSad - prevNetworkSad) > 0.001f) changed = true;
        if (Mathf.Abs(NetworkTongue - prevNetworkTongue) > 0.001f) changed = true;
        
        if (changed)
        {
            prevNetworkJaw = NetworkJaw;
            prevNetworkSmile = NetworkSmile;
            prevNetworkWide = NetworkWide;
            prevNetworkO = NetworkO;
            prevNetworkSad = NetworkSad;
            prevNetworkTongue = NetworkTongue;
        }
        
        return changed;
    }
    
    void ApplyNetworkFacialData()
    {
        // 네트워크에서 받은 데이터를 스무딩 타겟으로 설정
        float deltaTime = Time.deltaTime;
        float lerpSpeed = deltaTime / Mathf.Max(smoothing, 0.001f);
        
        smoothJaw = Mathf.Lerp(smoothJaw, NetworkJaw * intensity, lerpSpeed);
        smoothSmile = Mathf.Lerp(smoothSmile, NetworkSmile * intensity, lerpSpeed);
        smoothWide = Mathf.Lerp(smoothWide, NetworkWide * intensity, lerpSpeed);
        smoothO = Mathf.Lerp(smoothO, NetworkO * intensity, lerpSpeed);
        smoothSad = Mathf.Lerp(smoothSad, NetworkSad * intensity, lerpSpeed);
        smoothTongue = Mathf.Lerp(smoothTongue, NetworkTongue * intensity, lerpSpeed);
        
        if (showDebug)
        {
            Debug.Log($"Network Facial Data: Jaw={NetworkJaw:F2}, Smile={NetworkSmile:F2}, Wide={NetworkWide:F2}, O={NetworkO:F2}, Sad={NetworkSad:F2}, Tongue={NetworkTongue:F2}");
        }
    }
    
    void ApplySmoothedBlendshapes()
    {
        // 스무딩된 값들을 블렌드셰이프에 적용
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
        GUI.Box(new Rect(10, y, 200, 170), "");
        
        y += 5;
        GUI.Label(new Rect(15, y, 190, 20), "=== 네트워크 페이셜 ===");
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
        y += 20;
        GUI.Label(new Rect(15, y, 190, 20), $"권한: {(Object.HasInputAuthority ? "Host" : "Client")}");
    }
    
    // === 네트워킹용 데이터 접근 메서드들 ===
    
    /// <summary>
    /// 현재 페이셜 데이터를 가져옵니다 (호스트용 - 사용하지 않음)
    /// </summary>
    public void GetFacialData(out float jaw, out float smile, out float wide, out float o, out float sad, out float tongue)
    {
        // Apple Vision Pro 클라이언트에서는 데이터를 생성하지 않고 받기만 함
        jaw = NetworkJaw;
        smile = NetworkSmile;
        wide = NetworkWide;
        o = NetworkO;
        sad = NetworkSad;
        tongue = NetworkTongue;
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
    /// 네트워크로 페이셜 데이터를 설정합니다 (Host에서 호출)
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    public void SetNetworkFacialDataRPC(float jaw, float smile, float wide, float o, float sad, float tongue)
    {
        NetworkJaw = jaw;
        NetworkSmile = smile;
        NetworkWide = wide;
        NetworkO = o;
        NetworkSad = sad;
        NetworkTongue = tongue;
        
        if (showDebug)
        {
            Debug.Log($"RPC 페이셜 데이터 수신: Jaw={jaw:F2}, Smile={smile:F2}");
        }
    }
    
    /// <summary>
    /// 페이셜 트래킹이 활성화되어 있는지 확인 (항상 네트워크 기반으로 활성화)
    /// </summary>
    public bool IsFacialTrackingActive()
    {
        return enabled && targetMesh != null;
    }
} 