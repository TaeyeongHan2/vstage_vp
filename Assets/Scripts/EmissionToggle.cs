using UnityEngine;

public class EmissionToggle : MonoBehaviour
{
    public Renderer targetRenderer;   // 대상 오브젝트의 Renderer
    public Color emissionBaseColor = Color.white; // 기본 색상
    private Material mat;
    private bool isOn = false;

    void Start()
    {
        // 머티리얼 인스턴스화 (원본 변경 방지)
        mat = targetRenderer.material;
        // 처음엔 Emission 0으로
        mat.SetColor("_EmissionColor", emissionBaseColor * 0f);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isOn = !isOn;

            float intensity = isOn ? 3f : 0f;
            Color finalColor = emissionBaseColor * intensity;

            mat.SetColor("_EmissionColor", finalColor);

            // URP에서도 실시간 반영을 위해 EnableKeyword 필요
            if (isOn)
                mat.EnableKeyword("_EMISSION");
            else
                mat.DisableKeyword("_EMISSION");
        }
    }
}
