using System.Collections.Generic;
using System.Linq;
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
    public const string SceneResult = "03Result";

    [SerializeField] private int nightRewardPerFlower = 6; // 밤 퍼즐을 완료하면 퍼즐에 쓴 꽃 종류마다 늘어나는 보유 수량

    public DayPuzzleGenerator.DayOrder CurrentDayOrder { get; private set; }
    public List<FlowerData> ChosenGachaPool { get; private set; } = new();

    private const string DefaultRejectResponseLine = "그래요라고 하세요";

    private string pendingCustomerLine;
    private string pendingRejectResponseLine = DefaultRejectResponseLine;
    private bool pendingDayTimeUp;

    private List<NightPuzzleData> nightQueue = new();
    private int nightIndex;
    private bool nightCustomerPending; // 밤 요청을 마치고 밤 메인에서 밤 손님을 만나야 하는가
    private bool nightEndPending;      // 밤 시간이 끝나 밤 메인에서 마지막 손님이 외치고 떠나야 하는가
    private const int NightGridSize = 5; // 임시: 밤 퍼즐 판은 5x5 정사각형으로 고정

    protected override void OnAwake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        EventBus.OnDayTimeUp += HandleDayTimeUp;
        EventBus.OnNightTimeUp += HandleNightTimeUp;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        EventBus.OnDayTimeUp -= HandleDayTimeUp;
        EventBus.OnNightTimeUp -= HandleNightTimeUp;
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

    public void ConfirmFlowerSelection(List<FlowerData> chosen)
    {
        ChosenGachaPool = new List<FlowerData>(chosen);
        SceneManager.LoadScene(SceneDayPuzzle);
    }

    /// <summary>결과 팝업에서 확인을 누르면 밤 요청(꽃 선택) 화면으로 이동한다.</summary>
    public void ProceedToNightMain()
    {
        DayClock.Instance?.BeginNight(); // 밤이 시작된다(밤 시간이 가득 차서 흐르기 시작)
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

    /// <summary>하루 시간이 끝나면 하던 일과 상관없이 밤 메인으로 강제 전환한다. 밤 메인에서 손님이 마무리 대사를 한다.</summary>
    private void HandleDayTimeUp()
    {
        pendingDayTimeUp = true;
        SceneManager.LoadScene(SceneNightMain);
    }

    /// <summary>밤 메인 진입 시, 시간 초과로 강제 전환된 것이면 true를 한 번 반환하고 지운다.</summary>
    public bool ConsumeDayTimeUp()
    {
        bool value = pendingDayTimeUp;
        pendingDayTimeUp = false;
        return value;
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
        int[] requested = requestedFlowerIds.Distinct().ToArray();

        var obtained = CurrencyManager.Instance.GetObtainedFlowerIds();
        // 멈춤을 줄이려고 요청 퍼즐만 만든다. 잉여 퍼즐은 필요할 때 EnsureNightStage가 하나씩 만든다(요청이 없으면 첫 잉여 퍼즐만).
        nightQueue = ProceduralNightPuzzleGenerator.GenerateNightQueue(NightGridSize, requested, obtained, 0);
        nightIndex = 0;
        EnsureNightStage(0);

        // 밤 요청을 마치면 밤 메인에서 밤 손님이 퍼즐 이야기를 하고, 수락하면 퍼즐로 간다(만들 퍼즐이 없으면 바로 퍼즐 씬).
        nightCustomerPending = nightQueue.Count > 0;
        SceneManager.LoadScene(nightCustomerPending ? SceneNightMain : SceneNightPuzzle);
    }

    /// <summary>밤 메인 진입 시, 밤 손님을 만나야 하면 첫 퍼즐에 나올 꽃 이름들을 한 번 돌려주고(아니면 null) 지운다.</summary>
    public string ConsumeNightCustomerFlowers()
    {
        if (!nightCustomerPending) return null;
        nightCustomerPending = false;

        EnsureNightStage(nightIndex);
        var names = nightQueue[nightIndex].requiredFlowerIds
            .Select(id => BlockRegistry.Instance != null ? BlockRegistry.Instance.GetById(id) : null)
            .Where(f => f != null)
            .Select(f => string.IsNullOrEmpty(f.flowerName) ? $"꽃 {f.blockID}" : f.flowerName);
        return string.Join(", ", names);
    }

    /// <summary>밤 시간이 끝나기 전까지 퍼즐이 계속 나오도록, 큐가 모자라면 잉여 퍼즐을 더 만든다.</summary>
    private void EnsureNightStage(int index)
    {
        while (nightQueue.Count <= index)
        {
            var more = ProceduralNightPuzzleGenerator.GenerateNightQueue(NightGridSize, null, CurrencyManager.Instance.GetObtainedFlowerIds(), 1);
            if (more.Count == 0) return;
            nightQueue.AddRange(more);
        }
    }

    /// <summary>밤 손님의 부탁을 수락했을 때: 밤 퍼즐로 간다.</summary>
    public void StartNightPuzzle()
    {
        SceneManager.LoadScene(SceneNightPuzzle);
    }

    private void LoadCurrentNightStageInto(NightBoardController board)
    {
        EnsureNightStage(nightIndex);
        if (nightIndex >= nightQueue.Count)
        {
            EndNight(); // 만들 수 있는 퍼즐이 없으면 밤을 끝낸다
            return;
        }

        NightPuzzleData data = nightQueue[nightIndex];
        var allowedBlocks = new List<FlowerData>();
        foreach (int id in data.requiredFlowerIds)
        {
            var b = BlockRegistry.Instance.GetById(id);
            if (b != null) allowedBlocks.Add(b);
        }

        string flavor = data.isBonusStage ? "남는 시간, 손님들이 편안히 성불합니다" : "요청받은 꽃을 손질합니다";
        EventBus.RaiseAscensionMoment(flavor);

        board.OnStagePuzzleCompleted -= HandleStageCompleted;
        board.OnStagePuzzleCompleted += HandleStageCompleted;
        board.LoadPuzzle(data, allowedBlocks);
    }

    /// <summary>낮 퍼즐이 끝났을 때: 꽃 선택에서 고른 꽃을 쓴 것과 상관없이 고른 개수만큼 보유 수량에서 소모한다(한 번만).</summary>
    public void ConsumeChosenFlowers()
    {
        foreach (FlowerData flower in ChosenGachaPool) CurrencyManager.Instance.AddFlowerStock(flower.blockID, -1);
        ChosenGachaPool = new List<FlowerData>();
    }

    /// <summary>밤 퍼즐 완료(스킵 디버그 포함): 퍼즐에 쓴 꽃 종류마다 보유 수량을 늘리고 결과 팝업을 보여 준다. 확인하면 다음 손님으로 간다.</summary>
    private void HandleStageCompleted(List<int> obtainedFlowerIds)
    {
        var gains = new List<KeyValuePair<string, int>>();
        foreach (int id in obtainedFlowerIds)
        {
            CurrencyManager.Instance.AddFlowerStock(id, nightRewardPerFlower);

            FlowerData flower = BlockRegistry.Instance != null ? BlockRegistry.Instance.GetById(id) : null;
            string flowerName = flower != null && !string.IsNullOrEmpty(flower.flowerName) ? flower.flowerName : $"꽃 {id}";
            gains.Add(new KeyValuePair<string, int>(flowerName, nightRewardPerFlower));
        }

        var resultUI = FindFirstObjectByType<NightResultUI>();
        if (resultUI != null) resultUI.Show(gains);
        else ContinueNightAfterResult(); // 결과 팝업이 없는 씬이면 바로 다음으로
    }

    /// <summary>밤 퍼즐 결과 팝업의 확인: 밤 메인으로 돌아가 다음(잉여 퍼즐) 손님을 만난다.</summary>
    public void ContinueNightAfterResult()
    {
        nightIndex++;
        nightCustomerPending = true;
        SceneManager.LoadScene(SceneNightMain);
    }

    /// <summary>디버그용: 지금 밤 퍼즐을 풀지 않고 성공한 것으로 처리한다(이 퍼즐의 꽃을 모두 쓴 것으로 보고 보상/해금/다음 손님까지 같은 흐름).</summary>
    public void DebugSkipNightPuzzle()
    {
        if (SceneManager.GetActiveScene().name != SceneNightPuzzle || nightIndex >= nightQueue.Count) return;

        var flowerIds = nightQueue[nightIndex].requiredFlowerIds.Distinct().ToList();
        foreach (int id in flowerIds) CurrencyManager.Instance.ObtainFlower(id);
        HandleStageCompleted(flowerIds);
    }

    /// <summary>밤 시간이 끝났을 때: 진행 중이던 퍼즐/손님은 버리고 밤 메인에서 마지막 손님이 외치고 떠난다.</summary>
    private void HandleNightTimeUp()
    {
        nightCustomerPending = false;
        nightEndPending = true;

        if (SceneManager.GetActiveScene().name == SceneNightMain)
        {
            var ui = FindFirstObjectByType<DayMainUI>();
            if (ui != null)
            {
                nightEndPending = false; // 이미 밤 메인이라 그 자리에서 바로 처리한다
                ui.BeginNightEnd();
                return;
            }
        }

        SceneManager.LoadScene(SceneNightMain);
    }

    /// <summary>밤 메인 진입 시, 밤 시간이 끝나서 온 것이면 true를 한 번 반환하고 지운다.</summary>
    public bool ConsumeNightEnd()
    {
        bool value = nightEndPending;
        nightEndPending = false;
        return value;
    }

    /// <summary>밤이 끝나면(마지막 손님이 사라지면) 하루 결산 화면으로 간다.</summary>
    public void EndNight()
    {
        SceneManager.LoadScene(SceneResult);
    }

    /// <summary>결산 화면에서 다음으로: 날짜를 넘기고 다음 날 낮을 시작한다.</summary>
    public void StartNextDay()
    {
        CurrencyManager.Instance.AdvanceDay();
        BeginNewDay();
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
