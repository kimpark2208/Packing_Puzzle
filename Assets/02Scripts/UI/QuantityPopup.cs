using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 수량 팝업(카탈로그에서 꾸미기 용품의 주문을 누르면 뜬다). 예약 보관 팝업(ReservationStoragePopup)과 같은 틀이다:
/// 슬라이더와 +/− 버튼으로 주문할 수(0 ~ MaxQuantity)를 고르고 주문하기를 누르면 onConfirm(수량)을 부른다.
/// 주문하기 버튼 글씨에는 수량에 따른 총 가격이 실시간으로 보인다. 틀은 에디터에 만들어 두고, 코드는 글씨와 슬라이더 범위만 채운다.
/// 카드(PanelMotion이 붙은 몸통)가 중앙에서 커지며 나타난다.
/// </summary>
public class QuantityPopup : MonoBehaviour
{
    public const int MaxQuantity = 50;

    [SerializeField, Tooltip("카드(PanelMotion이 붙은, 막은 제외한 팝업 몸통).")] private GameObject card;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField, Tooltip("지금 가진 수.")] private TMP_Text stockText;
    [SerializeField, Tooltip("고른 수만큼 늘어난다는 표시(예: +3).")] private TMP_Text deltaText;
    [SerializeField] private Slider slider;
    [SerializeField, Tooltip("슬라이더 오른쪽 끝(최대) 숫자.")] private TMP_Text maxText;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;
    [SerializeField, Tooltip("주문하기. 안의 글씨에 총 가격이 보인다.")] private Button confirmButton;
    [SerializeField] private Button closeButton;

    private int unitPrice;
    private Func<int, bool> confirmed;

    private void Awake()
    {
        slider.wholeNumbers = true;
        slider.onValueChanged.AddListener(_ => Refresh());
        minusButton.onClick.AddListener(() => slider.value -= 1f);
        plusButton.onClick.AddListener(() => slider.value += 1f);
        confirmButton.onClick.AddListener(Confirm);
        closeButton.onClick.AddListener(Close);
    }

    /// <summary>팝업을 연다. art는 없어도 된다. 주문하기를 누르면 onConfirm(수량)을 부르고, true를 돌려주면 닫는다(돈을 내는 것은 부른 쪽이 하고, 돈이 모자라 false면 열어 둔다).</summary>
    public void Open(string itemName, Sprite art, int owned, int unitPrice, Func<int, bool> onConfirm)
    {
        this.unitPrice = unitPrice;
        confirmed = onConfirm;

        nameText.text = itemName;
        stockText.text = owned.ToString();
        icon.enabled = art != null;
        icon.sprite = art;
        maxText.text = MaxQuantity.ToString();
        slider.minValue = 0f;
        slider.maxValue = MaxQuantity;
        slider.SetValueWithoutNotify(0f);
        Refresh();

        card.SetActive(true); // 닫을 때 카드가 꺼지므로 다시 켠다
        gameObject.SetActive(true);
    }

    private void Refresh()
    {
        int amount = Mathf.RoundToInt(slider.value);
        deltaText.text = $"+{amount}";
        confirmButton.interactable = amount > 0;
        minusButton.interactable = amount > 0;
        plusButton.interactable = amount < MaxQuantity;

        TMP_Text label = confirmButton.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = $"주문하기 ({unitPrice * amount:N0})";
    }

    private void Confirm()
    {
        int amount = Mathf.RoundToInt(slider.value);
        if (amount <= 0) return;

        if (confirmed != null && !confirmed(amount)) return; // 돈이 모자라면 열어 둔다

        confirmed = null;
        Close();
    }

    private void Close()
    {
        PanelMotion.Close(card, () => gameObject.SetActive(false));
    }
}
