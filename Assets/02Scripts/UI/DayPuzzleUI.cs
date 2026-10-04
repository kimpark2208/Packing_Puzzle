using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 낮 퍼즐(포장) 화면의 흐름 담당.
/// - 씬 시작 시 오늘의 포장지 레벨로 보드를 만든다.
/// - 꽃이 놓일 때마다 완성/붕괴 여부를 판정하고, 끝나면 정산(Money 폴더)해 결과를 보여준다.
/// - 완성 결과는 결과 팝업으로, 붕괴는 붕괴 팝업으로 보여준다.
/// 팝업은 하이라키에 미리 배치되어 있고 평소엔 비활성 상태다.
/// </summary>
public class DayPuzzleUI : MonoBehaviour
{
    [Header("결과 팝업")]
    [SerializeField] private GameObject resultPopup;
    [SerializeField] private TMP_Text resultText;       // 보너스요소/감점요소 내역
    [SerializeField] private TMP_Text resultMoneyText;  // 획득한 돈
    [SerializeField] private Button resultConfirmButton;

    [Header("붕괴 팝업")]
    [SerializeField] private GameObject collapsePopup;
    [SerializeField] private Button collapseConfirmButton;

    [Header("강제 제출")]
    [SerializeField] private Button compulsionButton;

    [Header("퍼즐 금액 정산 규칙 수치")]
    [SerializeField] private MoneyRuleSettings moneySettings;

    private const float CollapseAutoProceedDelay = 1.2f;
    private const string RequirementNotMetLine = "이게뭐예요!!";
    private const string RequirementNotMetRejectResponse = "알긴 아시나보죠?";
    private const string GoodResultLine = "좋네요!";
    private const string LateLine = "늦으셨네요."; // 손님 기분이 다 닳은 뒤에 퍼즐이 끝났을 때(성공 여부와 무관)

    private WrapperBoardController board;
    private bool roundEnded;
    private bool collapseHandled;
    private int collapseCount; // 이 주문을 만드는 동안 꽃다발이 무너진 횟수
    private GachaManager gacha;
    private bool customerLate; // 퍼즐이 끝난 순간 손님 기분이 이미 다 닳아 있었는가
    private bool lastRequirementMet;
    private PuzzleSettlement settlement;

    private void Start()
    {
        board = WrapperBoardController.Instance;
        gacha = FindFirstObjectByType<GachaManager>();

        // 정산 조립: 규칙들(수치는 설정 에셋) + 계산기 + 지갑
        settlement = new PuzzleSettlement(new MoneyCalculator(MoneyRuleSet.Create(moneySettings)), CurrencyManager.Instance);

        if (board != null)
        {
            board.BuildLevel();
            board.OnFlowerPlaced += HandleFlowerPlaced;
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
            ShowResultPopup(Settle(PuzzleEndKind.Completed));
            return;
        }

        if (board.IsCollapsed())
        {
            roundEnded = true; // 팝업이 보이는 동안은 더 놓지 못한다
            collapseCount++;
            board.PlayCollapse(); // 더 무거운 쪽으로 쓰러지는 연출
            ShowCollapsePopup();
            return;
        }
    }

    /// <summary>CompulsionBTN: 완성 여부와 상관없이 지금까지 놓인 꽃으로 강제 제출한다.</summary>
    private void OnCompulsionClicked()
    {
        if (roundEnded || board == null) return;
        roundEnded = true;
        ShowResultPopup(Settle(PuzzleEndKind.ForcedSubmit));
    }

    /// <summary>퍼즐이 끝난 순간 정산한다(손님 기분 판정에 쓰이므로 기분 시간을 멈추기 전에 계산한다).</summary>
    private SettlementResult Settle(PuzzleEndKind endKind)
    {
        customerLate = IsCustomerLate();
        SettlementResult result = settlement.Settle(endKind, collapseCount);
        GameFlowController.Instance?.ConsumeChosenFlowers(); // 퍼즐이 끝났으니 고른 꽃이 소모된다(붕괴 재시작은 끝이 아니다)
        DayClock.Instance?.StopMood(); // 퍼즐이 끝났으니 기분 시간은 다음 주문까지 멈춘다
        return result;
    }

    private void ShowResultPopup(SettlementResult result)
    {
        lastRequirementMet = result.RequirementMet;

        if (resultText != null) resultText.text = SettlementFormatter.Breakdown(result.Ledger);
        if (resultMoneyText != null) resultMoneyText.text = $"획득한 돈: {result.Payout}";
        if (resultPopup != null) resultPopup.SetActive(true);
    }

    private static bool IsCustomerLate()
    {
        return DayClock.Instance != null && DayClock.Instance.MoodRemaining <= 0f;
    }

    private void ShowCollapsePopup()
    {
        collapseHandled = false;
        if (collapsePopup == null) // 붕괴 팝업이 연결되지 않았으면 알림 없이 바로 다시 시작한다
        {
            OnCollapseConfirmed();
            return;
        }

        collapsePopup.SetActive(true);
        StartCoroutine(AutoProceedAfterCollapse());
    }

    /// <summary>붕괴는 확인 버튼을 누를 때까지 기다리지 않고, 잠깐 보여준 뒤 바로 퍼즐을 다시 시작한다.</summary>
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
        RestartPuzzle();
    }

    /// <summary>붕괴 후 같은 손님의 주문을 처음부터 다시 만든다. 손님 기분 시간과 리롤 횟수는 그대로이고, 놓았던 꽃은 풀로 돌아온다.</summary>
    private void RestartPuzzle()
    {
        board.BuildLevel();
        if (gacha != null) gacha.RestorePool();
        roundEnded = false;
    }

    private void OnResultConfirmed()
    {
        if (resultPopup != null) resultPopup.SetActive(false);
        if (GameFlowController.Instance == null) return;

        // 손님 기분이 다 닳은 뒤에 끝났으면 "늦으셨네요.", 아니면 요구사항을 못 채웠으면 손님이 화내고,
        // 채웠으면 만족한다.
        if (customerLate)
            GameFlowController.Instance.ReturnToDayMainAngry(LateLine, RequirementNotMetRejectResponse);
        else if (!lastRequirementMet)
            GameFlowController.Instance.ReturnToDayMainAngry(RequirementNotMetLine, RequirementNotMetRejectResponse);
        else
            GameFlowController.Instance.ReturnToDayMainAngry(GoodResultLine);
    }
}
