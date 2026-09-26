using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 낮 퍼즐(포장) 보드. 태그+면(라인/매스/품/필러 x 좌/우/중앙) 조합마다 슬롯 하나씩,
/// 총 9개가 SlotArea 하이라키에 미리 배치되어 있다. 포장지 레벨이 정해지면 그중 이번
/// 레벨에 필요한 슬롯만 활성화하고 requiredCount를 채워 넣는다(생성/삭제 없음).
/// 드래그해온 꽃을 알맞은 슬롯에 매칭시키고, 불균형 붕괴/완성 판정도 여기서 담당한다.
/// </summary>
public class WrapperBoardController : MonoBehaviour
{
    public static WrapperBoardController Instance { get; private set; }

    [SerializeField] private RectTransform slotArea;

    /// <summary>한쪽 면에 놓인 꽃 개수가 반대쪽보다 이만큼 많아지면 붕괴한다.</summary>
    private const int CollapseDiff = 3;

    private static readonly Dictionary<BlockData.FlowerTag, Color> TagColors = new()
    {
        { BlockData.FlowerTag.Line, new Color(0.95f, 0.55f, 0.25f) },
        { BlockData.FlowerTag.Mass, new Color(0.55f, 0.75f, 0.95f) },
        { BlockData.FlowerTag.Form, new Color(0.95f, 0.65f, 0.80f) },
        { BlockData.FlowerTag.Filler, new Color(0.90f, 0.85f, 0.65f) },
    };

    private readonly Dictionary<(BlockData.FlowerTag tag, WrapperSlot.Side side), WrapperSlot> slotLookup = new();
    private readonly List<BlockData> placementHistory = new();
    private WrapperLevelDatabase.LevelDef currentLevel;

    /// <summary>꽃이 슬롯에 배치될 때마다 발행. 완성/붕괴/실패 판정은 DayPuzzleUI가 담당한다.</summary>
    public event Action OnFlowerPlaced;

    public bool IsComplete => AllSlots.Count > 0 && AllSlots.All(s => s.IsFull);

    private void Awake()
    {
        Instance = this;

        foreach (var slot in slotArea.GetComponentsInChildren<WrapperSlot>(true))
        {
            slotLookup[(slot.flowerTag, slot.side)] = slot;
        }
    }

    public void BuildLevel(WrapperLevelDatabase.LevelDef level)
    {
        currentLevel = level;
        placementHistory.Clear();

        foreach (var slot in slotLookup.Values)
        {
            slot.gameObject.SetActive(false);
            slot.Clear();
        }

        foreach (var region in level.regions)
        {
            if (region.centerCount > 0) Activate(region.tag, WrapperSlot.Side.None, region.centerCount);
            if (region.leftCount > 0) Activate(region.tag, WrapperSlot.Side.Left, region.leftCount);
            if (region.rightCount > 0) Activate(region.tag, WrapperSlot.Side.Right, region.rightCount);
        }
    }

    private void Activate(BlockData.FlowerTag tag, WrapperSlot.Side side, int requiredCount)
    {
        if (!slotLookup.TryGetValue((tag, side), out WrapperSlot slot))
        {
            Debug.LogWarning($"[WrapperBoardController] {tag}/{side} 조합의 슬롯이 하이라키에 없습니다.");
            return;
        }

        slot.requiredCount = requiredCount;
        slot.gameObject.SetActive(true);
        slot.SetEmptyVisual(TagColors.TryGetValue(tag, out Color c) ? c : Color.gray);
    }

    /// <summary>붕괴 시 슬롯 배치는 그대로 두고 놓인 꽃만 전부 비운다.</summary>
    public void ClearAllPlacements()
    {
        foreach (var slot in AllSlots) slot.Clear();
        placementHistory.Clear();
    }

    /// <summary>화면 좌표 아래에 있는, 이 꽃을 받을 수 있는 슬롯을 찾아 배치를 시도한다.</summary>
    public bool TryPlaceAtScreenPoint(BlockData flower, Vector2 screenPoint, Camera eventCamera)
    {
        foreach (var slot in AllSlots)
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
        foreach (var slot in AllSlots)
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

    /// <summary>이번 레벨에서 활성화된(사용 중인) 슬롯만 반환한다.</summary>
    public IReadOnlyList<WrapperSlot> AllSlots => slotLookup.Values.Where(s => s.gameObject.activeSelf).ToList();
}
