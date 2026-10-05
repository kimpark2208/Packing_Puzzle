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
/// 꽃을 하나씩 골라 가챠 풀(최대 4종)로 만든다. 같은 역할에서 다른 꽃을 누르면 바꿔 담는다.
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
        public readonly List<(Graphic graphic, Color baseColor)> shaded = new(); // 그림자를 씌울 나머지 그림과 글자(원래 색)
    }

    private static readonly Color UnselectedShade = new(0.35f, 0.35f, 0.35f); // 같은 역할에서 한 꽃이 선택되면 선택되지 않은 꽃에 씌우는 그림자(곱해서 어둡게)
    private const float SelectedLift = 30f; // 담긴 꽃이 화병에서 살짝 올라오는 높이
    private const int ExampleWrapperId = 3; // 고를 수 없는 예시로 보여 주는 포장지


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
    private readonly Dictionary<FlowerData, RectTransform> slotRects = new(); // 튜토리얼이 가리킬 슬롯 위치
    private readonly Dictionary<FlowerData.FlowerRole, int> limits = new(); // 역할별로 담는 개수: 고른 프리셋에 그 역할의 칸이 있으면 1, 없으면 0
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
        RectTransform firstItem = null;
        foreach (int wrapperId in owned)
        {
            RectTransform itemRT = AddWrapperItem(wrapperId, selectable: true);
            if (firstItem == null) firstItem = itemRT;
        }

        // 포장지마다 크기가 다르다는 걸 보여 주는 예시: 아직 없는 3번 포장지를 고를 수 없게 옆에 둔다.
        if (!owned.Contains(ExampleWrapperId)) AddWrapperItem(ExampleWrapperId, selectable: false);

        if (firstItem != null)
        {
            TutorialOverlay.Play("wrapper", new TutorialOverlay.Step(firstItem,
                $"손님이 말한 {TutorialOverlay.Em("포장지")}를 고르세요.\n{TutorialOverlay.Em("포장지")}마다 필요한 꽃의 개수가 달라요."));
        }
    }

    private RectTransform AddWrapperItem(int wrapperId, bool selectable)
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

        if (confirmBtn != null)
        {
            confirmBtn.interactable = selectable;
            if (selectable) confirmBtn.onClick.AddListener(() => OnWrapperChosen(wrapperId));
        }
        return itemRT;
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
            headerText.text = "역할별로 사용할 꽃을 하나씩 고르세요";
        }

        if (wrapperPopup != null) wrapperPopup.SetActive(false);
        if (flowerPopup != null) flowerPopup.SetActive(true);

        LoadRoleLimits(order);
        BuildFlowerChoices();
    }

    /// <summary>고른 프리셋에 칸이 있는 역할마다 꽃을 하나씩 담는다. 모두 채우면 바로 퍼즐로 넘어간다.</summary>
    private void LoadRoleLimits(DayPuzzleGenerator.DayOrder order)
    {
        limits.Clear();
        WrapperData data = WrapperRegistry.Instance != null ? WrapperRegistry.Instance.GetById(order.wrapperId) : null;
        bool valid = data != null && order.presetIndex >= 0 && order.presetIndex < data.presets.Count;
        foreach (var shelf in shelves) limits[shelf.role] = valid && data.presets[order.presetIndex].CountByTag(shelf.role) > 0 ? 1 : 0;
    }

    private int Limit(FlowerData.FlowerRole role)
    {
        return limits.TryGetValue(role, out int v) ? v : 0;
    }

    /// <summary>포장지 단계/보유 여부와 상관없이 등록된 모든 꽃을 후보로 보여준다.</summary>
    private void BuildFlowerChoices()
    {
        views.Clear();
        slotRects.Clear();
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
                slotRects[flower] = slot;
                BindFlowerSlot(slot, flower);
            }
        }

        PlayFlowerTutorial();
    }

    private void PlayFlowerTutorial()
    {
        RectTransform firstLabel = shelves.Length > 0 && shelves[0].label != null ? (RectTransform)shelves[0].label.transform : null;
        RectTransform firstPickable = slotRects.FirstOrDefault(kv => CurrencyManager.Instance == null || CurrencyManager.Instance.GetFlowerStock(kv.Key.blockID) > 0).Value;

        TutorialOverlay.Play("flowers",
            new TutorialOverlay.Step(firstLabel,
                $"꽃은 {TutorialOverlay.Em("라인-폼-매스-필러")} 네 가지 역할로 나뉘어요.\n역할마다 꽃을 {TutorialOverlay.Em("하나씩")} 골라야 해요."),
            new TutorialOverlay.Step(firstPickable,
                $"{TutorialOverlay.Em("x숫자")}는 남은 꽃 개수예요. 재고가 0인 꽃은 고를 수 없어요.\n주문서에 맞는 꽃을 눌러 담으세요."));
    }

    private void ClearSlots()
    {
        foreach (var shelf in shelves)
        {
            if (shelf.slotParent == null) continue;
            foreach (Transform old in shelf.slotParent) Destroy(old.gameObject);
        }
    }

    // 슬롯 프리셋마다 자식 위치가 달라(화병은 Button이 VaseImage에 있다) 이름으로 깊이 찾는다.
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
        var slotView = new SlotView { icon = view.Icon, badge = badgeT != null ? badgeT.GetComponentInChildren<TMP_Text>(true) : null };
        foreach (Graphic graphic in slot.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic != view.Icon) slotView.shaded.Add((graphic, graphic.color));
        }
        views[block] = slotView;
        RefreshSlot(block);

        var btn = slot.GetComponentInChildren<Button>(true);
        if (btn != null) btn.onClick.AddListener(() => OnSlotClicked(block));
    }

    /// <summary>역할당 꽃 하나만 담는다. 담은 꽃을 다시 누르면 비우고, 같은 역할의 다른 꽃을 누르면 바꿔 담는다.</summary>
    private void OnSlotClicked(FlowerData block)
    {
        Shelf shelf = shelves.FirstOrDefault(s => s.role == block.flowerRole);
        if (shelf == null) return;

        if (counts.ContainsKey(block))
        {
            counts.Remove(block);
        }
        else
        {
            if (Limit(shelf.role) == 0) return; // 이 프리셋엔 이 역할의 칸이 없다
            if (CurrencyManager.Instance != null && CurrencyManager.Instance.GetFlowerStock(block.blockID) <= 0) return; // 재고가 없는 꽃은 고를 수 없다
            foreach (FlowerData other in counts.Keys.Where(k => k.flowerRole == shelf.role).ToList()) counts.Remove(other);
            counts[block] = 1;
        }

        RefreshRole(shelf.role); // 선택이 바뀌면 같은 역할의 다른 꽃들의 그림자도 달라진다
        RefreshLabels();

        if (shelves.All(s => RoleTotal(s.role) >= Limit(s.role))) ConfirmSelection();
    }

    private void RefreshRole(FlowerData.FlowerRole role)
    {
        foreach (FlowerData flower in views.Keys.Where(f => f.flowerRole == role).ToList()) RefreshSlot(flower);
    }

    private int RoleTotal(FlowerData.FlowerRole role)
    {
        return counts.Where(kv => kv.Key.flowerRole == role).Sum(kv => kv.Value);
    }

    private void RefreshSlot(FlowerData block)
    {
        if (!views.TryGetValue(block, out var view)) return;

        counts.TryGetValue(block, out int n);

        // 같은 역할에서 다른 꽃이 선택됐으면 이 꽃(선택되지 않은 꽃)은 어둡게 한다.
        Color shade = RoleTotal(block.flowerRole) > 0 && n == 0 ? UnselectedShade : Color.white;
        view.icon.color = ColorPalette.ToUnityColor(block.color) * shade;
        foreach (var (graphic, baseColor) in view.shaded) graphic.color = baseColor * shade;
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
            shelf.label.text = roleName;
        }
    }

    private void ConfirmSelection()
    {
        if (!wrapperChosen || GameFlowController.Instance == null || counts.Count == 0) return;
        var pool = counts.SelectMany(kv => Enumerable.Repeat(kv.Key, kv.Value)).ToList();
        GameFlowController.Instance.ConfirmFlowerSelection(pool);
    }
}
