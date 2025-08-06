using System.Collections.Generic;
using UnityEngine;

public class EmotionColorMapper : MonoBehaviour
{
    [Header("감정 순서대로 20개 색상 입력")]
    [Tooltip("후보 리스트(20개) 순서와 일치시켜서 색상을 설정하세요.")]
    [SerializeField] private List<Color> emotionColors = new List<Color>(20);
    
    private Dictionary<string, Color> emotionColorMap = new Dictionary<string, Color>();

    //감정 이름 고정 배열
    public string[] emotionNames = new string[]
    {
        "사랑", "감사", "행복", "감동", "환희", "희열", "즐거움", "흥분",
        "자부심", "만족", "기대감", "평온", "경외감", "열정", "몰입", "유대감",
        "그리움", "설렘", "행복한 눈물", "충분함"
    };
    
    // 게임 시작 직전에 매핑 초기화
    private void Awake()
    {
        InitializeMap();
    }

    // 에디터 상에서 리스트를 바꿨을 때도 바로 반영되도록
    private void OnValidate()
    {
        InitializeMap();
    }
    
    // emotionNames 배열과 emotionColors 리스트 기반으로 딕셔너리 채우기
    private void InitializeMap()
    {
        emotionColorMap.Clear();
        int count = Mathf.Min(emotionNames.Length, emotionColors.Count);

        for (int i = 0; i < count; i++)
        {
            emotionColorMap[emotionNames[i]] = emotionColors[i];
        }

        // 혹시 배열 길이가 다르면 경고 로그
        if (emotionColors.Count != emotionNames.Length)
            Debug.LogWarning($"[EmotionColorMapper] emotionColors.Count({emotionColors.Count}) != emotionNames.Length({emotionNames.Length})",
                this);
    }
    
    /// <summary>
    /// 감정 이름에 매핑된 색상을 반환. 없으면 흰색.
    /// </summary>
    public Color GetColor(string emotion)
    {
        if (emotionColorMap.TryGetValue(emotion, out var col))
            return col;
        return Color.white;
    }
    
    // <summary>
    // 인덱스에 맞는 색상을 반환. 범위 벗어나면 흰색.
    // </summary>
    // public Color GetColor(int index)
    // {
    //     if (index >= 0 && index < emotionColors.Count)
    //         return emotionColors[index];
    //     return Color.white;
    // }
}