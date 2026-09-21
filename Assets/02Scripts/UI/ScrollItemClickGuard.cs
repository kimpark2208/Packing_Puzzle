using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// ScrollRect의 Content 안에 있는 버튼(아이템)에 붙인다.
/// 유니티 기본 드래그 판정(pixelDragThreshold)이 발동하기 전에 클릭이 먼저 처리되는 경우가 있어서,
/// 그보다 작은 임계값으로 직접 움직임을 감지해 Content의 raycast를 미리 꺼버린다.
/// 그러면 손을 뗄 때 이 버튼 위가 아니라 다른 곳(또는 아무데도 없음)에 레이캐스트가 맞아서
/// 클릭이 발동하지 않는다.
/// </summary>
public class ScrollItemClickGuard : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float moveThreshold = 6f;

    private CanvasGroup contentGroup;
    private Vector2 pressPosition;
    private bool pressed;

    private void Awake()
    {
        var scrollRect = GetComponentInParent<ScrollRect>();
        if (scrollRect == null || scrollRect.content == null) return;

        contentGroup = scrollRect.content.GetComponent<CanvasGroup>();
        if (contentGroup == null) contentGroup = scrollRect.content.gameObject.AddComponent<CanvasGroup>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        pressPosition = eventData.position;
        if (contentGroup != null) contentGroup.blocksRaycasts = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
        if (contentGroup != null) contentGroup.blocksRaycasts = true;
    }

    private void Update()
    {
        if (!pressed || contentGroup == null || !contentGroup.blocksRaycasts) return;

        Vector2 current = Pointer.current != null ? Pointer.current.position.ReadValue() : pressPosition;
        if (Vector2.Distance(current, pressPosition) >= moveThreshold)
        {
            contentGroup.blocksRaycasts = false;
        }
    }
}
