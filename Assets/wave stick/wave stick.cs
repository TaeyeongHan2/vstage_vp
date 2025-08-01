using UnityEngine;

public class wave : MonoBehaviour
{
    public float minAngle = -20f;
    public float maxAngle = 20f;
    public float minDuration = 0.5f;
    public float maxDuration = 1.5f;

    private Quaternion startRotation;
    private Quaternion targetRotation;
    private float duration;
    private float elapsed;

    void Start()
    {
        SetNewTargetRotation();
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        transform.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);

        if (t >= 1.0f)
        {
            SetNewTargetRotation();
        }
    }

    void SetNewTargetRotation()
    {
        startRotation = transform.localRotation;

        float randomX = Random.Range(minAngle, maxAngle);
        targetRotation = Quaternion.Euler(randomX, 0, 0); // 앞뒤로 흔들기: X축 회전

        duration = Random.Range(minDuration, maxDuration);
        elapsed = 0f;
    }
}