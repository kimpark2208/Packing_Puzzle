using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 밤 메인 화면: 오늘 밤 우선적으로 손질(획득)하고 싶은 꽃을 요청한다.
/// 확인을 누르면 GameFlowController가 요청 꽃 기반의 메인 스테이지 + 잉여 스테이지 큐를 생성한다.
/// 목록 항목은 하이라키에 미리 배치된 템플릿을 복제해서 만든다.
/// 목록에는 막아 둔 꽃(라벤더, 5칸짜리)을 뺀 모든 꽃이 보유 수량이 적은 순으로 나오고, 항목마다 보유 수량이 보인다.
/// </summary>
public class NightRequestUI : MonoBehaviour
{
    private static readonly Color ItemColor = new(0.25f, 0.22f, 0.35f);
    private static readonly Color SelectedColor = new(0.55f, 0.45f, 0.85f);
    private static readonly Color SelectedIconTint = new(0.55f, 0.55f, 0.55f);

    [SerializeField] private RectTransform listArea;
    [SerializeField] private RectTransform itemTemplate;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button goToNightMainButton;

    private readonly HashSet<int> selected = new();
    private readonly Dictionary<int, Image> itemImages = new();
    private readonly Dictionary<int, Image> iconImages = new();
    private readonly Dictionary<int, Color> iconBaseTints = new();

    private void Start()
    {
        if (itemTemplate != null) itemTemplate.gameObject.SetActive(false);

        if (CurrencyManager.Instance != null && BlockRegistry.Instance != null && listArea != null && itemTemplate != null)
        {
            // 임시: 막아 둔 꽃(라벤더, 5칸짜리)을 뺀 모든 꽃을 보유 수량이 적은 순으로 보여 준다.
            var ids = BlockRegistry.Instance.AllBlocks
                .Where(b => !CurrencyManager.Instance.IsStartingOutOfStock(b.blockID))
                .OrderBy(b => CurrencyManager.Instance.GetFlowerStock(b.blockID))
                .ThenBy(b => b.blockID)
                .Select(b => b.blockID);
            foreach (int id in ids)
            {
                CreateFlowerItem(id);
            }
        }

        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
        if (goToNightMainButton != null) goToNightMainButton.onClick.AddListener(OnGoToNightMain);
    }

    private void OnGoToNightMain()
    {
        if (GameFlowController.Instance == null) return;
        GameFlowController.Instance.GoToNightMainScene();
    }

    private void CreateFlowerItem(int flowerId)
    {
        if (itemTemplate == null || listArea == null) return;

        var block = BlockRegistry.Instance != null ? BlockRegistry.Instance.GetById(flowerId) : null;

        RectTransform itemRT = Instantiate(itemTemplate, listArea);
        itemRT.gameObject.SetActive(true);
        itemRT.name = $"Flower_{flowerId}";

        var img = itemRT.GetComponent<Image>();
        if (img != null)
        {
            img.color = ItemColor;
            itemImages[flowerId] = img;
        }

        if (block == null) return;

        // 임시 UI: 현재 보유 수량(템플릿에 미리 둔 StockText). 마이너스는 0으로 보여 준다.
        Transform stockT = itemRT.Find("StockText");
        TMP_Text stockText = stockT != null ? stockT.GetComponent<TMP_Text>() : null;
        if (stockText != null) stockText.text = $"x{Mathf.Max(0, CurrencyManager.Instance.GetFlowerStock(flowerId))}";

        FlowerPieceView view = itemRT.GetComponent<FlowerPieceView>();
        if (view == null) view = itemRT.gameObject.AddComponent<FlowerPieceView>();
        view.Apply(block, block.iconSprite != null ? block.iconSprite : block.dayPieceSprite, view.Icon.preserveAspect);

        iconImages[flowerId] = view.Icon;
        iconBaseTints[flowerId] = ColorPalette.ToUnityColor(block.color);

        var btn = view.Icon.GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(() => ToggleSelect(flowerId));
    }

    private void ToggleSelect(int flowerId)
    {
        iconBaseTints.TryGetValue(flowerId, out var baseTint);

        if (selected.Contains(flowerId))
        {
            selected.Remove(flowerId);
            if (itemImages.TryGetValue(flowerId, out var img1)) img1.color = ItemColor;
            if (iconImages.TryGetValue(flowerId, out var icon1)) icon1.color = baseTint;
        }
        else
        {
            selected.Add(flowerId);
            if (itemImages.TryGetValue(flowerId, out var img2)) img2.color = SelectedColor;
            if (iconImages.TryGetValue(flowerId, out var icon2)) icon2.color = baseTint * SelectedIconTint;
        }
    }

    private void OnConfirm()
    {
        if (GameFlowController.Instance == null) return;
        GameFlowController.Instance.ConfirmNightRequests(new List<int>(selected));
    }
}
