using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 꽃 선정 화면 왼쪽의 수첩: 주문한 꽃, 역할 태그 버튼 4개, 이번 꽃다발의 포장지 프리셋(예상 꽃다발) 그림.
/// 프리셋 그림은 낮 퍼즐 보드와 같은 칸 그림(링 4조각씩)을 쓰고, 칸마다 역할 색으로 칠한다. 고른 태그의 칸은 밝게, 나머지는 흐리게 보인다.
/// 채운 칸에는 퍼즐에서 그 칸에 꽃을 놓았을 때와 같은 꽃 그림(링에 맞는 그림)이 뜬다. 채운 칸을 누르면 그 칸이 비워진다(RemoveClicked).
/// 칸 오브젝트(Board와 그 안의 Flowers)는 에디터에 미리 배치해 두고, 이름은 WrapperRegionPosition과 같다. 이 컴포넌트는 수첩 종이(Image)에 붙어 있어야 누름을 받는다.
/// 선택 규칙(FlowerSelection)은 모르고 보여 주기만 한다. 태그와 칸을 누른 것은 이벤트로 알린다.
/// </summary>
public class FlowerNotebook : MonoBehaviour, IPointerClickHandler
{
    [Serializable]
    private class TagButton
    {
        public FlowerData.FlowerRole role;
        public Button button;
        public TMP_Text label;
        public Image background;
    }

    private const float CenterRadius = 70f;                         // 퍼즐 보드의 중앙 칸 반지름(칸 그림 140)
    private const float CellUnitsPerPixel = 0.5f;                   // 칸 그림 1픽셀 = 보드 0.5단위(퍼즐 보드와 같다)
    private static readonly float[] RingOuterRadius = { 170f, 270f, 370f }; // 1~3링 칸의 바깥 반지름(퍼즐 보드의 칸 크기). 2링 포장지의 맨 바깥 링은 바깥 그림 크기를 쓴다
    private const float IdleAlpha = 0.45f, ActiveAlpha = 0.95f;
    private static readonly Color ChipOn = new(0.6f, 0.6f, 0.6f), ChipOff = new(1f, 1f, 1f), ChipDisabled = new(0.8f, 0.8f, 0.8f);
    private static readonly Color TextDark = new(0.25f, 0.25f, 0.25f);

    [SerializeField] private TMP_Text orderText;
    [SerializeField] private TagButton[] tags;

    [Header("예상 꽃다발: 칸 그림(Board, 안의 Flowers에 칸마다 꽃 그림). 자식 이름은 모두 칸 이름(A_1Ring_Upper …)이다")]
    [SerializeField] private RectTransform board;
    [SerializeField] private Sprite ring2InnerSprite; // 3링 포장지의 2링 칸 그림
    [SerializeField] private Sprite ring2OuterSprite; // 2링 포장지의 맨 바깥 링 칸 그림
    [SerializeField, Min(50f), Tooltip("Board의 크기가 0일 때만 쓴다: 프리셋 그림의 맨 바깥 반지름이 이만큼 되도록 맞춘다.")] private float fitRadius = 170f;

    /// <summary>태그 버튼을 눌렀다.</summary>
    public event Action<FlowerData.FlowerRole> TagClicked;

    /// <summary>채운 칸을 눌렀다(칸 번호): 그 칸을 비우라는 뜻이다.</summary>
    public event Action<int> RemoveClicked;

    private static readonly int CellTotal = Enum.GetValues(typeof(WrapperRegionPosition)).Length;
    private readonly Image[] cells = new Image[CellTotal];
    private readonly Image[] flowerImages = new Image[CellTotal];
    private readonly bool[] removable = new bool[CellTotal]; // 채웠고 비울 수 있는 칸(예약 필수 꽃의 칸은 비울 수 없다)
    private int shownRings;        // 지금 그려진 프리셋의 링 수
    private float shownRing2Outer; // 그 프리셋에서 2링 칸의 바깥 반지름
    private bool wired;

    /// <summary>씬에 놓인 칸을 이름으로 찾아 두고 태그 버튼을 잇는다(한 번만).</summary>
    private void Wire()
    {
        if (wired) return;
        wired = true;

        for (int i = 0; i < cells.Length; i++)
        {
            string cellName = ((WrapperRegionPosition)i).ToString();
            Transform cell = board.Find(cellName);
            if (cell != null) cells[i] = cell.GetComponent<Image>();
            Transform flower = board.Find("Flowers/" + cellName); // 퍼즐 보드의 꽃 이미지와 같은 자리·크기·회전(Board 안이라 Board와 함께 크기가 맞춰진다)
            if (flower != null) flowerImages[i] = flower.GetComponent<Image>();
        }

        foreach (TagButton tag in tags)
        {
            FlowerData.FlowerRole role = tag.role;
            tag.button.onClick.AddListener(() => TagClicked?.Invoke(role));
        }
    }

    /// <summary>이번 꽃다발의 프리셋을 그린다: 프리셋에 있는 칸만 켜서 역할 색으로 칠하고, 링 수에 맞게 칸 그림을 맞춘다. 주문한 꽃 이름도 적는다.</summary>
    public void Show(WrapperPresetData preset, string orderedFlowers)
    {
        Wire();
        if (orderText != null) orderText.text = string.IsNullOrEmpty(orderedFlowers) ? "" : $"주문: {orderedFlowers}";

        int rings = preset != null ? preset.MaxRing : 0;
        float ring2Outer = rings >= 3 ? RingOuterRadius[1] : ring2OuterSprite != null ? ring2OuterSprite.rect.size.x * CellUnitsPerPixel : RingOuterRadius[1];
        float outerMost = rings >= 3 ? RingOuterRadius[2] : ring2Outer;
        board.localScale = Vector3.one * (FitRadius / Mathf.Max(1f, outerMost)); // 링이 Board 안에 꼭 맞게(링 수가 달라도 같은 크기로 보인다)
        shownRings = rings;
        shownRing2Outer = ring2Outer;

        for (int i = 0; i < cells.Length; i++)
        {
            bool inPreset = preset != null && i < preset.cellRoles.Length;
            removable[i] = false;
            if (cells[i] != null) cells[i].gameObject.SetActive(inPreset);
            if (flowerImages[i] != null) flowerImages[i].gameObject.SetActive(false); // 채운 칸만 Refresh가 켠다
            if (!inPreset) continue;

            if (((WrapperRegionPosition)i).Ring() == 2 && cells[i] != null)
            {
                Sprite sprite = rings >= 3 ? ring2InnerSprite : ring2OuterSprite; // 퍼즐 보드와 같다: 맨 바깥 링이면 바깥 그림, 안쪽 링이면 안쪽 그림
                if (sprite != null)
                {
                    cells[i].sprite = sprite;
                    cells[i].rectTransform.sizeDelta = sprite.rect.size * CellUnitsPerPixel;
                }
            }

            Color color = ColorPalette.ToRoleColor(preset.RoleAt(i));
            color.a = IdleAlpha;
            if (cells[i] != null) cells[i].color = color;
        }
    }

    /// <summary>링이 들어갈 반지름: Board(에디터에서 정한 사각형) 안에 들어가도록 짧은 쪽의 절반. Board의 크기가 0이면 fitRadius.</summary>
    private float FitRadius => board.rect.width > 1f && board.rect.height > 1f ? Mathf.Min(board.rect.width, board.rect.height) * 0.5f : fitRadius;

    /// <summary>수첩을 눌렀다: 링 위의 채운 칸이면 그 칸을 비우라고 알린다. 칸은 누른 자리의 반지름(어느 링)과 방향(어느 사분면)으로 찾는다.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (board == null || shownRings == 0) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(board, eventData.position, eventData.pressEventCamera, out Vector2 local)) return;

        int cell = CellAt(local - board.rect.center);
        if (cell >= 0 && removable[cell]) RemoveClicked?.Invoke(cell);
    }

    /// <summary>링 중심 기준 좌표(보드 단위)가 어느 칸인지(링 밖이면 -1).</summary>
    private int CellAt(Vector2 point)
    {
        float radius = point.magnitude;
        if (radius <= CenterRadius) return 0;

        float[] outers = shownRings >= 3 ? new[] { RingOuterRadius[0], shownRing2Outer, RingOuterRadius[2] } : new[] { RingOuterRadius[0], shownRing2Outer };
        int ring = 0;
        for (int k = 0; k < outers.Length; k++)
        {
            if (radius <= outers[k])
            {
                ring = k + 1;
                break;
            }
        }
        if (ring == 0) return -1;

        bool faceA = point.x < 0f; // A면 = 왼쪽, B면 = 오른쪽
        bool upper = point.y >= 0f;
        return 1 + (ring - 1) * 4 + (faceA ? 0 : 2) + (upper ? 0 : 1);
    }

#if UNITY_EDITOR
    /// <summary>에디터에서 모습을 확인한다: 3링 프리셋을 그린 상태로 맞춘다(플레이하면 고른 포장지의 프리셋으로 다시 그려진다). 컴포넌트 메뉴(⋮)에서 부른다.</summary>
    [ContextMenu("에디터 미리보기 (3링 프리셋)")]
    private void EditorPreview()
    {
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("Preset_3_1 t:WrapperPresetData"))
        {
            WrapperPresetData preset = UnityEditor.AssetDatabase.LoadAssetAtPath<WrapperPresetData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            Show(preset, "금어초, 스토크");

            // 꽃이 채워진 모습도 보이게 한다(칸마다 그 링의 꽃 그림). 플레이하면 고른 꽃으로 다시 그려진다.
            foreach (string flowerGuid in UnityEditor.AssetDatabase.FindAssets("t:FlowerData"))
            {
                FlowerData sample = UnityEditor.AssetDatabase.LoadAssetAtPath<FlowerData>(UnityEditor.AssetDatabase.GUIDToAssetPath(flowerGuid));
                for (int i = 0; i < preset.cellRoles.Length; i++)
                {
                    if (flowerImages[i] == null) continue;

                    Sprite sprite = sample.RingSprite(((WrapperRegionPosition)i).Ring());
                    if (sprite != null) flowerImages[i].sprite = sprite;
                    flowerImages[i].color = ColorPalette.ToUnityColor(sample.color);
                    flowerImages[i].gameObject.SetActive(true);
                }
                break;
            }
            UnityEditor.EditorUtility.SetDirty(gameObject);
            return;
        }
    }
#endif

    /// <summary>선택 상태를 보여 준다: 채운 칸의 꽃 그림, 고른 태그의 칸 강조, 태그 버튼의 채운 칸 수. locked 역할의 칸은 눌러서 비울 수 없다(예약 필수 꽃).</summary>
    public void Refresh(FlowerSelection selection, FlowerData.FlowerRole activeRole, Func<FlowerData.FlowerRole, bool> isLocked)
    {
        Wire();
        for (int i = 0; i < selection.CellCount; i++)
        {
            FlowerData flower = selection.At(i);
            FlowerData.FlowerRole role = selection.RoleAt(i);
            if (cells[i] != null)
            {
                Color color = cells[i].color;
                color.a = role == activeRole ? ActiveAlpha : IdleAlpha;
                cells[i].color = color;
            }
            if (flowerImages[i] != null)
            {
                flowerImages[i].gameObject.SetActive(flower != null);
                if (flower != null)
                {
                    Sprite sprite = flower.RingSprite(((WrapperRegionPosition)i).Ring()); // 퍼즐에서 그 칸에 놓았을 때와 같은 그림(링에 맞는 그림)을 꽃 색으로 칠한다
                    if (sprite != null) flowerImages[i].sprite = sprite;
                    flowerImages[i].color = ColorPalette.ToUnityColor(flower.color);
                }
            }
            removable[i] = flower != null && !isLocked(role);
        }

        foreach (TagButton tag in tags)
        {
            int total = selection.CellsOf(tag.role);
            bool locked = isLocked(tag.role);
            tag.button.gameObject.SetActive(total > 0); // 이 포장지에 칸이 없는 태그는 숨긴다
            tag.button.interactable = !locked;
            if (tag.label != null)
            {
                tag.label.text = RoleName(tag.role);
                tag.label.color = TextDark;
            }
            if (tag.background != null) tag.background.color = locked ? ChipDisabled : tag.role == activeRole ? ChipOn : ChipOff;
        }
    }

    private static string RoleName(FlowerData.FlowerRole role) => role switch
    {
        FlowerData.FlowerRole.Line => "라인",
        FlowerData.FlowerRole.Mass => "매스",
        FlowerData.FlowerRole.Form => "폼",
        _ => "필러",
    };
}
