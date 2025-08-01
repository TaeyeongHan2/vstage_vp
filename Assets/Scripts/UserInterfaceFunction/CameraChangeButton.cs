using UnityEngine;

public class CameraChangeButton : MonoBehaviour
{
    [Header("전환할 카메라들")]
    public Camera[] cameras;

    int current = 0;  

    private void Start()
    {
        // 처음엔 cameras[0]만 켜고 나머지는 끔
        for (int i = 0; i < cameras.Length; i++)
            cameras[i].enabled = (i == current);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        //오른손의 검지 손가락과 버튼이 충돌 했을 때 특정 기능이 실행됨. 
        if (other.CompareTag("IndexTip"))
        {
            Debug.Log("검지손가락과 충돌함");
            cameras[current].enabled = false;
            Debug.Log("현재 카메라 비활성화");
            current = (current + 1) % cameras.Length;
            cameras[current].enabled = true;
            Debug.Log("새로운 카메라로 토글됨");
        }
    }
}
