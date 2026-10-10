using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 꽃 선택 화면. "포장지 선택 -> 꽃 선택" 순서를 그대로 따른다.
/// 1단계: WrapperPopup의 포장지 박스 7개 중 쓸 수 있는 것을 눌러 고르고 '선택 완료'를 누른다 (목표 점수/정답 레시피가 이때 확정됨).
///        쓸 수 없는 포장지(보유하지 않았거나 퍼즐이 아직 지원하지 않는 링 수)는 속이 빈 어두운 박스로만 보인다.
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

    [Serializable]
    private class WrapperBox
    {
        public int wrapperId;
        public Button button;
        public Image box;   // 박스 그림(눌렀을 때 밝아진다)
        public Image rolls; // 박스 안 포장지 롤(포장지 색으로 물든다, 쓸 수 없는 포장지면 숨긴다)
        [NonSerialized] public Vector2 rollsBasePosition; // 씬에 놓인 롤의 자리(고르면 여기서 위로 올라온다)
    }

    private class SlotView
    {
        public Image icon;
        public TMP_Text badge;
        public readonly List<(Graphic graphic, Color baseColor)> shaded = new(); // 그림자를 씌울 나머지 그림과 글자(원래 색)
    }

    private static readonly Color UnselectedShade = new(0.35f, 0.35f, 0.35f); // 같은 역할에서 한 꽃이 선택되면 선택되지 않은 꽃에 씌우는 그림자(곱해서 어둡게)
    private const float SelectedLift = 30f; // 담긴 꽃이 화병에서 살짝 올라오는 높이
    private const float WrapperRollLift = 30f; // 고른 포장지(박스 안 롤)가 살짝 올라오는 높이
    private static readonly Color IdleBoxColor = new(0.78f, 0.78f, 0.78f);     // 고르지 않은 박스
    private static readonly Color SelectedBoxColor = new(0.86f, 0.86f, 0.86f); // 고른 박스(고르지 않은 박스보다 살짝만 밝게, 하얗지 않게)
    private static readonly Color UnusableBoxColor = new(0.5f, 0.5f, 0.5f);   // 쓸 수 없는 박스

    [SerializeField] private TMP_Text headerText;

    [Header("포장지 선택 팝업 (박스 7개 + 선택 완료)")]
    [SerializeField] private GameObject wrapperPopup;
    [SerializeField] private WrapperBox[] wrapperBoxes;
    [SerializeField] private Button wrapperConfirmButton;

    [Header("꽃 선택 팝업 (역할별 선반. 꽃 수만큼 에디터에 슬롯을 놓아 두고, 그 자리를 그대로 쓴다)")]
    [SerializeField] private GameObject flowerPopup;
    [SerializeField] private Shelf[] shelves;

    private readonly Dictionary<FlowerData, int> counts = new();
    private readonly Dictionary<FlowerData, SlotView> views = new();    private readonly Dictionary<FlowerData.FlowerRole, int> limits = new(); // 역할별로 담는 개수: 고른 프리셋에 그 역할의 칸이 있으면 1, 없으면 0
    private bool wrapperChosen;
    private int selectedWrapperId = -1;
    private readonly Dictionary<Shelf, List<RectTransform>> spots = new(); // 선반마다 에디터에 놓아 둔 슬롯 자리(위치는 에디터에서 정한 그대로 쓴다)

    private void Start()
    {
        if (wrapperPopup != null) wrapperPopup.SetActive(true);
        if (flowerPopup != null) flowerPopup.SetActive(false);
        CollectSpots(); // 에디터에 놓아 둔 슬롯은 자리로 기억해 두고, 꽃을 넣기 전까지 숨긴다

        RefreshLabels();
        SetUpWrapperBoxes();
    }

    /// <summary>박스마다 쓸 수 있는지 정한다. 쓸 수 있으면 롤을 포장지 색으로 물들여 보여 주고, 아니면 빈 어두운 박스로 두고 누를 수 없게 한다.</summary>
    private void SetUpWrapperBoxes()
    {
        if (wrapperConfirmButton != null)
        {
            wrapperConfirmButton.interactable = false; // 박스를 골라야 누를 수 있다
            wrapperConfirmButton.onClick.AddListener(() => OnWrapperChosen(selectedWrapperId));
        }

        foreach (WrapperBox box in wrapperBoxes)
        {
            box.rollsBasePosition = box.rolls.rectTransform.anchoredPosition;
            bool usable = IsWrapperUsable(box.wrapperId);
            box.button.interactable = usable;
            box.rolls.gameObject.SetActive(usable);
            if (usable)
            {
                box.rolls.color = WrapperRegistry.Instance.GetById(box.wrapperId).color;
                int id = box.wrapperId;
                box.button.onClick.AddListener(() => SelectWrapper(id));
            }
        }
        RefreshWrapperBoxes();
    }

    /// <summary>보유한 포장지이고, 퍼즐이 지원하는 링 수의 프리셋이 있어야 쓸 수 있다.</summary>
    private static bool IsWrapperUsable(int wrapperId)
    {
        if (CurrencyManager.Instance == null || WrapperRegistry.Instance == null) return false;
        WrapperData data = WrapperRegistry.Instance.GetById(wrapperId);
        return data != null && CurrencyManager.Instance.OwnedWrappers.Contains(wrapperId)
            && data.presets.Count > 0 && data.ringCount <= WrapperBoardController.SupportedRings;
    }

    private void SelectWrapper(int wrapperId)
    {
        selectedWrapperId = wrapperId;
        if (wrapperConfirmButton != null) wrapperConfirmButton.interactable = true;
        RefreshWrapperBoxes();
    }

    private void RefreshWrapperBoxes()
    {
        foreach (WrapperBox box in wrapperBoxes)
        {
            bool selected = box.wrapperId == selectedWrapperId;
            box.box.color = !box.button.interactable ? UnusableBoxColor : selected ? SelectedBoxColor : IdleBoxColor;
            box.rolls.rectTransform.anchoredPosition = box.rollsBasePosition + (selected ? new Vector2(0f, WrapperRollLift) : Vector2.zero); // 박스는 가만히, 고른 포장지만 올라온다
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
        counts.Clear();
        RefreshLabels();

        if (BlockRegistry.Instance == null) return;

        foreach (var shelf in shelves)
        {
            List<RectTransform> shelfSpots = spots[shelf];
            int i = 0;
            foreach (var flower in BlockRegistry.Instance.AllBlocks.Where(b => b.flowerRole == shelf.role))
            {
                if (i >= shelfSpots.Count)
                {
                    Debug.LogWarning($"[FlowerSelectUI] {shelf.role} 선반의 슬롯 자리({shelfSpots.Count}개)보다 꽃이 많습니다: {flower.flowerName}"); // 꽃 수와 에디터의 자리 수는 같아야 한다
                    break;
                }

                RectTransform slot = shelfSpots[i];
                slot.gameObject.SetActive(true);
                slot.name = $"Slot_{flower.blockID}";
                BindFlowerSlot(slot, flower);
                i++;
            }
            for (; i < shelfSpots.Count; i++) shelfSpots[i].gameObject.SetActive(false); // 꽃이 모자라는 자리는 비워 둔다
        }
    }

    private void CollectSpots()
    {
        foreach (var shelf in shelves)
        {
            var list = new List<RectTransform>();
            if (shelf.slotParent != null)
            {
                foreach (Transform child in shelf.slotParent)
                {
                    list.Add((RectTransform)child);
                    child.gameObject.SetActive(false);
                }
            }
            spots[shelf] = list;
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

        // 화병 앞면·뱃지처럼 안쪽 버튼 위에 겹친 그림이 클릭을 먼저 받으면 안쪽 버튼에는 클릭이 닿지 않는다.
        // 그래서 클릭은 슬롯 맨 위에서 받고(자식 그림이 받은 클릭은 위로 올라온다), 안쪽 버튼은 꺼 둔다.
        foreach (Button inner in slot.GetComponentsInChildren<Button>(true)) inner.enabled = false;
        Button click = slot.GetComponent<Button>();
        if (click == null)
        {
            click = slot.gameObject.AddComponent<Button>();
            click.transition = Selectable.Transition.None;
        }
        click.onClick.AddListener(() => OnSlotClicked(block));
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
            view.badge.text = $"x{owned}"; // 담아도 "1/보유"로 바꾸지 않는다(한 송이만 쓸 수 있다고 헷갈리므로). 담은 꽃은 위로 올라오는 것으로만 보여 준다
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
