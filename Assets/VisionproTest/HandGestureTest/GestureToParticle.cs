using UnityEngine;
using UnityEngine.InputSystem;

public class GestureToParticle : MonoBehaviour
{
    [Header("Particle System")]
    public ParticleSystem testParticles;
    
    [Header("Settings")]
    public float cooldownTime = 1f;
    
    private float lastTriggerTime;
    
    // Input Actions
    private InputAction selectAction;
    private InputAction tapAction;
    
    void Start()
    {
        // PolySpatial Input Actions에서 Select 액션 찾기
        var inputActions = FindObjectOfType<UnityEngine.InputSystem.PlayerInput>();
        if (inputActions != null)
        {
            selectAction = inputActions.actions["Select"];
            tapAction = inputActions.actions["Tap"];
        }
        
        // Input Action 이벤트 등록
        if (selectAction != null)
        {
            selectAction.performed += OnSelectPerformed;
            selectAction.Enable();
        }
        
        if (tapAction != null)
        {
            tapAction.performed += OnTapPerformed;
            tapAction.Enable();
        }
    }
    
    void OnDestroy()
    {
        // Input Action 이벤트 해제
        if (selectAction != null)
        {
            selectAction.performed -= OnSelectPerformed;
            selectAction.Disable();
        }
        
        if (tapAction != null)
        {
            tapAction.performed -= OnTapPerformed;
            tapAction.Disable();
        }
    }
    
    void Update()
    {
        // 키보드 테스트용 (에디터에서)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TriggerParticle();
        }
        
        // 마우스 테스트용 (에디터에서)
        if (Input.GetMouseButtonDown(0))
        {
            TriggerParticle();
        }
    }
    
    void OnSelectPerformed(InputAction.CallbackContext context)
    {
        // Vision Pro Select 제스처 (핀치)
        if (CanTrigger())
        {
            TriggerParticle();
        }
    }
    
    void OnTapPerformed(InputAction.CallbackContext context)
    {
        // Vision Pro Tap 제스처
        if (CanTrigger())
        {
            TriggerParticle();
        }
    }
    
    bool CanTrigger()
    {
        return Time.time - lastTriggerTime >= cooldownTime;
    }
    
    void TriggerParticle()
    {
        if (testParticles != null)
        {
            testParticles.Play();
            lastTriggerTime = Time.time;
            Debug.Log("Particle Triggered by Vision Pro Gesture!");
        }
        else
        {
            Debug.LogWarning("Test Particles not assigned!");
        }
    }
}