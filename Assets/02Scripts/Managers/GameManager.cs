using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 낮→밤→낮 코어 루프 전체를 조율하는 게임 매니저(씬 흐름 담당).
/// 부트스트랩 씬(00Start)에 배치되어 씬 전환 간 유지된다. UI를 직접 찾아 부르지 않고 EventBus로만 주고받는다(밤 퍼즐 씬이 로드되면
/// 현재 밤 퍼즐을 알리고, 퍼즐 완료/결과 확인 이벤트를 받는다). 오늘의 주문과 가챠 풀은 UI가 필요할 때 읽어 간다.
///
/// 손님을 만나는 메인 화면은 씬 하나(01DayMain)이고, 낮 손님/낮 종료/밤 손님/밤 종료 중 무엇을 보여 줄지는 MainState로 정해서 씬을 연다.
/// 씬 이름 상수는 Assets/01Scenes 폴더 구조를 그대로 따른다.
/// </summary>
public class GameManager : Singleton<GameManager>
{
    public const string SceneStart = "00Start";
    public const string SceneDayMain = "01DayMain";
    public const string SceneDayPuzzle = "03DayPuzzle";
    public const string SceneNightPuzzle = "02NightPuzzle";
    public const string SceneResult = "03Result";

    [SerializeField] private int nightRewardPerFlower = 6; // 밤 퍼즐을 완료하면 퍼즐에 쓴 꽃 종류마다 늘어나는 보유 수량

    [Header("색 주문에서 색 이름 대신 비유 문구(\"동백 같은 꽃\")로 말하는 확률")]
    [SerializeField, Range(0f, 1f)] private float colorMetaphorChance = 0.2f;

    public DayPuzzleGenerator.DayOrder CurrentDayOrder { get; private set; }
    public List<FlowerData> ChosenGachaPool { get; private set; } = new();

    /// <summary>다음에 열리는(또는 지금 열려 있는) 메인 화면이 보여 줄 상황. 메인 화면으로 가기 전에 정한다.</summary>
    public MainState MainState { get; private set; } = MainState.DayCustomer;

    private const string DefaultRejectResponseLine = "그래요라고 하세요";

    private string pendingCustomerLine;
    private string pendingRejectResponseLine = DefaultRejectResponseLine;
    private bool orderInProgress; // 주문을 수락해 꽃 선택/퍼즐을 하는 중
    private bool dayEndPending; // 주문을 하는 중에 하루 시간이 끝나, 그 주문이 끝나면 낮 종료로 가야 한다

    private List<NightPuzzleData> nightQueue = new();
    private readonly Queue<int[]> pendingRequestStages = new(); // 아직 퍼즐로 만들지 않은 부적 꽃 묶음(최대 2종씩)
    private int nightIndex;
    private const int NightGridSize = 5; // 임시: 밤 퍼즐 판은 5x5 정사각형으로 고정

    protected override void OnAwake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        EventBus.OnDayTimeUp += HandleDayTimeUp;
        EventBus.OnNightTimeUp += HandleNightTimeUp;
        EventBus.OnNightStageCompleted += HandleStageCompleted;
        EventBus.OnNightResultConfirmed += ContinueNightAfterResult;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        EventBus.OnDayTimeUp -= HandleDayTimeUp;
        EventBus.OnNightTimeUp -= HandleNightTimeUp;
        EventBus.OnNightStageCompleted -= HandleStageCompleted;
        EventBus.OnNightResultConfirmed -= ContinueNightAfterResult;
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
        dayEndPending = false;
        orderInProgress = false;
        MainState = MainState.DayCustomer;
        CurrentDayOrder = DayPuzzleGenerator.GenerateOrder(CurrencyManager.Instance.CurrentDay, colorMetaphorChance);
        CurrencyManager.Instance.SetCurrentRequirement(CurrentDayOrder.requirement);
        SceneManager.LoadScene(SceneDayMain);
    }

    /// <summary>플레이어가 포장지를 골랐을 때(꽃 선택 화면 상단). 포장지 단계의 태그 요구 레벨을 확정한다.</summary>
    public void ChooseWrapper(int wrapperId)
    {
        DayPuzzleGenerator.FinalizeForWrapper(CurrentDayOrder, wrapperId);
    }

    /// <summary>손님의 주문을 수락했다: 이때부터 퍼즐을 마칠 때까지가 한 주문이라, 그사이 하루 시간이 끝나도 주문을 마무리한 뒤에 낮 종료로 간다.
    /// 꽃 선택 화면은 메인 씬 옆 페이지라서 씬을 바꾸지 않고, 플레이어가 스와이프해서 간다.</summary>
    public void AcceptOrder()
    {
        orderInProgress = true;
    }

    public void ConfirmFlowerSelection(List<FlowerData> chosen)
    {
        ChosenGachaPool = new List<FlowerData>(chosen);
        SceneManager.LoadScene(SceneDayPuzzle);
    }

    /// <summary>카운터에서 등불을 켜면 밤이 시작된다: 밤 시간이 가득 차 흐르기 시작하고, 부적을 붙인 꽃의 퍼즐로 밤 손님이 찾아온다.</summary>
    public void StartNight(List<int> charmedFlowerIds)
    {
        GameClock.Instance?.BeginNight();
        ConfirmNightRequests(charmedFlowerIds);
    }

    /// <summary>낮 퍼즐이 끝나 다음 손님이 있는 낮 메인으로 돌아간다(성공이든 손님이 화났든). 돌아가면 손님이 customerLine을 첫 대사로 말한다.
    /// rejectResponseLine은 "이건아니지"를 눌렀을 때 나오는 대사(기본값: "그래요라고 하세요").</summary>
    public void ReturnToDayMain(string customerLine, string rejectResponseLine = DefaultRejectResponseLine)
    {
        if (dayEndPending) // 주문을 하는 동안 하루 시간이 끝났다: 이 주문은 마무리했으니 이제 낮 종료로 간다
        {
            dayEndPending = false;
            BeginDayEnding();
            return;
        }

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

    /// <summary>하루 시간이 끝나면 메인 화면의 낮 종료로 넘어간다. 손님이 마무리 대사를 하고 떠난다.
    /// 단, 주문을 수락해 꽃 선택/퍼즐을 하는 중(AcceptOrder 이후)이면 그 주문을 마무리한 뒤에 넘어간다(ReturnToDayMain).</summary>
    private void HandleDayTimeUp()
    {
        if (orderInProgress)
        {
            dayEndPending = true;
            return;
        }
        BeginDayEnding();
    }

    private void BeginDayEnding()
    {
        orderInProgress = false;
        MainState = MainState.DayEnding;
        SceneManager.LoadScene(SceneDayMain);
    }

    // ========== 밤 ==========

    /// <summary>requestedFlowerIds는 오늘 밤 부적을 붙인 꽃들.
    /// 퍼즐 하나에는 꽃이 최대 2종만 나오므로, 부적을 붙인 꽃을 섞어서 두 개씩 묶어 퍼즐 하나씩 만든다(6개면 퍼즐 3개).
    /// 묶음마다 퍼즐은 필요할 때 EnsureNightStage가 하나씩 만든다(한꺼번에 만들면 멈춤이 생긴다). 묶음을 다 쓰면 잉여 퍼즐로 이어진다.</summary>
    private void ConfirmNightRequests(List<int> requestedFlowerIds)
    {
        int[] requested = requestedFlowerIds.Distinct().OrderBy(_ => Random.value).ToArray();

        pendingRequestStages.Clear();
        int perStage = ProceduralNightPuzzleGenerator.MaxBlocksPerStage;
        for (int i = 0; i < requested.Length; i += perStage) pendingRequestStages.Enqueue(requested.Skip(i).Take(perStage).ToArray());

        nightQueue = new List<NightPuzzleData>();
        nightIndex = 0;
        EnsureNightStage(0);

        // 밤 요청을 마치면 메인 화면에서 밤 손님이 퍼즐 이야기를 하고, 수락하면 퍼즐로 간다(만들 퍼즐이 없으면 바로 퍼즐 씬).
        if (nightQueue.Count > 0)
        {
            MainState = MainState.NightCustomer;
            SceneManager.LoadScene(SceneDayMain);
        }
        else
        {
            SceneManager.LoadScene(SceneNightPuzzle);
        }
    }

    /// <summary>밤 손님이 지금 풀 퍼즐에 나올 꽃 이름들(없으면 null: 만들 수 있는 퍼즐이 더 없다).</summary>
    public string NightCustomerFlowerNames()
    {
        EnsureNightStage(nightIndex);
        if (nightIndex >= nightQueue.Count) return null;

        var names = nightQueue[nightIndex].requiredFlowerIds
            .Select(id => BlockRegistry.Instance != null ? BlockRegistry.Instance.GetById(id) : null)
            .Where(f => f != null)
            .Select(f => f.DisplayName);
        return string.Join(", ", names);
    }

    /// <summary>index번째 퍼즐까지 큐를 채운다. 부적 묶음이 남아 있으면 그 묶음의 퍼즐을, 없으면 밤 시간이 끝나기 전까지 퍼즐이 계속 나오도록 잉여 퍼즐을 만든다.</summary>
    private void EnsureNightStage(int index)
    {
        while (nightQueue.Count <= index)
        {
            if (pendingRequestStages.Count > 0)
            {
                nightQueue.Add(ProceduralNightPuzzleGenerator.GenerateStage(NightGridSize, pendingRequestStages.Dequeue(), isBonusStage: false));
            }
            else if (ProceduralNightPuzzleGenerator.TryGenerateBonusStage(NightGridSize, CurrencyManager.Instance.GetObtainedFlowerIds(), out NightPuzzleData bonus))
            {
                nightQueue.Add(bonus);
            }
            else
            {
                return;
            }
        }
    }

    /// <summary>밤 손님의 부탁을 수락했을 때: 밤 퍼즐로 간다.</summary>
    public void StartNightPuzzle()
    {
        SceneManager.LoadScene(SceneNightPuzzle);
    }

    /// <summary>밤 퍼즐 씬이 열렸을 때: 지금 풀 퍼즐을 알린다(보드가 이벤트로 받아 판을 만든다).</summary>
    private void PublishCurrentNightStage()
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

        EventBus.RaiseNightStageReady(data, allowedBlocks);
    }

    /// <summary>낮 퍼즐이 끝났을 때: 실제로 칸에 놓은 꽃 한 송이마다 보유 수량을 1씩 소모한다(한 번만).</summary>
    public void ConsumeUsedFlowers(IEnumerable<FlowerData> used)
    {
        foreach (FlowerData flower in used) CurrencyManager.Instance.AddFlowerStock(flower.blockID, -1);
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
            gains.Add(new KeyValuePair<string, int>(flower != null ? flower.DisplayName : $"꽃 {id}", nightRewardPerFlower));
        }

        if (!EventBus.RaiseNightStageResult(gains)) ContinueNightAfterResult(); // 결과 팝업이 없는 씬이면 바로 다음으로
    }

    /// <summary>밤 퍼즐 결과 팝업의 확인: 메인 화면으로 돌아가 다음(잉여 퍼즐) 손님을 만난다.</summary>
    public void ContinueNightAfterResult()
    {
        nightIndex++;
        MainState = MainState.NightCustomer;
        SceneManager.LoadScene(SceneDayMain);
    }

    /// <summary>디버그용: 지금 밤 퍼즐을 풀지 않고 성공한 것으로 처리한다(이 퍼즐의 꽃을 모두 쓴 것으로 보고 보상/해금/다음 손님까지 같은 흐름).</summary>
    public void DebugSkipNightPuzzle()
    {
        if (SceneManager.GetActiveScene().name != SceneNightPuzzle || nightIndex >= nightQueue.Count) return;

        var flowerIds = nightQueue[nightIndex].requiredFlowerIds.Distinct().ToList();
        foreach (int id in flowerIds) CurrencyManager.Instance.ObtainFlower(id);
        HandleStageCompleted(flowerIds);
    }

    /// <summary>밤 시간이 끝났을 때: 진행 중이던 퍼즐/손님은 버리고 메인 화면에서 마지막 손님이 외치고 떠난다(밤 종료).</summary>
    private void HandleNightTimeUp()
    {
        MainState = MainState.NightEnding;

        if (SceneManager.GetActiveScene().name == SceneDayMain) return; // 이미 메인 화면이면(밤 손님을 만나는 중) 그 자리의 DayMainUI가 같은 이벤트를 받아 바로 처리한다

        SceneManager.LoadScene(SceneDayMain);
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
        if (scene.name == SceneNightPuzzle) PublishCurrentNightStage();
    }
}
