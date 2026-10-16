using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 꽃다발 위에 올려 둔 꾸미기 용품 하나. 놓인 뒤에는 꽃다발 위에서 끌어서 옮길 수 있고, 꽃다발 밖에서 놓으면 사라진다(DecorationPlacer.Drop).
/// 그림은 원본 비율을 지킨 채 상자(maxSize) 안에 맞추고 용품의 sizeScale을 곱한다. 놓이는 기준점은 스프라이트의 피벗이다(리본은 매듭에 피벗을 두면 앵커에 매듭이 온다).
/// 부모(앵커 영역)가 기울어 있어도 맞도록 월드 좌표로 움직인다.
/// </summary>
[RequireComponent(typeof(Image))]
public class PlacedDecoration : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private DecorationPlacer owner;
    private RectTransform rect;
    private Vector3 grabOffset; // 잡은 자리와 기준점의 차이(끌어도 잡은 곳이 튀지 않게)

    public DecorationData Data { get; private set; }

    public void Init(DecorationData data, DecorationPlacer placer, Vector2 maxSize)
    {
        Data = data;
        owner = placer;
        rect = (RectTransform)transform;

        var image = GetComponent<Image>();
        image.sprite = data.sprite;
        image.preserveAspect = true;
        image.raycastTarget = true; // 잡아서 끌 수 있다
        rect.sizeDelta = FitSize(data.sprite, maxSize) * data.sizeScale;
        rect.pivot = PivotOf(data.sprite);
    }

    /// <summary>스프라이트의 가로세로 비율을 지킨 채 상자 안에 꽉 차는 크기.</summary>
    private static Vector2 FitSize(Sprite sprite, Vector2 box)
    {
        if (sprite == null || sprite.rect.width <= 0f || sprite.rect.height <= 0f) return box;
        float scale = Mathf.Min(box.x / sprite.rect.width, box.y / sprite.rect.height);
        return sprite.rect.size * scale;
    }

    /// <summary>스프라이트에 정해 둔 피벗(그림 안의 기준점)을 0~1 비율로.</summary>
    private static Vector2 PivotOf(Sprite sprite)
    {
        if (sprite == null || sprite.rect.width <= 0f || sprite.rect.height <= 0f) return new Vector2(0.5f, 0.5f);
        return new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
    }

    /// <summary>화면 좌표(포인터)가 가리키는, 부모 평면 위의 월드 좌표.</summary>
    private Vector3 PointerWorld(Vector2 screenPosition)
    {
        RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)rect.parent, screenPosition, owner.EventCamera, out Vector3 world);
        return world;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        transform.SetAsLastSibling(); // 잡은 것이 제일 위로
        grabOffset = rect.position - PointerWorld(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        rect.position = PointerWorld(eventData.position) + grabOffset;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        owner.Drop(this, eventData.position);
    }
}
