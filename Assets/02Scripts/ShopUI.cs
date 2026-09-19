using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상점 UI 관리
/// 탭별로 아이템 목록을 표시하고 구매 처리
/// </summary>
public class ShopUI : MonoBehaviour
{
    [SerializeField] private VerticalLayoutGroup itemContainer;
    [SerializeField] private HorizontalLayoutGroup tabButtonContainer;
    [SerializeField] private Text moneyText;

    private ShopManager.ShopCategory currentCategory = ShopManager.ShopCategory.Wrapper;
    private List<GameObject> currentItemUIs = new();

    // 탭 버튼들
    private Button wrapperTabButton;
    private Button furnitureTabButton;
    private Button decoTabButton;
    private Button artifactTabButton;

    private void Awake()
    {
        if (ShopManager.Instance == null)
        {
            Debug.LogError("[ShopUI] ShopManager를 찾을 수 없습니다!");
            return;
        }

        // 모바일 친화적 레이아웃 설정
        if (tabButtonContainer != null)
        {
            tabButtonContainer.padding = new RectOffset(10, 10, 10, 10);
            tabButtonContainer.spacing = 8;
            tabButtonContainer.childForceExpandHeight = true;
        }

        if (itemContainer != null)
        {
            itemContainer.padding = new RectOffset(10, 10, 10, 10);
            itemContainer.spacing = 10;
        }

        CreateTabButtons();
    }

    private void Start()
    {
        // 초기 탭: Wrapper
        ShowCategory(ShopManager.ShopCategory.Wrapper);
        UpdateMoneyDisplay();

        // 금액 변경 이벤트 구독
        EventBus.OnMoneyChanged += OnMoneyChanged;
    }

    private void OnDestroy()
    {
        EventBus.OnMoneyChanged -= OnMoneyChanged;
    }

    /// <summary>
    /// 탭 버튼 생성
    /// </summary>
    private void CreateTabButtons()
    {
        var tabs = new[]
        {
            ("포장지", ShopManager.ShopCategory.Wrapper),
            ("가구", ShopManager.ShopCategory.Furniture),
            ("장식", ShopManager.ShopCategory.Deco),
            ("예술품", ShopManager.ShopCategory.Artifact)
        };

        foreach (var (tabName, category) in tabs)
        {
            var buttonGo = new GameObject($"TabButton_{tabName}");
            buttonGo.transform.SetParent(tabButtonContainer.transform, false);

            var button = buttonGo.AddComponent<Button>();
            var layoutElement = buttonGo.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 100;
            layoutElement.preferredHeight = 54;
            layoutElement.flexibleWidth = 1f;

            var text = buttonGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = tabName;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
            text.fontSize = 18;

            var image = buttonGo.AddComponent<Image>();
            image.color = new Color(0.9f, 0.9f, 0.9f);

            button.onClick.AddListener(() => ShowCategory(category));
        }
    }

    /// <summary>
    /// 특정 카테고리의 아이템 표시
    /// </summary>
    public void ShowCategory(ShopManager.ShopCategory category)
    {
        currentCategory = category;

        // 기존 아이템 UI 제거
        foreach (var itemUI in currentItemUIs)
        {
            Destroy(itemUI);
        }
        currentItemUIs.Clear();

        // 해당 카테고리의 아이템 가져오기
        var items = ShopManager.Instance.GetItemsByCategory(category);

        // 각 아이템마다 UI 생성
        foreach (var item in items)
        {
            CreateItemUI(item);
        }

        Debug.Log($"[ShopUI] {category} 카테고리 표시 ({items.Count}개 아이템)");
    }

    /// <summary>
    /// 개별 아이템 UI 생성
    /// </summary>
    private void CreateItemUI(ShopManager.ShopItem item)
    {
        var itemGo = new GameObject($"ShopItem_{item.itemId}");
        itemGo.transform.SetParent(itemContainer.transform, false);

        var layoutElement = itemGo.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 110;
        layoutElement.flexibleHeight = 0;

        // 아이템 정보 텍스트
        var infoGo = new GameObject("ItemInfo");
        infoGo.transform.SetParent(itemGo.transform, false);

        var infoText = infoGo.AddComponent<Text>();
        infoText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        infoText.text = $"{item.itemName}\n{item.description}\n가격: {item.price}원";
        infoText.alignment = TextAnchor.MiddleLeft;
        infoText.color = Color.black;
        infoText.fontSize = 16;

        var infoRect = infoGo.GetComponent<RectTransform>();
        infoRect.offsetMin = new Vector2(15, 5);
        infoRect.offsetMax = new Vector2(-105, -5);

        // 구매/보유 버튼
        var buttonGo = new GameObject("BuyButton");
        buttonGo.transform.SetParent(itemGo.transform, false);

        var button = buttonGo.AddComponent<Button>();
        var buttonLayout = buttonGo.AddComponent<LayoutElement>();
        buttonLayout.preferredWidth = 90;
        buttonLayout.preferredHeight = 54;

        var buttonText = buttonGo.AddComponent<Text>();
        buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        buttonText.text = item.price == 0 ? "보유 중" : "구매";
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.color = Color.black;
        buttonText.fontSize = 16;

        var buttonImage = buttonGo.AddComponent<Image>();
        buttonImage.color = item.price == 0 ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.8f, 0.9f, 0.8f);

        var buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.offsetMin = new Vector2(-100, 5);
        buttonRect.offsetMax = new Vector2(-5, -5);

        // 배경
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(itemGo.transform, false);
        bgGo.transform.SetAsFirstSibling();

        var bgImage = bgGo.AddComponent<Image>();
        bgImage.color = new Color(1f, 1f, 1f, 0.95f);

        var bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // 구매 버튼 클릭 리스너
        if (item.price > 0 && !IsItemOwned(item))
        {
            button.onClick.AddListener(() => OnBuyClicked(item, button, buttonText));
        }
        else if (item.price > 0 && IsItemOwned(item))
        {
            button.interactable = false;
            buttonText.text = "보유 중";
            buttonImage.color = new Color(0.7f, 0.7f, 0.7f);
        }

        currentItemUIs.Add(itemGo);
    }

    /// <summary>
    /// 아이템 소유 여부 확인
    /// </summary>
    private bool IsItemOwned(ShopManager.ShopItem item)
    {
        if (item.category == ShopManager.ShopCategory.Wrapper)
        {
            return CurrencyManager.Instance.HasWrapper(item.itemId);
        }
        // TODO: 가구/장식/예술품은 나중에 구매 여부 추적
        return false;
    }

    /// <summary>
    /// 구매 버튼 클릭
    /// </summary>
    private void OnBuyClicked(ShopManager.ShopItem item, Button button, Text buttonText)
    {
        bool purchased = CurrencyManager.Instance.TrySpendMoney(item.price);

        if (purchased)
        {
            Debug.Log($"[ShopUI] {item.itemName} 구매 완료");

            // 포장지 해제
            if (item.category == ShopManager.ShopCategory.Wrapper)
            {
                CurrencyManager.Instance.UnlockWrapper(item.itemId);
            }

            // 버튼 비활성화
            button.interactable = false;
            buttonText.text = "보유 중";
            button.GetComponent<Image>().color = new Color(0.7f, 0.7f, 0.7f);
        }
        else
        {
            Debug.LogWarning($"[ShopUI] 금액 부족 ({item.price}원 필요)");
        }
    }

    /// <summary>
    /// 금액 표시 업데이트
    /// </summary>
    private void UpdateMoneyDisplay()
    {
        if (moneyText != null)
        {
            moneyText.text = $"금액: {CurrencyManager.Instance.CurrentMoney}원";
        }
    }

    /// <summary>
    /// 금액 변경 이벤트 핸들러
    /// </summary>
    private void OnMoneyChanged(int newAmount)
    {
        UpdateMoneyDisplay();
    }
}
