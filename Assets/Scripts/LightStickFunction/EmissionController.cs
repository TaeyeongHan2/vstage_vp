using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class EmissionController : MonoBehaviour
{
    [Header("속도 추적")]
    [Tooltip("VelocityEstimator가 붙어 있는 오브젝트 참조")]
    public VelocityEstimator velocityEstimator;

    [Header("Emission 매핑")]
    [Tooltip("이 속도 이상은 풀 밝기")]
    public float maxSpeed = 1f;
    [Tooltip("풀 밝기일 때 Emission 컬러 강도")]
    public float maxIntensity = 4f;
    [Tooltip("머티리얼에 설정된 베이스 Emission 컬러")]
    public Color baseEmissionColor = Color.cyan;

    Material _mat;

    void Awake()
    {
        // Renderer에서 인스턴스화된 머티리얼 가져오기
        _mat = GetComponent<Renderer>().material;
        // 초기 Emission 끄기
        // _mat.SetColor("_EmissionColor", Color.black);
        // _mat.DisableKeyword("_EMISSION");
    }

    void Update()
    {
        if (velocityEstimator == null) return;
        
        // 1) 추정 속도 가져오기
        float speed = velocityEstimator.GetVelocityEstimate().magnitude;
        //Debug.Log($"[Shake Speed] {speed:F2} m/s");

        // 2) 0~1 정규화
        float t = Mathf.Clamp01(speed / maxSpeed);

        // 3) Emission 강도 계산
        float intensity = t * maxIntensity;
        Color emitCol = baseEmissionColor * intensity;

        // 4) 머티리얼에 적용
        _mat.SetColor("_EmissionColor", emitCol);
        if (intensity > 0f) _mat.EnableKeyword("_EMISSION");
        else                _mat.DisableKeyword("_EMISSION");
    }
}