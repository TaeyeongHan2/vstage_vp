using UnityEngine;

namespace MicRecordFunction
{
    public class MicComponent : MonoBehaviour
    {
        [Header("녹음 관련")]
        public AudioSource audioSource;

        [Tooltip("녹음 가능한 최대 시간 (초)")]
        public int maxRecordingDuration = 60;
        private const int SampleRate = 44100;

        private string _micDevice;
        private bool _isRecording = false;
        private AudioClip _audioClip;
        
        [Header("손 추적 관련")]
        public Transform followTarget;
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;
        private bool _isFollowing = false;
        
        [Header("왼손바닥 위에 있던 위치")]
        public Transform leftHandTarget;

        private void Start()
        {
            _originalPosition = transform.position;
            _originalRotation = transform.rotation;

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

                followTarget = other.transform;
                _isFollowing = true;

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
            StopRecording();
            _isFollowing = false;

            // 왼손의 palm 아래로 다시 이동
            transform.SetParent(leftHandTarget);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            
            // transform.localPosition = _originalPosition;
            // transform.localRotation = _originalRotation;

            Debug.Log("[MicComponent] 주먹 제스처 풀림 -> 녹음 종료 + 왼손으로 복귀");
        }
        
        //녹음 켜지는 기능
        private void StartRecording()
        {
            if (_isRecording || _micDevice == null) return;

            Debug.Log("[MicComponent] 🎙 녹음 시작");

            audioSource.clip = Microphone.Start(_micDevice, false, maxRecordingDuration, 44100);
            audioSource.loop = false;

            // // 재생 시작은 입력 완료 후
            // while (!(Microphone.GetPosition(micDevice) > 0)) { } 
            // audioSource.Play();

            _isRecording = true;
        }

        //녹음 중지하는 기능
        private void StopRecording()
        {
            if (!_isRecording) return;
            Debug.Log("[MicComponent] ⏹ 녹음 종료");

            int position = Microphone.GetPosition(_micDevice);
            Microphone.End(_micDevice);
            audioSource.Stop();
            _isRecording = false;
    
            Debug.Log($"[MicComponent] 녹음 종료, 샘플 길이: {position}");

            // 실제 녹음된 길이만큼 클립을 잘라서 새 AudioClip 생성
            float[] samples = new float[position * _audioClip.channels];
            _audioClip.GetData(samples, 0);

            var trimmed = AudioClip.Create(
                "TrimmedRecording",
                position,
                _audioClip.channels,
                SampleRate,
                false);

            trimmed.SetData(samples, 0);

            // AudioSource에 할당
            audioSource.clip = trimmed;
            Debug.Log("[MicComponent] 잘라낸 클립 할당 완료");
    
            // // **즉시 재생** 추가!
            // Debug.Log("[MicComponent] 자동 재생 시작");
            // audioSource.Play();
        }
    }
}