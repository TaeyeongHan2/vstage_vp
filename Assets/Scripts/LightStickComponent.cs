using UnityEngine;

public class LightStickComponent : MonoBehaviour
{
    [Header("손 추적 관련")]
    public Transform followTarget;
    private bool _isFollowing = false;
    
    [Header("왼손바닥 위에 있던 위치")]
    public Transform leftHandTarget;
    
    [Header("충돌 시 비활성화할 오브젝트")]
    public GameObject disableRightHandMesh;
    public GameObject disableLeftHandGesFunc;
    [SerializeField]
    private EmissionController emissionController;
    private void OnTriggerEnter(Collider other)
    {
        //충돌 이벤트 발생했을 때 손바닥 위치를 따라가면서 녹음이 시작되는 부분
        if (other.CompareTag("Palm"))
        {
            Debug.Log("[LightStickComponent] 손과 충돌!");
            //오른손 mesh 비활성화
            if (disableRightHandMesh != null)
            {
                disableRightHandMesh.SetActive(false);
            }

            if (disableLeftHandGesFunc != null)
            {
                disableLeftHandGesFunc.SetActive(false);
            }
                
            _isFollowing = true;
            transform.position = followTarget.position;
            transform.rotation = followTarget.rotation;
        }
    }
    
    //오른손의 손바닥 위치를 계속 따라다님
    private void Update()
    {
        if (_isFollowing && followTarget != null)
        {
            transform.position = followTarget.position;
            transform.rotation = followTarget.rotation;
        }
    }
    
    //제스처 감지 추가해서 응원봉이 원래 왼손 바닥의 위치로 돌아가는 부분 추가
    public void OnGrabGestureReleased()
    {
        _isFollowing = false;

        // 왼손의 palm 아래로 다시 이동
        transform.SetParent(leftHandTarget);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        if (emissionController == null)
        {
            Debug.LogError("[LightStickComponent] <UNK> <UNK> <UNK> <UNK> <UNK>!");
            return;
        }
        
        emissionController.velocityEstimator.ResetToInitialState();
        Debug.Log("[LightStickComponent] 주먹 제스처 풀림 -> 왼손으로 복귀");
    }
}
