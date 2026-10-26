using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상시 표시 HUD의 주문서 팝업(오늘 손님의 주문 내용).
/// 탭 버튼을 누르면 팝업이 열리고, 닫기 버튼이나 탭을 다시 누르면 닫힌다.
/// 주문서 탭은 씬마다 켜고 끌 수 있다(데이메인에서는 끄고 꽃 선택부터 켠다).
/// </summary>
public class OrderPopup : MonoBehaviour
{
    [SerializeField] private Button orderTab;
    [SerializeField] private GameObject orderPopup;
    [SerializeField] private Button orderClose;
    [SerializeField] private TMP_Text orderNumberText;
    [SerializeField] private TMP_Text orderBodyText;

    private void Awake()
    {
        orderPopup.SetActive(false);

        orderTab.onClick.AddListener(Toggle);
        orderClose.onClick.AddListener(() => orderPopup.SetActive(false));
    }

    private void Start()
    {
        RefreshOrder(); // 씬에 들어온 시점의 주문(손님이 말한 주문)을 주문서에 미리 적어 둔다
    }

    /// <summary>주문서 탭을 보이거나 숨긴다(숨길 때 열려 있는 팝업도 닫는다).</summary>
    public void SetTabVisible(bool visible)
    {
        orderTab.gameObject.SetActive(visible);
        if (!visible) orderPopup.SetActive(false);
    }

    private void Toggle()
    {
        orderPopup.SetActive(!orderPopup.activeSelf);
        if (orderPopup.activeSelf) RefreshOrder();
    }

    /// <summary>손님이 말한 주문(포장지 색 + 요구사항)을 주문서에 적는다. 예약 꽃다발을 만드는 날에는 예약 제목과 지금까지 만든 꽃다발 수·누적 점수를 적는다.</summary>
    private void RefreshOrder()
    {
        var order = GameManager.Instance != null ? GameManager.Instance.CurrentDayOrder : null;
        int day = CurrencyManager.Instance != null ? CurrencyManager.Instance.CurrentDay : 1;

        orderNumberText.text = $"No. {day:0000}";
        bool crafting = GameManager.Instance != null && GameManager.Instance.Crafting;
        orderBodyText.text = crafting ? GameManager.Instance.Craft.OrderSheetText() : order == null ? "" : order.orderLine;
    }
}
