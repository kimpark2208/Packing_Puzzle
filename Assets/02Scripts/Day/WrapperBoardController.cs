using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 낮 퍼즐(포장) 보드 - 동심원 + 4분할(태그 무시 임시 버전).
/// 중앙 원 1개 + 링 여러 개(각 링은 4분할: 좌상/좌하/우상/우하)로 영역을 절차적으로 만든다.
/// 드래그해온 꽃을 태그 구분 없이 아무 빈 영역에나 놓을 수 있고, 놓으면 그 영역이 파랗게 칠해진다.
/// 맨 바깥 링은 배경 이미지의 찌그러진 윤곽선을 그대로 따르도록, 반지름 상한 대신
/// 배경 스프라이트의 실제 알파값(불투명 여부)으로 유효 범위를 판정한다.
/// </summary>
public class WrapperBoardController : MonoBehaviour
{
    public static WrapperBoardController Instance { get; private set; }

    [SerializeField] private RectTransform slotArea;
    [SerializeField] private float centerRadius = 70f;
    [SerializeField] private float ringSpacing = 100f;
    [SerializeField] private int ringCount = 3;
    [SerializeField] private float outerRingVisualRadius = 550f; // 맨 바깥 링 하이라이트용 시각적 반지름(알파 판정과 별개)
    [SerializeField] private float outlineThickness = 4f;
    [SerializeField] private Color outlineColor = new(0.15f, 0.15f, 0.17f, 1f);

    private static readonly Color HighlightColor = new(0.25f, 0.55f, 0.95f); // 파란색(채움/미리보기 공통, 투명도만 다름)
    private const float HoverAlpha = 0.35f;
    private const float FilledAlpha = 0.85f;
    private const int CollapseDiff = 2;

    private readonly List<WrapperSlot> regions = new();
    private readonly List<BlockData> placementHistory = new();
    private Transform outlineContainer;

    private Image backgroundImage;
    private Texture2D backgroundTexture;
    private WrapperSlot hoveredRegion;

    /// <summary>꽃이 영역에 배치될 때마다 발행.</summary>
    public event Action OnFlowerPlaced;

    public bool IsComplete => regions.Count > 0 && regions.All(r => r.IsFull);

    private void Awake()
    {
        Instance = this;

        // "PuzzleLim"은 찌그러진 모양대로 구멍이 뚫린 틀 이미지: 안쪽(퍼즐 영역)은 투명, 바깥은 불투명.
        // WrapperBoard 밑에서 이름으로 찾는다(SlotArea의 형제 노드).
        Transform bgT = FindDeep(transform, "PuzzleLim");
        if (bgT != null)
        {
            backgroundImage = bgT.GetComponent<Image>();
            if (backgroundImage != null && backgroundImage.sprite != null)
                backgroundTexture = backgroundImage.sprite.texture;
        }
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>항상 같은 동심원 구조(중앙 + 링 ringCount개, 각 4분할)를 만든다.</summary>
    public void BuildLevel()
    {
        placementHistory.Clear();
        foreach (var r in regions)
        {
            if (r != null) Destroy(r.gameObject);
        }
        regions.Clear();

        regions.Add(CreateCenterRegion());

        for (int ring = 0; ring < ringCount; ring++)
        {
            float inner = centerRadius + ring * ringSpacing;
            float outer = centerRadius + (ring + 1) * ringSpacing;
            bool isOuterMost = ring == ringCount - 1;
            float visualOuter = isOuterMost ? outerRingVisualRadius : outer;

            Sprite ringSprite = GenerateAnnulusSprite(inner, visualOuter);

            foreach (WrapperSlot.Quadrant q in QuadrantsInOrder)
            {
                regions.Add(CreateRingRegion(ring, q, visualOuter, ringSprite));
            }
        }

        BuildOutlines();
    }

    /// <summary>링 경계 원(중앙 원 포함, 찌그러진 맨 바깥 테두리는 배경 그림이 이미 그려주므로 제외) +
    /// 사분면을 나누는 십자선을 절차적으로 그린다.</summary>
    private void BuildOutlines()
    {
        if (outlineContainer != null) Destroy(outlineContainer.gameObject);

        var containerGO = new GameObject("Outlines", typeof(RectTransform));
        containerGO.transform.SetParent(slotArea, false);
        var containerRT = (RectTransform)containerGO.transform;
        containerRT.anchorMin = new Vector2(0.5f, 0.5f);
        containerRT.anchorMax = new Vector2(0.5f, 0.5f);
        containerRT.sizeDelta = Vector2.zero;
        containerRT.anchoredPosition = Vector2.zero;
        outlineContainer = containerGO.transform;

        // 중앙 원 경계 + 링 사이 경계(맨 바깥 찌그러진 경계는 제외)
        for (int i = 0; i < ringCount; i++)
        {
            float radius = centerRadius + i * ringSpacing;
            CreateOutlineCircle(radius);
        }

        // 사분면을 나누는 십자선
        float extent = outerRingVisualRadius;
        CreateOutlineBar(new Vector2(extent * 2f, outlineThickness));
        CreateOutlineBar(new Vector2(outlineThickness, extent * 2f));
    }

    private void CreateOutlineCircle(float radius)
    {
        var go = new GameObject($"Outline_Ring_{radius:F0}", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(outlineContainer, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(radius * 2f, radius * 2f);
        rt.anchoredPosition = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.sprite = GenerateRingOutlineSprite(radius, outlineThickness);
        img.color = outlineColor;
        img.raycastTarget = false;
    }

    private void CreateOutlineBar(Vector2 size)
    {
        var go = new GameObject("Outline_Cross", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(outlineContainer, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = outlineColor;
        img.raycastTarget = false;
    }

    private static readonly WrapperSlot.Quadrant[] QuadrantsInOrder =
    {
        WrapperSlot.Quadrant.TopRight, WrapperSlot.Quadrant.TopLeft,
        WrapperSlot.Quadrant.BottomLeft, WrapperSlot.Quadrant.BottomRight,
    };

    private WrapperSlot CreateCenterRegion()
    {
        var go = new GameObject("Region_Center", typeof(RectTransform));
        go.transform.SetParent(slotArea, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(centerRadius * 2f, centerRadius * 2f);
        rt.anchoredPosition = Vector2.zero;

        var slot = go.AddComponent<WrapperSlot>();
        slot.ringIndex = -1;
        slot.quadrant = WrapperSlot.Quadrant.Center;
        slot.requiredCount = 1;
        slot.SetHighlightSprite(GenerateCircleSprite(centerRadius), new Color(HighlightColor.r, HighlightColor.g, HighlightColor.b, 0f));
        return slot;
    }

    private WrapperSlot CreateRingRegion(int ring, WrapperSlot.Quadrant quadrant, float outerRadius, Sprite sprite)
    {
        var go = new GameObject($"Region_Ring{ring}_{quadrant}", typeof(RectTransform));
        go.transform.SetParent(slotArea, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        // 피벗은 항상 원점(텍스처의 "가상 중심" 코너)에 고정하고, 사분면 방향은 스케일 반전으로 만든다.
        // (피벗을 바꾸면 원점 자체가 이동해버려서 세 사분면이 중심에서 어긋난다.)
        rt.pivot = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(outerRadius, outerRadius);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = ScaleForQuadrant(quadrant);

        var slot = go.AddComponent<WrapperSlot>();
        slot.ringIndex = ring;
        slot.quadrant = quadrant;
        slot.requiredCount = 1;
        slot.SetHighlightSprite(sprite, new Color(HighlightColor.r, HighlightColor.g, HighlightColor.b, 0f));
        return slot;
    }

    /// <summary>피벗(원점)은 고정한 채, 텍스처가 뻗어나가는 방향만 반전시켜 4개 사분면을 만든다.
    /// 텍스처 자체는 항상 "원점에서 +x,+y로 뻗는" 우상단(TopRight) 모양 하나뿐이다.</summary>
    private static Vector3 ScaleForQuadrant(WrapperSlot.Quadrant q)
    {
        return q switch
        {
            WrapperSlot.Quadrant.TopRight => new Vector3(1f, 1f, 1f),
            WrapperSlot.Quadrant.TopLeft => new Vector3(-1f, 1f, 1f),
            WrapperSlot.Quadrant.BottomLeft => new Vector3(-1f, -1f, 1f),
            WrapperSlot.Quadrant.BottomRight => new Vector3(1f, -1f, 1f),
            _ => Vector3.one,
        };
    }

    /// <summary>붕괴 시 자리 배치는 그대로 두고 채워진 것만 전부 비운다.</summary>
    public void ClearAllPlacements()
    {
        foreach (var r in regions)
        {
            r.Clear();
            r.SetHighlightAlpha(0f);
        }
        placementHistory.Clear();
        hoveredRegion = null;
    }

    /// <summary>화면 좌표를 중심 기준 반지름/사분면으로 변환해 알맞은 영역을 찾아 배치를 시도한다(태그 무시).</summary>
    public bool TryPlaceAtScreenPoint(BlockData flower, Vector2 screenPoint, Camera eventCamera)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(slotArea, screenPoint, eventCamera, out Vector2 local))
            return false;

        WrapperSlot region = ResolveRegion(local);
        if (region == null || !region.CanAccept(flower)) return false;

        region.Fill(flower);
        region.SetHighlightAlpha(FilledAlpha);
        if (region == hoveredRegion) hoveredRegion = null;

        placementHistory.Add(flower);
        OnFlowerPlaced?.Invoke();
        return true;
    }

    /// <summary>드래그 중 호출: 현재 포인터 아래 영역을 반투명 파란색으로 미리 보여준다.</summary>
    public void PreviewHover(Vector2 screenPoint, Camera eventCamera)
    {
        WrapperSlot region = null;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(slotArea, screenPoint, eventCamera, out Vector2 local))
        {
            region = ResolveRegion(local);
        }

        if (region == hoveredRegion) return;

        ClearHoverPreview();

        if (region != null && !region.IsFull)
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
            hoveredRegion.SetHighlightAlpha(0f);
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

        for (int ring = 0; ring < ringCount; ring++)
        {
            float inner = centerRadius + ring * ringSpacing;
            float outer = centerRadius + (ring + 1) * ringSpacing;
            bool isOuterMost = ring == ringCount - 1;

            float bandOuter = isOuterMost ? outerRingVisualRadius : outer;
            bool withinBand = dist > inner && dist <= bandOuter;
            if (!withinBand) continue;

            if (isOuterMost && !IsInsidePuzzleLim(local)) return null; // 뚫린 틀(PuzzleLim) 구멍 바깥(=불투명한 부분)이면 무효

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

    /// <summary>PuzzleLim(뚫린 틀)의 "구멍" 안쪽(=투명한 부분)에 들어오는 좌표인지 확인한다.
    /// 틀은 찌그러진 퍼즐 영역 안쪽이 투명(뚫림)이고 바깥이 불투명이므로, 투명해야 유효하다.</summary>
    private bool IsInsidePuzzleLim(Vector2 local)
    {
        if (backgroundImage == null || backgroundTexture == null || backgroundImage.sprite == null) return true;

        var bgRT = (RectTransform)backgroundImage.transform;
        float u = local.x / bgRT.rect.width + 0.5f;
        float v = local.y / bgRT.rect.height + 0.5f;
        if (u < 0f || u > 1f || v < 0f || v > 1f) return false; // 틀 이미지 범위 밖 = 당연히 무효

        Rect spriteRect = backgroundImage.sprite.rect;
        int px = Mathf.Clamp(Mathf.RoundToInt(spriteRect.x + u * spriteRect.width), 0, backgroundTexture.width - 1);
        int py = Mathf.Clamp(Mathf.RoundToInt(spriteRect.y + v * spriteRect.height), 0, backgroundTexture.height - 1);
        return backgroundTexture.GetPixel(px, py).a < 0.5f; // 투명해야(뚫린 구멍 안) 유효
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

    // ===================== 절차적 텍스처 생성 =====================

    private static Sprite GenerateCircleSprite(float radius)
    {
        int size = Mathf.Max(2, Mathf.CeilToInt(radius * 2f));
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var pixels = new Color32[size * size];
        Vector2 center = new(size / 2f, size / 2f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                pixels[y * size + x] = d <= radius ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    /// <summary>한 변이 outer인 정사각형 안에, 원점(코너)을 중심으로 한 1사분면 링 밴드(inner~outer)를 그린다.
    /// 4개 사분면 조각이 전부 같은 모양을 피벗만 바꿔 재사용한다.</summary>
    private static Sprite GenerateAnnulusSprite(float inner, float outer)
    {
        int size = Mathf.Max(2, Mathf.CeilToInt(outer));
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.zero);
                bool inside = d > inner && d <= outer;
                pixels[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0f, 0f));
    }

    /// <summary>지름 2*radius인 정사각형 중앙에, radius 부근에서만 두께 thickness만큼 불투명한 얇은 원(테두리 선)을 그린다.</summary>
    private static Sprite GenerateRingOutlineSprite(float radius, float thickness)
    {
        int size = Mathf.Max(2, Mathf.CeilToInt(radius * 2f));
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var pixels = new Color32[size * size];
        Vector2 center = new(size / 2f, size / 2f);
        float half = thickness / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                bool onLine = d >= radius - half && d <= radius + half;
                pixels[y * size + x] = onLine ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
