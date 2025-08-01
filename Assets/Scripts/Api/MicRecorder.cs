using System;
using System.IO;
using System.Text;
using UnityEngine;

public class MicRecorder : MonoBehaviour
{
    public WebSocketVoiceClient voiceClient;
    public int sampleRate = 16000;

    private AudioClip recordedClip;
    private bool isRecording = false;
    private int startSample = 0;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartRecording();
        }

        if (Input.GetKeyUp(KeyCode.Space))
        {
            StopAndSend();
        }
    }

    void StartRecording()
    {
        if (isRecording) return;

        recordedClip = Microphone.Start(null, true, 300, sampleRate);
        startSample = Microphone.GetPosition(null);
        isRecording = true;

        Debug.Log($"[{Time.time:F2}] 녹음 시작");
    }

    void StopAndSend()
    {
        if (!isRecording) return;

        int endSample = Microphone.GetPosition(null);
        Microphone.End(null);
        isRecording = false;

        Debug.Log($"[{Time.time:F2}] 녹음 종료");

        float[] fullData = new float[recordedClip.samples * recordedClip.channels];
        recordedClip.GetData(fullData, 0);

        int length = endSample - startSample;
        if (length <= 0 || length > fullData.Length)
        {
            length = fullData.Length;
        }

        float[] segment = new float[length];
        Array.Copy(fullData, startSample, segment, 0, length);

        AudioClip segmentClip = AudioClip.Create("Segment", segment.Length, recordedClip.channels, sampleRate, false);
        segmentClip.SetData(segment, 0);

        byte[] wavBytes = ConvertClipToWav(segmentClip);

        if (voiceClient != null)
        {
            voiceClient.TrySendWav(wavBytes);
        }
        else
        {
            Debug.LogError("WebSocketVoiceClient가 연결되지 않았습니다.");
        }
    }

    byte[] ConvertClipToWav(AudioClip clip)
    {
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        short[] intData = new short[samples.Length];
        byte[] bytesData = new byte[samples.Length * 2];

        for (int i = 0; i < samples.Length; i++)
        {
            intData[i] = (short)(samples[i] * 32767);
            BitConverter.GetBytes(intData[i]).CopyTo(bytesData, i * 2);
        }

        using (MemoryStream stream = new MemoryStream())
        {
            int hz = clip.frequency;
            int channels = clip.channels;

            stream.Write(Encoding.ASCII.GetBytes("RIFF"), 0, 4);
            stream.Write(BitConverter.GetBytes(36 + bytesData.Length), 0, 4);
            stream.Write(Encoding.ASCII.GetBytes("WAVE"), 0, 4);
            stream.Write(Encoding.ASCII.GetBytes("fmt "), 0, 4);
            stream.Write(BitConverter.GetBytes(16), 0, 4);
            stream.Write(BitConverter.GetBytes((short)1), 0, 2);
            stream.Write(BitConverter.GetBytes((short)channels), 0, 2);
            stream.Write(BitConverter.GetBytes(hz), 0, 4);
            stream.Write(BitConverter.GetBytes(hz * channels * 2), 0, 4);
            stream.Write(BitConverter.GetBytes((short)(channels * 2)), 0, 2);
            stream.Write(BitConverter.GetBytes((short)16), 0, 2);
            stream.Write(Encoding.ASCII.GetBytes("data"), 0, 4);
            stream.Write(BitConverter.GetBytes(bytesData.Length), 0, 4);
            stream.Write(bytesData, 0, bytesData.Length);

            return stream.ToArray();
        }
    }
}
