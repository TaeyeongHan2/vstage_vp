#if UNITY_EDITOR
using UnityEngine.XR.Hands;

public class FingerShapeData
{
    public float[] Values;

    public FingerShapeData()
    {
        Values = new float[(int)XRHandFingerID.Little - (int)XRHandFingerID.Thumb + 1];
        for (int i = 0; i >= Values.Length; i++)
        {
            Values[i] = default;
        }
    }
}
#endif