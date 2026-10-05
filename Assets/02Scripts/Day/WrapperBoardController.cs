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

    /// <summary>에디터에 칸 오브젝트가 배치된 링 수. 3·4링 칸을 씬에 배치하면 이 값을 올린다(포장지는 최대 4링).</summary>
    public const int SupportedRings = 2;

    [SerializeField] private RectTransform slotArea;
    public RectTransform BoardArea => slotArea; // 튜토리얼이 가리키는 보드 영역
    [SerializeField] private RectTransform flowerArea; // 칸과 같은 이름의 꽃 이미지를 두는 곳 (SlotArea와 같은 위치/크기)
    [SerializeField] private float centerRadius = 70f;
    [SerializeField] private float ringSpacing = 100f;
    [Header("씬 단독 실행용 기본 포장지/프리셋 (주문이 확정돼 있으면 주문의 프리셋을 쓴다)")]
    [SerializeField] private WrapperData wrapper;
    [SerializeField] private int presetIndex;
    [SerializeField] private float outerRingVisualRadius = 550f; // 맨 바깥 링의 바깥 반지름(판정용)

    [Header("FlowerArea의 꽃 이미지에 채울 태그별 이미지 (FlowerRole 순서: Line, Mass, Form, Filler)")]
    [SerializeField] private Sprite[] tagSprites;

    // 태그 색: 라인=빨강, 매스=파랑, 폼=핑크, 필러=노랑. 비어있음/미리보기/채움은 투명도로만 구분한다.
    private const float IdleAlpha = 0.45f;
    private const float HoverAlpha = 0.90f;
    private const float FilledAlpha = 0f; // 꽃이 놓인 칸은 뒤의 셀 배경을 숨긴다
    private const int CollapseDiff = 2;
    private const int OverlapSamples = 5; // 꽃 사각형 한 변에 찍는 표본점 수(칸과의 겹침을 어림하는 데 쓴다)

    [Header("기울기: 좌우 꽃 개수가 1개 차이일 때 많은 쪽으로 기운다(Lerp로 서서히)")]
    [SerializeField] private RectTransform wrapperImage; // 기울이는 대상(포장지 이미지만. 칸/꽃 이미지는 그대로)
    [SerializeField] private float tiltAngle = 8f;
    [SerializeField] private float tiltLerpSpeed = 4f;
    [Header("붕괴: 더 무거운 쪽으로 쓰러진다(Lerp, 기울기보다 빠르게)")]
    [SerializeField] private float collapseAngle = 80f;
    [SerializeField] private float collapseLerpSpeed = 10f;

    private readonly List<WrapperSlot> regions = new();
    private readonly List<FlowerData> placementHistory = new();

    private WrapperSlot hoveredRegion;
    private float baseAngle;  // 에디터에 배치된 기본 z 회전
    private float tiltTarget; // 기본 회전에서 더 기울 각도
    private bool collapsing;  // 붕괴로 쓰러지는 중이면 더 빠른 속도로 기운다
    private int activeRings = SupportedRings; // 이번 포장지의 링 수(맨 바깥 링 판정에 쓴다)

    /// <summary>꽃이 영역에 배치될 때마다 발행.</summary>
    public event Action OnFlowerPlaced;

    public bool IsComplete => regions.Count > 0 && regions.All(r => r.IsFull);

    private void Awake()
    {
        Instance = this;
        if (wrapperImage != null) baseAngle = Mathf.DeltaAngle(0f, wrapperImage.localEulerAngles.z);
    }

    private void Update()
    {
        if (wrapperImage == null) return;
        float current = Mathf.DeltaAngle(0f, wrapperImage.localEulerAngles.z);
        wrapperImage.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(current, baseAngle + tiltTarget, (collapsing ? collapseLerpSpeed : tiltLerpSpeed) * Time.deltaTime));
    }

    /// <summary>붕괴: 꽃이 더 많은 쪽(더 무거운 쪽)으로 collapseAngle만큼 빠르게 쓰러진다. 다음 BuildLevel에서 원래대로 돌아온다.</summary>
    public void PlayCollapse()
    {
        collapsing = true;
        tiltTarget = -Mathf.Sign(RightMinusLeft()) * collapseAngle; // 오른쪽이 많으면 시계 방향(-z)
    }

    /// <summary>프리셋에 적힌 칸만 켜고 태그 색과 꽃 이미지를 채운다. 프리셋에 없는 칸은 끈다.</summary>
    public void BuildLevel()
    {
        placementHistory.Clear();
        regions.Clear();
        hoveredRegion = null;
        tiltTarget = 0f;
        collapsing = false;

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
        activeRings = Mathf.Min(data.ringCount, SupportedRings);
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
        return TryPlaceAtScreenRect(flower, new Rect(screenPoint, Vector2.zero), eventCamera);
    }

    /// <summary>꽃이 화면에서 차지하는 사각형과 겹치는 칸 중 꽃을 받을 수 있는 칸에 놓는다(가장 많이 겹친 칸).</summary>
    public bool TryPlaceAtScreenRect(FlowerData flower, Rect screenRect, Camera eventCamera)
    {
        WrapperSlot region = ResolveByOverlap(flower, screenRect, eventCamera);
        if (region == null) return false;

        region.Fill(flower);
        region.SetHighlightAlpha(FilledAlpha);
        Transform flowerImage = FlowerImageOf(region);
        if (flowerImage != null)
        {
            flowerImage.GetComponent<Image>().color = ColorPalette.ToUnityColor(flower.color); // 칸 이미지가 흰색이라 놓은 꽃의 색으로 물들인다
            flowerImage.gameObject.SetActive(true);
        }
        region.SetHover(false);
        if (region == hoveredRegion) hoveredRegion = null;

        placementHistory.Add(flower);
        tiltTarget = -Mathf.Clamp(RightMinusLeft(), -1, 1) * tiltAngle; // 오른쪽이 많으면 시계 방향(-z)
        OnFlowerPlaced?.Invoke();
        return true;
    }

    public void PreviewHover(FlowerData flower, Vector2 screenPoint, Camera eventCamera)
    {
        PreviewHover(flower, new Rect(screenPoint, Vector2.zero), eventCamera);
    }

    /// <summary>드래그 중 호출: 꽃과 겹친 칸(꽃을 받을 수 있는 칸 중 가장 많이 겹친 칸)을 강조해서 보여준다.</summary>
    public void PreviewHover(FlowerData flower, Rect screenRect, Camera eventCamera)
    {
        WrapperSlot region = ResolveByOverlap(flower, screenRect, eventCamera);

        if (region == hoveredRegion) return;

        ClearHoverPreview();

        if (region != null)
        {
            region.SetHighlightAlpha(HoverAlpha);
            region.SetHover(true);
            hoveredRegion = region;
        }
    }

    /// <summary>
    /// 꽃의 화면 사각형 안에 표본점을 촘촘히 찍어 각 점이 속한 칸을 센다. 꽃을 받을 수 있는 칸만 세고, 가장 많이 겹친 칸을 돌려준다.
    /// 칸이 부채꼴이라 겹침 면적을 직접 구하는 대신 표본점으로 어림한다. 사각형 크기가 0이면 한 점으로 판정한다.
    /// </summary>
    private WrapperSlot ResolveByOverlap(FlowerData flower, Rect screenRect, Camera eventCamera)
    {
        var counts = new Dictionary<WrapperSlot, int>();
        WrapperSlot best = null;
        int bestCount = 0;

        for (int i = 0; i < OverlapSamples; i++)
        {
            for (int j = 0; j < OverlapSamples; j++)
            {
                var p = new Vector2(
                    Mathf.Lerp(screenRect.xMin, screenRect.xMax, i / (float)(OverlapSamples - 1)),
                    Mathf.Lerp(screenRect.yMin, screenRect.yMax, j / (float)(OverlapSamples - 1)));
                if (!TryGetBoardPoint(p, eventCamera, out Vector2 local)) continue;

                WrapperSlot region = ResolveRegion(local);
                if (region == null || !region.CanAccept(flower)) continue;

                counts.TryGetValue(region, out int n);
                counts[region] = ++n;
                if (n > bestCount)
                {
                    best = region;
                    bestCount = n;
                }
            }
        }

        return best;
    }

    /// <summary>드래그가 끝나면(놓았든 반려됐든) 남아있는 미리보기 강조를 지운다.</summary>
    public void ClearHoverPreview()
    {
        if (hoveredRegion != null && !hoveredRegion.IsFull)
        {
            hoveredRegion.SetHighlightAlpha(IdleAlpha);
        }
        if (hoveredRegion != null) hoveredRegion.SetHover(false);
        hoveredRegion = null;
    }

    private WrapperSlot ResolveRegion(Vector2 local)
    {
        float dist = local.magnitude;
        if (dist <= centerRadius)
        {
            return regions.FirstOrDefault(r => r.ringIndex == -1);
        }

        int ringCount = activeRings;
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

    /// <summary>오른쪽 사분면들에 채워진 꽃 개수 - 왼쪽 사분면들에 채워진 꽃 개수.</summary>
    private int RightMinusLeft()
    {
        int left = 0, right = 0;
        foreach (var r in regions)
        {
            if (r.quadrant == WrapperSlot.Quadrant.TopLeft || r.quadrant == WrapperSlot.Quadrant.BottomLeft) left += r.FilledCount;
            else if (r.quadrant == WrapperSlot.Quadrant.TopRight || r.quadrant == WrapperSlot.Quadrant.BottomRight) right += r.FilledCount;
        }
        return right - left;
    }

    /// <summary>한쪽(좌/우)에 채워진 꽃이 반대쪽보다 CollapseDiff개 이상 많은가.
    /// 완성 판정(IsComplete)이 먼저라, 프리셋 자체가 좌우 2개 이상 차이 나도 완성하는 순간에는 붕괴로 보지 않는다.
    /// 미완성인 동안에는 계속 판정한다.</summary>
    public bool IsCollapsed()
    {
        return Mathf.Abs(RightMinusLeft()) >= CollapseDiff;
    }

    /// <summary>꽃을 놓은 순서(정산에서 색 조합 점수를 계산하는 데 쓴다).</summary>
    public IReadOnlyList<FlowerData> PlacementHistory => placementHistory;

    public IReadOnlyList<WrapperSlot> AllSlots => regions;
}
