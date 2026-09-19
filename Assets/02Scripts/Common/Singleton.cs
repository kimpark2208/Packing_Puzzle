using UnityEngine;

/// <summary>
/// 게임 전역에서 하나만 존재해야 하는 MonoBehaviour를 위한 공용 싱글톤 베이스.
/// 씬 전환 간 유지하려면 dontDestroyOnLoad를 true로 둔다(기본값).
/// </summary>
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T instance;
    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<T>();
            }
            return instance;
        }
    }

    [SerializeField] protected bool dontDestroyOnLoad = true;

    protected virtual void Awake()
    {
        if (instance != null && instance != this as T)
        {
            Destroy(gameObject);
            return;
        }

        instance = this as T;

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        OnAwake();
    }

    /// <summary>Awake 시점의 초기화 로직은 이 메서드를 override한다.</summary>
    protected virtual void OnAwake() { }

    protected virtual void OnDestroy()
    {
        if (instance == this as T)
        {
            instance = null;
        }
    }
}
