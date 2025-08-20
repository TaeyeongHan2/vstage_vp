using UnityEngine;
using TMPro;

public class FpsTmpOverlay : MonoBehaviour {
    public TMP_Text label;
    [Range(0.01f, 1f)] public float smoothing = 0.12f;
    public bool showFrameTimeMs = true;
    float emaDelta;

    void Update() {
        float dt = Time.unscaledDeltaTime;
        emaDelta = Mathf.Lerp(emaDelta <= 0 ? dt : emaDelta, dt, smoothing);
        float fps = 1f / Mathf.Max(emaDelta, 0.00001f);
        if (showFrameTimeMs) label.text = $"{fps:0} FPS  ({emaDelta*1000f:0.0} ms)";
        else label.text = $"{fps:0} FPS";
    }
}