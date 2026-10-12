using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 꾸미기 카드(DecorationCard 프리팹)에 붙는다. 카드/장식 카드는 끌어서 꽃다발에 놓고, 세로로 끌면 목록(ScrollRect)이 스크롤된다.
/// 리본/풀 카드는 끌 일이 없어 모든 드래그를 목록 스크롤에 넘긴다(누르기만 하면 DecoratePanel이 앵커에 놓는다).
/// </summary>
public class DecorationCardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private DecorationData data;
    private DecorationPlacer placer;
    private ScrollRect scroll;
    private PlacedDecoration dragging; // 지금 끌고 있는 용품(목록 스크롤 중이면 null)
    private bool scrolling;

    public void Bind(DecorationData decoration, DecorationPlacer decorationPlacer)
    {
        data = decoration;
        placer = decorationPlacer;
        scroll = GetComponentInParent<ScrollRect>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        Vector2 moved = eventData.position - eventData.pressPosition;
        bool vertical = Mathf.Abs(moved.y) > Mathf.Abs(moved.x); // 세로로 끌면 스크롤, 옆(꽃다발 쪽)으로 끌면 용품을 꺼낸다
        scrolling = placer == null || !DecorationPlacer.IsFree(data.category) || vertical;

        if (scrolling) scroll?.OnBeginDrag(eventData);
        else dragging = placer.BeginFreeDrag(data, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (scrolling) scroll?.OnDrag(eventData);
        else if (dragging != null) dragging.MoveToScreen(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (scrolling) scroll?.OnEndDrag(eventData);
        else if (dragging != null) placer.Drop(dragging, eventData.position);
        dragging = null;
        scrolling = false;
    }
}
