using System.Collections.Generic;
using UnityEngine;

public class EmotionColorMapper : MonoBehaviour
{
    [Header("감정 순서대로 20개 색상 입력")]
    [Tooltip("후보 리스트(20개) 순서와 일치시켜서 색상을 설정하세요.")]
    public List<Color> emotionColors = new List<Color>(20);

    // 감정 이름 고정 배열 (Inspector 에디터에서 라벨로 사용)
    public static readonly string[] emotionNames = new string[]
    {
        "사랑", "감사", "행복", "감동", "환희", "희열", "즐거움", "흥분",
        "자부심", "만족", "기대감", "평온", "경외감", "열정", "몰입", "유대감",
        "그리움", "설렘", "행복한 눈물", "충분함"
    };
    
    /// <summary>
    /// 인덱스에 맞는 색상을 반환. 범위 벗어나면 흰색.
    /// </summary>
    public Color GetColor(int index)
    {
        if (index >= 0 && index < emotionColors.Count)
            return emotionColors[index];
        return Color.white;
    }
}