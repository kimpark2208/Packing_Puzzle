using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 꽃 선택 화면. 좋피위피/기획서의 "포장지 선택 -> 꽃 선택" 순서를 그대로 따른다.
/// 1단계: 보유한 포장지 중 이번 주문에 쓸 것을 하나 고른다 (목표 점수/정답 레시피가 이때 확정됨).
/// 2단계: 포장지를 고르면, 그 포장지 단계에서 쓸 수 있는 꽃 중 최대 3종을 가챠 풀로 고른다.
/// 목록 항목(포장지/꽃)은 하이라키에 미리 배치된 템플릿을 복제해서 만든다.
/// </summary>
public class FlowerSelectUI : MonoBehaviour
{
    private const int MaxSelectable = 3;

    private static readonly Color ItemColor = new(1f, 1f, 1f);
    private static readonly Color SelectedColor = new(0.65f, 0.85f, 0.70f);
    private static readonly Color WrapperColor = new(0.85f, 0.80f, 0.95f);
    private static readonly Color WrapperSelectedColor = new(0.55f, 0.45f, 0.85f);

    [SerializeField] private Text headerText;
    [SerializeField] private RectTransform wrapperArea;
    [SerializeField] private RectTransform wrapperItemTemplate;
    [SerializeField] private RectTransform flowerArea;
    [SerializeField] private RectTransform flowerItemTemplate;
    [SerializeField] private Button confirmButton;

    private readonly HashSet<BlockData> selected = new();
    private readonly Dictionary<BlockData, Image> itemImages = new();
    private readonly Dictionary<int, Image> wrapperImages = new();
    private bool wrapperChosen;

    private void Start()
    {
        wrapperItemTemplate.gameObject.SetActive(false);
        flowerItemTemplate.gameObject.SetActive(false);
        flowerArea.gameObject.SetActive(true);

        confirmButton.interactable = false;
        confirmButton.onClick.AddListener(OnConfirm);

        BuildWrapperChoices();
    }

    private void BuildWrapperChoices()
    {
        var owned = CurrencyManager.Instance.OwnedWrappers;
        foreach (int wrapperId in owned)
        {
            var item = ShopManager.Instance.GetItemById(wrapperId);
            string label = item.HasValue ? item.Value.itemName : $"포장지 {wrapperId}";

            RectTransform itemRT = Instantiate(wrapperItemTemplate, wrapperArea);
            itemRT.gameObject.SetActive(true);
            itemRT.name = $"Wrapper_{wrapperId}";

            var img = itemRT.GetComponent<Image>();
            img.color = WrapperColor;
            wrapperImages[wrapperId] = img;

            itemRT.GetComponentInChildren<Text>().text = label;

            int capturedId = wrapperId;
            itemRT.GetComponent<Button>().onClick.AddListener(() => OnWrapperChosen(capturedId));
        }
    }

    private void OnWrapperChosen(int wrapperId)
    {
        foreach (var kvp in wrapperImages) kvp.Value.color = WrapperColor;
        wrapperImages[wrapperId].color = WrapperSelectedColor;

        GameFlowController.Instance.ChooseWrapper(wrapperId);
        wrapperChosen = true;

        var order = GameFlowController.Instance.CurrentDayOrder;
        headerText.text = $"{order.gridSize}x{order.gridSize} 포장지 확정! 목표 점수: {order.targetScore}\n오늘 사용할 꽃을 최대 {MaxSelectable}종 고르세요";

        BuildFlowerChoices(order);
    }

    private void BuildFlowerChoices(DayPuzzleGenerator.DayOrder order)
    {
        foreach (Transform child in flowerArea)
        {
            if (child == flowerItemTemplate.transform) continue;
            Destroy(child.gameObject);
        }
        itemImages.Clear();
        selected.Clear();
        confirmButton.interactable = false;

        int tier = ShopManager.Instance.GetTier(order.wrapperId);
        var obtainedIds = CurrencyManager.Instance.GetObtainedFlowerIds();
        var candidates = BlockDatabase.Instance.GetObtainedBlocksForWrapperTier(obtainedIds, tier);

        foreach (var block in candidates)
        {
            CreateFlowerItem(block);
        }
    }

    private void CreateFlowerItem(BlockData block)
    {
        RectTransform itemRT = Instantiate(flowerItemTemplate, flowerArea);
        itemRT.gameObject.SetActive(true);
        itemRT.name = $"Flower_{block.blockID}";

        var img = itemRT.GetComponent<Image>();
        img.color = ItemColor;
        itemImages[block] = img;

        string colorName = ColorPalette.ToKoreanName(block.color);
        itemRT.GetComponentInChildren<Text>().text = $"꽃 #{block.blockID}\n{colorName} / {block.CellCount}칸";

        itemRT.GetComponent<Button>().onClick.AddListener(() => ToggleSelect(block));
    }

    private void ToggleSelect(BlockData block)
    {
        if (selected.Contains(block))
        {
            selected.Remove(block);
            itemImages[block].color = ItemColor;
        }
        else
        {
            if (selected.Count >= MaxSelectable) return;
            selected.Add(block);
            itemImages[block].color = SelectedColor;
        }

        confirmButton.interactable = wrapperChosen && selected.Count > 0;
    }

    private void OnConfirm()
    {
        if (!wrapperChosen) return;
        GameFlowController.Instance.ConfirmFlowerSelection(new List<BlockData>(selected));
    }
}
