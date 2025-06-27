using UnityEngine;

public abstract class SceneSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    
    public static T Instance
    {
        get
        {
            if (_instance == null)
                Debug.LogWarning($"SceneSingleton<{typeof(T).Name}> 초기화 안됨");
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
        }
        else
        {
            Debug.LogWarning($"복사본 SceneSingleton {typeof(T).Name} 파괴됨.");
            Destroy(gameObject);
        }
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this) 
            _instance = null;
    }
}