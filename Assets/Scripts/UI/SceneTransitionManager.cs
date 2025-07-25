using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : MonoBehaviour
{
    [Header("Scene Management")]
    public string concertSceneName = "MicGrabFunc";
    
    [Header("UI References")]
    public GameObject startButton;
    //public GameObject loadingUI;
    
    //[Header("Audio")]
    //public AudioSource buttonClickSound;
    
    private bool isTransitioning = false;

    void Start()
    {
        SetupMRScene();
    }

    void SetupMRScene()
    {
        // MR 씬 초기 설정
        //if (loadingUI != null)
            //loadingUI.SetActive(false);
            
        if (startButton != null)
        {
            // 버튼 클릭 이벤트 설정
            var button = startButton.GetComponent<UnityEngine.UI.Button>();
            if (button != null)
            {
                button.onClick.AddListener(StartConcertTransition);
            }
        }
        
        Debug.Log("MR Start Scene initialized");
    }

    public void StartConcertTransition()
    {
        if (isTransitioning) return;
        
        StartCoroutine(TransitionToConcertScene());
    }

    private System.Collections.IEnumerator TransitionToConcertScene()
    {
        isTransitioning = true;
        
        // 1. 사운드 재생
        //if (buttonClickSound != null)
            //buttonClickSound.Play();
        
        // 2. 로딩 UI 표시
        //if (loadingUI != null)
            //loadingUI.SetActive(true);
            
        if (startButton != null)
            startButton.SetActive(false);
        
        Debug.Log("Starting transition to VR Concert Scene...");
        
        // 3. 잠시 대기 (사용자에게 전환을 알림)
        yield return new WaitForSeconds(1.0f);
        
        // 4. 콘서트 씬 로드 (Fully Immersive VR)
        // 이 시점에서 visionOS가 안전 대화상자를 표시할 수 있음
        SceneManager.LoadScene(concertSceneName);
    }
    
    // 디버그용 - Inspector에서 호출 가능
    [ContextMenu("Test Transition")]
    public void TestTransition()
    {
        StartConcertTransition();
    }
}