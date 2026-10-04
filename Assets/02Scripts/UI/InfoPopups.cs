using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상시 표시 HUD의 두 팝업: 주문서(오늘 손님의 주문 내용)와 색 조합도(색 궁합 안내).
/// 탭 버튼을 누르면 해당 팝업이 열리고(다른 팝업은 닫힘), 닫기 버튼이나 탭을 다시 누르면 닫힌다.
/// 주문서 탭은 씬마다 켜고 끌 수 있다(데이메인에서는 끄고 꽃 선택부터 켠다).
/// </summary>
public class InfoPopups : MonoBehaviour
{
    [SerializeField] private Button orderTab;
    [SerializeField] private Button comboTab;
    [SerializeField] private GameObject orderPopup;
    [SerializeField] private GameObject comboPopup;
    [SerializeField] private Button orderClose;
    [SerializeField] private Button comboClose;
    [SerializeField] private TMP_Text orderNumberText;
    [SerializeField] private TMP_Text orderBodyText;

    private void Awake()
    {
        orderPopup.SetActive(false);
        comboPopup.SetActive(false);

        orderTab.onClick.AddListener(() => Toggle(orderPopup, comboPopup));
        comboTab.onClick.AddListener(() => Toggle(comboPopup, orderPopup));
        orderClose.onClick.AddListener(() => orderPopup.SetActive(false));
        comboClose.onClick.AddListener(() => comboPopup.SetActive(false));
    }

    private void Start()
    {
        RefreshOrder(); // 씬에 들어온 시점의 주문(손님이 말한 주문)을 주문서에 미리 적어 둔다
    }

    private void Toggle(GameObject target, GameObject other)
    {
        bool show = !target.activeSelf;
        other.SetActive(false);
        target.SetActive(show);
        if (show && target == orderPopup) RefreshOrder();
    }

    /// <summary>손님이 말한 주문(포장지 색 + 요구사항)을 주문서에 적는다.</summary>
    private void RefreshOrder()
    {
        var order = GameFlowController.Instance != null ? GameFlowController.Instance.CurrentDayOrder : null;
        int day = CurrencyManager.Instance != null ? CurrencyManager.Instance.CurrentDay : 1;

        orderNumberText.text = $"No. {day:0000}";
        orderBodyText.text = order == null ? "" : CustomerDialogue.OrderLine(order);
    }
}
