using System;
using System.Collections.Generic;
using Newtonsoft.Json;

[Serializable]
public class EmotionKeywordData
{
    [JsonProperty("emotions")]
    public List<string> emotions { get; set; }

    [JsonProperty("keywords")]
    public List<string> keywords { get; set; }
}