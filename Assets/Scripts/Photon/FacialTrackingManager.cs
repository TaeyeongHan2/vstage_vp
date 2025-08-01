using UnityEngine;
using Fusion;

/// <summary>
/// 네트워크 환경에서 Host/Client에 따라 적절한 페이셜 트래킹 컴포넌트를 활성화하는 매니저
/// 아바타 프리팹에 붙여서 스폰 시 자동으로 역할에 맞는 컴포넌트를 설정합니다.
/// </summary>
public class FacialTrackingManager : NetworkBehaviour
{
    [Header("페이셜 트래킹 컴포넌트들")]
    [SerializeField] private SimpleShinanoFacialTracking_Host hostComponent;
    [SerializeField] private SimpleShinanoFacialTracking_Client clientComponent;
    
    [Header("자동 찾기 설정")]
    [SerializeField] private bool autoFindComponents = true;
    
    [Header("디버그")]
    [SerializeField] private bool showDebug = true;
    
    public override void Spawned()
    {
        // 스폰될 때 호출됨
        SetupFacialTracking();
    }
    
    void Start()
    {
        // Spawned()가 호출되지 않는 경우를 대비한 백업
        if (Object == null)
        {
            SetupFacialTracking();
        }
    }
    
    void SetupFacialTracking()
    {
        // 컴포넌트 자동 찾기
        if (autoFindComponents)
        {
            if (hostComponent == null)
                hostComponent = GetComponent<SimpleShinanoFacialTracking_Host>();
            
            if (clientComponent == null)
                clientComponent = GetComponent<SimpleShinanoFacialTracking_Client>();
        }
        
        // 컴포넌트가 없으면 경고
        if (hostComponent == null)
        {
            Debug.LogWarning($"[FacialTrackingManager] SimpleShinanoFacialTracking_Host 컴포넌트를 찾을 수 없습니다. ({gameObject.name})");
        }
        
        if (clientComponent == null)
        {
            Debug.LogWarning($"[FacialTrackingManager] SimpleShinanoFacialTracking_Client 컴포넌트를 찾을 수 없습니다. ({gameObject.name})");
        }
        
        // Host/Client 여부에 따라 적절한 컴포넌트 활성화
        ConfigureFacialTracking();
    }
    
    void ConfigureFacialTracking()
    {
        bool isHost = HasStateAuthority;
        
        if (showDebug)
        {
            Debug.Log($"[FacialTrackingManager] {gameObject.name} - 역할: {(isHost ? "Host" : "Client")}");
        }
        
        // Host인 경우
        if (isHost)
        {
            // Host 컴포넌트 활성화
            if (hostComponent != null)
            {
                hostComponent.enabled = true;
                if (showDebug)
                    Debug.Log($"[FacialTrackingManager] Host 페이셜 트래킹 활성화됨 ({gameObject.name})");
            }
            
            // Client 컴포넌트 비활성화
            if (clientComponent != null)
            {
                clientComponent.enabled = false;
                if (showDebug)
                    Debug.Log($"[FacialTrackingManager] Client 페이셜 트래킹 비활성화됨 ({gameObject.name})");
            }
        }
        // Client인 경우
        else
        {
            // Host 컴포넌트 비활성화
            if (hostComponent != null)
            {
                hostComponent.enabled = false;
                if (showDebug)
                    Debug.Log($"[FacialTrackingManager] Host 페이셜 트래킹 비활성화됨 ({gameObject.name})");
            }
            
            // Client 컴포넌트 활성화
            if (clientComponent != null)
            {
                clientComponent.enabled = true;
                if (showDebug)
                    Debug.Log($"[FacialTrackingManager] Client 페이셜 트래킹 활성화됨 ({gameObject.name})");
            }
        }
    }
    
    /// <summary>
    /// 역할이 변경되었을 때 수동으로 재설정
    /// </summary>
    public void ReconfigureFacialTracking()
    {
        ConfigureFacialTracking();
    }
    
    /// <summary>
    /// 현재 활성화된 페이셜 트래킹 컴포넌트 가져오기
    /// </summary>
    public SimpleShinanoFacialTracking_Host GetActiveHostComponent()
    {
        return (hostComponent != null && hostComponent.enabled) ? hostComponent : null;
    }
    
    /// <summary>
    /// 현재 활성화된 클라이언트 컴포넌트 가져오기
    /// </summary>
    public SimpleShinanoFacialTracking_Client GetActiveClientComponent()
    {
        return (clientComponent != null && clientComponent.enabled) ? clientComponent : null;
    }
    
    /// <summary>
    /// 현재 페이셜 트래킹 상태 확인
    /// </summary>
    public bool IsFacialTrackingActive()
    {
        if (HasStateAuthority)
        {
            return hostComponent != null && hostComponent.enabled && hostComponent.IsFacialTrackingActive();
        }
        else
        {
            return clientComponent != null && clientComponent.enabled;
        }
    }
    
    /// <summary>
    /// 모든 페이셜 트래킹 컴포넌트 비활성화
    /// </summary>
    public void DisableAllFacialTracking()
    {
        if (hostComponent != null)
            hostComponent.enabled = false;
        
        if (clientComponent != null)
            clientComponent.enabled = false;
        
        if (showDebug)
            Debug.Log($"[FacialTrackingManager] 모든 페이셜 트래킹 컴포넌트 비활성화됨 ({gameObject.name})");
    }
    
    /// <summary>
    /// 디버그 정보 표시
    /// </summary>
    void OnGUI()
    {
        if (!showDebug) return;
        
        int y = 200; // 다른 GUI와 겹치지 않도록 위치 조정
        GUI.Box(new Rect(10, y, 250, 120), "");
        
        y += 5;
        GUI.Label(new Rect(15, y, 240, 20), "=== 페이셜 트래킹 매니저 ===");
        y += 25;
        
        string role = HasStateAuthority ? "Host" : "Client";
        GUI.Label(new Rect(15, y, 240, 20), $"역할: {role}");
        y += 20;
        
        if (hostComponent != null)
        {
            GUI.color = hostComponent.enabled ? Color.green : Color.gray;
            GUI.Label(new Rect(15, y, 240, 20), $"Host 컴포넌트: {(hostComponent.enabled ? "활성" : "비활성")}");
            GUI.color = Color.white;
            y += 20;
        }
        
        if (clientComponent != null)
        {
            GUI.color = clientComponent.enabled ? Color.green : Color.gray;
            GUI.Label(new Rect(15, y, 240, 20), $"Client 컴포넌트: {(clientComponent.enabled ? "활성" : "비활성")}");
            GUI.color = Color.white;
            y += 20;
        }
        
        bool isActive = IsFacialTrackingActive();
        GUI.color = isActive ? Color.green : Color.red;
        GUI.Label(new Rect(15, y, 240, 20), $"페이셜 트래킹: {(isActive ? "작동 중" : "비활성")}");
        GUI.color = Color.white;
    }
}