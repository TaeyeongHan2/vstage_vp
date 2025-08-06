using UnityEngine;
using UnityEngine.VFX;

public class VFXColorController : MonoBehaviour
{
    public VisualEffect vfx;
    public Color colorA = Color.red;
    public Color colorB = Color.yellow;
    
    [Range(0.5f, 10f)]
    public float intensityA = 10f;

    [Range(0.5f, 10f)]
    public float intensityB = 10f;

    void Start()
    {
        if (vfx != null)
        {
            vfx.SetVector4("Color A", colorA * 8f);
            vfx.SetVector4("Color B", colorB * 8f);
        }
    }

    void Update()
    {
        // 키보드로 색상 랜덤 변경 (디버깅용)
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            colorA = Random.ColorHSV(0f, 1f, 1f, 1f, 1f, 1f);
            vfx.SetVector4("Color A", colorA * 8f);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            colorB = Random.ColorHSV(0f, 1f, 1f, 1f, 1f, 1f);
            vfx.SetVector4("Color B", colorB * 8f);
        }
    }
}

