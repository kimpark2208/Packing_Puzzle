using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 낮 퍼즐(포장) 보드. 포장지 레벨 데이터로 태그별 슬롯을 절차적으로 배치하고,
/// 드래그해온 꽃을 알맞은 슬롯에 매칭시킨다. 불균형 붕괴/완성 판정도 여기서 담당한다.
/// 슬롯 모양은 정식 아트 전까지 간단한 사각 블록으로 대체한다.
/// </summary>
public class WrapperBoardController : MonoBehaviour
{
    public static WrapperBoardController Instance { get; private set; }

    [SerializeField] private RectTransform slotArea;
    [SerializeField] private float slotSize = 110f;
    [SerializeField] private float slotGapX = 20f;
    [SerializeField] private float rowGapY = 140f;

    /// <summary>한쪽 면이 반대쪽보다 이만큼 많아지면 붕괴한다.</summary>
    private const int CollapseDiff = 3;

    private static readonly Dictionary<BlockData.FlowerTag, Color> TagColors = new()
    {
        { BlockData.FlowerTag.Line, new Color(0.95f, 0.55f, 0.25f) },
        { BlockData.FlowerTag.Mass, new Color(0.55f, 0.75f, 0.95f) },
        { BlockData.FlowerTag.Form, new Color(0.95f, 0.65f, 0.80f) },
        { BlockData.FlowerTag.Filler, new Color(0.90f, 0.85f, 0.65f) },
    };

    private readonly List<WrapperSlot> slots = new();
    private WrapperLevelDatabase.LevelDef currentLevel;

    /// <summary>꽃이 슬롯에 배치될 때마다 발행. 완성/붕괴/실패 판정은 DayPuzzleUI가 담당한다.</summary>
    public event Action OnFlowerPlaced;

    public bool IsComplete => slots.Count > 0 && slots.TrueForAll(s => s.PlacedFlower != null);

    private void Awake()
    {
        Instance = this;
    }

    public void BuildLevel(WrapperLevelDatabase.LevelDef level)
    {
        currentLevel = level;
        ClearSlots();

        int rowCount = level.regions.Count;
        float startY = (rowCount - 1) * rowGapY / 2f;

        for (int ring = 0; ring < rowCount; ring++)
        {
            float y = startY - ring * rowGapY;
            BuildRing(level.regions[ring], ring, y);
        }
    }

    /// <summary>붕괴 시 슬롯 배치는 그대로 두고 놓인 꽃만 전부 비운다.</summary>
    public void ClearAllPlacements()
    {
        foreach (var slot in slots) slot.Clear();
    }

    private void BuildRing(WrapperLevelDatabase.RegionDef region, int ringIndex, float y)
    {
        var offsets = new List<(float x, WrapperSlot.Side side)>();

        for (int i = region.leftCount; i >= 1; i--)
            offsets.Add((-i * (slotSize + slotGapX), WrapperSlot.Side.Left));

        if (region.centerCount > 0)
            offsets.Add((0f, WrapperSlot.Side.None));

        for (int i = 1; i <= region.rightCount; i++)
            offsets.Add((i * (slotSize + slotGapX), WrapperSlot.Side.Right));

        Color tagColor = TagColors.TryGetValue(region.tag, out Color c) ? c : Color.gray;

        for (int i = 0; i < offsets.Count; i++)
        {
            (float x, WrapperSlot.Side side) = offsets[i];

            var slotGO = new GameObject($"Slot_{region.tag}_{ringIndex}_{i}", typeof(RectTransform), typeof(Image));
            slotGO.transform.SetParent(slotArea, false);

            var rt = (RectTransform)slotGO.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(slotSize, slotSize);
            rt.anchoredPosition = new Vector2(x, y);

            var slot = slotGO.AddComponent<WrapperSlot>();
            slot.flowerTag = region.tag;
            slot.side = side;
            slot.ringIndex = ringIndex;
            slot.orderInRing = i;
            slot.SetEmptyVisual(tagColor);

            slots.Add(slot);
        }
    }

    private void ClearSlots()
    {
        foreach (var slot in slots)
        {
            if (slot != null) Destroy(slot.gameObject);
        }
        slots.Clear();
    }

    /// <summary>화면 좌표 아래에 있는, 이 꽃을 받을 수 있는 빈 슬롯을 찾아 배치를 시도한다.</summary>
    public bool TryPlaceAtScreenPoint(BlockData flower, Vector2 screenPoint, Camera eventCamera)
    {
        foreach (var slot in slots)
        {
            var rt = (RectTransform)slot.transform;
            if (!RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, eventCamera)) continue;
            if (!slot.CanAccept(flower)) continue;

            slot.Place(flower);
            OnFlowerPlaced?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>한쪽 면에 배치된 꽃 개수가 반대쪽보다 CollapseDiff개 이상 많은가.</summary>
    public bool IsCollapsed()
    {
        int left = 0, right = 0;
        foreach (var slot in slots)
        {
            if (slot.PlacedFlower == null) continue;
            if (slot.side == WrapperSlot.Side.Left) left++;
            else if (slot.side == WrapperSlot.Side.Right) right++;
        }
        return Mathf.Abs(left - right) >= CollapseDiff;
    }

    /// <summary>인접(같은 영역, 순서상 이웃) 슬롯 쌍의 색 조합 보너스 총합.</summary>
    public int ComputeColorBonus()
    {
        int bonus = 0;
        var byRing = new Dictionary<int, List<WrapperSlot>>();
        foreach (var slot in slots)
        {
            if (!byRing.TryGetValue(slot.ringIndex, out var list))
            {
                list = new List<WrapperSlot>();
                byRing[slot.ringIndex] = list;
            }
            list.Add(slot);
        }

        foreach (var list in byRing.Values)
        {
            list.Sort((a, b) => a.orderInRing.CompareTo(b.orderInRing));
            for (int i = 0; i < list.Count - 1; i++)
            {
                BlockData a = list[i].PlacedFlower;
                BlockData b = list[i + 1].PlacedFlower;
                if (a == null || b == null) continue;
                bonus += ColorCompatDatabase.GetBonus(a.color, b.color);
            }
        }
        return bonus;
    }

    public IReadOnlyList<WrapperSlot> AllSlots => slots;
}
