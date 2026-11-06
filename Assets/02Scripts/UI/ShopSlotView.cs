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

    public void Bind(FlowerData flower, bool affordable, bool soldOut, Action onBuy)
    {
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
