using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 낮 퍼즐(포장) 화면의 흐름 담당.
/// - 씬 시작 시 오늘의 포장지 레벨로 보드를 만든다.
/// - 꽃이 놓일 때마다 완성/붕괴/실패(리롤 소진) 여부를 판정한다.
/// - 완성/실패 결과는 결과 팝업으로, 붕괴는 붕괴 팝업으로 보여준다.
/// 팝업은 하이라키에 미리 배치되어 있고 평소엔 비활성 상태다.
/// </summary>
public class DayPuzzleUI : MonoBehaviour
{
    [SerializeField] private GameObject resultPopup;
    [SerializeField] private TMP_Text resultBodyText;
    [SerializeField] private Button resultConfirmButton;

    [Header("붕괴 팝업")]
    [SerializeField] private GameObject collapsePopup;
    [SerializeField] private TMP_Text collapseBodyText;
    [SerializeField] private Button collapseConfirmButton;

    [Header("강제 제출")]
    [SerializeField] private Button compulsionButton;

    private const float CollapseAutoProceedDelay = 1.2f;
    private const string RequirementNotMetLine = "이게뭐예요!!";
    private const string RequirementNotMetRejectResponse = "알긴 아시나보죠?";
    private const string GoodResultLine = "좋네요!";
    private const string LateLine = "늦으셨네요."; // 손님 기분이 다 닳은 뒤에 퍼즐이 끝났을 때(성공 여부와 무관)

    private WrapperBoardController board;
    private GachaManager gacha;
    private bool roundEnded;
    private bool collapseHandled;
    private bool customerLate; // 퍼즐이 끝난 순간 손님 기분이 이미 다 닳아 있었는가
    private PuzzleValidationResult lastResult;

    private void Start()
    {
        board = WrapperBoardController.Instance;
        gacha = FindFirstObjectByType<GachaManager>();

        if (board != null)
        {
            board.BuildLevel();
            board.OnFlowerPlaced += HandleFlowerPlaced;
            if (gacha != null) gacha.SetMaxRerolls(board.AllSlots.Count + 1); // 칸 개수 + 1
        }

        if (resultPopup != null) resultPopup.SetActive(false);
        if (resultConfirmButton != null) resultConfirmButton.onClick.AddListener(OnResultConfirmed);

        if (collapsePopup != null) collapsePopup.SetActive(false);
        if (collapseConfirmButton != null) collapseConfirmButton.onClick.AddListener(OnCollapseConfirmed);

        if (compulsionButton != null) compulsionButton.onClick.AddListener(OnCompulsionClicked);
    }

    private void OnDestroy()
    {
        if (board != null) board.OnFlowerPlaced -= HandleFlowerPlaced;
    }

    private void HandleFlowerPlaced()
    {
        if (roundEnded || board == null) return;

        if (board.IsComplete)
        {
            roundEnded = true;
            ShowResultPopup(PuzzleValidator.ValidateSuccess());
            return;
        }

        if (board.IsCollapsed())
        {
            roundEnded = true;
            ShowCollapsePopup();
            return;
        }

        // 하나를 놓으면 트레이의 나머지는 사라지므로, 뽑기 횟수를 다 썼는데 아직 못 채웠으면 더 놓을 조각이 없다.
        if (gacha != null && gacha.RerollExhausted)
        {
            roundEnded = true;
            ShowResultPopup(PuzzleValidator.ValidateFailure());
        }
    }

    /// <summary>CompulsionBTN: 완성 여부와 상관없이 지금까지 놓인 꽃으로 강제 제출한다.</summary>
    private void OnCompulsionClicked()
    {
        if (roundEnded || board == null) return;
        roundEnded = true;
        ShowResultPopup(PuzzleValidator.ValidateSuccess());
    }

    private void ShowResultPopup(PuzzleValidationResult result)
    {
        lastResult = result;
        customerLate = IsCustomerLate();

        string body = result.success
            ? $"꽃다발을 완성했어요!\n색 조합 보너스: {result.colorBonus}\n{(result.requirementMet ? $"고객 요구사항 달성! +{result.requirementBonus}" : "고객 요구사항 미달성")}\n\n매출: {result.totalEarnings}원"
            : "리롤을 모두 사용했지만 꽃다발을 완성하지 못했어요...\n\n매출: 0원";

        if (resultBodyText != null) resultBodyText.text = body;
        if (resultPopup != null) resultPopup.SetActive(true);
    }

    private static bool IsCustomerLate()
    {
        return DayClock.Instance != null && DayClock.Instance.MoodRemaining <= 0f;
    }

    private void ShowCollapsePopup()
    {
        customerLate = IsCustomerLate();
        collapseHandled = false;
        if (collapseBodyText != null) collapseBodyText.text = "꽃다발이 한쪽으로 기울며 무너졌어요!\n손님이 크게 실망합니다...";
        if (collapsePopup != null) collapsePopup.SetActive(true);
        StartCoroutine(AutoProceedAfterCollapse());
    }

    /// <summary>붕괴는 확인 버튼을 누를 때까지 기다리지 않고, 잠깐 보여준 뒤 바로 다음 손님으로 넘어간다.</summary>
    private System.Collections.IEnumerator AutoProceedAfterCollapse()
    {
        yield return new WaitForSeconds(CollapseAutoProceedDelay);
        OnCollapseConfirmed();
    }

    private void OnCollapseConfirmed()
    {
        if (collapseHandled) return;
        collapseHandled = true;

        if (collapsePopup != null) collapsePopup.SetActive(false);
        if (GameFlowController.Instance == null) return;
        if (customerLate) GameFlowController.Instance.ReturnToDayMainAngry(LateLine, RequirementNotMetRejectResponse);
        else GameFlowController.Instance.ReturnToDayMainAngry("장사접으세요");
    }

    private void OnResultConfirmed()
    {
        if (resultPopup != null) resultPopup.SetActive(false);
        if (GameFlowController.Instance == null) return;

        // 손님 기분이 다 닳은 뒤에 끝났으면 성공 여부와 상관없이 "늦으셨네요."
        // 아니면 완성/강제제출로 끝났는데(=success 경로) 고객 요구사항을 못 채웠으면 손님이 화내고,
        // 요구사항까지 채웠으면 만족한다. 리롤 소진 실패(success=false)는 그냥 다음 손님으로.
        if (customerLate)
            GameFlowController.Instance.ReturnToDayMainAngry(LateLine, RequirementNotMetRejectResponse);
        else if (lastResult.success && !lastResult.requirementMet)
            GameFlowController.Instance.ReturnToDayMainAngry(RequirementNotMetLine, RequirementNotMetRejectResponse);
        else if (lastResult.success && lastResult.requirementMet)
            GameFlowController.Instance.ReturnToDayMainAngry(GoodResultLine);
        else
            GameFlowController.Instance.BeginNewDay();
    }
}
