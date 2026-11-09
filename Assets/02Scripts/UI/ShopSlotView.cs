using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>밤 상점 진열대의 한 칸: 꽃 그림, 구매 버튼, 역할 이름표/꽃 이름/가격. 그림과 글자만 채우고 사는 일은 ShopUI가 한다. 산 꽃은 품절로 보인다.</summary>
public class ShopSlotView : MonoBehaviour
{
    private const string BuyLabel = "구매";
    private const string SoldOutLabel = "품절";
    private static readonly Color SoldOutShade = new(0.45f, 0.45f, 0.45f, 1f);

    [SerializeField] private Image flowerImage;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private GameObject newBadge; // 신규 꽃 NEW 뱃지(프리팹 ShopSlot의 InfoTag/NewBadge)

    private void Awake()
    {
        if (newBadge == null) newBadge = transform.Find("InfoTag/NewBadge")?.gameObject; // 인스펙터 연결이 빠졌을 때의 폴백
    }

    /// <summary>이 칸에 진열된 꽃(튜토리얼이 특정 꽃의 구매 버튼을 가리키는 데 쓴다).</summary>
    public FlowerData Flower { get; private set; }

    public RectTransform BuyRect => (RectTransform)buyButton.transform;

    public Button BuyButton => buyButton;

    public void Bind(FlowerData flower, bool affordable, bool soldOut, bool isNew, Action onBuy)
    {
        Flower = flower;
        if (newBadge != null) newBadge.SetActive(isNew);

        Sprite sprite = flower.stemSprite != null ? flower.stemSprite : flower.iconSprite;
        flowerImage.sprite = sprite;
        flowerImage.color = ColorPalette.ToUnityColor(flower.color) * (soldOut ? SoldOutShade : Color.white);
        flowerImage.enabled = sprite != null;
        roleText.text = flower.RoleName;
        nameText.text = flower.DisplayName;
        priceText.text = $"{flower.shopPrice:N0}원";

        TMP_Text buyLabel = buyButton.GetComponentInChildren<TMP_Text>(true);
        if (buyLabel != null) buyLabel.text = soldOut ? SoldOutLabel : BuyLabel;
        buyButton.interactable = affordable && !soldOut;
        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(() => onBuy());
    }
}
