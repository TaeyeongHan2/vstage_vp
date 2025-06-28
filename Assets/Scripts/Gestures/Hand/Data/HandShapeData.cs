#if UNITY_EDITOR
using UnityEngine.XR.Hands;

public class HandShapeData
{
    public FingerShapeData[] FingerShapeDatas { get; private set; }

    public HandShapeData()
    {
        FingerShapeDatas = new FingerShapeData[(int)XRHandFingerID.Little - (int)XRHandFingerID.Thumb + 1];
        for (int i = 0; i < FingerShapeDatas.Length; i++)
        {
            FingerShapeDatas[i] = new FingerShapeData();
        }
    }
}
#endif