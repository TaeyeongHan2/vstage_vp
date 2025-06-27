#if UNITY_EDITOR
using UnityEngine.XR.Hands.Gestures;

public static class FingerShapeDataGenerator
{
    public static FingerShapeData CreateData(XRFingerShape shapes)
    {
        var fingerShapeData = new FingerShapeData();
        
        shapes.TryGetFullCurl(out fingerShapeData.Values[0]);
        shapes.TryGetBaseCurl(out fingerShapeData.Values[1]);
        shapes.TryGetTipCurl(out fingerShapeData.Values[2]);
        shapes.TryGetPinch(out fingerShapeData.Values[3]);
        shapes.TryGetSpread(out fingerShapeData.Values[4]);

        return fingerShapeData;
    }
}
#endif