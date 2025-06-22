using UnityEngine;

public class GestureToParticle : MonoBehaviour
{
    [Header("Particle System")]
    public ParticleSystem testParticles;
    
    [Header("Settings")]
    public float cooldownTime = 1f;
    
    private float lastTriggerTime;
    
    void Update()
    {
        if (Time.time - lastTriggerTime < cooldownTime)
            return;
        
        // Vision Pro 터치 감지 (PolySpatial 호환)
        if (Input.GetButtonDown("Submit") || 
            Input.GetMouseButtonDown(0) ||
            Input.GetKeyDown(KeyCode.Space))
        {
            TriggerParticle();
        }
    }
    
    void TriggerParticle()
    {
        if (testParticles != null)
        {
            testParticles.Play();
            lastTriggerTime = Time.time;
            Debug.Log("Particle Triggered by Vision Pro Touch!");
        }
        else
        {
            Debug.LogWarning("Test Particles not assigned!");
        }
    }
}