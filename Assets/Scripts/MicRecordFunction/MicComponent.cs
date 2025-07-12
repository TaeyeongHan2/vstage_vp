using UnityEngine;

namespace MicRecordFunction
{
    public class MicComponent : MonoBehaviour
    {
        //충돌 감지
        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"마이크 충돌 이벤트 발생 / 충돌 대상: {other.gameObject.name}");
        }
    }
}