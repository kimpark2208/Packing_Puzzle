using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 꽃 선택 화면. 메인 씬(01DayMain)의 옆 페이지(ShelfRoot)이고, 주문을 수락한 뒤 스와이프로 들어온다(MainUI가 켠다). "포장지 선택 -> 꽃 선택" 순서를 그대로 따른다.
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
        public TMP_Text stock; // 박스 오른쪽 위 재고 배지의 글씨(꽃 화병의 재고 배지와 같은 모양)
        [NonSerialized] public Vector2 rollsBasePosition; // 씬에 놓인 롤의 자리(고르면 여기서 위로 올라온다)
    }

    private class SlotView
    {
        public FlowerPieceView piece;     // 꽃 이미지(화병은 여러 송이)를 들고 있다
        public Vector2 liftBasePosition;  // 씬에 놓인 꽃 이미지(들)의 자리(고르면 여기서 위로 올라온다)
        public TMP_Text badge;
        public GameObject check; // 담은 꽃에만 보이는 체크 표시(프리팹의 Check)
        public readonly List<(Graphic graphic, Color baseColor)> shaded = new(); // 재고가 없을 때 그림자를 씌울 나머지 그림과 글자(원래 색)
    }

    private static readonly Color OutOfStockShade = new(0.35f, 0.35f, 0.35f); // 재고가 없는 꽃에 씌우는 그림자(곱해서 어둡게)
    private const float WrapperRollLift = 30f; // 고른 포장지(박스 안 롤)가 살짝 올라오는 높이
    private static readonly Color IdleBoxColor = new(0.78f, 0.78f, 0.78f);     // 고르지 않은 박스
    private static readonly Color SelectedBoxColor = new(0.86f, 0.86f, 0.86f); // 고른 박스(고르지 않은 박스보다 살짝만 밝게, 하얗지 않게)
    private static readonly Color UnusableBoxColor = new(0.5f, 0.5f, 0.5f);   // 쓸 수 없는 박스

    [SerializeField] private TMP_Text headerText;
    [SerializeField] private float selectedLift = 50f; // 담긴 꽃이 화병에서 올라오는 높이

    [Header("포장지 선택 팝업 (박스 7개 + 선택 완료)")]
    [SerializeField] private GameObject wrapperPopup;
    [SerializeField] private WrapperBox[] wrapperBoxes;
    [SerializeField] private Button wrapperConfirmButton;
    [SerializeField] private GameObject wrapperPreview; // 포장지를 눌러야 나타난다

    [Header("꽃 선택 팝업 (역할별 선반. 꽃 수만큼 에디터에 슬롯을 놓아 두고, 그 자리를 그대로 쓴다)")]
    [SerializeField] private GameObject flowerPopup;
    [SerializeField, Min(0f), Tooltip("포장지 팝업이 나가기 시작한 뒤 꽃 선택이 들어오기 시작하기까지의 시간(초). 0이면 동시에(고개를 드는 느낌), 포장지가 나가는 시간보다 크면 그 사이에 쉬는 시간이 생긴다.")] private float flowerEnterDelay;
    [SerializeField] private Shelf[] shelves;
    [SerializeField] private Button flowerConfirmButton; // 선택 완료. 없으면 더 담을 꽃이 없어지는 순간 자동으로 넘어간다

    private readonly Dictionary<FlowerData, SlotView> views = new();
    private FlowerSelection selection; // 포장지를 고른 뒤에 만든다(역할별 한도와 전체 한도가 포장지와 프리셋에서 나온다)
    private bool wrapperChosen;
    private int selectedWrapperId = -1;
    private Sprite defaultPreviewSprite;
    private readonly Dictionary<Shelf, List<RectTransform>> spots = new(); // 선반마다 에디터에 놓아 둔 슬롯 자리(위치는 에디터에서 정한 그대로 쓴다)

    private void Start()
    {
        if (wrapperPopup != null) wrapperPopup.SetActive(true);
        if (wrapperPreview != null) wrapperPreview.SetActive(false);
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
            bool owned = CurrencyManager.Instance == null || CurrencyManager.Instance.HasWrapper(box.wrapperId);
            box.button.gameObject.SetActive(owned); // 아직 받지 못한 포장지는 박스를 끈다(자리는 그대로 비워 둔다)
            if (!owned) continue;

            box.rollsBasePosition = box.rolls.rectTransform.anchoredPosition;
            if (box.stock != null)
            {
                box.stock.transform.parent.gameObject.SetActive(!CurrencyManager.WrappersUnlimited); // 포장지 개수를 따지지 않는 동안은 재고 배지를 숨긴다(CountBadge는 글씨의 부모)
                box.stock.text = $"x{(CurrencyManager.Instance != null ? CurrencyManager.Instance.GetWrapperStock(box.wrapperId) : 0)}"; // 재고(0이어도 그대로 보여 준다)
            }
            bool usable = IsWrapperUsable(box.wrapperId);
            box.button.interactable = usable;
            box.rolls.gameObject.SetActive(usable);
            if (usable)
            {
                box.rolls.color = DataRegistry.Instance.GetWrapper(box.wrapperId).color;
                int id = box.wrapperId;
                box.button.onClick.AddListener(() => SelectWrapper(id));
            }
        }
        if (Craft != null && Craft.WrapperId > 0 && IsWrapperUsable(Craft.WrapperId)) SelectWrapper(Craft.WrapperId); // 예약 꽃다발은 쓸 포장지가 정해져 있어 미리 골라 둔다(선택 완료만 누르면 된다)
        RefreshWrapperBoxes();
    }

    /// <summary>재고가 있는 포장지이고(재고가 있는 게 하나도 없으면 보유한 포장지), 퍼즐이 지원하는 링 수의 프리셋이 있어야 쓸 수 있다.</summary>
    private static bool IsWrapperUsable(int wrapperId)
    {
        if (CurrencyManager.Instance == null || DataRegistry.Instance == null) return false;
        WrapperData data = DataRegistry.Instance.GetWrapper(wrapperId);
        ReservationCraft craft = Craft; // 예약 꽃다발은 예약이 정한 포장지만 쓴다
        bool allowed = craft == null || craft.WrapperId <= 0 || craft.WrapperId == wrapperId;
        return data != null && allowed && CurrencyManager.Instance.UsableWrappers.Contains(wrapperId)
            && data.presets.Count > 0 && data.ringCount <= DayPuzzleController.SupportedRings;
    }

    private void SelectWrapper(int wrapperId)
    {
        selectedWrapperId = wrapperId;
        if (wrapperPreview != null)
        {
            wrapperPreview.SetActive(true);
            if (wrapperPreview.TryGetComponent(out Image previewImage))
            {
                WrapperData data = DataRegistry.Instance.GetWrapper(wrapperId);
                defaultPreviewSprite ??= previewImage.sprite; // 씬에 놓인 기본 그림을 기억해 둔다
                previewImage.sprite = data.previewSprite != null ? data.previewSprite : defaultPreviewSprite;
                previewImage.color = data.color; // 프리셋 그림도 고른 포장지 색으로
            }
        }
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
        if (GameManager.Instance == null || wrapperChosen) return; // 팝업이 사라지는 동안 다시 눌러도 무시한다

        GameManager.Instance.ChooseWrapper(wrapperId);
        wrapperChosen = true;

        var order = GameManager.Instance.CurrentDayOrder;
        if (order == null) return;

        PanelMotion.Close(wrapperPopup); // 포장지는 아래로 내려가고, 꽃 선택은 위에서 내려온다
        StartCoroutine(ShowFlowerPopupAfterDelay(order));
    }

    /// <summary>포장지 팝업이 나가기 시작하고 flowerEnterDelay가 지나면 꽃 선택을 연다.</summary>
    private System.Collections.IEnumerator ShowFlowerPopupAfterDelay(DayPuzzleGenerator.DayOrder order)
    {
        if (flowerEnterDelay > 0f) yield return new WaitForSecondsRealtime(flowerEnterDelay);

        if (flowerPopup != null) flowerPopup.SetActive(true);

        CreateSelection(order);
        if (Craft != null) foreach (FlowerData required in Craft.RequiredFlowers()) selection.Toggle(required); // 예약 필수 꽃은 보관해 둔 만큼 저절로 담긴다
        if (flowerConfirmButton != null)
        {
            flowerConfirmButton.gameObject.SetActive(true); // 꽃 선반은 부적 화면과 같은 덩어리라, 선택 완료 버튼은 이 단계에서만 켠다
            flowerConfirmButton.onClick.AddListener(ConfirmSelection);
        }
        BuildFlowerChoices();
    }

    /// <summary>고른 포장지와 프리셋에서 선택 규칙을 만든다: 역할별 한도는 그 프리셋에서 그 역할의 칸 수(전체 한도는 그 합).</summary>
    private void CreateSelection(DayPuzzleGenerator.DayOrder order)
    {
        var roleLimits = new Dictionary<FlowerData.FlowerRole, int>();
        foreach (var shelf in shelves) roleLimits[shelf.role] = order.preset != null ? order.preset.CountByTag(shelf.role) : 0;

        selection = new FlowerSelection(roleLimits, StockOf);
    }

    /// <summary>예약 꽃다발을 만드는 중이면 그 제작(없으면 null).</summary>
    private static ReservationCraft Craft => GameManager.Instance != null && GameManager.Instance.Crafting ? GameManager.Instance.Craft : null;

    /// <summary>쓸 수 있는 꽃 수. 예약 꽃다발의 필수 꽃은 보관해 둔 것만 센다.</summary>
    private static int StockOf(FlowerData flower)
    {
        if (GameManager.Instance != null) return GameManager.Instance.CraftFlowerStock(flower.flowerID);

        return CurrencyManager.Instance != null ? CurrencyManager.Instance.GetFlowerStock(flower.flowerID) : 0;
    }

    /// <summary>예약 꽃다발에서 직접 고를 수 없는 꽃인가: 필수 꽃과 같은 역할의 꽃(필수 꽃은 제작을 시작하면 저절로 담긴다).</summary>
    private static bool IsLockedByReservation(FlowerData flower)
    {
        ReservationCraft craft = Craft;
        return craft != null && craft.BlocksRole(flower.flowerRole);
    }

    /// <summary>포장지 단계/보유 여부와 상관없이 등록된 모든 꽃을 후보로 보여준다.</summary>
    private void BuildFlowerChoices()
    {
        views.Clear();
        RefreshLabels();
        RefreshConfirmButton();

        if (DataRegistry.Instance == null) return;

        foreach (var shelf in shelves)
        {
            List<RectTransform> shelfSpots = spots[shelf];
            int i = 0;
            foreach (var flower in DataRegistry.Instance.Flowers.Where(b => b.flowerRole == shelf.role))
            {
                if (i >= shelfSpots.Count)
                {
                    Debug.LogWarning($"[FlowerSelectUI] {shelf.role} 선반의 슬롯 자리({shelfSpots.Count}개)보다 꽃이 많습니다: {flower.flowerName}"); // 꽃 수와 에디터의 자리 수는 같아야 한다
                    break;
                }

                RectTransform slot = shelfSpots[i++];
                bool obtained = CurrencyManager.Instance == null || CurrencyManager.Instance.HasObtainedFlower(flower.flowerID);
                slot.gameObject.SetActive(obtained); // 아직 얻지 못한 꽃은 화병을 끈다(자리는 그대로 비워 둔다). 얻었지만 재고가 없는 꽃은 아래 RefreshSlot이 어둡게 한다
                if (!obtained) continue;

                slot.name = $"Slot_{flower.flowerID}";
                BindFlowerSlot(slot, flower);
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

    private void BindFlowerSlot(RectTransform slot, FlowerData block)
    {
        // 줄기 꽃 이미지(stemSprite)가 비어있으면 프리팹의 기본 이미지를 그대로 둔다.
        FlowerPieceView view = FlowerPieceView.GetOrAdd(slot.gameObject);
        view.Apply(block, block.stemSprite, false);

        Transform badgeT = FlowerPieceView.FindIn(slot, "CountBadge");
        Transform checkT = FlowerPieceView.FindIn(slot, "Check");
        FlowerPieceView.FindIn(slot, "Charm")?.gameObject.SetActive(false); // 부적 뱃지는 부적 화면에서만 쓴다
        var slotView = new SlotView
        {
            piece = view,
            liftBasePosition = view.LiftTarget.anchoredPosition,
            badge = badgeT != null ? badgeT.GetComponentInChildren<TMP_Text>(true) : null,
            check = checkT != null ? checkT.gameObject : null,
        };
        foreach (Graphic graphic in slot.GetComponentsInChildren<Graphic>(true))
        {
            if (!(graphic is Image image && view.Icons.Contains(image))) slotView.shaded.Add((graphic, graphic.color)); // 꽃 아이콘은 꽃 색으로 따로 칠한다
        }
        views[block] = slotView;
        RefreshSlot(block);

        FlowerPieceView.AttachClick(slot, () => OnSlotClicked(block));
    }

    /// <summary>꽃을 누르면 선택 규칙(FlowerSelection)에 따라 담거나 비우거나 바꿔 담는다. 선택이 바뀌면 모든 꽃의 표시를 다시 그린다.</summary>
    private void OnSlotClicked(FlowerData block)
    {
        if (selection == null || IsLockedByReservation(block) || !selection.Toggle(block)) return;

        RefreshAllSlots(); // 같은 역할에서 바꿔 담으면 다른 꽃의 체크도 풀리므로 전부 다시 그린다
        RefreshLabels();
        RefreshConfirmButton();

        if (flowerConfirmButton == null && selection.IsValid && !views.Keys.Any(selection.CanAdd)) ConfirmSelection(); // 선택 완료 버튼이 없으면, 더 담을 꽃이 없어지는 순간(한도가 차거나 재고가 없을 때) 넘어간다
    }

    private void RefreshAllSlots()
    {
        foreach (FlowerData flower in views.Keys.ToList()) RefreshSlot(flower);
    }

    private void RefreshSlot(FlowerData block)
    {
        if (!views.TryGetValue(block, out var view)) return;

        bool picked = selection != null && selection.Contains(block);

        // 재고가 없는 꽃과 예약 꽃다발에서 고를 수 없는 꽃(필수 꽃과 같은 역할)은 어둡게 한다(담을 수 없다).
        Color shade = StockOf(block) <= 0 || (!picked && IsLockedByReservation(block)) ? OutOfStockShade : Color.white;
        view.piece.SetIconColor(ColorPalette.ToUnityColor(block.color) * shade);
        foreach (var (graphic, baseColor) in view.shaded) graphic.color = baseColor * shade;
        view.piece.LiftTarget.anchoredPosition = view.liftBasePosition + new Vector2(0f, picked ? selectedLift : 0f);
        if (view.check != null) view.check.SetActive(picked); // 담은 꽃에만 체크 표시

        if (view.badge != null)
        {
            // 보유 수량은 퍼즐이 끝난 뒤에 소모된다(선택 중에는 그대로). 마이너스는 임시로 0으로 표시한다.
            int owned = Mathf.Max(0, StockOf(block));
            view.badge.text = $"x{owned}"; // 담은 꽃은 위로 올라오는 것으로만 보여 준다
        }
    }

    /// <summary>선반 이름 옆에 고른 수/그 역할의 한도(포장지 프리셋의 그 역할 칸 수. 예: "매스(2/4)")를, 맨 위 문구에 전체 고른 수/전체 한도를 보여 준다.</summary>
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
            int limit = selection != null ? selection.Limit(shelf.role) : 0;
            shelf.label.text = limit > 0 ? $"{roleName}({selection.Count(shelf.role)}/{limit})" : roleName;
        }

        if (headerText != null && selection != null)
        {
            string stockWarning = selection.HasStockShortfall ? "\n재고가 모자란 역할이 있어요" : "";
            headerText.text = $"사용할 꽃을 고르세요 ({selection.Total}/{selection.TotalLimit}종){stockWarning}";
        }
    }

    private void RefreshConfirmButton()
    {
        if (flowerConfirmButton != null && selection != null) flowerConfirmButton.interactable = selection.IsValid;
    }

    private void ConfirmSelection()
    {
        if (!wrapperChosen || GameManager.Instance == null || selection == null || !selection.IsValid) return;
        GameManager.Instance.ConfirmFlowerSelection(selection.Picked.ToList());
    }
}
