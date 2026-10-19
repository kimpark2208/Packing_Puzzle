using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보관 팝업(달력 화면의 +버튼). 예약에 필요한 항목(꽃, 포장지)을 재고에서 덜어 예약용으로 따로 보관한다.
/// 슬라이더로 보관할 수를 고르고(0 ~ 지금 보관할 수 있는 최대: 재고와 더 필요한 수 중 작은 쪽) 보관하기를 누르면 보관된다. 한 번 보관한 것은 다시 꺼낼 수 없다.
/// 틀은 에디터에 만들어 두고, 코드는 글씨와 슬라이더 범위만 채운다. 카드(PanelMotion이 붙은 몸통)가 중앙에서 커지며 나타난다.
/// </summary>
public class ReservationStoragePopup : MonoBehaviour
{
    [SerializeField, Tooltip("카드(PanelMotion이 붙은, 막은 제외한 팝업 몸통).")] private GameObject card;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField, Tooltip("지금 재고(보관 전).")] private TMP_Text stockText;
    [SerializeField, Tooltip("고른 수만큼 재고에서 빠진다는 표시(예: -3).")] private TMP_Text deltaText;
    [SerializeField] private Slider slider;
    [SerializeField, Tooltip("슬라이더 오른쪽 끝(최대) 숫자.")] private TMP_Text maxText;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button closeButton;

    private ReservationRecord record;
    private int itemIndex;
    private Action stored;

    private void Awake()
    {
        slider.wholeNumbers = true;
        slider.onValueChanged.AddListener(_ => Refresh());
        minusButton.onClick.AddListener(() => slider.value -= 1f);
        plusButton.onClick.AddListener(() => slider.value += 1f);
        confirmButton.onClick.AddListener(Confirm);
        closeButton.onClick.AddListener(Close);
    }

    /// <summary>record의 itemIndex번째 항목을 보관하는 팝업을 연다. 보관하면 done을 부른다.</summary>
    public void Open(ReservationRecord record, int itemIndex, Action done)
    {
        this.record = record;
        this.itemIndex = itemIndex;
        stored = done;

        ReservationData.Item item = record.data.items[itemIndex];
        int max = CurrencyManager.Instance.MaxStorable(record, itemIndex);
        nameText.text = item.name;
        stockText.text = CurrencyManager.Instance.GetItemStock(item).ToString();
        Sprite art = item.ResolveIcon();
        icon.enabled = art != null;
        icon.sprite = art;
        maxText.text = max.ToString();
        slider.minValue = 0f;
        slider.maxValue = max;
        slider.SetValueWithoutNotify(0f);
        Refresh();

        card.SetActive(true); // 닫을 때 카드가 꺼지므로 다시 켠다
        gameObject.SetActive(true);
    }

    private void Refresh()
    {
        int amount = Mathf.RoundToInt(slider.value);
        deltaText.text = $"-{amount}";
        confirmButton.interactable = amount > 0;
        minusButton.interactable = amount > 0;
        plusButton.interactable = amount < Mathf.RoundToInt(slider.maxValue);
    }

    private void Confirm()
    {
        int amount = Mathf.RoundToInt(slider.value);
        if (!CurrencyManager.Instance.StoreForReservation(record, itemIndex, amount)) return;

        Action done = stored;
        stored = null;
        done?.Invoke();
        Close();
    }

    private void Close()
    {
        PanelMotion.Close(card, () => gameObject.SetActive(false));
    }
}
