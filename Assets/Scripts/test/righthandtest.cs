using UnityEngine;

public class NewMonoBehaviourScript : MonoBehaviour
{
    private void Update()
    {
        Debug.Log($"awake on {gameObject.name}이 활성화 중입니다.");
    }
}
