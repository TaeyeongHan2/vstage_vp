using UnityEngine;

public class CameraToggle : MonoBehaviour
{
    [Header("카메라 설정")]
    public Camera targetCamera; // 끄고 싶은 카메라를 드래그해서 연결
    
    [Header("키 설정")]
    public KeyCode toggleKey = KeyCode.N; // 기본값은 N키
    
    void Update()
    {
        // N키가 눌렸을 때
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleCamera();
        }
    }
    
    void ToggleCamera()
    {
        if (targetCamera != null)
        {
            // 카메라가 켜져있으면 끄고, 꺼져있으면 켜기
            targetCamera.enabled = !targetCamera.enabled;
            
            // 콘솔에 현재 상태 출력 (선택사항)
            Debug.Log($"카메라 {targetCamera.name}이(가) {(targetCamera.enabled ? "켜졌습니다" : "꺼졌습니다")}");
        }
        else
        {
            Debug.LogWarning("타겟 카메라가 설정되지 않았습니다!");
        }
    }
    
    // 카메라를 강제로 끄는 함수 (토글이 아닌 끄기만)
    public void DisableCamera()
    {
        if (targetCamera != null)
        {
            targetCamera.enabled = false;
            Debug.Log($"카메라 {targetCamera.name}이(가) 꺼졌습니다");
        }
    }
    
    // 카메라를 강제로 켜는 함수
    public void EnableCamera()
    {
        if (targetCamera != null)
        {
            targetCamera.enabled = true;
            Debug.Log($"카메라 {targetCamera.name}이(가) 켜졌습니다");
        }
    }
}