using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 꽃 선택 화면. "포장지 선택 -> 꽃 선택" 순서를 그대로 따른다.
/// 1단계: WrapperPopup에서 보유한 포장지 중 이번 주문에 쓸 것을 하나 고른다 (목표 점수/정답 레시피가 이때 확정됨).
/// 2단계: 포장지를 고르면 FlowerPopup으로 전환되고, 최대 3종을 가챠 풀로 고른다.
/// 포장지 목록은 개수가 가변적이라 템플릿을 복제해서 만들고, 꽃 목록은 개수가 21개로 고정이라
/// 미리 배치된 슬롯 21개를 활성/비활성 + 내용 갱신하는 방식으로 채운다.
/// </summary>
public class FlowerSelectUI : MonoBehaviour
{
    private enum SortMode { Color, Size, Name }

    private const int MaxSelectable = 3;

    private static readonly Color ItemColor = Color.white;
    private static readonly Color SelectedColor = new(0.65f, 0.85f, 0.70f);
    private static readonly Color WrapperColor = new(0.85f, 0.80f, 0.95f);
    private static readonly Color WrapperSelectedColor = new(0.55f, 0.45f, 0.85f);

    [SerializeField] private TMP_Text headerText;

    [Header("포장지 선택 팝업")]
    [SerializeField] private GameObject wrapperPopup;
    [SerializeField] private RectTransform wrapperArea;
    [SerializeField] private RectTransform wrapperItemTemplate;

    [Header("꽃 선택 팝업 (슬롯 21개 고정 배치, Instantiate 안 함)")]
    [SerializeField] private GameObject flowerPopup;
    [SerializeField] private RectTransform flowerArea;
    [SerializeField] private TMP_Dropdown sortDropdown;
    [SerializeField] private Button flowerBucketButton;
    [SerializeField] private TMP_Text flowerCurSelectedText;

    private readonly HashSet<BlockData> selected = new();
    private readonly Dictionary<BlockData, Image> itemImages = new();
    private readonly Dictionary<int, Image> wrapperImages = new();
    private readonly List<RectTransform> flowerSlots = new();
    private List<BlockData> currentCandidates = new();
    private bool wrapperChosen;

    private void Start()
    {
        if (wrapperItemTemplate != null) wrapperItemTemplate.gameObject.SetActive(false);
        if (wrapperPopup != null) wrapperPopup.SetActive(true);
        if (flowerPopup != null) flowerPopup.SetActive(false);

        if (flowerArea != null)
        {
            foreach (Transform child in flowerArea) flowerSlots.Add((RectTransform)child);
        }

        if (flowerBucketButton != null) flowerBucketButton.onClick.AddListener(OnConfirm);
        if (sortDropdown != null) sortDropdown.onValueChanged.AddListener(OnSortChanged);

        UpdateBucketPreview();
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

            RectTransform itemRT = Instantiate(wrapperItemTemplate, wrapperArea);
            itemRT.gameObject.SetActive(true);
            itemRT.name = $"Wrapper_{wrapperId}";

            var img = itemRT.Find("WrapperImage") != null ? itemRT.Find("WrapperImage").GetComponent<Image>() : null;
            if (img != null)
            {
                img.color = WrapperColor;
                wrapperImages[wrapperId] = img;
            }

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

        if (wrapperImages.TryGetValue(wrapperId, out var chosenImg))
        {
            foreach (var kvp in wrapperImages) kvp.Value.color = WrapperColor;
            chosenImg.color = WrapperSelectedColor;
        }

        GameFlowController.Instance.ChooseWrapper(wrapperId);
        wrapperChosen = true;

        var order = GameFlowController.Instance.CurrentDayOrder;
        if (order == null) return;

        if (headerText != null)
        {
            headerText.text = $"{order.gridSize}x{order.gridSize} 포장지 확정! 목표 점수: {order.targetScore}\n오늘 사용할 꽃을 최대 {MaxSelectable}종 고르세요";
        }

        if (wrapperPopup != null) wrapperPopup.SetActive(false);
        if (flowerPopup != null) flowerPopup.SetActive(true);

        BuildFlowerChoices(order);
    }

    private void BuildFlowerChoices(DayPuzzleGenerator.DayOrder order)
    {
        itemImages.Clear();
        selected.Clear();
        UpdateBucketPreview();

        if (ShopManager.Instance == null || CurrencyManager.Instance == null || BlockDatabase.Instance == null) return;

        int tier = ShopManager.Instance.GetTier(order.wrapperId);
        var obtainedIds = CurrencyManager.Instance.GetObtainedFlowerIds();
        currentCandidates = BlockDatabase.Instance.GetObtainedBlocksForWrapperTier(obtainedIds, tier);

        ApplySort();
    }

    private void OnSortChanged(int _)
    {
        ApplySort();
    }

    private void ApplySort()
    {
        SortMode mode = sortDropdown != null ? (SortMode)sortDropdown.value : SortMode.Color;
        IEnumerable<BlockData> sorted = mode switch
        {
            SortMode.Size => currentCandidates.OrderBy(b => b.CellCount),
            SortMode.Name => currentCandidates.OrderBy(b => b.name),
            _ => currentCandidates.OrderBy(b => (int)b.color)
        };
        var sortedList = sorted.ToList();

        for (int i = 0; i < flowerSlots.Count; i++)
        {
            RectTransform slot = flowerSlots[i];
            if (i < sortedList.Count)
            {
                slot.gameObject.SetActive(true);
                BindFlowerSlot(slot, sortedList[i]);
            }
            else
            {
                slot.gameObject.SetActive(false);
            }
        }
    }

    private void BindFlowerSlot(RectTransform slot, BlockData block)
    {
        Transform imgT = slot.Find("FlowerImage");
        var img = imgT != null ? imgT.GetComponent<Image>() : null;
        if (img == null) return;

        img.sprite = block.flowerIcon != null ? block.flowerIcon : block.blockImage;
        img.color = selected.Contains(block) ? SelectedColor : ItemColor;
        itemImages[block] = img;

        var btn = imgT.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => ToggleSelect(block));
        }
    }

    private void ToggleSelect(BlockData block)
    {
        if (selected.Contains(block))
        {
            selected.Remove(block);
        }
        else
        {
            if (selected.Count >= MaxSelectable) return;
            selected.Add(block);
        }

        if (itemImages.TryGetValue(block, out var img))
            img.color = selected.Contains(block) ? SelectedColor : ItemColor;

        UpdateBucketPreview();
    }

    private void UpdateBucketPreview()
    {
        if (flowerCurSelectedText == null) return;
        flowerCurSelectedText.text = selected.Count == 0
            ? "선택된 꽃 없음"
            : string.Join(", ", selected.Select(b => $"#{b.blockID}"));
    }

    private void OnConfirm()
    {
        if (!wrapperChosen || GameFlowController.Instance == null || selected.Count == 0) return;
        GameFlowController.Instance.ConfirmFlowerSelection(new List<BlockData>(selected));
    }
}
