using TMPro;
using UnityEngine;
using System.Collections;

public class TMP_PRO : MonoBehaviour
{
    public TMP_Text keywordText;
    public TMP_Text emotionText;
    
    [Header("화면 표시용 텍스트")]
    public TMP_Text transcriptionText;

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
        AIResponseStore.Instance.OnTranscriptionUpdated += UpdateTranscription;

        // 기존 데이터로 한 번 즉시 갱신
        UpdateText();
        UpdateTranscription();
    }

    public void UpdateText()
    {
        var keywords = AIResponseStore.Instance?.LatestKeywords ?? new();
        var emotions = AIResponseStore.Instance?.LatestEmotions ?? new();

        Debug.Log("TMP_PRO UpdateText 호출됨");
        Debug.Log("키워드: " + string.Join(", ", keywords));
        Debug.Log("감정: " + string.Join(", ", emotions));

        if (keywordText != null)
        {
            keywordText.text = "키워드: " + string.Join(", ", keywords);
        }
        else
        {
            Debug.LogWarning("keywordText is null");
        }

        if (emotionText != null)
        {
            emotionText.text = "감정: " + string.Join(", ", emotions);
        }
        else
        {
            Debug.LogWarning("emotionText is null");
        }
    }

    void UpdateTranscription() 
    { 
        
        if (transcriptionText == null) return;
        transcriptionText.text = AIResponseStore.Instance.LatestTranscription;
        transcriptionText.ForceMeshUpdate();
    }
    
    void OnDestroy()
    {
        if (AIResponseStore.Instance != null)
        {
            AIResponseStore.Instance.OnDataUpdated -= UpdateText;
        }
    }
}