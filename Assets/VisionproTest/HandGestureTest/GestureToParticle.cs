using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class GestureToParticle : MonoBehaviour
{
    [Header("Particle System")]
    public ParticleSystem testParticles;
    
    [Header("Settings")]
    public float cooldownTime = 1f;
    
    [Header("XR Interaction")]
    public XRDirectInteractor leftHandInteractor;
    public XRDirectInteractor rightHandInteractor;
    
    private float lastTriggerTime;
    private bool wasSelectingLastFrame = false;
    
    void Start()
    {
        // 자동으로 Hand Interactor 찾기
        if (leftHandInteractor == null || rightHandInteractor == null)
        {
            FindHandInteractors();
        }
    }
    
    void FindHandInteractors()
    {
        XRDirectInteractor[] interactors = FindObjectsOfType<XRDirectInteractor>();
        
        foreach (var interactor in interactors)
        {
            if (interactor.name.ToLower().Contains("left"))
                leftHandInteractor = interactor;
            else if (interactor.name.ToLower().Contains("right"))
                rightHandInteractor = interactor;
        }
    }
    
    void Update()
    {
        // 쿨다운 체크
        if (Time.time - lastTriggerTime < cooldownTime)
            return;
        
        // Vision Pro 핀치 제스처 감지
        DetectPinchGesture();
        
        // 키보드 테스트용 (에디터에서)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TriggerParticle();
        }
    }
    
    void DetectPinchGesture()
    {
        bool isCurrentlySelecting = false;
        
        // 왼손 또는 오른손이 핀치(select) 중인지 확인
        if (leftHandInteractor != null && leftHandInteractor.isSelectActive)
            isCurrentlySelecting = true;
        
        if (rightHandInteractor != null && rightHandInteractor.isSelectActive)
            isCurrentlySelecting = true;
        
        // 핀치 시작 순간 감지 (이전 프레임에는 안하고 있었는데 지금 하고 있음)
        if (isCurrentlySelecting && !wasSelectingLastFrame)
        {
            TriggerParticle();
        }
        
        wasSelectingLastFrame = isCurrentlySelecting;
    }
    
    void TriggerParticle()
    {
        if (testParticles != null)
        {
            testParticles.Play();
            lastTriggerTime = Time.time;
            Debug.Log("Particle Triggered by Vision Pro Pinch!");
        }
        else
        {
            Debug.LogWarning("Test Particles not assigned!");
        }
    }
}

