using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 낮 퍼즐(포장) 화면의 오버레이 UI.
/// - 씬 시작 시 GameFlowController가 들고 있는 선택된 꽃 풀을 GachaManager에 주입한다.
/// - "포장 완료" 버튼으로 제출하면 PuzzleValidator 결과를 팝업으로 보여준다.
/// 팝업은 하이라키에 미리 배치되어 있고 평소엔 비활성 상태다.
/// </summary>
public class DayPuzzleUI : MonoBehaviour
{
    [SerializeField] private Button submitButton;
    [SerializeField] private GameObject resultPopup;
    [SerializeField] private TMP_Text resultBodyText;
    [SerializeField] private Button resultConfirmButton;

    private void Start()
    {
        var gacha = FindFirstObjectByType<GachaManager>();
        if (gacha != null && GameFlowController.Instance != null)
        {
            gacha.SetPool(GameFlowController.Instance.ChosenGachaPool);
        }

        if (resultPopup != null) resultPopup.SetActive(false);
        if (submitButton != null) submitButton.onClick.AddListener(OnSubmitClicked);
        if (resultConfirmButton != null) resultConfirmButton.onClick.AddListener(OnResultConfirmed);

        EventBus.OnDayPuzzleComplete += ShowResultPopup;
    }

    private void OnDestroy()
    {
        EventBus.OnDayPuzzleComplete -= ShowResultPopup;
    }

    private void OnSubmitClicked()
    {
        if (GameFlowController.Instance == null) return;
        GameFlowController.Instance.SubmitDayPuzzle();
    }

    private void ShowResultPopup(PuzzleValidationResult result)
    {
        string body =
            $"{(result.isPerfect ? "빈틈없이 포장했어요!" : "포장에 빈틈이 있어요")}\n" +
            $"{(result.isPerfectBouquet ? "완벽한 꽃다발 보너스!" : "")}\n" +
            $"점수: {result.scoreBeforePenalty} / 목표 {result.targetScore} {(result.targetScoreMet ? "달성!" : "미달성...")}\n\n" +
            $"매출: {result.totalEarnings}원";

        if (resultBodyText != null) resultBodyText.text = body;
        if (resultPopup != null) resultPopup.SetActive(true);
    }

    private void OnResultConfirmed()
    {
        if (resultPopup != null) resultPopup.SetActive(false);
        if (GameFlowController.Instance == null) return;
        GameFlowController.Instance.ProceedToNightMain();
    }
}
