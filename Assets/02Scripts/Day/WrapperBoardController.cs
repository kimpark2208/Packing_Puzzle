using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 낮 퍼즐(포장) 보드. 포장지 레벨 데이터로 태그별 슬롯을 동심원 형태로 절차적으로 배치하고,
/// 드래그해온 꽃을 알맞은 슬롯에 매칭시킨다. 불균형 붕괴/완성 판정도 여기서 담당한다.
/// 정식 원형 포장지 아트 전까지, 슬롯 자체는 사각 블록으로 대체하되 배치는 기획서의
/// "중앙(라인)에서 바깥(필러)으로 향하는 동심원 + 좌/우 면" 구조를 그대로 따른다.
/// </summary>
public class WrapperBoardController : MonoBehaviour
{
    public static WrapperBoardController Instance { get; private set; }

    [SerializeField] private RectTransform slotArea;
    [SerializeField] private float slotSize = 90f;
    [SerializeField] private float baseRadius = 90f;   // 가장 안쪽 태그(라인) 링의 반지름
    [SerializeField] private float ringSpacing = 110f; // 링 사이 간격

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
    private Image background;
    private static Sprite circleSprite;

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
        EnsureBackground();

        int ringCount = level.regions.Count;
        float outerRadius = baseRadius + Mathf.Max(0, ringCount - 1) * ringSpacing;

        for (int ring = 0; ring < ringCount; ring++)
        {
            float radius = baseRadius + ring * ringSpacing;
            BuildRing(level.regions[ring], ring, radius);
        }

        float bgDiameter = (outerRadius + slotSize) * 2f;
        var bgRT = (RectTransform)background.transform;
        bgRT.sizeDelta = new Vector2(bgDiameter, bgDiameter);
    }

    /// <summary>붕괴 시 슬롯 배치는 그대로 두고 놓인 꽃만 전부 비운다.</summary>
    public void ClearAllPlacements()
    {
        foreach (var slot in slots) slot.Clear();
    }

    private void EnsureBackground()
    {
        if (background != null) return;

        var bgGO = new GameObject("WrapperBackground", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(slotArea, false);
        var rt = (RectTransform)bgGO.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        background = bgGO.GetComponent<Image>();
        background.sprite = GetCircleSprite();
        background.color = new Color(0.98f, 0.94f, 0.88f);
        background.raycastTarget = false;
    }

    private void BuildRing(WrapperLevelDatabase.RegionDef region, int ringIndex, float radius)
    {
        var placements = new List<(Vector2 pos, WrapperSlot.Side side)>();

        // 소속 면이 없는(정중앙) 슬롯: 원점 부근에 모아 배치.
        for (int i = 0; i < region.centerCount; i++)
        {
            if (region.centerCount == 1)
            {
                placements.Add((Vector2.zero, WrapperSlot.Side.None));
            }
            else
            {
                float angle = 90f + (i - (region.centerCount - 1) / 2f) * 50f;
                Vector2 pos = AnglePos(angle, slotSize * 0.6f);
                placements.Add((pos, WrapperSlot.Side.None));
            }
        }

        // 왼쪽 면(180도 기준), 오른쪽 면(0도 기준) 부채꼴로 배치 -> 동심원 좌/우 구조.
        AddArc(placements, region.leftCount, radius, 180f, WrapperSlot.Side.Left);
        AddArc(placements, region.rightCount, radius, 0f, WrapperSlot.Side.Right);

        Color tagColor = TagColors.TryGetValue(region.tag, out Color c) ? c : Color.gray;

        for (int i = 0; i < placements.Count; i++)
        {
            (Vector2 pos, WrapperSlot.Side side) = placements[i];

            var slotGO = new GameObject($"Slot_{region.tag}_{ringIndex}_{i}", typeof(RectTransform), typeof(Image));
            slotGO.transform.SetParent(slotArea, false);

            var rt = (RectTransform)slotGO.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(slotSize, slotSize);
            rt.anchoredPosition = pos;

            var slot = slotGO.AddComponent<WrapperSlot>();
            slot.flowerTag = region.tag;
            slot.side = side;
            slot.ringIndex = ringIndex;
            slot.orderInRing = i;
            slot.SetEmptyVisual(tagColor);

            slots.Add(slot);
        }
    }

    /// <summary>centerAngleDeg를 중심으로 count개의 슬롯을 부채꼴로 펼쳐 원 위에 배치한다.</summary>
    private void AddArc(List<(Vector2 pos, WrapperSlot.Side side)> list, int count, float radius, float centerAngleDeg, WrapperSlot.Side side)
    {
        if (count <= 0) return;

        float spreadDeg = Mathf.Min(150f, 45f * (count - 1));
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : i / (float)(count - 1);
            float angle = centerAngleDeg - spreadDeg / 2f + t * spreadDeg;
            list.Add((AnglePos(angle, radius), side));
        }
    }

    private static Vector2 AnglePos(float angleDeg, float radius)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius);
    }

    private static Sprite GetCircleSprite()
    {
        if (circleSprite != null) return circleSprite;

        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var pixels = new Color32[size * size];
        Vector2 center = new(size / 2f, size / 2f);
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool inside = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) <= radius;
                pixels[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return circleSprite;
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
