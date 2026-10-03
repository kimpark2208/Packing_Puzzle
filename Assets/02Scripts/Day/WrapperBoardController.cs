using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 낮 퍼즐(포장) 보드 - 동심원 + 4분할.
/// 칸(WrapperSlot)은 SlotArea 아래에 에디터에서 직접 배치해 두고(이름 = A_1Ring_Upper 등),
/// BuildLevel이 프리셋(WrapperPresetData)에 적힌 칸만 켜서 태그 색으로 칠하고 태그별 꽃 이미지를 채운다.
/// 드래그해온 꽃은 자기 역할(속성)과 같은 태그의 빈 칸에만 놓을 수 있고, 놓으면 그 칸이 진하게 채워진다.
///
/// TODO: 지금은 2링 포장지와 그 프리셋만 지원하는 임시 구조다. 3링 이상 포장지는 에디터에 둘 칸 구성과
/// 맨 바깥 링 처리(반지름/이미지)를 새로 정해야 하고, 판정도 반지름·사분면 수식이라 칸 이미지와 따로 논다.
/// </summary>
public class WrapperBoardController : MonoBehaviour
{
    public static WrapperBoardController Instance { get; private set; }

    /// <summary>에디터에 배치된 동심원 칸의 링 수. TODO: 링 수가 다른 포장지를 지원하려면 칸 구성을 새로 정해야 한다.</summary>
    public const int SupportedRings = 2;

    [SerializeField] private RectTransform slotArea;
    [SerializeField] private RectTransform flowerArea; // 칸과 같은 이름의 꽃 이미지를 두는 곳 (SlotArea와 같은 위치/크기)
    [SerializeField] private float centerRadius = 70f;
    [SerializeField] private float ringSpacing = 100f;
    [Header("씬 단독 실행용 기본 포장지/프리셋 (주문이 확정돼 있으면 주문의 프리셋을 쓴다)")]
    [SerializeField] private WrapperData wrapper;
    [SerializeField] private int presetIndex;
    [SerializeField] private float outerRingVisualRadius = 550f; // 맨 바깥 링의 바깥 반지름(판정용)

    [Header("FlowerArea의 꽃 이미지에 채울 태그별 이미지 (FlowerRole 순서: Line, Mass, Form, Filler)")]
    [SerializeField] private Sprite[] tagSprites;

    // 태그 색(기획서): 라인=주황, 매스=파랑, 폼=핑크, 필러=노랑. 비어있음/미리보기/채움은 투명도로만 구분한다.
    private const float IdleAlpha = 0.45f;
    private const float HoverAlpha = 0.90f;
    private const float FilledAlpha = 1f;
    private const int CollapseDiff = 3;

    private readonly List<WrapperSlot> regions = new();
    private readonly List<FlowerData> placementHistory = new();

    private WrapperSlot hoveredRegion;

    /// <summary>꽃이 영역에 배치될 때마다 발행.</summary>
    public event Action OnFlowerPlaced;

    public bool IsComplete => regions.Count > 0 && regions.All(r => r.IsFull);

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>프리셋에 적힌 칸만 켜고 태그 색과 꽃 이미지를 채운다. 프리셋에 없는 칸은 끈다.</summary>
    public void BuildLevel()
    {
        placementHistory.Clear();
        regions.Clear();
        hoveredRegion = null;

        WrapperPresetData preset = CurrentPreset();
        if (preset == null) return;

        foreach (WrapperSlot slot in slotArea.GetComponentsInChildren<WrapperSlot>(true))
        {
            Transform flowerImage = FlowerImageOf(slot);
            if (flowerImage != null) flowerImage.gameObject.SetActive(false); // 칸이 채워질 때 켠다
            int i = preset.regions.FindIndex(r => r.position == slot.position);
            slot.gameObject.SetActive(i >= 0);
            if (i < 0) continue;

            slot.Clear();
            slot.tag = preset.regions[i].tag;
            slot.SetHighlightColor(TagColor(slot.tag));
            if (flowerImage != null) flowerImage.GetComponent<Image>().sprite = tagSprites[(int)slot.tag];
            regions.Add(slot);
        }
    }

    /// <summary>이번 퍼즐에 쓸 프리셋. 주문이 확정돼 있으면 그 프리셋을, 아니면(씬 단독 실행 등) 인스펙터 기본값을 쓴다.</summary>
    private WrapperPresetData CurrentPreset()
    {
        DayPuzzleGenerator.DayOrder order = GameFlowController.Instance != null ? GameFlowController.Instance.CurrentDayOrder : null;
        WrapperData data = order != null && order.isFinalized && WrapperRegistry.Instance != null ? WrapperRegistry.Instance.GetById(order.wrapperId) : null;
        int index = data != null ? order.presetIndex : presetIndex;
        if (data == null || index < 0 || index >= data.presets.Count)
        {
            data = wrapper;
            index = presetIndex;
        }
        if (data == null || data.presets.Count == 0) return null;
        return data.presets[Mathf.Clamp(index, 0, data.presets.Count - 1)];
    }

    /// <summary>꽃 이미지는 FlowerArea 아래에 칸과 같은 이름(= position)으로 둔다.</summary>
    private Transform FlowerImageOf(WrapperSlot slot)
    {
        return flowerArea.Find(slot.position.ToString());
    }

    private static Color TagColor(FlowerData.FlowerRole tag)
    {
        Color c = ColorPalette.ToRoleColor(tag);
        c.a = IdleAlpha;
        return c;
    }

    /// <summary>화면 좌표를 보드 중심 기준 좌표로 바꾼다. 칸은 SlotArea 사각형의 중심에 배치되므로 피벗이 아니라 사각형 중심이 원점이다.</summary>
    private bool TryGetBoardPoint(Vector2 screenPoint, Camera eventCamera, out Vector2 local)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(slotArea, screenPoint, eventCamera, out local)) return false;
        local -= slotArea.rect.center;
        return true;
    }

    /// <summary>화면 좌표를 중심 기준 반지름/사분면으로 변환해 알맞은 영역을 찾아 배치를 시도한다(꽃의 역할과 칸의 태그가 같아야 한다).</summary>
    public bool TryPlaceAtScreenPoint(FlowerData flower, Vector2 screenPoint, Camera eventCamera)
    {
        if (!TryGetBoardPoint(screenPoint, eventCamera, out Vector2 local)) return false;

        WrapperSlot region = ResolveRegion(local);
        if (region == null || !region.CanAccept(flower)) return false;

        region.Fill(flower);
        region.SetHighlightAlpha(FilledAlpha);
        Transform flowerImage = FlowerImageOf(region);
        if (flowerImage != null) flowerImage.gameObject.SetActive(true);
        if (region == hoveredRegion) hoveredRegion = null;

        placementHistory.Add(flower);
        OnFlowerPlaced?.Invoke();
        return true;
    }

    /// <summary>드래그 중 호출: 현재 포인터 아래 영역을 반투명 파란색으로 미리 보여준다.</summary>
    public void PreviewHover(FlowerData flower, Vector2 screenPoint, Camera eventCamera)
    {
        WrapperSlot region = null;
        if (TryGetBoardPoint(screenPoint, eventCamera, out Vector2 local))
        {
            region = ResolveRegion(local);
        }

        if (region == hoveredRegion) return;

        ClearHoverPreview();

        if (region != null && region.CanAccept(flower))
        {
            region.SetHighlightAlpha(HoverAlpha);
            hoveredRegion = region;
        }
    }

    /// <summary>드래그가 끝나면(놓았든 반려됐든) 남아있는 미리보기 강조를 지운다.</summary>
    public void ClearHoverPreview()
    {
        if (hoveredRegion != null && !hoveredRegion.IsFull)
        {
            hoveredRegion.SetHighlightAlpha(IdleAlpha);
        }
        hoveredRegion = null;
    }

    private WrapperSlot ResolveRegion(Vector2 local)
    {
        float dist = local.magnitude;
        if (dist <= centerRadius)
        {
            return regions.FirstOrDefault(r => r.ringIndex == -1);
        }

        const int ringCount = SupportedRings;
        for (int ring = 0; ring < ringCount; ring++)
        {
            float inner = centerRadius + ring * ringSpacing;
            float outer = centerRadius + (ring + 1) * ringSpacing;
            bool isOuterMost = ring == ringCount - 1;

            float bandOuter = isOuterMost ? outerRingVisualRadius : outer;
            if (dist <= inner || dist > bandOuter) continue;

            WrapperSlot.Quadrant quadrant = GetQuadrant(local);
            return regions.FirstOrDefault(r => r.ringIndex == ring && r.quadrant == quadrant);
        }

        return null;
    }

    private static WrapperSlot.Quadrant GetQuadrant(Vector2 local)
    {
        if (local.x >= 0f && local.y >= 0f) return WrapperSlot.Quadrant.TopRight;
        if (local.x < 0f && local.y >= 0f) return WrapperSlot.Quadrant.TopLeft;
        if (local.x < 0f && local.y < 0f) return WrapperSlot.Quadrant.BottomLeft;
        return WrapperSlot.Quadrant.BottomRight;
    }

    /// <summary>한쪽(좌/우) 사분면들에 채워진 꽃 개수가 반대쪽보다 CollapseDiff개 이상 많은가.</summary>
    public bool IsCollapsed()
    {
        int left = 0, right = 0;
        foreach (var r in regions)
        {
            if (r.quadrant == WrapperSlot.Quadrant.TopLeft || r.quadrant == WrapperSlot.Quadrant.BottomLeft) left += r.FilledCount;
            else if (r.quadrant == WrapperSlot.Quadrant.TopRight || r.quadrant == WrapperSlot.Quadrant.BottomRight) right += r.FilledCount;
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

    public IReadOnlyList<WrapperSlot> AllSlots => regions;
}
