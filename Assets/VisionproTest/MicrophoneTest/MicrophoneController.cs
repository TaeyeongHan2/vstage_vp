using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System;

public class MicrophoneController : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioSource audioSource;
    public Button recordButton;
    public Button stopButton;
    public Button playButton;
    public Button saveButton;
    
    [Header("Recording Settings")]
    public int recordingLength = 60; // 최대 녹음 시간 (초)
    public int sampleRate = 44100;
    
    [Header("Save Settings")]
    public string saveFileName = "recorded_audio"; // 저장할 파일명 (확장자 제외)
    public bool saveToDesktop = true; // 바탕화면에 저장할지 여부
    public string customSavePath = ""; // 커스텀 저장 경로 (비어있으면 기본 경로 사용)
    
    private bool isRecording = false;
    private AudioClip recordedClip;
    private string lastSavedFilePath = "";
    
    void Start()
    {
        // AudioSource가 없으면 자동으로 추가
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        
        // 버튼 이벤트 연결
        if (recordButton != null)
            recordButton.onClick.AddListener(StartRecording);
        
        if (stopButton != null)
            stopButton.onClick.AddListener(StopRecording);
            
        if (playButton != null)
            playButton.onClick.AddListener(PlayRecording);
            
        if (saveButton != null)
            saveButton.onClick.AddListener(SaveRecordingToFile);
        
        // 사용 가능한 마이크 장치 확인
        CheckMicrophoneDevices();
        
        // 초기 버튼 상태 설정
        UpdateButtonStates();
        
        // 저장 경로 확인
        Debug.Log($"기본 저장 경로: {GetSavePath()}");
    }
    
    void CheckMicrophoneDevices()
    {
        Debug.Log("=== 마이크 장치 확인 ===");
        
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("마이크 장치를 찾을 수 없습니다!");
            return;
        }
        
        for (int i = 0; i < Microphone.devices.Length; i++)
        {
            Debug.Log($"마이크 장치 {i}: {Microphone.devices[i]}");
        }
        
        Debug.Log($"총 {Microphone.devices.Length}개의 마이크 장치가 발견되었습니다.");
    }
    
    public void StartRecording()
    {
        if (isRecording)
        {
            Debug.LogWarning("이미 녹음 중입니다!");
            return;
        }
        
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("마이크 장치가 없습니다!");
            return;
        }
        
        Debug.Log("🎙️ 녹음 시작");
        
        // 기본 마이크 장치 사용 (null = 기본 장치)
        recordedClip = Microphone.Start(null, false, recordingLength, sampleRate);
        isRecording = true;
        
        UpdateButtonStates();
        
        // 녹음 시작 확인
        if (recordedClip != null)
        {
            Debug.Log($"녹음 시작됨 - 길이: {recordingLength}초, 샘플레이트: {sampleRate}Hz");
        }
        else
        {
            Debug.LogError("녹음 시작 실패!");
            isRecording = false;
            UpdateButtonStates();
        }
    }
    
    public void StopRecording()
    {
        if (!isRecording)
        {
            Debug.LogWarning("녹음 중이 아닙니다!");
            return;
        }
        
        Debug.Log("⏹️ 녹음 정지");
        
        // 녹음 정지
        Microphone.End(null);
        isRecording = false;
        
        UpdateButtonStates();
        
        // 녹음된 클립을 AudioSource에 할당
        if (recordedClip != null && audioSource != null)
        {
            audioSource.clip = recordedClip;
            Debug.Log("녹음 완료! 재생하거나 저장할 수 있습니다.");
        }
    }
    
    public void PlayRecording()
    {
        if (audioSource != null && audioSource.clip != null)
        {
            Debug.Log("🔊 녹음 재생");
            audioSource.Play();
        }
        else
        {
            Debug.LogWarning("재생할 녹음이 없습니다!");
        }
    }
    
    public void SaveRecordingToFile()
    {
        if (recordedClip == null)
        {
            Debug.LogWarning("저장할 녹음이 없습니다!");
            return;
        }
        
        try
        {
            string savePath = GetSavePath();
            string fileName = GetUniqueFileName(savePath, saveFileName);
            string fullPath = Path.Combine(savePath, fileName + ".wav");
            
            // 디렉토리가 없으면 생성
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }
            
            // WAV 파일로 저장
            SaveWav(fullPath, recordedClip);
            
            lastSavedFilePath = fullPath;
            Debug.Log($"💾 녹음 파일 저장 완료: {fullPath}");
            
            // 윈도우에서 파일 탐색기로 파일 위치 열기 (에디터에서만)
            if (Application.isEditor && Application.platform == RuntimePlatform.WindowsEditor)
            {
                System.Diagnostics.Process.Start("explorer.exe", "/select," + fullPath.Replace("/", "\\"));
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"파일 저장 실패: {e.Message}");
        }
    }
    
    private string GetSavePath()
    {
        if (!string.IsNullOrEmpty(customSavePath))
        {
            return customSavePath;
        }
        
        if (saveToDesktop)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "UnityRecordings");
        }
        
        return Path.Combine(Application.persistentDataPath, "Recordings");
    }
    
    private string GetUniqueFileName(string path, string baseName)
    {
        string fileName = baseName;
        int counter = 1;
        
        while (File.Exists(Path.Combine(path, fileName + ".wav")))
        {
            fileName = $"{baseName}_{counter}";
            counter++;
        }
        
        return fileName;
    }
    
    private void SaveWav(string filePath, AudioClip clip)
    {
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);
        
        using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
        using (BinaryWriter writer = new BinaryWriter(fileStream))
        {
            // WAV 헤더 작성
            WriteWavHeader(writer, clip.frequency, clip.channels, samples.Length);
            
            // 오디오 데이터 작성
            foreach (float sample in samples)
            {
                short intSample = (short)(sample * short.MaxValue);
                writer.Write(intSample);
            }
        }
    }
    
    private void WriteWavHeader(BinaryWriter writer, int sampleRate, int channels, int sampleCount)
    {
        int byteRate = sampleRate * channels * 2; // 16비트 = 2바이트
        int blockAlign = channels * 2;
        int dataSize = sampleCount * 2;
        
        // RIFF 헤더
        writer.Write("RIFF".ToCharArray());
        writer.Write(36 + dataSize);
        writer.Write("WAVE".ToCharArray());
        
        // fmt 청크
        writer.Write("fmt ".ToCharArray());
        writer.Write(16); // PCM 포맷 크기
        writer.Write((short)1); // PCM 포맷
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write((short)16); // 비트 깊이
        
        // data 청크
        writer.Write("data".ToCharArray());
        writer.Write(dataSize);
    }
    
    public void ClearRecording()
    {
        if (isRecording)
        {
            StopRecording();
        }
        
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }
        
        recordedClip = null;
        lastSavedFilePath = "";
        Debug.Log("🗑️ 녹음 데이터 삭제");
        UpdateButtonStates();
    }
    
    void UpdateButtonStates()
    {
        if (recordButton != null)
            recordButton.interactable = !isRecording;
        
        if (stopButton != null)
            stopButton.interactable = isRecording;
            
        if (playButton != null)
            playButton.interactable = !isRecording && recordedClip != null;
            
        if (saveButton != null)
            saveButton.interactable = !isRecording && recordedClip != null;
    }
    
    // 녹음 상태 확인용 프로퍼티
    public bool IsRecording
    {
        get { return isRecording; }
    }
    
    // 현재 녹음 시간 확인 (초)
    public float GetRecordingTime()
    {
        if (isRecording && Microphone.IsRecording(null))
        {
            return (float)Microphone.GetPosition(null) / sampleRate;
        }
        return 0f;
    }
    
    // 마지막 저장된 파일 경로 반환
    public string GetLastSavedFilePath()
    {
        return lastSavedFilePath;
    }
    
    void Update()
    {
        // 녹음 시간 실시간 표시 (옵션)
        if (isRecording && Application.isEditor)
        {
            float currentTime = GetRecordingTime();
            if (currentTime > 0)
            {
                // 1초마다 로그 출력 (너무 많은 로그 방지)
                if (Mathf.FloorToInt(currentTime) != Mathf.FloorToInt(currentTime - Time.deltaTime))
                {
                    Debug.Log($"녹음 중... {currentTime:F1}초");
                }
            }
        }
    }
    
    void OnDestroy()
    {
        // 컴포넌트 파괴 시 녹음 정지
        if (isRecording)
        {
            Microphone.End(null);
        }
    }
}