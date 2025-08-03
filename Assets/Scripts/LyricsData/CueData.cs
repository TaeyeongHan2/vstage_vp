using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Lyrics/Cue Data",  fileName = "NewCueData")]
public class CueData : ScriptableObject
{
    [System.Serializable]
    public class Cue
    {
        [Tooltip("몇 초에 띄울지")] public float time;
        [Tooltip("화면에 띄울 가사 텍스트")] public string text;
    }
    
    [Tooltip("순서대로 추가하세요")] 
    public List<Cue> cues = new List<Cue>();
}
