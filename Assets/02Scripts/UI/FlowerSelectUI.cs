using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 꽃 선정 화면. 메인 씬(01DayMain)의 옆 페이지(ShelfRoot)이고, 주문을 수락한 뒤 스와이프로 들어온다(MainUI가 켠다). "포장지 선택 -> 꽃 선정" 순서를 그대로 따른다.
/// 1단계: WrapperPopup의 포장지 박스 중 쓸 수 있는 것을 눌러 고르고 '선택 완료'를 누른다 (목표 점수/정답 레시피가 이때 확정됨).
///        쓸 수 없는 포장지(보유하지 않았거나 퍼즐이 아직 지원하지 않는 링 수)는 속이 빈 어두운 박스로만 보인다.
/// 2단계: 포장지를 고르면 FlowerPopup으로 전환된다. 왼쪽 수첩(FlowerNotebook)에 이번 포장지의 프리셋(예상 꽃다발)이 뜨고, 태그(라인/매스/폼/필러)를 고르면 오른쪽 선반에
///        그 태그의 꽃이 진열된다. 선반의 꽃을 누를 때마다 그 태그의 빈 칸 하나에 무작위로 채워지고(같은 꽃도 여러 번), 수첩에서 채운 칸을 누르면 비운다.
///        꽃을 하나라도 채우면 '선택 완료'가 켜지고(칸을 다 채우지 않아도 된다), 누르면 채운 꽃(송이 수까지)이 퍼즐의 꽃 공급이 되어 퍼즐이 시작된다.
/// 포장지 목록과 꽃 슬롯은 에디터에 놓아 둔 자리를 그대로 쓴다.
/// </summary>
public class FlowerSelectUI : MonoBehaviour
{
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
        public GameObject check;      // 채운 꽃에 보이는 표시(프리팹의 Check)
        public GameObject checkIcon;  // Check 안의 체크 그림(한 번 채웠을 때)
        public TMP_Text pickCount;    // Check 안의 채운 횟수(두 번째부터 x2, x3)
        public readonly List<(Graphic graphic, Color baseColor)> shaded = new(); // 재고가 없을 때 그림자를 씌울 나머지 그림과 글자(원래 색)
    }

    private static readonly Color OutOfStockShade = new(0.35f, 0.35f, 0.35f); // 재고가 없는 꽃에 씌우는 그림자(곱해서 어둡게)
    private const float WrapperRollLift = 30f; // 고른 포장지(박스 안 롤)가 살짝 올라오는 높이
    private static readonly Color IdleBoxColor = new(0.78f, 0.78f, 0.78f);     // 고르지 않은 박스
    private static readonly Color SelectedBoxColor = new(0.86f, 0.86f, 0.86f); // 고른 박스(고르지 않은 박스보다 살짝만 밝게, 하얗지 않게)
    private static readonly Color UnusableBoxColor = new(0.5f, 0.5f, 0.5f);   // 쓸 수 없는 박스

    [SerializeField] private TMP_Text headerText;
    [SerializeField] private float selectedLift = 50f; // 채운 꽃이 화병에서 올라오는 높이

    [Header("포장지 선택 팝업 (박스 7개 + 선택 완료)")]
    [SerializeField] private GameObject wrapperPopup;
    [SerializeField] private WrapperBox[] wrapperBoxes;
    [SerializeField] private Button wrapperConfirmButton;
    [SerializeField] private GameObject wrapperPreview; // 포장지를 눌러야 나타난다

    [Header("꽃 선정 팝업 (왼쪽 수첩 + 오른쪽 선반 하나. 선반은 고른 태그의 꽃으로 그때그때 채운다)")]
    [SerializeField] private GameObject flowerPopup;
    [SerializeField, Min(0f), Tooltip("포장지 팝업이 나가기 시작한 뒤 꽃 선정이 들어오기 시작하기까지의 시간(초). 0이면 동시에(고개를 드는 느낌), 포장지가 나가는 시간보다 크면 그 사이에 쉬는 시간이 생긴다.")] private float flowerEnterDelay;
    [SerializeField] private FlowerNotebook notebook;
    [SerializeField, Tooltip("선반의 꽃 슬롯 자리들의 부모(자식 화병 슬롯의 위치는 에디터에서 정한 그대로 쓴다).")] private RectTransform shelfSlots;
    [SerializeField, Tooltip("선반 이름표의 글씨(고른 태그 이름).")] private TMP_Text shelfLabel;
    [SerializeField, Tooltip("선반 이름표의 테이프(고른 태그의 색으로 칠한다). 없으면 그대로 둔다.")] private Image shelfTape;
    [SerializeField] private Button flowerConfirmButton; // 선택 완료. 모든 칸을 채우면 켜진다

    private readonly Dictionary<FlowerData, SlotView> views = new();
    private readonly List<RectTransform> spots = new(); // 선반에 에디터로 놓아 둔 슬롯 자리
    private readonly Dictionary<RectTransform, List<(Graphic graphic, Color baseColor)>> baseColors = new(); // 슬롯마다 그림자를 씌우기 전의 원래 색
    private readonly Dictionary<RectTransform, Vector2> liftBases = new(); // 슬롯마다 꽃 이미지가 씬에 놓인 원래 자리(처음 연결할 때만 기억한다)
    private FlowerSelection selection; // 포장지를 고른 뒤에 만든다(칸 수와 역할이 프리셋에서 나온다)
    private FlowerData.FlowerRole activeRole = FlowerData.FlowerRole.Line; // 지금 선반에 진열된 태그
    private bool wrapperChosen;
    private int selectedWrapperId = -1;
    private Sprite defaultPreviewSprite;

    private void Start()
    {
        if (wrapperPopup != null) wrapperPopup.SetActive(true);
        if (wrapperPreview != null) wrapperPreview.SetActive(false);
        if (flowerPopup != null) flowerPopup.SetActive(false);
        CollectSpots(); // 에디터에 놓아 둔 슬롯은 자리로 기억해 두고, 꽃을 넣기 전까지 숨긴다

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

        PanelMotion.Close(wrapperPopup); // 포장지는 아래로 내려가고, 꽃 선정은 위에서 내려온다
        StartCoroutine(ShowFlowerPopupAfterDelay(order));
    }

    /// <summary>포장지 팝업이 나가기 시작하고 flowerEnterDelay가 지나면 꽃 선정을 연다.</summary>
    private System.Collections.IEnumerator ShowFlowerPopupAfterDelay(DayPuzzleGenerator.DayOrder order)
    {
        if (flowerEnterDelay > 0f) yield return new WaitForSecondsRealtime(flowerEnterDelay);

        if (flowerPopup != null) flowerPopup.SetActive(true);

        selection = new FlowerSelection(order.preset, StockOf);
        if (Craft != null) foreach (FlowerData required in Craft.RequiredFlowers()) selection.FillRole(required); // 예약 필수 꽃은 보관해 둔 만큼 그 역할의 칸에 저절로 채워진다

        if (notebook != null)
        {
            notebook.Show(order.preset, OrderedFlowerNames(order));
            notebook.TagClicked += OnTagClicked;
            notebook.RemoveClicked += OnRemoveClicked;
        }
        if (flowerConfirmButton != null)
        {
            flowerConfirmButton.gameObject.SetActive(true); // 선택 완료 버튼은 이 단계에서만 켠다
            flowerConfirmButton.onClick.AddListener(ConfirmSelection);
        }

        activeRole = FirstUsableRole();
        BuildShelf();
    }

    /// <summary>손님이 주문에서 말한 꽃의 이름들(수첩 맨 위에 적는다). 색으로만 말한 주문이면 색 이름, 말한 꽃이 없으면 빈 글.</summary>
    private static string OrderedFlowerNames(DayPuzzleGenerator.DayOrder order)
    {
        OrderComposer.CustomerRequirement requirement = order.requirement;
        if (requirement.type == OrderComposer.RequirementType.Color) return $"{ColorPalette.ToKoreanName(requirement.color)} 꽃";

        IEnumerable<string> names = requirement.FlowerTargets()
            .Select(t => DataRegistry.Instance != null ? DataRegistry.Instance.GetFlower(t.flowerId) : null)
            .Where(f => f != null)
            .Select(f => f.DisplayName);
        return string.Join(", ", names);
    }

    /// <summary>예약 꽃다발을 만드는 중이면 그 제작(없으면 null).</summary>
    private static ReservationCraft Craft => GameManager.Instance != null && GameManager.Instance.Crafting ? GameManager.Instance.Craft : null;

    /// <summary>쓸 수 있는 꽃 수. 예약 꽃다발의 필수 꽃은 보관해 둔 것만 센다.</summary>
    private static int StockOf(FlowerData flower)
    {
        if (GameManager.Instance != null) return GameManager.Instance.CraftFlowerStock(flower.flowerID);

        return CurrencyManager.Instance != null ? CurrencyManager.Instance.GetFlowerStock(flower.flowerID) : 0;
    }

    /// <summary>예약 꽃다발에서 직접 고를 수 없는 역할인가: 필수 꽃과 같은 역할(필수 꽃은 제작을 시작하면 저절로 채워진다).</summary>
    private static bool IsRoleLocked(FlowerData.FlowerRole role)
    {
        ReservationCraft craft = Craft;
        return craft != null && craft.BlocksRole(role);
    }

    /// <summary>처음 선반에 보일 태그: 프리셋에 칸이 있고 고를 수 있는 첫 역할(라인, 매스, 폼, 필러 순).</summary>
    private FlowerData.FlowerRole FirstUsableRole()
    {
        foreach (FlowerData.FlowerRole role in new[] { FlowerData.FlowerRole.Line, FlowerData.FlowerRole.Mass, FlowerData.FlowerRole.Form, FlowerData.FlowerRole.Filler })
        {
            if (selection.CellsOf(role) > 0 && !IsRoleLocked(role)) return role;
        }
        return FlowerData.FlowerRole.Line;
    }

    private void OnTagClicked(FlowerData.FlowerRole role)
    {
        if (selection == null || selection.CellsOf(role) == 0 || IsRoleLocked(role) || role == activeRole) return;

        activeRole = role;
        BuildShelf();
    }

    /// <summary>수첩에서 채운 칸을 눌렀다: 그 칸을 비운다(예약 필수 꽃의 칸은 비울 수 없다).</summary>
    private void OnRemoveClicked(int cell)
    {
        if (selection == null || IsRoleLocked(selection.RoleAt(cell)) || !selection.Clear(cell)) return;

        RefreshAll();
    }

    /// <summary>선반을 고른 태그의 꽃으로 채운다. 얻은 꽃만 보이고(재고가 없으면 어둡다), 에디터에 놓은 슬롯 자리를 순서대로 쓴다.</summary>
    private void BuildShelf()
    {
        views.Clear();
        if (shelfLabel != null) shelfLabel.text = RoleName(activeRole);
        if (shelfTape != null) shelfTape.color = ColorPalette.ToRoleColor(activeRole);

        if (DataRegistry.Instance != null)
        {
            int i = 0;
            foreach (FlowerData flower in DataRegistry.Instance.Flowers.Where(f => f != null && f.flowerRole == activeRole).OrderBy(f => f.flowerID))
            {
                if (i >= spots.Count)
                {
                    Debug.LogWarning($"[FlowerSelectUI] 선반의 슬롯 자리({spots.Count}개)보다 {RoleName(activeRole)} 꽃이 많습니다: {flower.flowerName}"); // 꽃 수보다 에디터의 자리가 많거나 같아야 한다
                    break;
                }

                RectTransform slot = spots[i++];
                bool obtained = CurrencyManager.Instance == null || CurrencyManager.Instance.HasObtainedFlower(flower.flowerID);
                slot.gameObject.SetActive(obtained); // 아직 얻지 못한 꽃은 화병을 끈다(자리는 그대로 비워 둔다). 얻었지만 재고가 없는 꽃은 아래 RefreshSlot이 어둡게 한다
                if (!obtained) continue;

                slot.name = $"Slot_{flower.flowerID}";
                BindFlowerSlot(slot, flower);
            }
            for (; i < spots.Count; i++) spots[i].gameObject.SetActive(false); // 꽃이 모자라는 자리는 비워 둔다
        }

        RefreshAll();
    }

    private void CollectSpots()
    {
        if (shelfSlots == null) return;

        foreach (Transform child in shelfSlots)
        {
            spots.Add((RectTransform)child);
            child.gameObject.SetActive(false);
        }
        spots.Sort((a, b) => a.position.x.CompareTo(b.position.x)); // 꽃은 선반의 왼쪽 자리부터 진열한다
    }

    private void BindFlowerSlot(RectTransform slot, FlowerData flower)
    {
        // 줄기 꽃 이미지(stemSprite)가 비어있으면 프리팹의 기본 이미지를 그대로 둔다.
        FlowerPieceView view = FlowerPieceView.GetOrAdd(slot.gameObject);
        view.Apply(flower, flower.stemSprite, false);

        Transform badgeT = FlowerPieceView.FindIn(slot, "CountBadge");
        Transform checkT = FlowerPieceView.FindIn(slot, "Check");
        FlowerPieceView.FindIn(slot, "Charm")?.gameObject.SetActive(false); // 부적 뱃지는 부적 화면에서만 쓴다
        // 꽃이 올라온 자리를 원래 자리로 잡으면 슬롯을 다시 연결할 때마다 점점 높이 뜬다. 원래 자리는 슬롯을 처음 연결할 때 한 번만 기억한다.
        if (!liftBases.TryGetValue(slot, out Vector2 liftBase))
        {
            liftBase = view.LiftTarget.anchoredPosition;
            liftBases[slot] = liftBase;
        }
        var slotView = new SlotView
        {
            piece = view,
            liftBasePosition = liftBase,
            badge = badgeT != null ? badgeT.GetComponentInChildren<TMP_Text>(true) : null,
            check = checkT != null ? checkT.gameObject : null,
            checkIcon = checkT != null ? FlowerPieceView.FindIn(slot, "Icon")?.gameObject : null,
            pickCount = checkT != null ? FlowerPieceView.FindIn(slot, "PickCount")?.GetComponent<TMP_Text>() : null,
        };
        // 그림자를 씌울 그림과 글자의 원래 색은 슬롯을 처음 연결할 때 한 번만 기억한다(다시 연결할 때 이미 어두워진 색을 원래 색으로 잡으면 점점 어두워진다)
        if (!baseColors.TryGetValue(slot, out var originals))
        {
            originals = new List<(Graphic graphic, Color baseColor)>();
            foreach (Graphic graphic in slot.GetComponentsInChildren<Graphic>(true))
            {
                if (!(graphic is Image image && view.Icons.Contains(image))) originals.Add((graphic, graphic.color)); // 꽃 아이콘은 꽃 색으로 따로 칠한다
            }
            baseColors[slot] = originals;
        }
        slotView.shaded.AddRange(originals);
        views[flower] = slotView;

        Button click = slot.GetComponent<Button>();
        if (click != null) click.onClick.RemoveAllListeners(); // 선반을 다른 태그로 다시 채울 때 앞의 꽃에 연결했던 클릭이 남지 않게
        FlowerPieceView.AttachClick(slot, () => OnSlotClicked(flower));
    }

    /// <summary>꽃을 누르면 그 태그의 빈 칸 하나에 채운다(채울 수 없으면 아무 일도 없다).</summary>
    private void OnSlotClicked(FlowerData flower)
    {
        if (selection == null || IsRoleLocked(flower.flowerRole) || selection.Add(flower) < 0) return;

        RefreshAll();
    }

    private void RefreshAll()
    {
        foreach (FlowerData flower in views.Keys.ToList()) RefreshSlot(flower);
        if (notebook != null) notebook.Refresh(selection, activeRole, IsRoleLocked);
        RefreshHeader();
        RefreshConfirmButton();
    }

    private void RefreshSlot(FlowerData flower)
    {
        if (!views.TryGetValue(flower, out var view)) return;

        int picked = selection != null ? selection.Count(flower) : 0;
        int stock = StockOf(flower);

        // 더 채울 수 없는 꽃(재고를 다 채웠거나 재고가 없다)과 예약 필수 꽃과 같은 역할의 꽃은 어둡게 한다.
        Color shade = stock - picked <= 0 || IsRoleLocked(flower.flowerRole) ? OutOfStockShade : Color.white;
        view.piece.SetIconColor(ColorPalette.ToUnityColor(flower.color) * shade);
        foreach (var (graphic, baseColor) in view.shaded) graphic.color = baseColor * shade;
        view.piece.LiftTarget.anchoredPosition = view.liftBasePosition + new Vector2(0f, picked > 0 ? selectedLift : 0f);

        if (view.check != null) view.check.SetActive(picked > 0); // 채운 꽃에만 표시: 한 번이면 체크, 두 번째부터 x2, x3
        if (view.checkIcon != null) view.checkIcon.SetActive(picked == 1);
        if (view.pickCount != null)
        {
            view.pickCount.gameObject.SetActive(picked >= 2);
            view.pickCount.text = $"x{picked}";
        }

        // 재고는 퍼즐이 끝난 뒤에 소모된다(선택 중에는 그대로). 마이너스는 임시로 0으로 표시한다.
        if (view.badge != null) view.badge.text = $"x{Mathf.Max(0, stock)}";
    }

    private static string RoleName(FlowerData.FlowerRole role) => role switch
    {
        FlowerData.FlowerRole.Line => "라인",
        FlowerData.FlowerRole.Mass => "매스",
        FlowerData.FlowerRole.Form => "폼",
        _ => "필러",
    };

    /// <summary>맨 위 문구: 채운 칸 수와 전체 칸 수. 재고가 모자라 채울 수 없는 역할이 있으면 알려 준다.</summary>
    private void RefreshHeader()
    {
        if (headerText == null || selection == null) return;

        string shortage = ShortOfStock() ? "\n재고가 모자란 역할이 있어요" : "";
        headerText.text = $"사용할 꽃을 고르세요 ({selection.FilledCount}/{selection.CellCount}칸){shortage}";
    }

    /// <summary>비어 있는 칸이 있는 역할 중, 그 역할의 꽃 재고(이미 채운 만큼 빼고)를 다 합쳐도 빈 칸을 못 채우는 역할이 있는가.</summary>
    private bool ShortOfStock()
    {
        if (DataRegistry.Instance == null) return false;

        foreach (FlowerData.FlowerRole role in Enum.GetValues(typeof(FlowerData.FlowerRole)))
        {
            int empty = selection.CellsOf(role) - selection.FilledOf(role);
            if (empty <= 0) continue;

            int available = DataRegistry.Instance.Flowers.Where(f => f != null && f.flowerRole == role).Sum(f => Mathf.Max(0, StockOf(f) - selection.Count(f)));
            if (available < empty) return true;
        }
        return false;
    }

    private void RefreshConfirmButton()
    {
        if (flowerConfirmButton != null && selection != null) flowerConfirmButton.interactable = selection.IsComplete; // 모든 칸을 채워야 시작할 수 있다
    }

    private void ConfirmSelection()
    {
        if (!wrapperChosen || GameManager.Instance == null || selection == null || !selection.IsComplete) return;
        GameManager.Instance.ConfirmFlowerSelection(selection.Chosen());
    }
}
