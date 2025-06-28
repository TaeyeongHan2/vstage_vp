#if UNITY_EDITOR
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;

public static class HandShapeDataGenerator
{
    private static XRFingerShape[] _XRFingerShapes =
        new XRFingerShape[(int)XRFingerShapeType.Spread - (int)XRFingerShapeType.FullCurl + 1];
    
    public static HandShapeData CreateData(XRHand hand)
    {
        var handShapeData = new HandShapeData();

        CalculateFingerShapes(hand);
        
        for (var fingerIndex = (int)XRHandFingerID.Thumb;
             fingerIndex <= (int)XRHandFingerID.Little;
             ++fingerIndex)
        {
            var fingerShapeData = FingerShapeDataGenerator.CreateData(_XRFingerShapes[fingerIndex]);
            handShapeData.FingerShapeDatas[fingerIndex] = fingerShapeData;
        }
        
        return handShapeData;
    }

    private static void CalculateFingerShapes(XRHand hand)
    {
        for (var fingerIndex = (int)XRHandFingerID.Thumb;
             fingerIndex <= (int)XRHandFingerID.Little;
             ++fingerIndex)
        {
            _XRFingerShapes[fingerIndex] = hand.CalculateFingerShape(
                (XRHandFingerID)fingerIndex, 
                XRFingerShapeTypes.All);
        }
    }
}
#endif