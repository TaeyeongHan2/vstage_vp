using UnityEngine;
using System.Collections;

public class LightColorController : MonoBehaviour
{
    public Light targetLight;
    public Color color = Color.cornflowerBlue;

    void Start()
    {
        // 코루틴 시작
        StartCoroutine(ChangeColorRoutine());
    }

    void Update()
    {
        if (targetLight != null)
        {
            targetLight.color = color;
        }
    }

    IEnumerator ChangeColorRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f); // 1초 대기
            
            // 랜덤 색상 변경
            color = Random.ColorHSV();
        }
    }
}