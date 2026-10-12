using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 꾸미기 용품을 꽃다발에 올려 두는 곳.
/// - 리본/풀: 에디터에 놓아 둔 앵커의 자식으로 한 종류당 하나만 놓인다(다른 것을 고르면 바뀌고, 놓인 것을 다시 고르면 빠진다).
///   앵커가 속한 영역(RibbonArea/LeafArea)의 순서가 곧 겹침 순서다: 풀은 포장지와 꽃 사이, 리본은 꽃 위. 꽃다발이 기울면 같이 기운다.
/// - 카드/장식: 카드에서 끌어다 꽃다발 영역 안에 놓는다. 이 오브젝트의 RectTransform이 "꽃다발 제일 위 레이어"라서 PuzzleRoot 맨 끝에 둔다.
///   여러 개 놓을 수 있고, 놓은 뒤에도 끌어서 옮긴다. 꽃다발 밖에서 놓으면 사라진다.
/// - 꽃다발 모양(포장지): 고른 포장지 그림으로 꽃다발 이미지를 바꾼다.
/// 그림은 모두 원본 비율을 지킨 채 항목별 상자(maxSizeByCategory) 안에 맞추고, 놓이는 기준점은 스프라이트의 피벗이다(PlacedDecoration).
/// </summary>
public class DecorationPlacer : MonoBehaviour
{
    [SerializeField] private PlacedDecoration itemPrefab;
    [SerializeField] private RectTransform bouquetArea; // 포장지 이미지(Image). 이 영역 안에 놓아야 남는다
    [SerializeField] private RectTransform ribbonAnchor;
    [SerializeField] private RectTransform leafAnchor;

    [Header("항목별로 그림이 들어가는 최대 크기(비율은 유지한다). 순서는 항목 순서: 0 포장지(안 씀), 1 리본, 2 카드, 3 장식, 4 잎")]
    [SerializeField] private Vector2[] maxSizeByCategory = { new(300f, 300f), new(480f, 480f), new(260f, 300f), new(180f, 180f), new(300f, 420f) };

    private readonly Dictionary<DecorationCategory, PlacedDecoration> anchored = new();
    private RectTransform layer;
    private Canvas canvas;
    private Image bouquetImage;

    private void Awake()
    {
        layer = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
        bouquetImage = bouquetArea.GetComponent<Image>();
    }

    /// <summary>카드를 끌어서 놓는 종류인가(카드/장식).</summary>
    public static bool IsFree(DecorationCategory category) => category == DecorationCategory.Card || category == DecorationCategory.Ornament;

    /// <summary>앵커에 놓는 종류인가(리본/풀).</summary>
    public static bool IsAnchored(DecorationCategory category) => category == DecorationCategory.Ribbon || category == DecorationCategory.Leaf;

    /// <summary>이 용품이 지금 앵커에 놓여 있는가.</summary>
    public bool IsPlaced(DecorationData data)
    {
        return anchored.TryGetValue(data.category, out PlacedDecoration item) && item != null && item.Data == data;
    }

    /// <summary>앵커 종류(리본/풀): 같은 것이 놓여 있으면 빼고, 아니면 그 자리의 것을 바꿔 놓는다.</summary>
    public void ToggleAnchored(DecorationData data)
    {
        RectTransform anchor = data.category == DecorationCategory.Ribbon ? ribbonAnchor : data.category == DecorationCategory.Leaf ? leafAnchor : null;
        if (anchor == null) return;

        bool same = IsPlaced(data);
        if (anchored.TryGetValue(data.category, out PlacedDecoration old) && old != null) Destroy(old.gameObject);
        anchored.Remove(data.category);
        if (same) return;

        anchored[data.category] = Create(data, anchor, maxSizeByCategory[(int)data.category], false);
    }

    /// <summary>카드/장식: 카드에서 끌기 시작했다. 포인터 자리에 새 것을 만들어 돌려준다(끄는 동안 카드가 MoveToScreen으로 따라가게 한다).</summary>
    public PlacedDecoration BeginFreeDrag(DecorationData data, Vector2 screenPosition)
    {
        PlacedDecoration item = Create(data, layer, maxSizeByCategory[(int)data.category], true);
        item.MoveToScreen(screenPosition);
        return item;
    }

    /// <summary>놓았다: 꽃다발 영역 밖이면 사라진다.</summary>
    public void Drop(PlacedDecoration item, Vector2 screenPosition)
    {
        if (!RectTransformUtility.RectangleContainsScreenPoint(bouquetArea, screenPosition, Camera())) Destroy(item.gameObject);
    }

    /// <summary>꽃다발 모양(포장지)을 고른다: 꽃다발 이미지를 그 포장지 그림으로 바꾼다.</summary>
    public void SelectShape(DecorationData data)
    {
        bouquetImage.sprite = data.sprite;
        bouquetImage.preserveAspect = true; // 포장지마다 그림 비율이 달라도 찌그러지지 않게
    }

    /// <summary>이 포장지가 지금 꽃다발 모양인가.</summary>
    public bool IsShapeSelected(DecorationData data) => bouquetImage.sprite == data.sprite;

    /// <summary>화면 좌표를 이 레이어 안의 좌표로 바꾼다.</summary>
    public Vector2 ScreenToLayer(Vector2 screenPosition)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screenPosition, Camera(), out Vector2 local);
        return local;
    }

    private PlacedDecoration Create(DecorationData data, RectTransform parent, Vector2 maxSize, bool draggable)
    {
        PlacedDecoration item = Instantiate(itemPrefab, parent);
        item.name = $"Placed_{data.name}";
        item.Init(data, this, maxSize, draggable);
        return item;
    }

    private Camera Camera() => canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
}
