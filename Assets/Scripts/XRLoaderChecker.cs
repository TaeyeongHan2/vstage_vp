using UnityEngine;
using UnityEngine.XR.Management;

public class XRLoaderChecker : MonoBehaviour
{
    void Awake()
    {
        var xrMgr = XRGeneralSettings.Instance.Manager;
        Debug.Log($"[XRLoaderChecker] XR Manager 인스턴스: {xrMgr}");
        Debug.Log($"[XRLoaderChecker] 초기화 완료 상태: {xrMgr.isInitializationComplete}");
        Debug.Log($"[XRLoaderChecker] Active Loader: {xrMgr.activeLoader}");
        if (xrMgr.activeLoader != null)
            Debug.Log($"[XRLoaderChecker] 로더 이름: {xrMgr.activeLoader.name}");
    }
}