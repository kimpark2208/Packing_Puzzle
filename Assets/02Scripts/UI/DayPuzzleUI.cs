using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 낮 퍼즐(포장) 화면의 오버레이 UI.
/// - 씬 시작 시 GameFlowController가 들고 있는 선택된 꽃 풀을 GachaManager에 주입한다.
/// - "포장 완료" 버튼으로 제출하면 PuzzleValidator 결과를 팝업으로 보여준다.
/// </summary>
public class DayPuzzleUI : MonoBehaviour
{
    private static readonly Color ButtonColor = new(0.95f, 0.6f, 0.35f);
    private static readonly Color PopupBg = new(0f, 0f, 0f, 0.55f);
    private static readonly Color CardColor = Color.white;
    private static readonly Color ConfirmColor = new(0.45f, 0.65f, 0.95f);

    private RectTransform popupRoot;

    private void Start()
    {
        var gacha = FindFirstObjectByType<GachaManager>();
        if (gacha != null && GameFlowController.Instance != null)
        {
            gacha.SetPool(GameFlowController.Instance.ChosenGachaPool);
        }

        Canvas canvas = UIFactory.EnsureCanvas();

        Button submitButton = UIFactory.CreateButton(canvas.transform, "포장 완료", ButtonColor, Color.white);
        var rect = (RectTransform)submitButton.transform;
        rect.anchorMin = new Vector2(0.35f, 0.02f);
        rect.anchorMax = new Vector2(0.65f, 0.10f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        submitButton.onClick.AddListener(OnSubmitClicked);

        EventBus.OnDayPuzzleComplete += ShowResultPopup;
    }

    private void OnDestroy()
    {
        EventBus.OnDayPuzzleComplete -= ShowResultPopup;
    }

    private void OnSubmitClicked()
    {
        GameFlowController.Instance.SubmitDayPuzzle();
    }

    private void ShowResultPopup(PuzzleValidationResult result)
    {
        Canvas canvas = UIFactory.EnsureCanvas();
        popupRoot = UIFactory.CreateFullStretchPanel("DayResultPopup", canvas.transform, PopupBg);

        var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
        var cardRect = (RectTransform)card.transform;
        cardRect.SetParent(popupRoot, false);
        cardRect.anchorMin = new Vector2(0.15f, 0.3f);
        cardRect.anchorMax = new Vector2(0.85f, 0.7f);
        cardRect.offsetMin = Vector2.zero;
        cardRect.offsetMax = Vector2.zero;
        card.GetComponent<Image>().color = CardColor;

        string body =
            $"{(result.isPerfect ? "빈틈없이 포장했어요!" : "포장에 빈틈이 있어요")}\n" +
            $"{(result.isPerfectBouquet ? "완벽한 꽃다발 보너스!" : "")}\n" +
            $"점수: {result.scoreBeforePenalty} / 목표 {result.targetScore} {(result.targetScoreMet ? "달성!" : "미달성...")}\n\n" +
            $"매출: {result.totalEarnings}원";

        UIFactory.CreateText(cardRect, body, 26, Color.black);

        Button confirmButton = UIFactory.CreateButton(popupRoot, "확인", ConfirmColor, Color.white);
        var btnRect = (RectTransform)confirmButton.transform;
        btnRect.anchorMin = new Vector2(0.35f, 0.18f);
        btnRect.anchorMax = new Vector2(0.65f, 0.27f);
        btnRect.offsetMin = Vector2.zero;
        btnRect.offsetMax = Vector2.zero;
        confirmButton.onClick.AddListener(() =>
        {
            Destroy(popupRoot.gameObject);
            GameFlowController.Instance.ProceedToNightMain();
        });
    }
}
