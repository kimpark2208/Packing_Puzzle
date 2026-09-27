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

    private const float CollapseAutoProceedDelay = 1.2f;

    private WrapperBoardController board;
    private GachaManager gacha;
    private bool roundEnded;
    private bool collapseHandled;

    private void Start()
    {
        board = WrapperBoardController.Instance;
        gacha = FindFirstObjectByType<GachaManager>();

        if (board != null)
        {
            board.BuildLevel();
            board.OnFlowerPlaced += HandleFlowerPlaced;
        }

        if (resultPopup != null) resultPopup.SetActive(false);
        if (resultConfirmButton != null) resultConfirmButton.onClick.AddListener(OnResultConfirmed);

        if (collapsePopup != null) collapsePopup.SetActive(false);
        if (collapseConfirmButton != null) collapseConfirmButton.onClick.AddListener(OnCollapseConfirmed);
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

        if (gacha != null && gacha.RerollExhausted && gacha.IsTrayEmpty)
        {
            roundEnded = true;
            ShowResultPopup(PuzzleValidator.ValidateFailure());
        }
    }

    private void ShowResultPopup(PuzzleValidationResult result)
    {
        string body = result.success
            ? $"꽃다발을 완성했어요!\n색 조합 보너스: {result.colorBonus}\n{(result.requirementMet ? $"고객 요구사항 달성! +{result.requirementBonus}" : "고객 요구사항 미달성")}\n\n매출: {result.totalEarnings}원"
            : "리롤을 모두 사용했지만 꽃다발을 완성하지 못했어요...\n\n매출: 0원";

        if (resultBodyText != null) resultBodyText.text = body;
        if (resultPopup != null) resultPopup.SetActive(true);
    }

    private void ShowCollapsePopup()
    {
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
        GameFlowController.Instance.ReturnToDayMainAngry("장사접으세요");
    }

    private void OnResultConfirmed()
    {
        if (resultPopup != null) resultPopup.SetActive(false);
        if (GameFlowController.Instance == null) return;
        GameFlowController.Instance.BeginNewDay();
    }
}
