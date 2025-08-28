using UnityEngine;
using UnityEngine.VFX;
using System.Collections;

public class VFXColorController : MonoBehaviour
{
    public VisualEffect vfx;
    public Color colorA = Color.red;
    public Color colorB = Color.yellow;

    [Range(0.5f, 10f)] public float intensityA = 10f;

    [Range(0.5f, 10f)] public float intensityB = 10f;

    void Start()
    {
        if (vfx != null)
        {
            vfx.SetVector4("Color A", colorA * 8f);
            vfx.SetVector4("Color B", colorB * 8f);
        }

        StartCoroutine(ChangeColorsRoutine());
    }

    IEnumerator ChangeColorsRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            colorA = Random.ColorHSV(0f, 1f, 1f, 1f, 1f, 1f);
            vfx.SetVector4("Color A", colorA * 8f);

            yield return new WaitForSeconds(1f);

            colorB = Random.ColorHSV(0f, 1f, 1f, 1f, 1f, 1f);
            vfx.SetVector4("Color B", colorB * 8f);
        }
    }
}