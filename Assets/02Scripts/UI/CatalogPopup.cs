using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전화로 여는 카탈로그 팝업. 탭(가구/꾸미기 용품/유물)마다 해당 아이템을 2열 카드 목록으로 보여 주고, 카드의 주문 버튼으로 구매한다.
/// 꾸미기 용품·유물은 ShopManager의 아이템, 가구는 FurnitureData(DataRegistry)에서 읽으며 가구 탭의 칩이 가구 카테고리(카운터/야외/꽃 선반/벽지&바닥재)를 고른다.
/// UI는 프리팹에 미리 배치되어 있고, 카드 템플릿을 복제해 내용만 채운다.
/// </summary>
public class CatalogPopup : MonoBehaviour
{
    [Serializable]
    private class Tab
    {
        public Button button;
        public ShopManager.ShopCategory category;
    }

    private static readonly Color ChipOnColor = new(0.35f, 0.35f, 0.35f);

    [SerializeField] private Tab[] tabs;
    [Header("탭: 프리팹에 둔 색과 높이가 기본이고, 선택된 탭만 아래 값으로 바꾼다")]
    [SerializeField] private Color selectedTabColor = Color.clear; // 색 선택기로 고르면 알파가 0인 채로 남기 쉬워서 알파는 무시하고(탭의 원래 알파를 쓴다), 값이 (0,0,0,0) 그대로일 때만 색을 바꾸지 않는다
    [SerializeField] private float selectedTabHeightBonus; // 선택된 탭이 기본 높이보다 더 커지는 양(0이면 그대로)
    [SerializeField, Tooltip("항목 칩(같은 ToggleGroup, 하나만 켜짐): 켜져 있는 동안 어두운 배경 + 흰 글자 (꺼진 모습은 프리팹에 둔 색). 가구 탭에서만 보이고, 가구 카테고리마다 하나씩 쓴다(카테고리보다 칩이 적으면 모자란 만큼 못 쓴다).")] private Toggle[] chips;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private RectTransform content;
    [SerializeField] private RectTransform itemTemplate;
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private Button closeButton;
    [Header("확대 보기: 카드의 확대 버튼을 누르면 카드 목록 대신 이 영역에 그림을 크게 보여 준다")]
    [SerializeField] private GameObject expandArea;
    [SerializeField, Tooltip("확대 보기 영역 안에 그림을 보여 줄 Image(ZoomPanView의 content).")] private Image expandedSprite;
    [SerializeField, Tooltip("확대 보기 영역의 축소 버튼: 누르면 카드 목록으로 돌아간다.")] private Button shrinkButton;

    private readonly HashSet<string> ordered = new(); // 이번 씬에서 주문한 아이템("종류번호" 키). TODO: 가구와 유물의 구매(보유) 여부는 아직 저장하지 않는다(꾸미기 용품은 주문하면 CurrencyManager 재고에 들어간다)
    private static readonly FurnitureCategory[] FurnitureCategories = (FurnitureCategory[])Enum.GetValues(typeof(FurnitureCategory));

    private bool clockPaused; // 이 팝업이 시간을 멈춰 두었는가(열릴 때 멈추고 닫힐 때 푼다)
    private int selected;
    private int selectedChip;    // 가구 탭에서 고른 칩(가구 카테고리 번호)
    private bool syncingChips;    // 코드가 칩을 맞추는 동안은 칩을 눌러도 목록을 다시 채우지 않는다
    private Color[] tabBaseColors; // 프리팹에 둔 탭 색/높이(고르지 않은 탭의 모습)
    private float[] tabBaseHeights;

    private void Awake()
    {
        itemTemplate.gameObject.SetActive(false);
        expandArea.SetActive(false);
        closeButton.onClick.AddListener(() => PanelMotion.Close(gameObject));
        shrinkButton.onClick.AddListener(CloseExpand);
        tabBaseColors = new Color[tabs.Length];
        tabBaseHeights = new float[tabs.Length];
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            tabs[i].button.onClick.AddListener(() => Select(index));
            tabBaseColors[i] = tabs[i].button.targetGraphic.color;
            tabBaseHeights[i] = ((RectTransform)tabs[i].button.transform).sizeDelta.y;
        }

        for (int i = 0; i < chips.Length; i++)
        {
            int index = i;
            BindChip(chips[i]);
            chips[i].onValueChanged.AddListener(on => OnChip(index, on));
        }
    }

    private void OnChip(int index, bool on)
    {
        if (syncingChips || !on) return;

        CloseExpand();
        selectedChip = index;
        Fill(ShopManager.ShopCategory.Furniture);
        scroll.horizontalNormalizedPosition = 0f;
    }

    /// <summary>지금 카탈로그에 나오는 가구: 카탈로그 표시가 켜져 있고, 나오기 시작하는 일차가 된 것.</summary>
    private static IEnumerable<FurnitureData> AvailableFurniture()
    {
        if (DataRegistry.Instance == null) return Enumerable.Empty<FurnitureData>();

        int day = CurrencyManager.Instance != null ? CurrencyManager.Instance.CurrentDay : int.MaxValue;
        return DataRegistry.Instance.Furniture.Where(f => f != null && f.inCatalog && f.availableFromDay <= day);
    }

    /// <summary>칩으로 보여 줄 가구 카테고리: 지금 나오는 가구가 하나라도 있는 카테고리만.</summary>
    private static List<FurnitureCategory> ChipCategories()
    {
        List<FurnitureData> available = AvailableFurniture().ToList();
        return FurnitureCategories.Where(c => available.Any(f => f.category == c)).ToList();
    }

    /// <summary>가구 탭의 칩을 가구 카테고리 이름으로 채우고 고른 칩을 켠다. 가구 탭이 아니면 칩 줄을 숨긴다.</summary>
    private void ShowChips(bool furniture)
    {
        List<FurnitureCategory> categories = furniture ? ChipCategories() : new List<FurnitureCategory>();
        chips[0].transform.parent.gameObject.SetActive(categories.Count > 0);
        if (categories.Count == 0) return;

        selectedChip = Mathf.Clamp(selectedChip, 0, categories.Count - 1);
        syncingChips = true;
        for (int i = 0; i < chips.Length; i++)
        {
            bool used = i < categories.Count;
            chips[i].gameObject.SetActive(used);
            if (used) chips[i].GetComponentInChildren<TMP_Text>().text = FurnitureData.CategoryName(categories[i]);
        }
        chips[selectedChip].isOn = true; // 같은 ToggleGroup의 다른 칩은 저절로 꺼진다
        syncingChips = false;
    }

    private static void BindChip(Toggle chip)
    {
        Graphic fill = chip.targetGraphic;
        TMP_Text label = chip.GetComponentInChildren<TMP_Text>();
        Color offFill = fill.color, offText = label.color;

        void Apply(bool on)
        {
            fill.color = on ? ChipOnColor : offFill;
            label.color = on ? Color.white : offText;
        }

        chip.onValueChanged.AddListener(Apply);
        Apply(chip.isOn);
    }

    private void OnEnable()
    {
        Select(selected);
        if (GameClock.Instance != null && !clockPaused)
        {
            GameClock.Instance.SetPaused(true); // 카탈로그를 보는 동안은 하루 시간과 손님 기분 시간이 멈춘다
            clockPaused = true;
        }
    }

    private void OnDisable()
    {
        if (GameClock.Instance != null && clockPaused) GameClock.Instance.SetPaused(false);
        clockPaused = false;
    }

    private void Select(int index)
    {
        CloseExpand();
        selected = index;
        for (int i = 0; i < tabs.Length; i++)
        {
            bool on = i == index;
            var rt = (RectTransform)tabs[i].button.transform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, tabBaseHeights[i] + (on ? selectedTabHeightBonus : 0f));
            Color baseColor = tabBaseColors[i];
            bool tint = on && selectedTabColor != Color.clear;
            tabs[i].button.targetGraphic.color = tint ? new Color(selectedTabColor.r, selectedTabColor.g, selectedTabColor.b, baseColor.a) : baseColor;
        }

        titleText.text = tabs[index].button.GetComponentInChildren<TMP_Text>().text;
        ShowChips(tabs[index].category == ShopManager.ShopCategory.Furniture);
        Fill(tabs[index].category);
        scroll.horizontalNormalizedPosition = 0f;
    }

    private void Fill(ShopManager.ShopCategory category)
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            if (child == itemTemplate) continue;
            child.gameObject.SetActive(false); // Destroy는 프레임 끝에 실행되므로, 그 전까지 목록 배치에 끼지 않게 먼저 끈다
            Destroy(child.gameObject);
        }

        if (category == ShopManager.ShopCategory.Furniture) // 가구는 FurnitureData에서 읽고, 고른 칩의 카테고리만 보여 준다
        {
            List<FurnitureCategory> categories = ChipCategories();
            if (categories.Count == 0) return;

            FurnitureCategory chipCategory = categories[Mathf.Clamp(selectedChip, 0, categories.Count - 1)];
            foreach (FurnitureData furniture in AvailableFurniture().Where(f => f.category == chipCategory).OrderBy(f => f.id))
            {
                FurnitureData item = furniture;
                string key = $"Furniture{item.id}";
                if (CurrencyManager.Instance != null && CurrencyManager.Instance.IsFurnitureOrdered(item.id)) ordered.Add(key); // 주문했거나 이미 설치된 가구는 다시 주문할 수 없다
                AddCard(key, item.displayName, item.description, item.price, item.CatalogSprite, () => CurrencyManager.Instance.OrderFurniture(item.id)); // 주문하면 다음 날 설치된다
            }
            return;
        }

        if (category == ShopManager.ShopCategory.Deco) // 꾸미기 용품은 DecorationData(DataRegistry)에서 읽고, 주문하면 바로 재고에 들어간다
        {
            foreach (DecorationData decoration in AvailableDecorations().OrderBy(d => d.id))
            {
                DecorationData item = decoration;
                AddCard($"Deco{item.id}", item.displayName, item.description, item.price, item.sprite, () => CurrencyManager.Instance.AddDecorationStock(item.id, 1));
            }
            return;
        }

        if (ShopManager.Instance == null) return;

        foreach (var item in ShopManager.Instance.GetItemsByCategory(category))
            AddCard($"{category}{item.itemId}", item.itemName, item.description, item.price, null);
    }

    /// <summary>지금 카탈로그에 나오는 꾸미기 용품: 카탈로그 표시가 켜져 있고, 나오기 시작하는 일차가 되었고, 아직 가지고 있지 않은 것(이미 산 용품은 나오지 않는다).</summary>
    private static IEnumerable<DecorationData> AvailableDecorations()
    {
        if (DataRegistry.Instance == null) return Enumerable.Empty<DecorationData>();

        CurrencyManager currency = CurrencyManager.Instance;
        int day = currency != null ? currency.CurrentDay : int.MaxValue;
        return DataRegistry.Instance.Decorations.Where(d => d != null && d.inCatalog && d.availableFromDay <= day && (currency == null || currency.GetDecorationStock(d.id) <= 0));
    }

    /// <summary>카드 템플릿을 복제해 내용을 채운다. sprite가 없으면 템플릿의 그림을 그대로 둔다. 주문하면(돈을 낸 뒤) onOrdered를 부른다.</summary>
    private void AddCard(string key, string itemName, string description, int price, Sprite sprite, Action onOrdered = null)
    {
        RectTransform card = Instantiate(itemTemplate, content);
        card.gameObject.SetActive(true);
        card.name = $"Item_{key}";
        card.Find("Body/Name").GetComponent<TMP_Text>().text = itemName;
        card.Find("Body/Desc").GetComponent<TMP_Text>().text = description;
        card.Find("Body/PriceIcon/PriceText").GetComponent<TMP_Text>().text = price.ToString("N0"); // PriceText는 PriceIcon의 자식이다
        var art = card.Find("Image/Sprite").GetComponent<Image>(); // 이미지 칸 안에 가운데로 놓인 그림(비율을 지킨다). 그림이 없으면 칸만 보인다
        art.enabled = sprite != null;
        art.sprite = sprite;

        var expand = card.Find("Image/ExpandBTN").GetComponent<Button>();
        expand.gameObject.SetActive(sprite != null); // 그림이 없으면 크게 볼 것도 없다
        expand.onClick.AddListener(() => OpenExpand(sprite));

        var order = card.Find("Body/OrderBTN").GetComponent<Button>();
        var orderText = order.GetComponentInChildren<TMP_Text>();
        if (ordered.Contains(key))
        {
            MarkOrdered(order, orderText);
            return;
        }

        order.onClick.AddListener(() =>
        {
            if (CurrencyManager.Instance == null || !CurrencyManager.Instance.TrySpendMoney(price)) return;
            ordered.Add(key);
            onOrdered?.Invoke();
            MarkOrdered(order, orderText);
        });
    }

    /// <summary>카드 목록(목록과 스크롤바)을 숨기고 확대 보기 영역에 그림을 크게 보여 준다.</summary>
    private void OpenExpand(Sprite sprite)
    {
        expandedSprite.sprite = sprite;
        expandedSprite.enabled = true;
        for (Transform t = expandedSprite.transform; t != null && t != expandArea.transform; t = t.parent) t.gameObject.SetActive(true); // 그림을 담은 오브젝트(ExpandIMG, Sprite)도 에디터에서 꺼 두었더라도 같이 켠다
        expandArea.GetComponent<ZoomPanView>().ResetView();
        SetListVisible(false);
        expandArea.SetActive(true);
    }

    /// <summary>확대 보기를 닫고 카드 목록으로 돌아간다.</summary>
    private void CloseExpand()
    {
        expandArea.SetActive(false);
        SetListVisible(true);
    }

    private void SetListVisible(bool visible)
    {
        scroll.gameObject.SetActive(visible);
        scroll.horizontalScrollbar.gameObject.SetActive(visible);
    }

    private static void MarkOrdered(Button button, TMP_Text text)
    {
        button.interactable = false;
        text.text = "주문 완료";
    }
}
