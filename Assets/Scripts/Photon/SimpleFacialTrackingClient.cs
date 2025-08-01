using UnityEngine;
using Fusion;

[System.Serializable]
public class FacialExpressionData
{
    public float jaw = 0f;
    public float smile = 0f;
    public float wide = 0f;
    public float o = 0f;
    public float sad = 0f;
    public float tongue = 0f;
    
    public FacialExpressionData() { }
    
    public FacialExpressionData(float jaw, float smile, float wide, float o, float sad, float tongue)
    {
        this.jaw = jaw;
        this.smile = smile;
        this.wide = wide;
        this.o = o;
        this.sad = sad;
        this.tongue = tongue;
    }
}

public class SimpleFacialTrackingClient : NetworkBehaviour
{
    [Header("필수 설정")]
    public SkinnedMeshRenderer targetMesh;
    
    [Header("조정값")]
    [Range(0f, 2f)] public float intensity = 1.0f;
    [Range(0f, 1f)] public float smoothing = 0.1f;
    
    [Header("네트워크 설정")]
    [Range(0.01f, 0.5f)] public float networkSendRate = 0.1f; // RPC 전송 간격 (초)
    [Range(0.001f, 0.1f)] public float changeThreshold = 0.01f; // 변화 감지 임계값
    
    [Header("시뮬레이션 설정")]
    public bool enableSimulation = false; // 시뮬レ이션 모드 활성화
    public bool enableKeyboardControl = true; // 키보드 조작 활성화
    [Range(0.1f, 5f)] public float simulationSpeed = 1f; // 시뮬레이션 속도
    
    [Header("디버그")]
    public bool showDebug = false;
    public bool showGUI = true; // GUI 컨트롤 표시
    
    // 현재 표정 데이터
    private FacialExpressionData currentExpression = new FacialExpressionData();
    private FacialExpressionData targetExpression = new FacialExpressionData();
    
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
    
    // 시뮬레이션용 변수들
    private float simulationTime = 0f;
    private bool isSimulating = false;
    
    // GUI용 변수들
    private bool showControlPanel = false;
    private Rect controlPanelRect = new Rect(220, 10, 250, 300);
    
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
        
        Debug.Log("SimpleFacialTrackingClient 초기화 완료!");
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
        
        // 네트워크 오브젝트가 제대로 스폰되었는지 확인
        if (!IsNetworkReady())
        {
            return;
        }
        
        // 키보드 입력 처리
        if (enableKeyboardControl)
        {
            HandleKeyboardInput();
        }
        
        // 시뮬레이션 처리
        if (enableSimulation && isSimulating)
        {
            UpdateSimulation();
        }
        
        // 호스트에서만 네트워크 전송
        if (HasStateAuthority)
        {
            // 표정 처리
            ProcessFacialExpressions();
            
            // 네트워크 전송 체크
            CheckAndSendFacialData();
        }
        else
        {
            // 클라이언트는 수신된 데이터만 적용
            ProcessFacialExpressions();
        }
    }
    
    /// <summary>
    /// 네트워크 오브젝트가 준비되었는지 확인
    /// </summary>
    bool IsNetworkReady()
    {
        // Fusion 오브젝트가 제대로 스폰되었는지 확인
        return Object != null && Object.IsValid;
    }
    
    void HandleKeyboardInput()
    {
        float inputDelta = Time.deltaTime * 2f; // 입력 속도
        
        // 숫자 키로 표정 조작
        if (Input.GetKey(KeyCode.Alpha1)) // 입 벌리기
        {
            targetExpression.jaw = Mathf.Min(1f, targetExpression.jaw + inputDelta);
        }
        if (Input.GetKey(KeyCode.Alpha2)) // 미소
        {
            targetExpression.smile = Mathf.Min(1f, targetExpression.smile + inputDelta);
        }
        if (Input.GetKey(KeyCode.Alpha3)) // 입 넓히기
        {
            targetExpression.wide = Mathf.Min(1f, targetExpression.wide + inputDelta);
        }
        if (Input.GetKey(KeyCode.Alpha4)) // O 모양
        {
            targetExpression.o = Mathf.Min(1f, targetExpression.o + inputDelta);
        }
        if (Input.GetKey(KeyCode.Alpha5)) // 슬픈 표정
        {
            targetExpression.sad = Mathf.Min(1f, targetExpression.sad + inputDelta);
        }
        if (Input.GetKey(KeyCode.Alpha6)) // 혀 내밀기
        {
            targetExpression.tongue = Mathf.Min(1f, targetExpression.tongue + inputDelta);
        }
        
        // 리셋 키
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ResetExpressions();
        }
        
        // 시뮬레이션 토글
        if (Input.GetKeyDown(KeyCode.S))
        {
            ToggleSimulation();
        }
        
        // GUI 토글
        if (Input.GetKeyDown(KeyCode.G))
        {
            showControlPanel = !showControlPanel;
        }
    }
    
    void UpdateSimulation()
    {
        simulationTime += Time.deltaTime * simulationSpeed;
        
        // 간단한 사인파 기반 시뮬레이션
        targetExpression.jaw = Mathf.Max(0, Mathf.Sin(simulationTime * 1.5f) * 0.5f + 0.2f);
        targetExpression.smile = Mathf.Max(0, Mathf.Sin(simulationTime * 0.8f + 1f) * 0.7f + 0.3f);
        targetExpression.wide = Mathf.Max(0, Mathf.Sin(simulationTime * 1.2f + 2f) * 0.4f + 0.1f);
        targetExpression.o = Mathf.Max(0, Mathf.Sin(simulationTime * 0.6f + 3f) * 0.6f + 0.2f);
        targetExpression.sad = Mathf.Max(0, Mathf.Sin(simulationTime * 0.9f + 4f) * 0.3f + 0.1f);
        targetExpression.tongue = Mathf.Max(0, Mathf.Sin(simulationTime * 2f + 5f) * 0.5f);
    }
    
    void ProcessFacialExpressions()
    {
        // 타겟 값으로 스무딩
        smoothJaw = Mathf.Lerp(smoothJaw, targetExpression.jaw * intensity, smoothing);
        smoothSmile = Mathf.Lerp(smoothSmile, targetExpression.smile * intensity, smoothing);
        smoothWide = Mathf.Lerp(smoothWide, targetExpression.wide * intensity, smoothing);
        smoothO = Mathf.Lerp(smoothO, targetExpression.o * intensity, smoothing);
        smoothSad = Mathf.Lerp(smoothSad, targetExpression.sad * intensity, smoothing);
        smoothTongue = Mathf.Lerp(smoothTongue, targetExpression.tongue * intensity, smoothing);
        
        // 블렌드셰이프에 적용
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
        
        if (showDebug && (smoothJaw > 0.01f || smoothSmile > 0.01f))
        {
            Debug.Log($"Expression - Jaw: {smoothJaw:F2}, Smile: {smoothSmile:F2}, Wide: {smoothWide:F2}");
        }
    }
    
    void CheckAndSendFacialData()
    {
        // 네트워크가 준비되지 않았으면 전송하지 않음
        if (!IsNetworkReady()) return;
        
        float currentTime = Time.time;
        
        // 시간 간격 체크
        if (currentTime - lastNetworkSendTime < networkSendRate) return;
        
        // 변화량 체크
        bool hasSignificantChange = 
            Mathf.Abs(smoothJaw - lastJaw) > changeThreshold ||
            Mathf.Abs(smoothSmile - lastSmile) > changeThreshold ||
            Mathf.Abs(smoothWide - lastWide) > changeThreshold ||
            Mathf.Abs(smoothO - lastO) > changeThreshold ||
            Mathf.Abs(smoothSad - lastSad) > changeThreshold ||
            Mathf.Abs(smoothTongue - lastTongue) > changeThreshold;
        
        if (hasSignificantChange)
        {
            // RPC 호출
            SendFacialDataRPC(smoothJaw, smoothSmile, smoothWide, smoothO, smoothSad, smoothTongue);
            
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
    
    // === 공개 메서드들 ===
    
    /// <summary>
    /// 특정 표정을 수동으로 설정
    /// </summary>
    public void SetExpression(string expressionName, float value)
    {
        value = Mathf.Clamp01(value);
        
        switch (expressionName.ToLower())
        {
            case "jaw":
            case "mouth_a1":
                targetExpression.jaw = value;
                break;
            case "smile":
            case "mouth_smile":
                targetExpression.smile = value;
                break;
            case "wide":
            case "mouth_wide":
                targetExpression.wide = value;
                break;
            case "o":
            case "mouth_o1":
                targetExpression.o = value;
                break;
            case "sad":
            case "mouth_sad":
                targetExpression.sad = value;
                break;
            case "tongue":
            case "tongue_pero":
                targetExpression.tongue = value;
                break;
        }
    }
    
    /// <summary>
    /// 모든 표정 리셋
    /// </summary>
    public void ResetExpressions()
    {
        targetExpression = new FacialExpressionData();
        if (showDebug)
            Debug.Log("모든 표정이 리셋되었습니다.");
    }
    
    /// <summary>
    /// 시뮬레이션 토글
    /// </summary>
    public void ToggleSimulation()
    {
        isSimulating = !isSimulating;
        if (isSimulating)
        {
            simulationTime = 0f;
            Debug.Log("표정 시뮬레이션 시작");
        }
        else
        {
            Debug.Log("표정 시뮬레이션 중지");
        }
    }
    
    /// <summary>
    /// 표정 데이터를 직접 설정 (네트워크 수신용)
    /// </summary>
    public void SetFacialData(float jaw, float smile, float wide, float o, float sad, float tongue)
    {
        targetExpression.jaw = jaw;
        targetExpression.smile = smile;
        targetExpression.wide = wide;
        targetExpression.o = o;
        targetExpression.sad = sad;
        targetExpression.tongue = tongue;
    }
    
    /// <summary>
    /// 현재 표정 데이터 가져오기
    /// </summary>
    public FacialExpressionData GetCurrentExpression()
    {
        return new FacialExpressionData(smoothJaw, smoothSmile, smoothWide, smoothO, smoothSad, smoothTongue);
    }
    
    // GUI
    void OnGUI()
    {
        if (!showGUI) return;
        
        // 디버그 정보 표시
        if (showDebug)
        {
            DrawDebugInfo();
        }
        
        // 컨트롤 패널 표시
        if (showControlPanel)
        {
            controlPanelRect = GUI.Window(0, controlPanelRect, DrawControlPanel, "표정 컨트롤");
        }
        
        // 간단한 도움말
        int helpY = Screen.height - 120;
        GUI.Box(new Rect(10, helpY, 200, 110), "");
        GUI.Label(new Rect(15, helpY + 5, 190, 20), "=== 조작법 ===");
        GUI.Label(new Rect(15, helpY + 25, 190, 20), "1-6: 표정 조작");
        GUI.Label(new Rect(15, helpY + 40, 190, 20), "Space: 리셋");
        GUI.Label(new Rect(15, helpY + 55, 190, 20), "S: 시뮬레이션 토글");
        GUI.Label(new Rect(15, helpY + 70, 190, 20), "G: 컨트롤 패널 토글");
        
        GUI.color = isSimulating ? Color.green : Color.gray;
        GUI.Label(new Rect(15, helpY + 90, 190, 20), $"시뮬레이션: {(isSimulating ? "ON" : "OFF")}");
        GUI.color = Color.white;
    }
    
    void DrawDebugInfo()
    {
        int y = 10;
        GUI.Box(new Rect(10, y, 200, 180), "");
        
        y += 5;
        GUI.Label(new Rect(15, y, 190, 20), "=== 페이셜 트래킹 ===");
        y += 25;
        
        // 네트워크 상태
        if (!IsNetworkReady())
        {
            GUI.color = Color.red;
            GUI.Label(new Rect(15, y, 190, 20), "네트워크 준비 중...");
            GUI.color = Color.white;
            return;
        }
        
        if (HasStateAuthority)
        {
            GUI.color = Color.green;
            GUI.Label(new Rect(15, y, 190, 20), "호스트 (송신 모드)");
        }
        else
        {
            GUI.color = Color.yellow;
            GUI.Label(new Rect(15, y, 190, 20), "클라이언트 (수신 모드)");
        }
        GUI.color = Color.white;
        y += 20;
        
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
    
    void DrawControlPanel(int windowID)
    {
        int y = 25;
        
        // 강도 조절
        GUI.Label(new Rect(10, y, 100, 20), "강도:");
        intensity = GUI.HorizontalSlider(new Rect(60, y + 2, 120, 20), intensity, 0f, 2f);
        GUI.Label(new Rect(190, y, 50, 20), $"{intensity:F1}");
        y += 25;
        
        // 스무딩 조절
        GUI.Label(new Rect(10, y, 100, 20), "스무딩:");
        smoothing = GUI.HorizontalSlider(new Rect(60, y + 2, 120, 20), smoothing, 0f, 1f);
        GUI.Label(new Rect(190, y, 50, 20), $"{smoothing:F2}");
        y += 30;
        
        // 개별 표정 조절
        GUI.Label(new Rect(10, y, 230, 20), "=== 표정 조절 ===");
        y += 25;
        
        targetExpression.jaw = DrawExpressionSlider("입 벌림", targetExpression.jaw, y);
        y += 25;
        targetExpression.smile = DrawExpressionSlider("미소", targetExpression.smile, y);
        y += 25;
        targetExpression.wide = DrawExpressionSlider("입 넓힘", targetExpression.wide, y);
        y += 25;
        targetExpression.o = DrawExpressionSlider("O 모양", targetExpression.o, y);
        y += 25;
        targetExpression.sad = DrawExpressionSlider("슬픔", targetExpression.sad, y);
        y += 25;
        targetExpression.tongue = DrawExpressionSlider("혀", targetExpression.tongue, y);
        y += 30;
        
        // 버튼들
        if (GUI.Button(new Rect(10, y, 100, 25), "모두 리셋"))
        {
            ResetExpressions();
        }
        
        if (GUI.Button(new Rect(120, y, 110, 25), isSimulating ? "시뮬레이션 중지" : "시뮬레이션 시작"))
        {
            ToggleSimulation();
        }
        
        GUI.DragWindow();
    }
    
    float DrawExpressionSlider(string label, float value, int y)
    {
        GUI.Label(new Rect(10, y, 60, 20), label + ":");
        float newValue = GUI.HorizontalSlider(new Rect(70, y + 2, 120, 20), value, 0f, 1f);
        GUI.Label(new Rect(200, y, 40, 20), $"{(newValue * 100f):F0}%");
        return newValue;
    }
    
    // === 네트워킹 ===
    
    /// <summary>
    /// 호스트에서 클라이언트들에게 페이셜 데이터 전송 (RPC)
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void SendFacialDataRPC(float jaw, float smile, float wide, float o, float sad, float tongue)
    {
        // 네트워크가 준비되지 않았으면 무시
        if (!IsNetworkReady()) return;
        
        // 호스트는 이미 로컬에서 적용했으므로 클라이언트만 적용
        if (!HasStateAuthority)
        {
            SetFacialData(jaw, smile, wide, o, sad, tongue);
            
            if (showDebug)
            {
                Debug.Log($"[클라이언트] 페이셜 데이터 수신 - Jaw: {jaw:F2}, Smile: {smile:F2}, Wide: {wide:F2}");
            }
        }
    }
}