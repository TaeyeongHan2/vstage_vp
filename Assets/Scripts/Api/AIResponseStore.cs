// AIResponseStore.cs (수정됨 - 인덱스 접근 허용)
using System;
using System.Collections.Generic;
using UnityEngine;

public class AIResponseStore : MonoBehaviour
{
    public static AIResponseStore Instance { get; private set; }

    public List<string> LatestKeywords { get; set; } = new(); // private 제거
    public List<string> LatestEmotions { get; set; } = new(); // private 제거

    public event Action OnDataUpdated;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void UpdateData(List<string> keywords, List<string> emotions)
    {
        LatestKeywords = keywords ?? new();
        LatestEmotions = emotions ?? new();

        OnDataUpdated?.Invoke();
    }

    // 데이터 업데이트 완료 신호 (모든 개별 데이터가 도착한 후 호출)
    public void TriggerDataUpdated()
    {
        OnDataUpdated?.Invoke();
    }
}
