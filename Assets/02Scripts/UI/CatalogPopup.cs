using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전화로 여는 카탈로그 팝업. 탭(가구/꾸미기 용품/유물)마다 ShopManager의 해당 카테고리 아이템을
/// 2열 카드 목록으로 보여 주고, 카드의 주문 버튼으로 구매한다.
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
    [SerializeField] private Toggle[] chips; // 항목 칩: 켜져 있는 동안 어두운 배경 + 흰 글자 (꺼진 모습은 프리팹에 둔 색)
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private RectTransform content;
    [SerializeField] private RectTransform itemTemplate;
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private Button closeButton;

    private readonly HashSet<int> ordered = new(); // 이번 씬에서 주문한 아이템. TODO: 가구/꾸미기 용품/유물의 구매(보유) 여부는 아직 저장하지 않는다
    private int selected;
    private Color[] tabBaseColors; // 프리팹에 둔 탭 색/높이(고르지 않은 탭의 모습)
    private float[] tabBaseHeights;

    private void Awake()
    {
        itemTemplate.gameObject.SetActive(false);
        closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        tabBaseColors = new Color[tabs.Length];
        tabBaseHeights = new float[tabs.Length];
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            tabs[i].button.onClick.AddListener(() => Select(index));
            tabBaseColors[i] = tabs[i].button.targetGraphic.color;
            tabBaseHeights[i] = ((RectTransform)tabs[i].button.transform).sizeDelta.y;
        }

        foreach (Toggle chip in chips) BindChip(chip);
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
    }

    private void Select(int index)
    {
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
        Fill(tabs[index].category);
        scroll.verticalNormalizedPosition = 1f;
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

        if (ShopManager.Instance == null) return;

        foreach (var item in ShopManager.Instance.GetItemsByCategory(category))
        {
            RectTransform card = Instantiate(itemTemplate, content);
            card.gameObject.SetActive(true);
            card.name = $"Item_{item.itemId}";
            card.Find("Name").GetComponent<TMP_Text>().text = item.itemName;
            card.Find("Name/Desc").GetComponent<TMP_Text>().text = item.description; // Desc는 Name의 자식이다
            card.Find("PriceText").GetComponent<TMP_Text>().text = item.price.ToString("N0");

            var order = card.Find("OrderBTN").GetComponent<Button>();
            var orderText = order.GetComponentInChildren<TMP_Text>();
            if (ordered.Contains(item.itemId))
            {
                MarkOrdered(order, orderText);
                continue;
            }

            order.onClick.AddListener(() =>
            {
                if (CurrencyManager.Instance == null || !CurrencyManager.Instance.TrySpendMoney(item.price)) return;
                ordered.Add(item.itemId);
                MarkOrdered(order, orderText);
            });
        }
    }

    private static void MarkOrdered(Button button, TMP_Text text)
    {
        button.interactable = false;
        text.text = "주문 완료";
    }
}
