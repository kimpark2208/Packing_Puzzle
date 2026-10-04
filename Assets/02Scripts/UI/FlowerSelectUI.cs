using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 꽃 선택 화면. "포장지 선택 -> 꽃 선택" 순서를 그대로 따른다.
/// 1단계: WrapperPopup에서 보유한 포장지 중 이번 주문에 쓸 것을 하나 고른다 (목표 점수/정답 레시피가 이때 확정됨).
/// 2단계: 포장지를 고르면 FlowerPopup(역할별 선반 4개)으로 전환되고, 역할(라인/폼/매스/필러)마다
/// 정해진 개수까지 꽃을 담아 가챠 풀로 만든다. 같은 꽃을 여러 번 담으면 풀에 그만큼 중복되어 더 자주 나온다.
/// 포장지 목록과 꽃 선택 슬롯은 모두 개수가 가변적이라 템플릿(프리팹)을 복제해서 만든다.
/// </summary>
public class FlowerSelectUI : MonoBehaviour
{
    [Serializable]
    private class Shelf
    {
        public FlowerData.FlowerRole role;
        public TMP_Text label;
        public RectTransform slotParent;
    }

    private class SlotView
    {
        public Image icon;
        public TMP_Text badge;
    }

    private static readonly Color SelectedColor = new(0.65f, 0.85f, 0.70f);
    private const float SelectedLift = 30f; // 담긴 꽃이 화병에서 살짝 올라오는 높이


    [SerializeField] private TMP_Text headerText;

    [Header("포장지 선택 팝업")]
    [SerializeField] private GameObject wrapperPopup;
    [SerializeField] private RectTransform wrapperArea;
    [SerializeField] private RectTransform wrapperItemTemplate;

    [Header("꽃 선택 팝업 (역할별 선반, 슬롯은 템플릿을 복제해서 생성)")]
    [SerializeField] private GameObject flowerPopup;
    [SerializeField] private RectTransform slotPrefab;
    [SerializeField] private Shelf[] shelves;

    private readonly Dictionary<FlowerData, int> counts = new();
    private readonly Dictionary<FlowerData, SlotView> views = new();
    private readonly Dictionary<FlowerData.FlowerRole, int> limits = new(); // 역할별로 담아야 하는 개수(= 고른 프리셋의 속성별 칸 수)
    private bool wrapperChosen;

    private void Start()
    {
        if (wrapperItemTemplate != null) wrapperItemTemplate.gameObject.SetActive(false);
        if (wrapperPopup != null) wrapperPopup.SetActive(true);
        if (flowerPopup != null) flowerPopup.SetActive(false);
        ClearSlots(); // 에디터에서 배치 확인용으로 미리 넣어둔 슬롯은 시작할 때 지운다

        RefreshLabels();
        BuildWrapperChoices();
    }

    private void BuildWrapperChoices()
    {
        if (CurrencyManager.Instance == null || ShopManager.Instance == null) return;
        if (wrapperArea == null || wrapperItemTemplate == null) return;

        var owned = CurrencyManager.Instance.OwnedWrappers;
        foreach (int wrapperId in owned)
        {
            var item = ShopManager.Instance.GetItemById(wrapperId);
            string label = item.HasValue ? item.Value.itemName : $"포장지 {wrapperId}";

            // 손님이 말하는 포장지 색과 같은 색/이름을 보여준다(WrapperData의 색)
            WrapperData data = WrapperRegistry.Instance != null ? WrapperRegistry.Instance.GetById(wrapperId) : null;
            if (data != null) label = $"{data.colorName}색 {label}";

            RectTransform itemRT = Instantiate(wrapperItemTemplate, wrapperArea);
            itemRT.gameObject.SetActive(true);
            itemRT.name = $"Wrapper_{wrapperId}";

            var img = itemRT.Find("WrapperImage") != null ? itemRT.Find("WrapperImage").GetComponent<Image>() : null;
            if (img != null && data != null) img.color = data.color;

            var confirmBtnT = itemRT.Find("ConfirmBTN");
            var confirmBtn = confirmBtnT != null ? confirmBtnT.GetComponent<Button>() : null;
            var confirmLabel = confirmBtn != null ? confirmBtn.GetComponentInChildren<TMP_Text>() : null;
            if (confirmLabel != null) confirmLabel.text = label;

            int capturedId = wrapperId;
            if (confirmBtn != null) confirmBtn.onClick.AddListener(() => OnWrapperChosen(capturedId));
        }
    }

    private void OnWrapperChosen(int wrapperId)
    {
        if (GameFlowController.Instance == null) return;

        GameFlowController.Instance.ChooseWrapper(wrapperId);
        wrapperChosen = true;

        var order = GameFlowController.Instance.CurrentDayOrder;
        if (order == null) return;

        if (headerText != null)
        {
            headerText.text = "역할별로 사용할 꽃을 고르세요";
        }

        if (wrapperPopup != null) wrapperPopup.SetActive(false);
        if (flowerPopup != null) flowerPopup.SetActive(true);

        LoadRoleLimits(order);
        BuildFlowerChoices();
    }

    /// <summary>고른 프리셋의 속성별 칸 수가 역할별로 담아야 하는 꽃 개수다. 모두 채우면 바로 퍼즐로 넘어간다.</summary>
    private void LoadRoleLimits(DayPuzzleGenerator.DayOrder order)
    {
        limits.Clear();
        WrapperData data = WrapperRegistry.Instance != null ? WrapperRegistry.Instance.GetById(order.wrapperId) : null;
        bool valid = data != null && order.presetIndex >= 0 && order.presetIndex < data.presets.Count;
        foreach (var shelf in shelves) limits[shelf.role] = valid ? data.presets[order.presetIndex].CountByTag(shelf.role) : 0;
    }

    private int Limit(FlowerData.FlowerRole role)
    {
        return limits.TryGetValue(role, out int v) ? v : 0;
    }

    /// <summary>포장지 단계/보유 여부와 상관없이 등록된 모든 꽃을 후보로 보여준다.</summary>
    private void BuildFlowerChoices()
    {
        views.Clear();
        counts.Clear();
        RefreshLabels();

        if (BlockRegistry.Instance == null) return;

        ClearSlots();

        foreach (var shelf in shelves)
        {
            foreach (var flower in BlockRegistry.Instance.AllBlocks.Where(b => b.flowerRole == shelf.role))
            {
                RectTransform slot = Instantiate(slotPrefab, shelf.slotParent);
                slot.name = $"Slot_{flower.blockID}";
                BindFlowerSlot(slot, flower);
            }
        }
    }

    private void ClearSlots()
    {
        foreach (var shelf in shelves)
        {
            if (shelf.slotParent == null) continue;
            foreach (Transform old in shelf.slotParent) Destroy(old.gameObject);
        }
    }

    // 슬롯 프리셋마다 자식 위치가 달라(화병은 Button이 VaseImage에, ColorMarker가 그 아래) 이름으로 깊이 찾는다.
    private static Transform FindIn(RectTransform slot, string childName)
    {
        return slot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == childName);
    }

    private void BindFlowerSlot(RectTransform slot, FlowerData block)
    {
        // 줄기 꽃 이미지(stemSprite)가 비어있으면 프리팹의 기본 이미지를 그대로 둔다.
        FlowerPieceView view = slot.GetComponent<FlowerPieceView>();
        if (view == null) view = slot.gameObject.AddComponent<FlowerPieceView>();
        view.Apply(block, block.stemSprite, false);

        Transform badgeT = FindIn(slot, "CountBadge");
        views[block] = new SlotView { icon = view.Icon, badge = badgeT != null ? badgeT.GetComponentInChildren<TMP_Text>(true) : null };
        RefreshSlot(block);

        var btn = slot.GetComponentInChildren<Button>(true);
        if (btn != null) btn.onClick.AddListener(() => OnSlotClicked(block));
    }

    /// <summary>역할별 상한까지 한 번 누를 때마다 1개씩 담는다. 상한이 찼으면 이미 담은 꽃을 눌러 비운다.</summary>
    private void OnSlotClicked(FlowerData block)
    {
        Shelf shelf = shelves.FirstOrDefault(s => s.role == block.flowerRole);
        if (shelf == null) return;

        counts.TryGetValue(block, out int n);
        if (RoleTotal(shelf.role) < Limit(shelf.role)) counts[block] = n + 1;
        else if (n > 0) counts.Remove(block);
        else return;

        RefreshSlot(block);
        RefreshLabels();

        if (shelves.All(s => RoleTotal(s.role) >= Limit(s.role))) ConfirmSelection();
    }

    private int RoleTotal(FlowerData.FlowerRole role)
    {
        return counts.Where(kv => kv.Key.flowerRole == role).Sum(kv => kv.Value);
    }

    private void RefreshSlot(FlowerData block)
    {
        if (!views.TryGetValue(block, out var view)) return;

        counts.TryGetValue(block, out int n);
        Color tint = ColorPalette.ToUnityColor(block.color);
        view.icon.color = n > 0 ? tint * SelectedColor : tint;
        view.icon.rectTransform.anchoredPosition = new Vector2(0f, n > 0 ? SelectedLift : 0f);

        if (view.badge != null)
        {
            // 보유 수량은 퍼즐이 끝난 뒤에 소모된다(선택 중에는 그대로). 마이너스는 임시로 0으로 표시한다.
            int owned = Mathf.Max(0, CurrencyManager.Instance != null ? CurrencyManager.Instance.GetFlowerStock(block.blockID) : 0);
            view.badge.text = n > 0 ? $"{n}/{owned}" : $"x{owned}"; // 평소엔 보유 수량만, 담으면 선택/보유
        }
    }

    private void RefreshLabels()
    {
        if (shelves == null) return;
        foreach (var shelf in shelves)
        {
            if (shelf.label == null) continue;
            string roleName = shelf.role switch
            {
                FlowerData.FlowerRole.Line => "라인",
                FlowerData.FlowerRole.Mass => "매스",
                FlowerData.FlowerRole.Form => "폼",
                _ => "필러",
            };
            shelf.label.text = $"{roleName}({RoleTotal(shelf.role)}/{Limit(shelf.role)})";
        }
    }

    private void ConfirmSelection()
    {
        if (!wrapperChosen || GameFlowController.Instance == null || counts.Count == 0) return;
        var pool = counts.SelectMany(kv => Enumerable.Repeat(kv.Key, kv.Value)).ToList();
        GameFlowController.Instance.ConfirmFlowerSelection(pool);
    }
}
