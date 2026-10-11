using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 낮 퍼즐(포장) 화면의 흐름 담당.
/// - 씬 시작 시 오늘의 포장지 레벨로 보드를 만든다.
/// - 꽃이 놓일 때마다 완성/붕괴 여부를 판정한다. 완성하면 꾸미기 단계로 넘어간다: 꽃 버켓은 끄고, 꾸미기 창과 되돌리기/다시하기/작업 완료 버튼을 켠다.
///   손님 기분 시간은 완성한 순간 멈추고, 점수 정산(Settlement 폴더)과 결과 화면(DayResultView)은 작업 완료 버튼을 눌렀을 때 한다.
/// - 붕괴는 붕괴 팝업으로 보여주고 처음부터 다시 시작한다.
/// 결과 화면과 팝업은 하이라키에 미리 배치되어 있고 평소엔 비활성 상태다. 꾸미기 단계의 오브젝트는 이름으로 찾는다(SceneFind).
/// </summary>
public class DayPuzzleUI : MonoBehaviour
{
    [Header("결과 화면 (점수 사유가 하나씩 나오는 오버레이)")]
    [SerializeField] private DayResultView resultView;

    [Header("붕괴 팝업")]
    [SerializeField] private GameObject collapsePopup;

    [Header("퍼즐 점수 정산 규칙 수치")]
    [SerializeField] private ScoreRuleSettings scoreSettings;

    // 씬에서 찾을 오브젝트 이름. 이름을 바꾸면 여기도 같이 바꾼다.
    private const string FlowerBucketName = "FlowerBucket";
    private const string DecoratePanelName = "DecoratePanel";
    private const string UndoButtonName = "UndoBTN";
    private const string RetryButtonName = "RetryBTN";
    private const string FinishButtonName = "ConfirmBTN"; // 작업 완료 버튼
    private const string FinishLabel = "작업 완료";

    private const float CollapseAutoProceedDelay = 1.2f;
    private const string RequirementNotMetLine = "주문이랑 다른데요. 유명한 곳이라고 들었는데… 실망이네요.";
    private const string RequirementNotMetRejectResponse = "알긴 아시나보죠?";
    private const string GoodResultLine = "우와, 좋네요! 친구가 분명 좋아할 거예요.";
    private const string LateLine = "늦으셨네요."; // 손님 기분이 다 닳은 뒤에 퍼즐이 끝났을 때(성공 여부와 무관)

    private DayPuzzleController board;
    private bool roundEnded;
    private bool collapseHandled;
    private int collapseCount; // 이 주문을 만드는 동안 꽃다발이 무너진 횟수
    private GachaManager gacha;
    private bool customerLate; // 퍼즐이 끝난 순간 손님 기분이 이미 다 닳아 있었는가
    private bool lastRequirementMet;
    private PuzzleSettlement settlement;

    private GameObject flowerBucket;
    private GameObject[] decorateObjects; // 꾸미기 단계에서만 보이는 것들(꾸미기 창, 되돌리기, 다시하기, 작업 완료)
    private Button finishButton;

    private void Start()
    {
        board = DayPuzzleController.Instance;
        gacha = FindFirstObjectByType<GachaManager>();

        settlement = new PuzzleSettlement(scoreSettings); // 규칙 수치는 설정 에셋
        SetUpDecorateStage();

        if (board != null)
        {
            board.BuildLevel();
            board.OnFlowerPlaced += HandleFlowerPlaced;
        }

        if (resultView != null) resultView.gameObject.SetActive(false);

        if (collapsePopup != null) collapsePopup.SetActive(false);
    }

    private void OnDestroy()
    {
        if (board != null) board.OnFlowerPlaced -= HandleFlowerPlaced;
    }

    /// <summary>꾸미기 단계의 오브젝트를 이름으로 찾아 두고 숨긴다. 되돌리기/다시하기는 아직 기능이 없고 켜지기만 한다.</summary>
    private void SetUpDecorateStage()
    {
        Scene scene = gameObject.scene;
        flowerBucket = SceneFind.One<Transform>(scene, FlowerBucketName)?.gameObject;

        finishButton = SceneFind.One<Button>(scene, FinishButtonName);
        if (finishButton != null)
        {
            finishButton.onClick.AddListener(OnFinishClicked);
            TMP_Text label = finishButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = FinishLabel;
        }

        decorateObjects = new[]
        {
            SceneFind.One<Transform>(scene, DecoratePanelName)?.gameObject,
            SceneFind.One<Transform>(scene, UndoButtonName)?.gameObject,
            SceneFind.One<Transform>(scene, RetryButtonName)?.gameObject,
            finishButton != null ? finishButton.gameObject : null,
        };
        SetDecorating(false);
    }

    private void SetDecorating(bool on)
    {
        if (flowerBucket != null) flowerBucket.SetActive(!on);
        foreach (GameObject target in decorateObjects) if (target != null) target.SetActive(on);
    }

    /// <summary>작업 완료 버튼: 이제 정산하고 결과 화면을 보여 준다.</summary>
    private void OnFinishClicked()
    {
        if (finishButton != null) finishButton.interactable = false; // 결과 화면이 뜨는 동안 다시 누르지 못하게
        ShowResult(Settle(PuzzleEndKind.Completed));
    }

    private void HandleFlowerPlaced()
    {
        if (roundEnded || board == null) return;

        if (board.IsComplete)
        {
            roundEnded = true;
            GameClock.Instance?.StopMood(); // 완성한 순간 손님 기분 시간은 멈춘다(정산은 작업 완료를 누를 때)
            SetDecorating(true);
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

    /// <summary>디버그용(CurrencyManager의 디버그 버튼): 완성 여부와 상관없이 지금까지 놓인 꽃으로 강제 제출한다.</summary>
    public void DebugForceSubmit()
    {
        if (roundEnded || board == null) return;
        roundEnded = true;
        ShowResult(Settle(PuzzleEndKind.ForcedSubmit));
    }

    /// <summary>퍼즐이 끝난 순간 정산한다(손님 기분 판정에 쓰이므로 기분 시간을 멈추기 전에 계산한다).</summary>
    private SettlementResult Settle(PuzzleEndKind endKind)
    {
        customerLate = IsCustomerLate();
        SettlementResult result = settlement.Settle(endKind, collapseCount);
        CurrencyManager.Instance.AddMoney(result.Payout); // 정산은 금액만 계산하고, 지급은 여기서 한다
        CurrencyManager.Instance.RecordRoundScore(result.Score); // 하루 평점은 그날 퍼즐 점수의 평균으로 정한다
        GameManager.Instance?.ConsumeUsedFlowers(board.PlacementHistory); // 퍼즐이 끝났으니 놓은 꽃이 소모된다(붕괴 재시작은 끝이 아니다)
        GameClock.Instance?.StopMood(); // 퍼즐이 끝났으니 기분 시간은 다음 주문까지 멈춘다
        return result;
    }

    private void ShowResult(SettlementResult result)
    {
        lastRequirementMet = result.RequirementMet;
        GameClock.Instance?.SetPaused(true); // 결과를 보는 동안 하루 시간도 멈춘다(기분 시간은 정산에서 이미 멈췄다)

        if (resultView == null) // 결과 화면이 연결되지 않았으면 알림 없이 바로 넘어간다
        {
            OnResultConfirmed();
            return;
        }

        resultView.Show(result, OnResultConfirmed); // 마지막에 한 번 더 누르면 닫히고 OnResultConfirmed로 이어진다
    }

    private static bool IsCustomerLate()
    {
        return GameClock.Instance != null && GameClock.Instance.MoodRemaining <= 0f;
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

    /// <summary>붕괴 후 같은 손님의 주문을 처음부터 다시 만든다. 손님 기분 시간과 리롤 횟수는 그대로이고, 버켓의 꽃은 새로 뽑는다.</summary>
    private void RestartPuzzle()
    {
        board.BuildLevel(rebuild: true); // 특수 칸은 처음 정한 그대로 둔다
        if (gacha != null) gacha.Redraw();
        roundEnded = false;
    }

    private void OnResultConfirmed()
    {
        GameClock.Instance?.SetPaused(false);
        if (GameManager.Instance == null) return;

        // 손님 기분이 다 닳은 뒤에 끝났으면 "늦으셨네요.", 아니면 요구사항을 못 채웠으면 손님이 화내고,
        // 채웠으면 만족한다.
        if (customerLate)
            GameManager.Instance.ReturnToDayMain(LateLine, RequirementNotMetRejectResponse);
        else if (!lastRequirementMet)
            GameManager.Instance.ReturnToDayMain(RequirementNotMetLine, RequirementNotMetRejectResponse);
        else
            GameManager.Instance.ReturnToDayMain(GoodResultLine);
    }
}
