using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 튜토리얼을 코드에서 켠다. 씬이나 프리팹에서 TutorialOverlay·TutorialPopup 오브젝트가 꺼진 채 저장돼도(에디터에서 작업하느라 꺼 두는 일이 잦다)
/// 씬이 열릴 때마다 이 둘을 찾아 켠다. 튜토리얼을 끄려면 Enabled를 false로 바꾼다.
/// </summary>
public static class TutorialBootstrap
{
    /// <summary>false면 튜토리얼 오브젝트를 끈다(씬/프리팹에 켜진 채 저장돼 있어도). 다시 보이려면 true로 바꾼다.</summary>
    public const bool Enabled = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Start()
    {
        SceneManager.sceneLoaded += (scene, mode) => EnableAll();
        EnableAll(); // 첫 씬은 sceneLoaded가 구독되기 전에 열렸다
    }

    private static void EnableAll()
    {
        foreach (TutorialOverlay overlay in Object.FindObjectsByType<TutorialOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (overlay.gameObject.activeSelf != Enabled) overlay.gameObject.SetActive(Enabled);
        }
        foreach (TutorialPopup popup in Object.FindObjectsByType<TutorialPopup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (popup.gameObject.activeSelf != Enabled) popup.gameObject.SetActive(Enabled);
        }
    }
}
