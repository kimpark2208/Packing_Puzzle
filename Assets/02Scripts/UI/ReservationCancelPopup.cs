using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 예약 취소 확인 팝업(달력 화면의 예약 취소 버튼). 남은 기한에 따라 안내가 달라진다:
/// 3일 이상 남았으면 추가 금액 없이 취소되고, 2일 이하면 예약금의 1.5배를 물게 되며 "예" 위에 그 금액이 빨간 글씨로 보인다. 예약일 당일에는 취소할 수 없다(버튼을 막는 일은 달력이 한다).
/// 틀은 에디터에 만들어 두고, 코드는 글씨만 채운다. 카드(PanelMotion이 붙은 몸통)가 중앙에서 커지며 나타난다.
/// </summary>
public class ReservationCancelPopup : MonoBehaviour
{
    private const string Orange = "#E58A2B";

    [SerializeField, Tooltip("카드(PanelMotion이 붙은, 막은 제외한 팝업 몸통).")] private GameObject card;
    [SerializeField] private TMP_Text messageText;
    [SerializeField, Tooltip("\"예\" 위에 뜨는 위약금 글씨(빨간색). 위약금이 없으면 숨는다.")] private TMP_Text feeText;
    [SerializeField] private Button noButton;
    [SerializeField] private Button yesButton;

    private ReservationRecord record;
    private Action cancelled;

    private void Awake()
    {
        noButton.onClick.AddListener(Close);
        yesButton.onClick.AddListener(Confirm);
    }

    /// <summary>record를 취소할지 묻는 팝업을 연다. "예"를 누르면 취소하고 done을 부른다.</summary>
    public void Open(ReservationRecord record, Action done)
    {
        this.record = record;
        cancelled = done;

        int fee = CurrencyManager.Instance.CancelFee(record);
        int free = CurrencyManager.FreeCancelDays;
        messageText.text = fee == 0
            ? $"예약일까지 <color={Orange}>{free}일 이상</color> 남았으므로 추가 금액 없이 취소됩니다. 취소하시겠습니까?"
            : $"예약일까지 <color={Orange}>{free - 1}일 이하</color> 남았으므로 예약금의 {CurrencyManager.LateCancelFee:0.#}배를 지불하게 됩니다. 취소하시겠습니까?";
        feeText.gameObject.SetActive(fee > 0);
        feeText.text = $"(-{fee:N0})";

        card.SetActive(true); // 닫을 때 카드가 꺼지므로 다시 켠다
        gameObject.SetActive(true);
    }

    private void Confirm()
    {
        if (!CurrencyManager.Instance.CancelReservation(record)) return;

        Action done = cancelled;
        cancelled = null;
        done?.Invoke();
        Close();
    }

    private void Close()
    {
        PanelMotion.Close(card, () => gameObject.SetActive(false));
    }
}
