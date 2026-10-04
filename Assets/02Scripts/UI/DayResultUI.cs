using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>하루 결산 화면(03Result). 오늘 번 돈 합계를 보여 주고, 다음으로 넘어가면 다음 날 낮이 시작된다.</summary>
public class DayResultUI : MonoBehaviour
{
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Button goNextButton;

    private void Start()
    {
        if (bodyText != null && CurrencyManager.Instance != null)
            bodyText.text = CurrencyManager.Instance.TodayEarned.ToString();

        if (goNextButton != null) goNextButton.onClick.AddListener(OnGoNext);
    }

    private void OnGoNext()
    {
        if (GameFlowController.Instance != null) GameFlowController.Instance.StartNextDay();
    }
}
