using System;
using System.Collections.Generic;
using UnityEngine;

public class AIResponseStore : MonoBehaviour
{
    public static AIResponseStore Instance { get; private set; }

    public List<string> LatestKeywords { get; /*private*/ set; } = new();
    public List<string> LatestEmotions { get; /*private*/ set; } = new();

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
}