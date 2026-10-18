using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 버튼을 누르는 순간(손을 떼기 전에) 소리를 낸다. 버튼의 클릭은 손을 뗄 때 일어나서 터치 소리(누르는 순간)보다 늦으므로, 소리를 클릭 대신 이쪽에 둔다.
/// 소리는 목록 중 하나를 무작위로 낸다. watch를 정하면 그 오브젝트가 켜져 있을 때의 누름(예: 열린 팝업을 닫는 누름)에는 whenWatchedActive를 쓴다.
/// 버튼이 눌릴 수 없는 상태(interactable이 꺼짐)면 소리를 내지 않는다.
/// </summary>
public class PressSound : MonoBehaviour, IPointerDownHandler
{
    [SerializeField, Tooltip("누를 때 나는 소리. 이 중 하나가 그때그때 무작위로 난다.")] private AudioClip[] clips;
    [SerializeField, Tooltip("(선택) 이 오브젝트가 켜져 있을 때 누르면 clips 대신 whenWatchedActive를 쓴다. 예: 전화기 = 카탈로그가 꺼져 있을 때 열기 소리, 켜져 있을 때 닫기 소리.")] private GameObject watch;
    [SerializeField] private AudioClip[] whenWatchedActive;
    [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (TryGetComponent(out Selectable selectable) && !selectable.IsInteractable()) return;

        Play();
    }

    /// <summary>눌렀을 때의 소리를 코드에서 직접 낸다(버튼이 눌릴 수 없는 상태여도 낸다. 예: 전등이 저절로 켜질 때의 딸깍 소리).</summary>
    public void Play()
    {
        bool watching = watch != null && watch.activeInHierarchy;
        Sfx.PlayRandom(watching ? whenWatchedActive : clips, volume);
    }
}
