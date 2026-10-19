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
/// 2링과 3링 포장지를 지원한다. 4링 포장지는 에디터에 4링 칸을 놓고 SupportedRings를 올려야 한다.
/// 판정은 반지름·사분면 수식이라 칸 이미지와 따로 논다(칸 이미지를 바꾸면 centerRadius, ringSpacing도 맞출 것).
/// </summary>
public class DayPuzzleController : MonoBehaviour
{
    public static DayPuzzleController Instance { get; private set; }

    /// <summary>에디터에 칸 오브젝트가 배치된 링 수. 3·4링 칸을 씬에 배치하면 이 값을 올린다(포장지는 최대 4링).</summary>
    public const int SupportedRings = 3;

    [SerializeField] private RectTransform slotArea;    [SerializeField] private RectTransform flowerArea; // 칸과 같은 이름의 꽃 이미지를 두는 곳 (SlotArea와 같은 위치/크기)
    [SerializeField] private float centerRadius = 70f;
    [SerializeField] private float ringSpacing = 100f;
    [Header("씬 단독 실행용 기본 포장지/프리셋 (주문이 확정돼 있으면 주문의 프리셋을 쓴다)")]
    [SerializeField] private WrapperData wrapper;
    [SerializeField] private int presetIndex;
    [SerializeField] private float outerRingVisualRadius = 550f; // 2링 포장지의 맨 바깥 링의 바깥 반지름(판정용)
    [SerializeField, Tooltip("3링 포장지의 맨 바깥 링(3링 칸)의 바깥 반지름(판정용). 3링 칸 그림 740px의 반지름 370.")] private float threeRingOuterVisualRadius = 370f;

    [Header("링 칸 그림: 2링 포장지는 2링 칸이 맨 바깥(바깥 그림)이고, 3링 포장지는 2링 칸이 안쪽 그림으로 바뀌며 3링 칸이 맨 바깥이 된다")]
    [SerializeField] private Sprite ring2InnerSprite; // Cell_2Ring
    [SerializeField] private Sprite ring2OuterSprite; // Cell_2RingOuter
    [Header("3링 포장지 크기: 3링 칸이 커서 칸과 그 위에 올라가는 것(꽃, 글로우, 데코)은 줄이고, 포장지 이미지는 늘린다. 2링 포장지는 모두 1배다")]
    [SerializeField, Tooltip("보이는 포장지 이미지(WrapperArea의 자식 WrapperImage).")] private RectTransform wrapperArt;
    [SerializeField, Tooltip("칸과 같이 크기가 바뀌는 영역들: SlotArea, FlowerArea, GlowArea, LeafArea, RibbonArea.")] private RectTransform[] scaledWithSlots;
    [SerializeField, Range(0.5f, 1f)] private float threeRingSlotScale = 0.95f;
    [SerializeField, Range(1f, 1.5f)] private float threeRingWrapperScale = 1.1f;

    [Header("FlowerArea의 꽃 이미지에 채울 태그별 이미지 (FlowerRole 순서: Line, Mass, Form, Filler)")]
    [SerializeField] private Sprite[] tagSprites;

    // 태그 색: 라인=빨강, 매스=파랑, 폼=핑크, 필러=노랑. 비어있음/미리보기/채움은 투명도로만 구분한다.
    private const float IdleAlpha = 0.45f;
    private const float HoverAlpha = 0.90f;
    private const float FilledAlpha = 0f; // 꽃이 놓인 칸은 뒤의 셀 배경을 숨긴다
    private const int CollapseDiff = 2;
    private const int OverlapSamples = 5; // 꽃 사각형 한 변에 찍는 표본점 수(칸과의 겹침을 어림하는 데 쓴다)
    private const float CellUnitsPerPixel = 0.5f; // 칸 그림 1픽셀 = 캔버스 0.5단위(1링 칸 그림 340px = 칸 170)

    [Header("꽃의 빛: 흐림은 더 투명하게, 빛남은 꽃 뒤의 글로우 이미지(GlowArea)를 켠다. 글로우 이미지는 꽃과 같은 그림을 쓰고 셰이더(UI/FlowerGlow)가 번지게 한다")]
    [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.4f;
    [SerializeField] private RectTransform glowArea; // 칸과 같은 이름의 글로우 이미지를 두는 곳(FlowerArea와 같은 위치/크기이고 꽃보다 먼저 그려진다). 없으면 후광 없이 동작한다

    [Header("특수 칸: 퍼즐을 만든 뒤 종류마다 확률로 만들지 정하고, 만들면 칸 하나를 무작위로 고른다(별 칸이 먼저, 그늘 칸은 별 칸이 아닌 칸). 붕괴 후 다시 만들 때는 같은 칸을 쓴다. 특수 칸은 꽃을 놓아도 사라지지 않고, 그늘 칸만 둘레의 꽃이 빛나면 일반 칸이 된다")]
    [SerializeField, Range(0f, 1f)] private float starChance = 0.3f;
    [SerializeField, Range(0f, 1f)] private float shadeChance = 0.3f;
    [SerializeField] private Material starMaterial;  // 별 칸 이미지에 쓸 머티리얼(UI/CellStar)
    [SerializeField] private Material shadeMaterial; // 그늘 칸 이미지에 쓸 머티리얼(UI/CellShade)

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

    /// <summary>포장지의 모든 칸을 켜고 프리셋이 정한 역할의 색과 꽃 이미지를 채운다. 포장지 링 수보다 바깥에 있는 칸은 끈다.
    /// 처음 만들 때는 특수 칸도 새로 정하고, 붕괴로 다시 만들 때(rebuild)는 원래 특수 칸을 그대로 쓴다.</summary>
    public void BuildLevel(bool rebuild = false)
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
            Image glowImage = GlowImageOf(slot);
            if (glowImage != null) glowImage.gameObject.SetActive(false);     // 빛남일 때만 켠다
            int i = (int)slot.position;
            bool inWrapper = i < preset.cellRoles.Length; // 이 포장지의 링 수보다 바깥 칸만 없다. 포장지 안의 칸은 모두 쓰인다(빈 칸 없음)
            slot.gameObject.SetActive(inWrapper);
            if (!inWrapper) continue;

            ApplyRingSkin(slot);
            slot.Clear();
            slot.tag = preset.cellRoles[i];
            slot.SetHighlightColor(TagColor(slot.tag));
            if (flowerImage != null) flowerImage.GetComponent<Image>().sprite = tagSprites[(int)slot.tag];
            if (glowImage != null) glowImage.sprite = tagSprites[(int)slot.tag]; // 후광은 꽃 그림을 번지게 해서 그린다
            regions.Add(slot);
        }

        ApplyBoardScale();

        if (rebuild)
        {
            foreach (WrapperSlot slot in regions) slot.SetSpecialVisible(true); // 사라졌던 그늘도 다시 생긴다
        }
        else
        {
            ApplySpecialCells();
        }
    }

    /// <summary>3링 포장지는 칸과 그 위의 것(꽃, 글로우, 데코)을 줄이고 포장지 이미지를 늘린다. 2링 포장지는 모두 1배(에디터에는 3링 상태를 보며 작업해 둔다).</summary>
    private void ApplyBoardScale()
    {
        bool three = activeRings >= 3;
        if (wrapperArt != null) wrapperArt.localScale = Vector3.one * (three ? threeRingWrapperScale : 1f);
        if (scaledWithSlots == null) return;

        foreach (RectTransform area in scaledWithSlots)
        {
            if (area != null) area.localScale = Vector3.one * (three ? threeRingSlotScale : 1f);
        }
    }

    /// <summary>2링 칸은 맨 바깥 링이면 바깥 그림, 안쪽 링이면(3링 포장지) 안쪽 그림을 쓴다. 칸 크기는 그림 크기를 따른다.</summary>
    private void ApplyRingSkin(WrapperSlot slot)
    {
        if (slot.ringIndex != 1 || ring2InnerSprite == null || ring2OuterSprite == null) return;

        Sprite sprite = activeRings >= 3 ? ring2InnerSprite : ring2OuterSprite;
        slot.GetComponent<Image>().sprite = sprite;
        ((RectTransform)slot.transform).sizeDelta = sprite.rect.size * CellUnitsPerPixel;
    }

    /// <summary>퍼즐이 만들어진 뒤 특수 칸을 정해 칸 이미지에 효과 머티리얼을 입힌다.</summary>
    private void ApplySpecialCells()
    {
        var rolled = SpecialCellRoller.Roll(regions.Select(r => r.position), starChance, shadeChance);

        foreach (WrapperSlot slot in regions)
        {
            rolled.TryGetValue(slot.position, out SpecialCell kind);
            Material material = kind == SpecialCell.Star ? starMaterial : kind == SpecialCell.Shade ? shadeMaterial : null;
            slot.SetSpecial(material != null ? kind : SpecialCell.None, material); // 머티리얼이 없으면 일반 칸으로 둔다
        }
    }

    /// <summary>이번 퍼즐에 쓸 프리셋. 주문이 확정돼 있으면 그 프리셋을, 아니면(씬 단독 실행 등) 인스펙터 기본값을 쓴다.</summary>
    private WrapperPresetData CurrentPreset()
    {
        DayPuzzleGenerator.DayOrder order = GameManager.Instance != null ? GameManager.Instance.CurrentDayOrder : null;
        if (order != null && order.preset != null)
        {
            activeRings = Mathf.Min(order.preset.MaxRing, SupportedRings); // 포장지가 2링·3링 프리셋을 함께 가질 수 있어서 프리셋의 링 수를 따른다
            TintWrapper(order.wrapper);
            return order.preset;
        }

        if (wrapper == null || wrapper.presets.Count == 0) return null;
        WrapperPresetData standalone = wrapper.presets[Mathf.Clamp(presetIndex, 0, wrapper.presets.Count - 1)];
        activeRings = Mathf.Min(standalone.MaxRing, SupportedRings);
        TintWrapper(wrapper);
        return standalone;
    }

    /// <summary>포장지 이미지를 고른 포장지의 색으로 물들인다. 꾸미기에서 포장지 그림을 바꿔도 이미지의 색은 그대로라서 같은 색으로 보인다.</summary>
    private void TintWrapper(WrapperData data)
    {
        if (wrapperImage != null && wrapperImage.TryGetComponent(out Image image)) image.color = data.color;
        if (wrapperArt != null && wrapperArt.TryGetComponent(out Image art)) art.color = data.color; // 보이는 포장지 그림은 WrapperArea의 자식이다
    }

    /// <summary>꽃 이미지는 FlowerArea 아래에 칸과 같은 이름(= position)으로 둔다.</summary>
    private Transform FlowerImageOf(WrapperSlot slot)
    {
        return flowerArea.Find(slot.position.ToString());
    }

    /// <summary>글로우 이미지도 GlowArea 아래에 칸과 같은 이름으로 둔다(없으면 null).</summary>
    private Image GlowImageOf(WrapperSlot slot)
    {
        Transform t = glowArea != null ? glowArea.Find(slot.position.ToString()) : null;
        return t != null ? t.GetComponent<Image>() : null;
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

    /// <summary>꽃이 화면에서 차지하는 사각형과 겹치는 칸 중 꽃을 받을 수 있는 칸에 놓는다(가장 많이 겹친 칸).</summary>
    public bool TryPlaceAtScreenRect(FlowerData flower, Rect screenRect, Camera eventCamera)
    {
        WrapperSlot region = ResolveByOverlap(flower, screenRect, eventCamera);
        if (region == null) return false;

        region.Fill(flower);
        region.SetHighlightAlpha(region.special == SpecialCell.None ? FilledAlpha : IdleAlpha); // 특수 칸은 꽃을 놓아도 칸이 남는다
        Transform flowerImage = FlowerImageOf(region);
        if (flowerImage != null)
        {
            flowerImage.GetComponent<Image>().color = ColorPalette.ToUnityColor(flower.color); // 칸 이미지가 흰색이라 놓은 꽃의 색으로 물들인다
            flowerImage.gameObject.SetActive(true);
        }
        region.SetHover(false);
        if (region == hoveredRegion) hoveredRegion = null;

        placementHistory.Add(flower);
        RefreshGlow();
        tiltTarget = -Mathf.Clamp(RightMinusLeft(), -1, 1) * tiltAngle; // 오른쪽이 많으면 시계 방향(-z)
        OnFlowerPlaced?.Invoke();
        return true;
    }

    /// <summary>놓인 꽃마다 최종 상태(이웃과의 관계 합계에 특수 칸 보정을 더한 것)를 다시 구해 보여 준다: 흐림=투명하게, 선명=그대로, 빛남=꽃 뒤에 후광. 꽃을 놓을 때마다 이웃의 상태도 바뀐다.
    /// 둘레의 꽃이 빛나서 효과가 사라진 그늘 칸은 일반 칸 모습(꽃이 놓인 칸처럼 사라짐)으로 바꾼다.
    /// showDim이 false면 흐림 꽃도 선명하게 보여 준다(꽃다발이 완성됐을 때. 빛남의 후광과 특수 칸은 그대로다).</summary>
    public void RefreshGlow(bool showDim = true)
    {
        var (glows, clearedShades) = FlowerLight.Resolve(PlacedCells(), SpecialCells());
        foreach (WrapperSlot region in regions)
        {
            if (!region.IsFull) continue;

            if (region.special == SpecialCell.Shade)
            {
                bool cleared = clearedShades.Contains(region.position);
                region.SetSpecialVisible(!cleared);
                region.SetHighlightAlpha(cleared ? FilledAlpha : IdleAlpha);
            }

            Transform flowerImage = FlowerImageOf(region);
            if (flowerImage == null) continue;

            FlowerGlow glow = glows[region.position];
            Image image = flowerImage.GetComponent<Image>();
            Color c = image.color;
            c.a = glow == FlowerGlow.Dim && showDim ? dimAlpha : 1f;
            image.color = c;

            Image glowImage = GlowImageOf(region);
            if (glowImage != null) glowImage.gameObject.SetActive(glow == FlowerGlow.Bright);
        }
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

            float bandOuter = isOuterMost ? (ringCount >= 3 ? threeRingOuterVisualRadius : outerRingVisualRadius) : outer;
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

    /// <summary>꽃을 놓은 순서(소모할 꽃을 세는 데 쓴다).</summary>
    public IReadOnlyList<FlowerData> PlacementHistory => placementHistory;

    /// <summary>꽃이 놓인 칸과 그 꽃의 색(정산의 '꽃의 빛'이 이웃 칸과의 관계를 계산하는 데 쓴다).</summary>
    public Dictionary<WrapperRegionPosition, FlowerData.Color> PlacedCells()
    {
        return regions.Where(r => r.IsFull).ToDictionary(r => r.position, r => r.PlacedFlowers[0].color);
    }

    /// <summary>이번 퍼즐의 특수 칸(별 칸/그늘 칸). 특수가 아닌 칸은 없다.</summary>
    public Dictionary<WrapperRegionPosition, SpecialCell> SpecialCells()
    {
        return regions.Where(r => r.special != SpecialCell.None).ToDictionary(r => r.position, r => r.special);
    }

    public IReadOnlyList<WrapperSlot> AllSlots => regions;

#if UNITY_EDITOR
    /// <summary>Play 없이 포장지 보드의 모습을 확인하는 에디터 미리보기(Tools/Day/Ring Preview). 프리셋의 칸만 켜서 태그 색으로 칠하고, 링 수에 맞는 크기와 칸 그림을 적용한다.
    /// ringSprites는 중앙, 1링, 2링, 3링 순서로 칸 그림을 바꿔 본다(null이면 씬의 그림 그대로). 저장하지 않는 미리보기라서 씬을 저장하면 안 된다.</summary>
    public void EditorPreview(WrapperData data, WrapperPresetData preset, Sprite[] ringSprites, bool showFlowers)
    {
        activeRings = Mathf.Min(preset.MaxRing, SupportedRings);
        TintWrapper(data);
        ApplyBoardScale();

        foreach (WrapperSlot slot in slotArea.GetComponentsInChildren<WrapperSlot>(true))
        {
            int i = (int)slot.position;
            bool inWrapper = i < preset.cellRoles.Length;
            slot.gameObject.SetActive(inWrapper);
            Transform flowerImage = FlowerImageOf(slot);
            Image glowImage = GlowImageOf(slot);
            if (glowImage != null) glowImage.gameObject.SetActive(false);
            if (flowerImage != null) flowerImage.gameObject.SetActive(inWrapper && showFlowers);
            if (!inWrapper) continue;

            ApplyRingSkin(slot);
            var image = slot.GetComponent<Image>();
            int ring = slot.ringIndex + 1; // 0 = 중앙
            if (ringSprites != null && ring < ringSprites.Length && ringSprites[ring] != null)
            {
                image.sprite = ringSprites[ring];
                ((RectTransform)slot.transform).sizeDelta = ringSprites[ring].rect.size * CellUnitsPerPixel;
            }

            FlowerData.FlowerRole role = preset.cellRoles[i];
            image.color = TagColor(role);
            if (flowerImage != null && showFlowers && tagSprites != null && (int)role < tagSprites.Length)
            {
                var flower = flowerImage.GetComponent<Image>();
                flower.sprite = tagSprites[(int)role];
                flower.color = Color.white;
            }
        }
    }
#endif
}
