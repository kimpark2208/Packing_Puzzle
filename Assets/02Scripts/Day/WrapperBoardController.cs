using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 낮 퍼즐(포장) 보드. 포장지 레벨 데이터로 태그별 슬롯을 동심원 형태로 절차적으로 배치하고,
/// 드래그해온 꽃을 알맞은 슬롯에 매칭시킨다. 불균형 붕괴/완성 판정도 여기서 담당한다.
/// 태그+면(라인/매스/품/필러 x 좌/우/중앙) 조합마다 슬롯은 하나이며, 필요한 개수만큼 반복해서
/// 채운다(WrapperSlot.requiredCount). 배경은 기획서 예시 이미지(WrapperTemplateExample)를
/// 장식용으로 깔고, 실제 드래그 판정 영역은 그 위의 사각 슬롯이 담당한다.
/// </summary>
public class WrapperBoardController : MonoBehaviour
{
    public static WrapperBoardController Instance { get; private set; }

    [SerializeField] private RectTransform slotArea;
    [SerializeField] private float slotSize = 100f;
    [SerializeField] private float baseRadius = 90f;   // 가장 안쪽 태그(라인) 링의 반지름
    [SerializeField] private float ringSpacing = 110f; // 링 사이 간격
    [SerializeField] private Sprite backgroundSprite;

    /// <summary>한쪽 면에 놓인 꽃 개수가 반대쪽보다 이만큼 많아지면 붕괴한다.</summary>
    private const int CollapseDiff = 3;
    private const string BackgroundSpritePath = "Assets/03Images/UI/WrapperTemplateExample.png";

    private static readonly Dictionary<BlockData.FlowerTag, Color> TagColors = new()
    {
        { BlockData.FlowerTag.Line, new Color(0.95f, 0.55f, 0.25f) },
        { BlockData.FlowerTag.Mass, new Color(0.55f, 0.75f, 0.95f) },
        { BlockData.FlowerTag.Form, new Color(0.95f, 0.65f, 0.80f) },
        { BlockData.FlowerTag.Filler, new Color(0.90f, 0.85f, 0.65f) },
    };

    private readonly List<WrapperSlot> slots = new();
    private readonly List<BlockData> placementHistory = new();
    private WrapperLevelDatabase.LevelDef currentLevel;
    private Image background;

    /// <summary>꽃이 슬롯에 배치될 때마다 발행. 완성/붕괴/실패 판정은 DayPuzzleUI가 담당한다.</summary>
    public event Action OnFlowerPlaced;

    public bool IsComplete => slots.Count > 0 && slots.TrueForAll(s => s.IsFull);

    private void Awake()
    {
        Instance = this;
    }

    public void BuildLevel(WrapperLevelDatabase.LevelDef level)
    {
        currentLevel = level;
        ClearSlots();
        placementHistory.Clear();
        EnsureBackground();

        int ringCount = level.regions.Count;
        float outerRadius = baseRadius + Mathf.Max(0, ringCount - 1) * ringSpacing;

        for (int ring = 0; ring < ringCount; ring++)
        {
            float radius = baseRadius + ring * ringSpacing;
            BuildRing(level.regions[ring], radius);
        }

        float bgHeight = (outerRadius + slotSize) * 2f;
        Sprite sprite = background.sprite;
        float aspect = sprite != null && sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 1f;
        var bgRT = (RectTransform)background.transform;
        bgRT.sizeDelta = new Vector2(bgHeight * aspect, bgHeight);
    }

    /// <summary>붕괴 시 슬롯 배치는 그대로 두고 놓인 꽃만 전부 비운다.</summary>
    public void ClearAllPlacements()
    {
        foreach (var slot in slots) slot.Clear();
        placementHistory.Clear();
    }

    private void EnsureBackground()
    {
        if (background != null) return;

        var bgGO = new GameObject("WrapperBackground", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(slotArea, false);
        bgGO.transform.SetAsFirstSibling();
        var rt = (RectTransform)bgGO.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        background = bgGO.GetComponent<Image>();
        background.sprite = backgroundSprite != null ? backgroundSprite : LoadDefaultBackgroundSprite();
        background.raycastTarget = false;
        background.preserveAspect = true;
    }

    private static Sprite LoadDefaultBackgroundSprite()
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundSpritePath);
#else
        return null;
#endif
    }

    private void BuildRing(WrapperLevelDatabase.RegionDef region, float radius)
    {
        Color tagColor = TagColors.TryGetValue(region.tag, out Color c) ? c : Color.gray;

        if (region.centerCount > 0)
            CreateSlot(region.tag, WrapperSlot.Side.None, region.centerCount, Vector2.zero, tagColor);

        if (region.leftCount > 0)
            CreateSlot(region.tag, WrapperSlot.Side.Left, region.leftCount, AnglePos(180f, radius), tagColor);

        if (region.rightCount > 0)
            CreateSlot(region.tag, WrapperSlot.Side.Right, region.rightCount, AnglePos(0f, radius), tagColor);
    }

    private void CreateSlot(BlockData.FlowerTag tag, WrapperSlot.Side side, int requiredCount, Vector2 pos, Color tagColor)
    {
        var slotGO = new GameObject($"Slot_{tag}_{side}", typeof(RectTransform), typeof(Image));
        slotGO.transform.SetParent(slotArea, false);

        var rt = (RectTransform)slotGO.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(slotSize, slotSize);
        rt.anchoredPosition = pos;

        var slot = slotGO.AddComponent<WrapperSlot>();
        slot.flowerTag = tag;
        slot.side = side;
        slot.requiredCount = requiredCount;
        slot.SetEmptyVisual(tagColor);

        slots.Add(slot);
    }

    private static Vector2 AnglePos(float angleDeg, float radius)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius);
    }

    private void ClearSlots()
    {
        foreach (var slot in slots)
        {
            if (slot != null) Destroy(slot.gameObject);
        }
        slots.Clear();
    }

    /// <summary>화면 좌표 아래에 있는, 이 꽃을 받을 수 있는 슬롯을 찾아 배치를 시도한다.</summary>
    public bool TryPlaceAtScreenPoint(BlockData flower, Vector2 screenPoint, Camera eventCamera)
    {
        foreach (var slot in slots)
        {
            var rt = (RectTransform)slot.transform;
            if (!RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, eventCamera)) continue;
            if (!slot.CanAccept(flower)) continue;

            slot.Place(flower);
            placementHistory.Add(flower);
            OnFlowerPlaced?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>한쪽 면에 놓인 꽃 개수가 반대쪽보다 CollapseDiff개 이상 많은가.</summary>
    public bool IsCollapsed()
    {
        int left = 0, right = 0;
        foreach (var slot in slots)
        {
            if (slot.side == WrapperSlot.Side.Left) left += slot.FilledCount;
            else if (slot.side == WrapperSlot.Side.Right) right += slot.FilledCount;
        }
        return Mathf.Abs(left - right) >= CollapseDiff;
    }

    /// <summary>놓은 순서대로 연속된 두 꽃의 색 조합 보너스 총합.</summary>
    public int ComputeColorBonus()
    {
        int bonus = 0;
        for (int i = 0; i < placementHistory.Count - 1; i++)
        {
            bonus += ColorCompatDatabase.GetBonus(placementHistory[i].color, placementHistory[i + 1].color);
        }
        return bonus;
    }

    public IReadOnlyList<WrapperSlot> AllSlots => slots;
}
