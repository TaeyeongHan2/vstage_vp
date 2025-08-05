using UnityEngine;

public class LightColorController : MonoBehaviour
{
    public Light targetLight;
    public Color color = Color.cornflowerBlue;

    void Update()
    {
        if (targetLight != null)
        {
            targetLight.color = color;
        }

        // 키로 랜덤 색상 바꾸기 (디버깅용)
        if (Input.GetKeyDown(KeyCode.C))
        {
            color = Random.ColorHSV();
        }
    }
}

