using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 꽃다발 위에 올려 둔 꾸미기 용품 하나. 앵커에 놓인 것(리본/풀)은 가만히 있고, 드래그로 놓는 것(카드/장식)은 다시 끌어서 옮길 수 있다.
/// 꽃다발 밖에서 놓으면 사라진다(DecorationPlacer.Drop). 그림은 원본 비율을 지킨 채 상자(maxSize) 안에 맞춘다.
/// 놓이는 기준점은 스프라이트의 피벗이다(리본은 매듭에 피벗을 두면 앵커에 매듭이 온다). 피벗은 스프라이트 임포트에서 정한다.
/// </summary>
[RequireComponent(typeof(Image))]
public class PlacedDecoration : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private DecorationPlacer owner;
    private RectTransform rect;
    private bool draggable;
    private Vector2 grabOffset; // 잡은 자리와 기준점의 차이(끌어도 잡은 곳이 튀지 않게)

    public DecorationData Data { get; private set; }

    public void Init(DecorationData data, DecorationPlacer placer, Vector2 maxSize, bool canDrag)
    {
        Data = data;
        owner = placer;
        draggable = canDrag;
        rect = (RectTransform)transform;

        var image = GetComponent<Image>();
        image.sprite = data.sprite;
        image.preserveAspect = true;
        image.raycastTarget = canDrag; // 앵커에 놓인 것은 클릭을 가로채지 않는다
        rect.sizeDelta = FitSize(data.sprite, maxSize);
        rect.pivot = PivotOf(data.sprite);
    }

    /// <summary>스프라이트의 가로세로 비율을 지킨 채 상자 안에 꽉 차는 크기.</summary>
    public static Vector2 FitSize(Sprite sprite, Vector2 box)
    {
        if (sprite == null || sprite.rect.width <= 0f || sprite.rect.height <= 0f) return box;
        float scale = Mathf.Min(box.x / sprite.rect.width, box.y / sprite.rect.height);
        return sprite.rect.size * scale;
    }

    /// <summary>스프라이트에 정해 둔 피벗(그림 안의 기준점)을 0~1 비율로.</summary>
    public static Vector2 PivotOf(Sprite sprite)
    {
        if (sprite == null || sprite.rect.width <= 0f || sprite.rect.height <= 0f) return new Vector2(0.5f, 0.5f);
        return new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
    }

    /// <summary>화면 좌표(포인터)로 옮긴다.</summary>
    public void MoveToScreen(Vector2 screenPosition)
    {
        rect.anchoredPosition = owner.ScreenToLayer(screenPosition) + grabOffset;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!draggable) return;
        transform.SetAsLastSibling(); // 잡은 것이 제일 위로
        grabOffset = rect.anchoredPosition - owner.ScreenToLayer(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (draggable) MoveToScreen(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (draggable) owner.Drop(this, eventData.position);
    }
}
