using System;
using UnityEngine;
using UnityEngine.VFX;
using System.Collections;

public class VFXPositionSetter : MonoBehaviour
{
    public VisualEffect vfx;       // Visual Effect 컴포넌트
    public Transform target;       // 큐브 오브젝트 (타겟)
    
    // 이전에 전달한 위치를 저장
    private Vector3 lastPosition = Vector3.zero;
    private bool hasPlayed = false;
    
    [Header("이펙트 한번 재생된 후 이펙트 끔")] 
    public GameObject recordEndEffect;

    // private void OnEnable()
    // {
    //     vfx.Play();
    // }

    void Update()
    {
        if (vfx != null && target != null)
        {
            Vector3 currentPos = target.position;
            
            // 위치가 변경되었을 때만 업데이트
            if (currentPos != lastPosition)
            {
                // 큐브의 월드 위치를 VFX에 전달
                vfx.SetVector3("TargetPosition", currentPos);
                
                // VFX를 재생/재시작 (필요한 경우)
                //vfx.Play();

                //StartCoroutine(DisableWhenFinished());
                
                // 현재 위치 저장
                lastPosition = currentPos;
            }
            
            // 한 번도 재생된 적 없으면 Play() + recordEndEffect 끄기
            if (!hasPlayed)
            {
                vfx.Play();
                hasPlayed = true;

                if (recordEndEffect != null)
                    recordEndEffect.SetActive(false);
            }
        }
    }

    // private IEnumerator DisableWhenFinished()
    // {
    //     yield return null;
    //     while(vfx.aliveParticleCount > 0)
    //     {
    //         yield return null;
    //     }
    //     
    //     vfx.gameObject.SetActive(false);
    // }
}