using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 예 / 아니오를 묻는 작은 확인 팝업(예약 화면 안에 있다). 문구와 두 버튼의 글씨는 프리팹에 적어 두고, 코드는 누른 쪽만 알려 준다.
/// 쓰는 곳: 예약일에 예약자가 찾아왔는데 수납하지 않은 경우의 "달력 화면으로 갈까요?"(ReservationCalendar.PickupStoreFlow). 카드(PanelMotion이 붙은 몸통)가 중앙에서 커지며 나타난다.
/// </summary>
public class ReservationConfirmPopup : MonoBehaviour
{
    [SerializeField, Tooltip("카드(PanelMotion이 붙은, 막은 제외한 팝업 몸통).")] private GameObject card;
    [SerializeField] private TMP_Text messageText;
    [SerializeField, Tooltip("거절 버튼(누르면 false).")] private Button noButton;
    [SerializeField, Tooltip("수락 버튼(누르면 true).")] private Button yesButton;

    private Action<bool> decided;

    private void Awake()
    {
        noButton.onClick.AddListener(() => Decide(false));
        yesButton.onClick.AddListener(() => Decide(true));
    }

    /// <summary>팝업을 연다. message가 비어 있으면 프리팹에 적어 둔 문구를 쓴다. 수락이면 true, 거절이면 false로 decided를 부르고 닫는다.</summary>
    public void Open(string message, Action<bool> decided)
    {
        this.decided = decided;
        if (!string.IsNullOrEmpty(message)) messageText.text = message;

        card.SetActive(true); // 닫을 때 카드가 꺼지므로 다시 켠다
        gameObject.SetActive(true);
    }

    private void Decide(bool accepted)
    {
        if (decided == null) return; // 닫히는 모션 중에 또 눌렀다

        Action<bool> callback = decided;
        decided = null;
        PanelMotion.Close(card, () => gameObject.SetActive(false));
        callback(accepted);
    }
}
