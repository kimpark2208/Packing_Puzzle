using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 꽃 선택 화면. 좋피위피/기획서의 "포장지 선택 -> 꽃 선택" 순서를 그대로 따른다.
/// 1단계: 보유한 포장지 중 이번 주문에 쓸 것을 하나 고른다 (목표 점수/정답 레시피가 이때 확정됨).
/// 2단계: 포장지를 고르면, 그 포장지 단계에서 쓸 수 있는 꽃 중 최대 3종을 가챠 풀로 고른다.
/// </summary>
public class FlowerSelectUI : MonoBehaviour
{
    private const int MaxSelectable = 3;

    private static readonly Color BgColor = new(0.90f, 0.95f, 0.92f);
    private static readonly Color ItemColor = new(1f, 1f, 1f);
    private static readonly Color SelectedColor = new(0.65f, 0.85f, 0.70f);
    private static readonly Color ConfirmColor = new(0.45f, 0.65f, 0.95f);
    private static readonly Color WrapperColor = new(0.85f, 0.80f, 0.95f);
    private static readonly Color WrapperSelectedColor = new(0.55f, 0.45f, 0.85f);

    private readonly HashSet<BlockData> selected = new();
    private readonly Dictionary<BlockData, Image> itemImages = new();
    private readonly Dictionary<int, Image> wrapperImages = new();

    private RectTransform wrapperArea;
    private RectTransform flowerArea;
    private Text headerText;
    private Button confirmButton;
    private bool wrapperChosen;

    private void Start()
    {
        Canvas canvas = UIFactory.EnsureCanvas();
        RectTransform root = UIFactory.CreateFullStretchPanel("FlowerSelectRoot", canvas.transform, BgColor);

        var header = CreateBox(root, new Vector2(0, 0.88f), new Vector2(1, 1f));
        headerText = UIFactory.CreateText(header, "먼저 사용할 포장지를 고르세요", 26, Color.black);

        wrapperArea = CreateBox(root, new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.86f));
        var wrapperGrid = wrapperArea.gameObject.AddComponent<GridLayoutGroup>();
        wrapperGrid.cellSize = new Vector2(220, 90);
        wrapperGrid.spacing = new Vector2(12, 12);
        wrapperGrid.childAlignment = TextAnchor.UpperCenter;

        flowerArea = CreateBox(root, new Vector2(0.05f, 0.2f), new Vector2(0.95f, 0.6f));
        var flowerGrid = flowerArea.gameObject.AddComponent<GridLayoutGroup>();
        flowerGrid.cellSize = new Vector2(220, 90);
        flowerGrid.spacing = new Vector2(12, 12);
        flowerGrid.childAlignment = TextAnchor.UpperCenter;
        flowerArea.gameObject.SetActive(false);

        confirmButton = UIFactory.CreateButton(root, "확인", ConfirmColor, Color.white);
        var confirmRect = (RectTransform)confirmButton.transform;
        confirmRect.anchorMin = new Vector2(0.3f, 0.04f);
        confirmRect.anchorMax = new Vector2(0.7f, 0.14f);
        confirmRect.offsetMin = Vector2.zero;
        confirmRect.offsetMax = Vector2.zero;
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

            var go = new GameObject($"Wrapper_{wrapperId}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(wrapperArea, false);

            var img = go.GetComponent<Image>();
            img.color = WrapperColor;
            wrapperImages[wrapperId] = img;

            UIFactory.CreateText(go.transform, label, 20, Color.black);
            go.GetComponent<Button>().onClick.AddListener(() => OnWrapperChosen(wrapperId));
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
        foreach (Transform child in flowerArea) Destroy(child.gameObject);
        itemImages.Clear();
        selected.Clear();
        confirmButton.interactable = false;
        flowerArea.gameObject.SetActive(true);

        int tier = ShopManager.Instance.GetTier(order.wrapperId);
        var obtainedIds = CurrencyManager.Instance.GetObtainedFlowerIds();
        var candidates = BlockDatabase.Instance.GetObtainedBlocksForWrapperTier(obtainedIds, tier);

        foreach (var block in candidates)
        {
            CreateFlowerItem(flowerArea, block);
        }
    }

    private void CreateFlowerItem(Transform parent, BlockData block)
    {
        var go = new GameObject($"Flower_{block.blockID}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = ItemColor;
        itemImages[block] = img;

        string colorName = ColorPalette.ToKoreanName(block.color);
        UIFactory.CreateText(go.transform, $"꽃 #{block.blockID}\n{colorName} / {block.CellCount}칸", 18, Color.black);

        go.GetComponent<Button>().onClick.AddListener(() => ToggleSelect(block));
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

    private static RectTransform CreateBox(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject("Box", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }
}
