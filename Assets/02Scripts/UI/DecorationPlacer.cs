using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 꾸미기 용품을 꽃다발에 올려 두는 곳. 카드를 누르면 용품이 그 항목의 기본 자리에 놓이고, 놓인 용품은 꽃다발 위에서 끌어서 옮긴다(PlacedDecoration).
/// - 리본/풀: 에디터에 놓아 둔 앵커의 자식으로 한 종류당 하나만 놓인다(다른 것을 고르면 바뀌고, 놓인 것을 다시 고르면 빠진다).
///   앵커가 속한 영역(RibbonArea/LeafArea)의 순서가 곧 겹침 순서다: 풀은 포장지와 꽃 사이, 리본은 꽃 위. 꽃다발이 기울면 같이 기운다.
/// - 카드/장식: 이 오브젝트의 RectTransform이 "꽃다발 제일 위 레이어"라서 PuzzleRoot 맨 끝에 둔다. 누를 때마다 하나씩 더 놓인다(기본 자리: 항목별 앵커, 없으면 꽃다발 가운데).
/// - 꽃다발 밖에서 놓으면 사라진다.
/// - 꽃다발 모양(포장지): 고른 포장지 그림으로 꽃다발 이미지를 바꾼다.
/// 그림은 모두 원본 비율을 지킨 채 항목별 상자(maxSizeByCategory) 안에 맞추고 용품의 sizeScale을 곱한다. 놓이는 기준점은 스프라이트의 피벗이다(PlacedDecoration).
/// </summary>
public class DecorationPlacer : MonoBehaviour
{
    [SerializeField] private PlacedDecoration itemPrefab;
    [SerializeField] private RectTransform bouquetArea; // 포장지 이미지(Image). 이 영역 안에 놓아야 남는다
    [SerializeField] private RectTransform ribbonAnchor;
    [SerializeField] private RectTransform leafAnchor;

    [Header("카드/장식의 기본 자리(선택): 비우면 꽃다발 가운데")]
    [SerializeField] private RectTransform cardAnchor;
    [SerializeField] private RectTransform ornamentAnchor;

    [Header("항목별로 그림이 들어가는 최대 크기(비율은 유지한다). 순서는 항목 순서: 0 포장지(안 씀), 1 리본, 2 카드, 3 장식, 4 잎")]
    [SerializeField] private Vector2[] maxSizeByCategory = { new(300f, 300f), new(480f, 480f), new(260f, 300f), new(180f, 180f), new(300f, 420f) };

    private readonly Dictionary<DecorationCategory, PlacedDecoration> anchored = new();
    private RectTransform layer;
    private Canvas canvas;
    private Image bouquetImage;

    public Camera EventCamera => canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

    private void Awake()
    {
        layer = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
        bouquetImage = bouquetArea.GetComponent<Image>();
    }

    /// <summary>한 종류에 하나만 놓이는가(리본/풀). 카드/장식은 여러 개 놓을 수 있다.</summary>
    private static bool IsSingle(DecorationCategory category) => category == DecorationCategory.Ribbon || category == DecorationCategory.Leaf;

    /// <summary>이 용품이 지금 한 종류 한 자리(리본/풀)에 놓여 있는가.</summary>
    public bool IsPlaced(DecorationData data)
    {
        return anchored.TryGetValue(data.category, out PlacedDecoration item) && item != null && item.Data == data;
    }

    /// <summary>이 용품이 지금 꽃다발 위에 놓여 있는가(리본/풀은 자리에, 카드/장식은 어디든). 예약 필수 용품을 놓았는지 볼 때 쓴다.</summary>
    public bool Contains(DecorationData data)
    {
        if (IsPlaced(data)) return true;

        foreach (PlacedDecoration item in layer.GetComponentsInChildren<PlacedDecoration>())
        {
            if (item != null && item.Data == data) return true;
        }
        return false;
    }

    /// <summary>카드를 눌렀다: 용품을 기본 자리에 놓는다. 리본/풀은 같은 것이 놓여 있으면 빼고, 아니면 그 자리의 것을 바꿔 놓는다.</summary>
    public void Place(DecorationData data)
    {
        Vector2 maxSize = maxSizeByCategory[(int)data.category];

        if (!IsSingle(data.category))
        {
            RectTransform anchor = data.category == DecorationCategory.Card ? cardAnchor : ornamentAnchor;
            PlacedDecoration item = Instantiate(itemPrefab, layer);
            item.name = $"Placed_{data.name}";
            item.Init(data, this, maxSize);
            item.transform.position = anchor != null ? anchor.position : bouquetArea.TransformPoint(bouquetArea.rect.center);
            return;
        }

        bool same = IsPlaced(data);
        if (anchored.TryGetValue(data.category, out PlacedDecoration old) && old != null) Destroy(old.gameObject);
        anchored.Remove(data.category);
        if (same) return;

        RectTransform parent = data.category == DecorationCategory.Ribbon ? ribbonAnchor : leafAnchor;
        if (parent == null) return;
        PlacedDecoration placed = Instantiate(itemPrefab, parent);
        placed.name = $"Placed_{data.name}";
        placed.Init(data, this, maxSize);
        anchored[data.category] = placed;
    }

    /// <summary>놓았다: 꽃다발 영역 밖이면 사라진다.</summary>
    public void Drop(PlacedDecoration item, Vector2 screenPosition)
    {
        if (RectTransformUtility.RectangleContainsScreenPoint(bouquetArea, screenPosition, EventCamera)) return;
        Destroy(item.gameObject);
        if (anchored.TryGetValue(item.Data.category, out PlacedDecoration current) && current == item) anchored.Remove(item.Data.category); // Destroy는 프레임 끝에 되므로 바로 빼 둔다
        Removed?.Invoke();
    }

    /// <summary>끌어다 놓아서 꽃다발에서 용품이 사라졌을 때(카드 표시를 갱신하라고 알린다).</summary>
    public event System.Action Removed;

    /// <summary>꽃다발 모양(포장지)을 고른다: 꽃다발 이미지를 그 포장지 그림으로 바꾼다.</summary>
    public void SelectShape(DecorationData data)
    {
        bouquetImage.sprite = data.sprite;
        bouquetImage.preserveAspect = true; // 포장지마다 그림 비율이 달라도 찌그러지지 않게
    }

    /// <summary>이 포장지가 지금 꽃다발 모양인가.</summary>
    public bool IsShapeSelected(DecorationData data) => bouquetImage.sprite == data.sprite;
}
