using UnityEngine;
using System;
using System.IO;
using System.Text;

namespace MicRecordFunction
{
    public class MicComponent : MonoBehaviour
    {
        [Header("녹음 파일 -> AI 서버 전달")]
        public WebSocketVoiceClient voiceClient;
        public int sampleRate = 16000;
        private int startSample = 0;
        
        [Header("녹음 관련")]
        public AudioSource audioSource;

        [Tooltip("녹음 가능한 최대 시간 (초)")]
        public int maxRecordingDuration = 60;

        private string _micDevice;
        private bool _isRecording = false;
        private AudioClip _audioClip;
        
        [Header("손 추적 관련")]
        public Transform followTarget;
        // private Vector3 _originalPosition;
        // private Quaternion _originalRotation;
        private bool _isFollowing = false;
        
        [Header("왼손바닥 위에 있던 위치")]
        public Transform leftHandTarget;
        
        [Header("충돌 시 비활성화할 오브젝트")]
        public GameObject disableRightHandMesh;
        public GameObject disableLeftHandGesFunc;
        

        private void Start()
        {
            // _originalPosition = transform.position;
            // _originalRotation = transform.rotation;

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            // 기본 마이크 장치 선택
            if (Microphone.devices.Length > 0)
            {
                _micDevice = Microphone.devices[0];
                Debug.Log("[MicComponent] 사용 가능한 마이크: " + _micDevice);
            }
            else
            {
                Debug.LogError("[MicComponent] 마이크를 찾을 수 없음!");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            //충돌 이벤트 발생했을 때 손바닥 위치를 따라가면서 녹음이 시작되는 부분
            if (other.CompareTag("Palm"))
            {
                Debug.Log("[MicComponent] 손과 충돌!");
                //오른손 mesh 비활성화
                if (disableRightHandMesh != null)
                {
                    disableRightHandMesh.SetActive(false);
                }

                if (disableLeftHandGesFunc != null)
                {
                    disableLeftHandGesFunc.SetActive(false);
                }
                
                _isFollowing = true;
                transform.position = followTarget.position;
                transform.rotation = followTarget.rotation;

                StartRecording();
                
            }
        }

        //오른손의 손바닥 위치를 계속 따라다님
        private void Update()
        {
            if (_isFollowing && followTarget != null)
            {
                transform.position = followTarget.position;
                transform.rotation = followTarget.rotation;
            }
        }

        //제스처 감지 추가해서 녹음 기능 꺼지고 원래 왼손 바닥의 위치로 돌아가는 부분 추가
        public void OnGrabGestureReleased()
        {
            StopAndSend();
            _isFollowing = false;

            // 왼손의 palm 아래로 다시 이동
            transform.SetParent(leftHandTarget);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            
            // transform.localPosition = _originalPosition;
            // transform.localRotation = _originalRotation;

            Debug.Log("[MicComponent] 주먹 제스처 풀림 -> 녹음 종료 + 왼손으로 복귀");
        }
        
        //녹음 켜지는 기능 (MicRecorder에서 가져온 로직 적용)
        private void StartRecording()
        {
            if (_isRecording || _micDevice == null) return;

            Debug.Log($"[{Time.time:F2}] 🎙 녹음 시작");

            _audioClip = Microphone.Start(_micDevice, true, maxRecordingDuration, sampleRate);
            startSample = Microphone.GetPosition(_micDevice);
            _isRecording = true;
        }

        //녹음 중지하고 웹소켓으로 전송하는 기능 (MicRecorder에서 가져온 로직 적용)
        private void StopAndSend()
        {
            if (!_isRecording) return;
            
            Debug.Log($"[{Time.time:F2}] ⏹ 녹음 종료");

            int endSample = Microphone.GetPosition(_micDevice);
            Microphone.End(_micDevice);
            _isRecording = false;

            float[] fullData = new float[_audioClip.samples * _audioClip.channels];
            _audioClip.GetData(fullData, 0);

            int length = endSample - startSample;
            if (length <= 0 || length > fullData.Length)
            {
                length = fullData.Length;
            }

            float[] segment = new float[length];
            Array.Copy(fullData, startSample, segment, 0, length);

            AudioClip segmentClip = AudioClip.Create("Segment", segment.Length, _audioClip.channels, sampleRate, false);
            segmentClip.SetData(segment, 0);

            byte[] wavBytes = ConvertClipToWav(segmentClip);

            if (voiceClient != null)
            {
                voiceClient.TrySendWav(wavBytes);
                Debug.Log("[MicComponent] WAV 데이터 전송 완료");
            }
            else
            {
                Debug.LogError("[MicComponent] WebSocketVoiceClient가 연결되지 않았습니다.");
            }

            Debug.Log($"[MicComponent] 녹음 종료, 샘플 길이: {length}");
        }

        // WAV 포맷으로 변환하는 메소드 (MicRecorder에서 가져옴)
        private byte[] ConvertClipToWav(AudioClip clip)
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
}