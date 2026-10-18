using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 그림(content)을 핀치(두 손가락 벌리기/좁히기)로 확대·축소하고, 한 손가락으로 끌어서 옮긴다(핀치하는 동안은 두 손가락의 가운데가 움직이는 만큼 옮겨진다). 모바일용이다.
/// content는 부모(보이는 창, 예: 마스크가 달린 ExpandIMG) 가운데 기준으로 놓여 있고(Pivot 0.5, 0.5) 확대·옮김의 범위도 그 부모 크기로 잰다.
/// 이 오브젝트(content의 조상)에는 눌림을 받을 Image(Raycast Target)가 있어야 한다. 에디터에서는 터치스크린이 없으니 마우스 휠로 확대·축소를 시험할 수 있다.
/// </summary>
public class ZoomPanView : MonoBehaviour, IScrollHandler, IDragHandler
{
    [SerializeField] private RectTransform content;
    [SerializeField, Min(1f)] private float maxZoom = 4f;
    [SerializeField, Min(0.01f), Tooltip("(에디터 시험용) 마우스 휠 한 칸당 확대·축소 비율.")] private float wheelStep = 0.15f;

    private RectTransform area; // 그림이 보이는 창(content의 부모)
    private Camera eventCamera; // 오버레이 캔버스면 null
    private float zoom = 1f;

    private void Awake()
    {
        area = (RectTransform)content.parent;
        Canvas canvas = GetComponentInParent<Canvas>();
        eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    /// <summary>처음 모습(확대 없이 가운데)으로 되돌린다.</summary>
    public void ResetView()
    {
        zoom = 1f;
        content.localScale = Vector3.one;
        content.anchoredPosition = Vector2.zero;
    }

    public void OnScroll(PointerEventData eventData)
    {
#if UNITY_EDITOR
        ZoomAt(zoom * (1f + eventData.scrollDelta.y * wheelStep), eventData.position);
#endif
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (TouchCount() >= 2) return; // 두 손가락일 때는 Update의 핀치가 확대·옮김을 맡는다

        Pan(eventData.position - eventData.delta, eventData.position, eventData.pressEventCamera);
    }

    private void Update()
    {
        if (!TryGetPinch(out Vector2 a, out Vector2 aDelta, out Vector2 b, out Vector2 bDelta)) return;

        Vector2 middle = (a + b) * 0.5f;
        if (!RectTransformUtility.RectangleContainsScreenPoint(area, middle, eventCamera)) return;

        float before = ((a - aDelta) - (b - bDelta)).magnitude;
        if (before > 1f) ZoomAt(zoom * (a - b).magnitude / before, middle);
        Pan(((a - aDelta) + (b - bDelta)) * 0.5f, middle, eventCamera);
    }

    private static int TouchCount()
    {
        Touchscreen screen = Touchscreen.current;
        if (screen == null) return 0;

        int count = 0;
        foreach (var touch in screen.touches) if (touch.isInProgress) count++;
        return count;
    }

    /// <summary>눌려 있는 손가락이 정확히 둘이면 그 위치와 이번 프레임에 움직인 만큼을 돌려준다.</summary>
    private static bool TryGetPinch(out Vector2 a, out Vector2 aDelta, out Vector2 b, out Vector2 bDelta)
    {
        a = aDelta = b = bDelta = default;
        Touchscreen screen = Touchscreen.current;
        if (screen == null) return false;

        int found = 0;
        foreach (var touch in screen.touches)
        {
            if (!touch.isInProgress) continue;

            if (found == 0) { a = touch.position.ReadValue(); aDelta = touch.delta.ReadValue(); }
            else if (found == 1) { b = touch.position.ReadValue(); bDelta = touch.delta.ReadValue(); }
            found++;
        }
        return found == 2;
    }

    /// <summary>화면의 from 자리에서 to 자리로 손가락이 움직인 만큼 그림을 옮긴다.</summary>
    private void Pan(Vector2 from, Vector2 to, Camera camera)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(area, from, camera, out Vector2 before);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(area, to, camera, out Vector2 after);
        content.anchoredPosition = Clamp(content.anchoredPosition + after - before);
    }

    /// <summary>screenPoint 아래의 그림 자리가 그대로 있도록 배율을 바꾼다.</summary>
    private void ZoomAt(float newZoom, Vector2 screenPoint)
    {
        newZoom = Mathf.Clamp(newZoom, 1f, maxZoom);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screenPoint, eventCamera, out Vector2 point);
        float ratio = newZoom / zoom;
        zoom = newZoom;
        content.localScale = Vector3.one * zoom;
        content.anchoredPosition = Clamp(point - (point - content.anchoredPosition) * ratio);
    }

    /// <summary>확대한 그림이 영역 밖으로 빈 곳을 드러내지 않게 옮김을 제한한다.</summary>
    private Vector2 Clamp(Vector2 position)
    {
        Vector2 spare = (content.rect.size * zoom - area.rect.size) * 0.5f;
        spare = Vector2.Max(spare, Vector2.zero);
        return new Vector2(Mathf.Clamp(position.x, -spare.x, spare.x), Mathf.Clamp(position.y, -spare.y, spare.y));
    }
}
