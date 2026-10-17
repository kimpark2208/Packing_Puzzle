using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 낮→밤→낮 코어 루프 전체를 조율하는 게임 매니저(씬 흐름 담당).
/// 부트스트랩 씬(00Start)에 배치되어 씬 전환 간 유지된다. UI를 직접 찾아 부르지 않는다. 오늘의 주문, 가챠 풀, 지금 밤 손님의 진단 문제(CurrentNightCase)는 UI가 필요할 때 읽어 간다.
///
/// 손님을 만나는 메인 화면은 씬 하나(01DayMain)이고, 낮 손님/낮 종료/밤 손님/밤 종료 중 무엇을 보여 줄지는 MainState로 정해서 씬을 연다.
/// 씬 이름 상수는 Assets/01Scenes 폴더 구조를 그대로 따른다.
/// </summary>
public class GameManager : Singleton<GameManager>
{
    public const string SceneDayMain = "01DayMain";
    public const string SceneDayPuzzle = "03DayPuzzle";
    public const string SceneNightPuzzle = "02NightPuzzle";
    public const string SceneResult = "03Result";

    [Header("손님 주문 대사의 템플릿과 확률(직설형/은유형 비율, 특수주문 확률 포함)")]
    [SerializeField] private OrderTemplateData orderTemplates;

    public DayPuzzleGenerator.DayOrder CurrentDayOrder { get; private set; }
    public List<FlowerData> ChosenGachaPool { get; private set; } = new();

    /// <summary>지금 주문을 한 낮 손님의 모습. 퍼즐을 마치고 메인 화면이 다시 열려도 반응 대사와 낮 종료 대사를 같은 손님이 하도록 기억해 둔다(메인 화면은 열릴 때마다 새로 만들어진다).</summary>
    public CharacterAppearance? CustomerLook { get; set; }

    /// <summary>지금 만나는 밤 손님의 진단 문제(꽃과 아픔). 밤 손님이 없으면 null.</summary>
    public NightDiagnosisCase CurrentNightCase { get; private set; }

    /// <summary>다음에 열리는(또는 지금 열려 있는) 메인 화면이 보여 줄 상황. 메인 화면으로 가기 전에 정한다.</summary>
    public MainState MainState { get; private set; } = MainState.DayCustomer;

    private const string DefaultRejectResponseLine = CustomerSpeech.DefaultForceAcceptLine;

    private string pendingCustomerLine;
    private string pendingRejectResponseLine = DefaultRejectResponseLine;
    private bool orderInProgress; // 주문을 수락해 꽃 선택/퍼즐을 하는 중
    private bool dayEndPending; // 주문을 하는 중에 하루 시간이 끝나, 그 주문이 끝나면 낮 종료로 가야 한다

    private readonly Queue<FlowerData> nightFlowers = new(); // 오늘 밤 손님들이 들고 올 꽃(부적을 붙인 꽃부터. 다 나오면 보유한 꽃 중에서 무작위)

    protected override void OnAwake()
    {
        EventBus.OnDayTimeUp += HandleDayTimeUp;
        EventBus.OnNightTimeUp += HandleNightTimeUp;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        EventBus.OnDayTimeUp -= HandleDayTimeUp;
        EventBus.OnNightTimeUp -= HandleNightTimeUp;
    }

    // ========== 낮 ==========

    /// <summary>시작 화면(00Start)에서 터치했다: 로딩이 기니 로딩 씬을 거쳐 낮 메인으로 간다. 첫 손님은 로딩 씬에서 정한다.</summary>
    public void StartGame()
    {
        SceneLoader.Load(SceneDayMain, StartGameWork());
    }

    private IEnumerator<float> StartGameWork()
    {
        yield return 0f;
        PrepareNewDay();
        yield return 1f;
    }

    public void BeginNewDay()
    {
        PrepareNewDay();
        SceneTransition.Go(SceneDayMain);
    }

    /// <summary>새 손님(오늘의 주문)을 정하고 메인 화면이 낮 손님을 보여 주도록 해 둔다.</summary>
    private void PrepareNewDay()
    {
        dayEndPending = false;
        orderInProgress = false;
        MainState = MainState.DayCustomer;
        CurrentDayOrder = DayPuzzleGenerator.GenerateOrder(CurrencyManager.Instance.CurrentDay, orderTemplates);
        CurrencyManager.Instance.SetCurrentRequirement(CurrentDayOrder.requirement);
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
        SceneTransition.Go(SceneDayPuzzle);
    }

    /// <summary>카운터에서 등불을 켜면 밤이 시작된다: 밤 시간이 가득 차 흐르기 시작하고, 부적을 붙인 꽃부터 차례로 밤 손님이 꽃을 들고 찾아온다.
    /// 밤 시간은 밤 화면이 열려야 흐른다.</summary>
    public void StartNight(List<int> charmedFlowerIds)
    {
        GameClock.Instance?.BeginNight();

        nightFlowers.Clear();
        foreach (int id in charmedFlowerIds.Distinct().OrderBy(_ => Random.value))
        {
            FlowerData flower = DataRegistry.Instance != null ? DataRegistry.Instance.GetFlower(id) : null;
            if (flower != null) nightFlowers.Enqueue(flower);
        }

        if (!PrepareNextNightCase())
        {
            EndNight(); // 진단 그림이 있는 꽃이 하나도 없으면 밤을 건너뛴다
            return;
        }
        MainState = MainState.NightCustomer;
        SceneTransition.Go(SceneDayMain);
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
        SceneTransition.Go(SceneDayMain);
    }

    // ========== 밤 ==========

    /// <summary>다음 밤 손님의 진단 문제를 만든다: 부적을 붙인 꽃부터, 다 나오면 보유한 꽃 중에서 무작위로 손님의 꽃을 고른다.
    /// 꽃 그림은 그 꽃에 진단용 부위 그림이 있으면 그것을, 없으면 그림이 있는 다른 꽃의 그림을 대신 쓴다(그림이 채워지기 전의 임시 처리). 만들 수 없으면 false.</summary>
    private bool PrepareNextNightCase()
    {
        FlowerData flower = nightFlowers.Count > 0 ? nightFlowers.Dequeue() : RandomOwnedFlower();
        FlowerData art = flower != null && flower.HasDiagnosisArt ? flower : DataRegistry.Instance?.Flowers.FirstOrDefault(f => f != null && f.HasDiagnosisArt);
        CurrentNightCase = flower != null && art != null ? NightDiagnosisCase.Generate(flower, art) : null;
        return CurrentNightCase != null;
    }

    /// <summary>보유한 꽃 중 하나(없으면 진단 그림이 있는 아무 꽃).</summary>
    private static FlowerData RandomOwnedFlower()
    {
        if (DataRegistry.Instance == null) return null;

        List<int> owned = CurrencyManager.Instance.GetObtainedFlowerIds();
        FlowerData flower = owned.Count > 0 ? DataRegistry.Instance.GetFlower(owned[Random.Range(0, owned.Count)]) : null;
        return flower != null ? flower : DataRegistry.Instance.Flowers.FirstOrDefault(f => f != null && f.HasDiagnosisArt);
    }

    /// <summary>밤 손님이 말하는 꽃 이름(없으면 null: 만날 밤 손님이 더 없다).</summary>
    public string NightCustomerFlowerName() => CurrentNightCase?.flower.DisplayName;

    /// <summary>밤 손님의 부탁을 수락했을 때: 밤 퍼즐로 간다.</summary>
    public void StartNightPuzzle()
    {
        SceneTransition.Go(SceneNightPuzzle);
    }

    /// <summary>낮 퍼즐이 끝났을 때: 실제로 칸에 놓은 꽃 한 송이마다 보유 수량을 1씩 소모한다(한 번만).</summary>
    public void ConsumeUsedFlowers(IEnumerable<FlowerData> used)
    {
        foreach (FlowerData flower in used) CurrencyManager.Instance.AddFlowerStock(flower.flowerID, -1);
        ChosenGachaPool = new List<FlowerData>();
    }

    /// <summary>밤 진단을 확정했다: 체크한 아픔을 정답과 비교해 결과를 콘솔에 남기고, 새 밤 손님을 만나러 메인 화면으로 간다(수리 퍼즐과 보상은 아직 없다).
    /// 만날 손님이 더 없으면 밤을 끝낸다.</summary>
    public void FinishNightDiagnosis(IReadOnlyCollection<FlowerData.Ailment> picked)
    {
        if (CurrentNightCase != null)
        {
            CurrentNightCase.Compare(picked, out int missed, out int wrong);
            Debug.Log($"[GameManager] 밤 진단 결과: {CurrentNightCase.flower.DisplayName} / 정답 {string.Join(", ", CurrentNightCase.ailments.Select(NightDiagnosisUI.AilmentName))} / 체크 {string.Join(", ", picked.Select(NightDiagnosisUI.AilmentName))} → 놓친 {missed}, 틀린 {wrong}");
        }

        NextNightCustomer();
    }

    /// <summary>다음 밤 손님을 만나러 메인 화면으로 간다. 만날 손님이 더 없으면 밤을 끝낸다.</summary>
    private void NextNightCustomer()
    {
        if (!PrepareNextNightCase())
        {
            EndNight();
            return;
        }
        MainState = MainState.NightCustomer;
        SceneTransition.Go(SceneDayMain);
    }

    /// <summary>디버그용: 지금 밤 손님을 건너뛴다. 진단 씬이면 정답 그대로 체크한 것으로 확정하고, 메인 화면에서 손님을 만나는 중이면 진단 없이 다음 손님으로 간다.</summary>
    public void DebugSkipNightPuzzle()
    {
        if (CurrentNightCase == null || MainState != MainState.NightCustomer || SceneTransition.Busy) return;

        string scene = SceneManager.GetActiveScene().name;
        if (scene == SceneNightPuzzle) FinishNightDiagnosis(CurrentNightCase.ailments);
        else if (scene == SceneDayMain) NextNightCustomer();
    }

    /// <summary>밤 시간이 끝났을 때: 진행 중이던 퍼즐/손님은 버리고 메인 화면에서 마지막 손님이 외치고 떠난다(밤 종료).</summary>
    private void HandleNightTimeUp()
    {
        MainState = MainState.NightEnding;

        if (SceneManager.GetActiveScene().name == SceneDayMain) return; // 이미 메인 화면이면(밤 손님을 만나는 중) 그 자리의 MainUI가 같은 이벤트를 받아 바로 처리한다

        SceneTransition.Go(SceneDayMain);
    }

    /// <summary>밤이 끝나면(마지막 손님이 사라지면) 하루 결산 화면으로 간다.</summary>
    public void EndNight()
    {
        SceneTransition.Go(SceneResult);
    }

    /// <summary>결산 화면에서 다음으로: 날짜를 넘기고 다음 날 낮을 시작한다.</summary>
    public void StartNextDay()
    {
        CurrencyManager.Instance.AdvanceDay();
        BeginNewDay();
    }
}
