using TMPro;
using UnityEngine;
using System.Text; 
using System.Collections;

public class TMP_PRO : MonoBehaviour
{
    [Header("TextMeshPro Fields")]
    public TMP_Text keywordText;
    public TMP_Text emotionText;
    
    [Header("Emotion→Color 매핑 컴포넌트")]
    public EmotionColorMapper colorMapper; 

    void Start()
    {
        StartCoroutine(WaitForStore());
    }

    private IEnumerator WaitForStore()
    {
        // AIResponseStore.Instance가 생성될 때까지 대기
        while (AIResponseStore.Instance == null)
        {
            yield return null;
        }

        // 이벤트 구독
        AIResponseStore.Instance.OnDataUpdated += UpdateText;

        // 기존 데이터로 한 번 즉시 갱신
        UpdateText();
    }

    public void UpdateText()
    {
        var keywords = AIResponseStore.Instance?.LatestKeywords ?? new();
        var emotions = AIResponseStore.Instance?.LatestEmotions ?? new();

        Debug.Log("TMP_PRO UpdateText 호출됨");
        Debug.Log("키워드: " + string.Join(", ", keywords));
        Debug.Log("감정: " + string.Join(", ", emotions));
        // 1) 키워드에 감정색 매핑해서 Rich Text 조합
        if (keywordText != null && colorMapper != null)
        {
            var sb = new StringBuilder();
            int count = Mathf.Min(keywords.Count, emotions.Count);
            for (int i = 0; i < count; i++)
            {
                // i번째 감정의 색상 얻기
                Color c = colorMapper.GetColor(i);
                string hex = ColorUtility.ToHtmlStringRGB(c);

                // 색상 태그 적용
                sb.Append($"<color=#{hex}>{keywords[i]}</color>");
                if (i < count - 1)
                    sb.Append(", ");
            }
            keywordText.text = sb.ToString();
        }
        else if (keywordText != null)
        {
            // 컬러 매퍼 없으면 그냥 텍스트만
            keywordText.text = string.Join(", ", keywords);
        }

        // 2) 감정 리스트는 그대로(혹은 원하시면 이쪽도 컬러 처리)
        if (emotionText != null)
            emotionText.text = string.Join(", ", emotions);
        
        // if (keywordText != null)
        // {
        //     // keywordText.text = "키워드: " + string.Join(", ", keywords);
        //     keywordText.text = string.Join(", ", keywords);
        // }
        // else
        // {
        //     Debug.LogWarning("keywordText is null");
        // }
        //
        // if (emotionText != null)
        // {
        //     // emotionText.text = "감정: " + string.Join(", ", emotions);
        //     emotionText.text = string.Join(", ", emotions);
        // }
        // else
        // {
        //     Debug.LogWarning("emotionText is null");
        // }
    }

    void OnDestroy()
    {
        if (AIResponseStore.Instance != null)
        {
            AIResponseStore.Instance.OnDataUpdated -= UpdateText;
        }
    }
}