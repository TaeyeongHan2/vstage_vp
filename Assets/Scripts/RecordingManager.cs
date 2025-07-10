using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;

[RequireComponent(typeof(AudioSource))]
public class RecordingManager : MonoBehaviour
{
    [Header("Gesture Settings")]
    [SerializeField] private StaticHandGesture gesture;

    [Header("Audio Settings")]
    [Tooltip("녹음된 오디오를 재생할 AudioSource")]
    [SerializeField] private AudioSource audioSource;
    
    private const int k_MaxRecordSeconds = 300; 
    private const int k_SampleRate       = 44100;

    private AudioClip _fullClip;
    private bool      _isRecording;
    private string    _deviceName;

    public void Start()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        Debug.Log("오디오 소스가 할당되었습니다.");
    }

    public void OnEnable()
    {
        if (gesture == null)
        {
            Debug.LogError("[RecordingManager] OnEnable(): StaticHandGesture 레퍼런스가 없습니다!");
            return;
        }

        Debug.Log("[RecordingManager] OnEnable(): 제스처 리스너 등록 시작");
        gesture.GesturePerformed.AddListener(StartRecording);
        gesture.GestureEnded   .AddListener(StopRecording);
        Debug.Log("[RecordingManager] OnEnable(): 제스처 리스너 등록 완료");
    }

    public void OnDisable()
    {
        if (gesture != null)
        {
            Debug.Log("[RecordingManager] OnDisable(): 제스처 리스너 해제 시작");
            gesture.GesturePerformed.RemoveListener(StartRecording);
            gesture.GestureEnded   .RemoveListener(StopRecording);
            Debug.Log("[RecordingManager] OnDisable(): 제스처 리스너 해제 완료");
        }
    }

    public void StartRecording()
    {
        if (_isRecording) return;
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("[RecordingManager] 마이크 장치 없음");
            return;
        }

        _deviceName = null;
        Debug.Log("[RecordingManager] 녹음 시작");
        _fullClip = Microphone.Start(_deviceName, false, k_MaxRecordSeconds, k_SampleRate);
        _isRecording = true;
    }

    public void StopRecording()
    {
        if (!_isRecording) return;

        int position = Microphone.GetPosition(_deviceName);
        Microphone.End(_deviceName);
        _isRecording = false;

        Debug.Log($"[RecordingManager] ⏹️ 녹음 종료, 샘플 길이: {position}");

        // 실제 녹음된 길이만큼 클립을 잘라서 새 AudioClip 생성
        float[] samples = new float[position * _fullClip.channels];
        _fullClip.GetData(samples, 0);

        var trimmed = AudioClip.Create(
            "TrimmedRecording",
            position,
            _fullClip.channels,
            k_SampleRate,
            false);

        trimmed.SetData(samples, 0);

        // AudioSource에 할당
        audioSource.clip = trimmed;
        Debug.Log("[RecordingManager]  잘라낸 클립 할당 완료");
        
        // **즉시 재생** 추가!
        Debug.Log("[RecordingManager]  자동 재생 시작");
        audioSource.Play();
    }

    /// <summary>
    /// 녹음된 오디오를 재생합니다.
    /// </summary>
    public void PlayRecording()
    {
        if (audioSource.clip == null)
        {
            Debug.LogWarning("[RecordingManager] 재생할 녹음이 없습니다.");
            return;
        }

        Debug.Log("[RecordingManager]  녹음 재생");
        audioSource.Play();
    }
}
