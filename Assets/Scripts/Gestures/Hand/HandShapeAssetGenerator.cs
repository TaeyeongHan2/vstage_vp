#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;

public class HandShapeAssetGenerator : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] Handedness m_Handedness = Handedness.Right;
    [SerializeField] private KeyCode _captureKey = KeyCode.Alpha1;
    [SerializeField] private string _savePath = "Assets/Hand Shapes";
    [SerializeField] private string _saveFileName = "Test Hand Shape";
    [SerializeField, Min(2)] private int _needCaptureCount = 10;
    
    [Header("Message")]
    [SerializeField] private TMP_Text _messageText;
    
    static List<XRHandSubsystem> s_SubsystemsReuse = new();
    
    void Start()
    {
        if (m_Handedness == Handedness.Invalid)
        {
            Debug.LogWarning($"The Handedness property of { GetType() } is set to Invalid and will default to Right.", this);
            m_Handedness = Handedness.Right;
        }
        
        StartCoroutine(CreateAssetSequence());
    }

    private IEnumerator CreateAssetSequence()
    {
        var waitKeyDown = new WaitUntil(() => Input.GetKeyDown(_captureKey));
        
        List<HandShapeData> handShapes = new List<HandShapeData>();
        
        _messageText.text = $"Capture {_needCaptureCount} times";
        Debug.Log($"에셋으로 만들고 싶은 손 모양을 {_needCaptureCount}번 캡쳐해 주세요!");
        
        while (handShapes.Count < _needCaptureCount)
        {
            yield return waitKeyDown;
            HandShapeData handShapeData = CaptureHandShape();
            handShapes.Add(handShapeData);
            
            _messageText.text = $"Capture success: {handShapes.Count}/{_needCaptureCount}";
            Debug.Log($"캡쳐 완료! : {handShapes.Count}/{_needCaptureCount}");
            yield return null;
        }

        CreateHandShapeAsset(handShapes);
    }
    
    private HandShapeData CaptureHandShape()
    {
        var subsystem = TryGetSubsystem();
        if (subsystem == null)
            return null;

        var hand = m_Handedness == Handedness.Left ? subsystem.leftHand : subsystem.rightHand;

        HandShapeData handShapeData = HandShapeDataGenerator.CreateData(hand);
        
        return handShapeData;
    }
    
    private void CreateHandShapeAsset(List<HandShapeData> handShapes)
    {
        XRHandShape asset = ScriptableObject.CreateInstance<XRHandShape>();
        
        List<XRFingerShapeCondition> conditions = new List<XRFingerShapeCondition>();
        for (var fingerIndex = (int)XRHandFingerID.Thumb;
             fingerIndex <= (int)XRHandFingerID.Little;
             ++fingerIndex)
        {
            XRFingerShapeCondition fingerShapeCondition = new XRFingerShapeCondition();
            fingerShapeCondition.targets =
                new XRFingerShapeCondition.Target[(int)XRFingerShapeType.Spread - (int)XRFingerShapeType.FullCurl + 1];

            fingerShapeCondition.fingerID = (XRHandFingerID)fingerIndex;
            for (var shapeIndex = (int)XRFingerShapeType.FullCurl;
                 shapeIndex <= (int)XRFingerShapeType.Spread;
                 shapeIndex++)
            {
                List<float> values = new List<float>();
                foreach (var handShape in handShapes)
                {
                    values.Add(handShape.FingerShapeDatas[fingerIndex].Values[shapeIndex]);    
                }
                
                var target = new XRFingerShapeCondition.Target();

                var average = Statistics.Average(values);
                var stDev = Statistics.StandardDeviation(values, GroupType.Sample);
                
                target.shapeType = (XRFingerShapeType)shapeIndex;
                target.upperTolerance = 1.5f * stDev;
                target.lowerTolerance = 1.5f * stDev;
                target.desired = average;
                
                fingerShapeCondition.targets[shapeIndex] = target;
            }

            conditions.Add(fingerShapeCondition);
        }

        asset.fingerShapeConditions = conditions;

        AssetDatabase.CreateAsset(asset, $"{_savePath}/{_saveFileName}.asset");
        AssetDatabase.SaveAssets();
        
        _messageText.text = "Asset created successfully!";
        Debug.Log("에셋이 생성되었습니다!");
    }
    
    private static XRHandSubsystem TryGetSubsystem()
    {
        SubsystemManager.GetSubsystems(s_SubsystemsReuse);
        return s_SubsystemsReuse.Count > 0 ? s_SubsystemsReuse[0] : null;
    }
}
#endif