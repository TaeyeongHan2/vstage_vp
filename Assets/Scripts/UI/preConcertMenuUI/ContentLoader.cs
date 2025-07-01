using UnityEngine;

public class ContentLoader : MonoBehaviour
{
    [Header("Content Parent")]
    public Transform mainContentParent;
    
    [Header("View Prefabs")]
    public GameObject homeViewPrefab;
    public GameObject userGuideViewPrefab;
    public GameObject concertDetailViewPrefab;

    private void ClearOld()
    {
        foreach (Transform t in mainContentParent)
        {
            Destroy((t.gameObject));
        }
    }

    private void LoadContent(GameObject prefab)
    {
        ClearOld();
        Instantiate(prefab, mainContentParent,false);
    }
    
    public void ShowHomeView() => LoadContent(homeViewPrefab);
    public void ShowUserGuideView() => LoadContent(userGuideViewPrefab);
    public void ShowConcertDetailView() => LoadContent(concertDetailViewPrefab);
    
    
}