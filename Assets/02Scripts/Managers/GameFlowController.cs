using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 낮→밤→낮 코어 루프 전체를 조율하는 씬 플로우 컨트롤러.
/// 부트스트랩 씬(00Start)에 배치되어 씬 전환 간 유지되며, 각 씬이 로드될 때마다
/// 그 씬에 필요한 데이터(오늘의 주문, 가챠 풀, 밤 퍼즐 큐)를 필요한 컴포넌트에 꽂아준다.
///
/// 씬 이름 상수는 Assets/01Scenes 폴더 구조를 그대로 따른다.
/// </summary>
public class GameFlowController : Singleton<GameFlowController>
{
    public const string SceneStart = "00Start";
    public const string SceneDayMain = "01DayMain";
    public const string SceneFlowerSelect = "02FlowerSellect";
    public const string SceneDayPuzzle = "03DayPuzzle";
    public const string SceneDayToNight = "04DayToNight";
    public const string SceneNightMain = "01NightMain";
    public const string SceneNightPuzzle = "02NightPuzzle";

    [SerializeField] private int minBonusNightStages = 3;
    [SerializeField] private int maxBonusNightStages = 5;

    public DayPuzzleGenerator.DayOrder CurrentDayOrder { get; private set; }
    public List<BlockData> ChosenGachaPool { get; private set; } = new();

    private const string DefaultRejectResponseLine = "그래요라고 하세요";

    private string pendingCustomerLine;
    private string pendingRejectResponseLine = DefaultRejectResponseLine;

    private List<NightPuzzleData> nightQueue = new();
    private int nightIndex;

    protected override void OnAwake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        // 부트스트랩에서 바로 낮 메인으로 진입한다.
        if (SceneManager.GetActiveScene().name == SceneStart)
        {
            BeginNewDay();
        }
    }

    // ========== 낮 ==========

    public void BeginNewDay()
    {
        CurrentDayOrder = DayPuzzleGenerator.GenerateOrder(CurrencyManager.Instance.CurrentDay);
        CurrencyManager.Instance.SetCurrentRequirement(CurrentDayOrder.requirement);
        SceneManager.LoadScene(SceneDayMain);
    }

    /// <summary>플레이어가 포장지를 골랐을 때(꽃 선택 화면 상단). 포장지 단계의 태그 요구 레벨을 확정한다.</summary>
    public void ChooseWrapper(int wrapperId)
    {
        DayPuzzleGenerator.FinalizeForWrapper(CurrentDayOrder, wrapperId);
    }

    public void GoToFlowerSelect()
    {
        SceneManager.LoadScene(SceneFlowerSelect);
    }

    public void ConfirmFlowerSelection(List<BlockData> chosen)
    {
        ChosenGachaPool = new List<BlockData>(chosen);
        SceneManager.LoadScene(SceneDayPuzzle);
    }

    /// <summary>결과 팝업에서 확인을 누르면 밤 요청(꽃 선택) 화면으로 이동한다.</summary>
    public void ProceedToNightMain()
    {
        SceneManager.LoadScene(SceneDayToNight);
    }

    /// <summary>꽃다발 붕괴/요구사항 미달성처럼 손님이 화나서 돌아갈 때. 데이메인으로 돌아가면 손님이 이 대사를 말한다.
    /// rejectResponseLine은 "이건아니지"를 눌렀을 때 나오는 대사(기본값: "그래요라고 하세요").</summary>
    public void ReturnToDayMainAngry(string customerLine, string rejectResponseLine = DefaultRejectResponseLine)
    {
        pendingCustomerLine = customerLine;
        pendingRejectResponseLine = rejectResponseLine;
        BeginNewDay();
    }

    /// <summary>데이메인 진입 시 화난 손님 대사가 예약되어 있으면 한 번 꺼내 쓰고 지운다.</summary>
    public string ConsumePendingCustomerLine()
    {
        string line = pendingCustomerLine;
        pendingCustomerLine = null;
        return line;
    }

    /// <summary>화난 손님에게 "이건아니지"로 반박했을 때 나올 대사를 꺼내 쓰고 기본값으로 되돌린다.</summary>
    public string ConsumePendingRejectResponseLine()
    {
        string line = pendingRejectResponseLine;
        pendingRejectResponseLine = DefaultRejectResponseLine;
        return line;
    }

    // ========== 밤 ==========

    /// <summary>DayToNight 화면에서 밤 메인 허브로 넘어갈 때 호출. 지금은 단순 씬 이동만 한다.</summary>
    public void GoToNightMainScene()
    {
        SceneManager.LoadScene(SceneNightMain);
    }

    /// <summary>밤 메인(요청 선택) 화면에서 확인을 눌렀을 때. requestedFlowerIds는 오늘 밤 최우선으로 만들 꽃들.</summary>
    public void ConfirmNightRequests(List<int> requestedFlowerIds)
    {
        foreach (int id in requestedFlowerIds)
        {
            CurrencyManager.Instance.RequestFlowerForTonight(id);
        }

        int[] requested = CurrencyManager.Instance.ClearDailyRequestsAndGet();
        EventBus.RaiseDayEnded(requested);

        var obtained = CurrencyManager.Instance.GetObtainedFlowerIds();
        int gridSize = ShopManager.Instance.GetGridSize(CurrentDayOrder?.wrapperId ?? 1);
        int bonusCount = Random.Range(minBonusNightStages, maxBonusNightStages + 1);

        nightQueue = ProceduralNightPuzzleGenerator.GenerateNightQueue(gridSize, requested, obtained, bonusCount);
        nightIndex = 0;

        EventBus.RaiseNightPuzzlesGenerated(nightQueue.ToArray());
        SceneManager.LoadScene(SceneNightPuzzle);
    }

    private void LoadCurrentNightStageInto(NightBoardController board)
    {
        if (nightIndex >= nightQueue.Count)
        {
            FinishNight();
            return;
        }

        NightPuzzleData data = nightQueue[nightIndex];
        var allowedBlocks = new List<BlockData>();
        foreach (int id in data.requiredFlowerIds)
        {
            var b = BlockDatabase.Instance.GetById(id);
            if (b != null) allowedBlocks.Add(b);
        }

        string flavor = data.isBonusStage ? "남는 시간, 손님들이 편안히 성불합니다" : "요청받은 꽃을 손질합니다";
        EventBus.RaiseAscensionMoment(flavor);

        board.OnStagePuzzleCompleted -= HandleStageCompleted;
        board.OnStagePuzzleCompleted += HandleStageCompleted;
        board.LoadPuzzle(data, allowedBlocks);
    }

    private void HandleStageCompleted(List<int> obtainedFlowerIds)
    {
        nightIndex++;

        var board = FindFirstObjectByType<NightBoardController>();
        if (board != null)
        {
            LoadCurrentNightStageInto(board);
        }
    }

    private void FinishNight()
    {
        StartCoroutine(FinishNightRoutine());
    }

    private System.Collections.IEnumerator FinishNightRoutine()
    {
        EventBus.RaiseAscensionMoment("오늘 밤도 손님들이 모두 편안히 떠났습니다");
        EventBus.RaiseAllNightPuzzlesFinished();

        yield return new WaitForSeconds(1.6f);

        CurrencyManager.Instance.AdvanceDay();
        BeginNewDay();
    }

    /// <summary>밤 퍼즐을 그만 풀고 싶을 때(잉여 스테이지 스킵) 호출.</summary>
    public void SkipRemainingNightStages()
    {
        FinishNight();
    }

    // ========== 씬 로드 훅 ==========

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == SceneDayMain)
        {
            var dayMainUI = FindFirstObjectByType<DayMainUI>();
            if (dayMainUI != null)
            {
                dayMainUI.Initialize(CurrentDayOrder);
            }
        }
        else if (scene.name == SceneNightPuzzle)
        {
            var board = FindFirstObjectByType<NightBoardController>();
            if (board != null)
            {
                LoadCurrentNightStageInto(board);
            }
        }
    }
}
