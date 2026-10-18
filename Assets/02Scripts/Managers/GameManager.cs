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

    [Header("밤 손님의 반응 대사(퍼즐 후 메인 화면에서 말한다). {flower}는 꽃 이름, {count}는 받는 복제품 수")]
    [SerializeField, TextArea, Tooltip("아픔을 모두 고쳤을 때.")] private string nightReactionAll = "와!! 몰라보게 좋아졌네요. 여기 {flower} {count}개 드릴게요!";
    [SerializeField, TextArea, Tooltip("아픔이 둘 중 하나만 고쳐졌을 때.")] private string nightReactionPartial = "애매하네요... 그래도 전보다는 상태가 좋아진 것 같아요. 여기 약속했던 {flower} {count}개 드릴게요";
    [SerializeField, TextArea, Tooltip("아픔을 하나도 고치지 못했을 때.")] private string nightReactionNone = "엥? 수리 맡기기 전과 다를 바가 없는데요? 보수는 없던일로 합시다.";
    [SerializeField, TextArea, Tooltip("반응 대사에서 거절 버튼을 눌렀을 때: 보상이 있는 경우.")] private string nightRejectWithReward = "보상을 안 받으시겠다는건가요? 그래도 드릴게요 선물은 원래 주는놈 마음입니다.";
    [SerializeField, TextArea, Tooltip("반응 대사에서 거절 버튼을 눌렀을 때: 보상이 없는 경우.")] private string nightRejectNoReward = "보상 못 드리겠다구요. 주고싶어도 제 꽃이 고쳐져야 꽃을 만들 수 있어요.";

    public DayPuzzleGenerator.DayOrder CurrentDayOrder { get; private set; }
    public List<FlowerData> ChosenGachaPool { get; private set; } = new();

    /// <summary>지금 주문을 한 낮 손님의 모습. 퍼즐을 마치고 메인 화면이 다시 열려도 반응 대사와 낮 종료 대사를 같은 손님이 하도록 기억해 둔다(메인 화면은 열릴 때마다 새로 만들어진다).</summary>
    public CharacterAppearance? CustomerLook { get; set; }

    /// <summary>지금 만나는 밤 손님의 진단 문제(꽃과 아픔). 밤 손님이 없거나 대화만 하는 방문이면 null.</summary>
    public NightDiagnosisCase CurrentNightCase { get; private set; }

    /// <summary>지금 밤 방문이 시나리오에 정해진 방문이면 그 방문(NPC, 대화, 확정 지급), 아니면 null(평소 밤 손님).</summary>
    public NightVisit? CurrentNightVisit { get; private set; }

    /// <summary>만날 밤 손님이 있는가(수리를 부탁하는 손님이거나, 시나리오에 정해진 대화만 하는 방문).</summary>
    public bool HasNightCustomer => CurrentNightCase != null || (CurrentNightVisit.HasValue && CurrentNightVisit.Value.dialogueOnly);

    /// <summary>방금 수리를 맡기고 간 밤 손님이 정해진 NPC였으면 그 NPC(반응 대사를 할 때 그 모습을 보인다). 아니면 null.</summary>
    public NpcData PreviousNpc { get; private set; }

    /// <summary>다음에 열리는(또는 지금 열려 있는) 메인 화면이 보여 줄 상황. 메인 화면으로 가기 전에 정한다.</summary>
    public MainState MainState { get; private set; } = MainState.DayCustomer;

    /// <summary>오늘 낮에 지금 만나는 방문자가 몇 번째인가(1부터. 방문자가 올 때마다 하나씩 늘고, 날이 바뀌면 0부터 다시 센다).</summary>
    public int DayVisitIndex { get; private set; }

    /// <summary>지금 방문자가 시나리오에 정해진 NPC면 그 방문, 아니면 null(무작위 손님).</summary>
    public DayVisit? CurrentScriptedVisit { get; private set; }

    private const string DefaultRejectResponseLine = CustomerSpeech.DefaultForceAcceptLine;

    private string pendingCustomerLine;
    private string pendingRejectResponseLine = DefaultRejectResponseLine;
    private bool orderInProgress; // 주문을 수락해 꽃 선택/퍼즐을 하는 중
    private bool dayEndPending; // 주문을 하는 중에 하루 시간이 끝나, 그 주문이 끝나면 낮 종료로 가야 한다

    private readonly Queue<FlowerData> nightFlowers = new(); // 오늘 밤 손님들이 들고 올 꽃(부적을 붙인 꽃부터. 다 나오면 보유한 꽃 중에서 무작위)
    private readonly Queue<NightVisit> scriptedNightVisits = new(); // 오늘 밤 시나리오에 정해진 방문(이게 먼저 오고, 끝나면 위의 평소 밤 손님이 이어진다)
    private readonly List<NightVisit> carriedNightVisits = new(); // 밤 시간이 끝나 못 만난 정해진 방문(다음 밤 맨 앞으로 넘어간다)
    private bool currentNightVisitDone;  // 지금 정해진 방문을 마쳤는가(퍼즐을 마치거나 대화를 마침)
    private bool nightFree;              // 오늘 밤은 밤 시간이 없고 정해진 손님을 모두 마쳐야 끝난다(1일차)
    private int freeNightTotal; // 밤 시간이 없는 날의 수리 손님 수(시계 표시용)
    private bool nightPuzzleInProgress;  // 밤 퍼즐을 하는 중
    private bool nightTimeUpPending;     // 밤 퍼즐을 하는 중에 밤 시간이 끝나, 그 손님을 마무리한 뒤 밤을 끝내야 한다

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
        PreviousNpc = CurrentScriptedVisit.HasValue ? CurrentScriptedVisit.Value.npc : null; // 방금 주문을 한 손님(반응 대사 때 그 모습을 보인다)

        int day = CurrencyManager.Instance.CurrentDay;
        DayVisitIndex++;
        CurrentScriptedVisit = ScenarioManager.Instance != null ? ScenarioManager.Instance.DayVisitAt(day, DayVisitIndex) : null;

        CurrentDayOrder = ScriptedOrder(CurrentScriptedVisit, day) ?? DayPuzzleGenerator.GenerateOrder(day, orderTemplates);
        CurrencyManager.Instance.SetCurrentRequirement(CurrentDayOrder.requirement);
    }

    /// <summary>정해진 방문의 대화에 주문 효과(`주문:꽃xN`, `주문:포장지N`)가 있으면 그 주문으로 낮 주문을 만든다. 주문 대사는 주문 효과가 달린 줄이다. 없거나 쓸 수 없으면 null(무작위 주문).</summary>
    private static DayPuzzleGenerator.DayOrder ScriptedOrder(DayVisit? visit, int day)
    {
        if (!visit.HasValue || ScenarioManager.Instance == null || DataRegistry.Instance == null) return null;

        Dialogue dialogue = ScenarioManager.Instance.Dialogue.Get(visit.Value.dialogueId);
        DialogueLine orderLine = dialogue?.lines.LastOrDefault(l => l.effects.Any(e => e.kind == DialogueEffect.Order));
        if (orderLine == null) return null;

        var flowers = new List<(FlowerData flower, int count)>();
        int wrapperId = 0;
        foreach (DialogueEffect effect in orderLine.effects.Where(e => e.kind == DialogueEffect.Order))
        {
            if (effect.WrapperNumber > 0) { wrapperId = effect.WrapperNumber; continue; }

            FlowerData flower = DataRegistry.Instance.Flowers.FirstOrDefault(f => f != null && f.DisplayName == effect.target);
            if (flower == null) Debug.LogWarning($"[GameManager] 대화 '{dialogue.id}'의 주문 꽃 '{effect.target}'을(를) 찾을 수 없습니다.");
            else flowers.Add((flower, effect.amount));
        }
        if (flowers.Count == 0) return null;

        return DayPuzzleGenerator.GenerateFixedOrder(day, flowers, wrapperId, orderLine.text);
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

        scriptedNightVisits.Clear();
        DayScenarioData scenario = ScenarioManager.Instance != null ? ScenarioManager.Instance.ForDay(CurrencyManager.Instance.CurrentDay) : null;
        foreach (NightVisit visit in carriedNightVisits) scriptedNightVisits.Enqueue(visit); // 지난 밤에 못 만난 정해진 손님(새 꽃)이 가장 먼저 온다
        carriedNightVisits.Clear();
        if (scenario != null) foreach (NightVisit visit in scenario.nightVisits) scriptedNightVisits.Enqueue(visit);

        nightFree = scenario != null && scenario.freeNight;
        freeNightTotal = scriptedNightVisits.Count(v => !v.dialogueOnly); // 밤 시간이 없는 날은 손님 한 명을 마칠 때마다 시계가 1/total씩 줄어든다
        if (GameClock.Instance != null) GameClock.Instance.NightFrozen = nightFree;
        nightTimeUpPending = false;
        nightPuzzleInProgress = false;
        currentNightVisitDone = false;

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
    public void ReturnToDayMain(string customerLine, string rejectResponseLine = DefaultRejectResponseLine, ReactionTier tier = ReactionTier.Success)
    {
        if (dayEndPending) // 주문을 하는 동안 하루 시간이 끝났다: 이 주문은 마무리했으니 이제 낮 종료로 간다
        {
            dayEndPending = false;
            BeginDayEnding();
            return;
        }

        // 시나리오에 정해진 NPC(아직 다음 방문자로 넘어가기 전이라 CurrentScriptedVisit이 방금 주문한 손님이다)는 CSV의 반응 대사를 하고, 아니면 평소 반응 대사를 한다
        pendingCustomerLine = ScriptedReaction(CurrentScriptedVisit?.reactionPrefix, tier.ToString().ToLowerInvariant()) ?? customerLine;
        pendingRejectResponseLine = rejectResponseLine;
        BeginNewDay();
    }

    private string pendingNotice; // 다음 메인 화면이 열릴 때 띄울 획득 알림(밤 손님을 고쳐 받은 꽃)

    /// <summary>예약된 획득 알림 글씨를 한 번 꺼내 쓰고 지운다. 없으면 null.</summary>
    public string ConsumePendingNotice()
    {
        string notice = pendingNotice;
        pendingNotice = null;
        return notice;
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
        CurrentNightVisit = null;
        if (nightFree && scriptedNightVisits.Count == 0) // 밤 시간이 없는 날은 정해진 손님을 다 마치면 끝난다
        {
            CurrentNightCase = null;
            return false;
        }
        if (scriptedNightVisits.Count > 0) // 시나리오에 정해진 방문이 먼저 온다
        {
            NightVisit visit = scriptedNightVisits.Dequeue();
            CurrentNightVisit = visit;
            currentNightVisitDone = false;
            if (visit.dialogueOnly)
            {
                CurrentNightCase = null; // 대화만 하고 가는 손님: 진단 문제가 없다
                return true;
            }
            return MakeNightCase(visit.flower != null ? visit.flower : NextNightFlower());
        }
        return MakeNightCase(NextNightFlower());
    }

    private FlowerData NextNightFlower() => nightFlowers.Count > 0 ? nightFlowers.Dequeue() : RandomOwnedFlower();

    private bool MakeNightCase(FlowerData flower)
    {
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

    /// <summary>지금 밤 손님이 시나리오가 새 꽃을 얻게 해 주는 모브 손님인가(NPC·대화 없이 꽃만 정해진 방문).</summary>
    public bool NightCustomerBringsNewFlower => CurrentNightVisit.HasValue && CurrentNightVisit.Value.flower != null
        && CurrentNightVisit.Value.npc == null && string.IsNullOrEmpty(CurrentNightVisit.Value.dialogueId);

    /// <summary>밤 손님의 부탁을 수락했을 때: 밤 퍼즐로 간다.</summary>
    public void StartNightPuzzle()
    {
        nightPuzzleInProgress = true;
        SceneTransition.Go(SceneNightPuzzle);
    }

    /// <summary>낮 퍼즐이 끝났을 때: 쓴 포장지(소모품) 한 장을 소모한다(재고는 0 아래로 내려가지 않는다).</summary>
    public void ConsumeUsedWrapper()
    {
        WrapperData wrapper = CurrentDayOrder != null ? CurrentDayOrder.wrapper : null;
        if (wrapper != null) CurrencyManager.Instance.AddWrapperStock(wrapper.wrapperId, -1);
    }

    /// <summary>낮 퍼즐이 끝났을 때: 실제로 칸에 놓은 꽃 한 송이마다 보유 수량을 1씩 소모한다(한 번만).</summary>
    public void ConsumeUsedFlowers(IEnumerable<FlowerData> used)
    {
        foreach (FlowerData flower in used) CurrencyManager.Instance.AddFlowerStock(flower.flowerID, -1);
        ChosenGachaPool = new List<FlowerData>();
    }

    /// <summary>밤 진단을 확정했다: 체크한 아픔을 정답과 비교해 결과를 콘솔에 남기고 복제품을 지급한 뒤, 메인 화면으로 돌아간다.
    /// 돌아가면 방금 그 손님이 고쳐진 정도에 맞는 반응 대사를 말하고(낮 손님의 화난 대사와 같은 방식), 그 뒤에 다음 손님이 온다. 다음 손님이 없으면 반응 대사 뒤에 밤이 끝난다.</summary>
    public void FinishNightDiagnosis(IReadOnlyCollection<FlowerData.Ailment> picked)
    {
        nightPuzzleInProgress = false;
        currentNightVisitDone = true;
        if (nightFree && CurrentNightCase != null && freeNightTotal > 0)
        {
            GameClock.Instance?.SetNightRatio((float)scriptedNightVisits.Count(v => !v.dialogueOnly) / freeNightTotal); // 남은 수리 손님 비율
        }
        if (CurrentNightCase == null)
        {
            NextNightCustomer();
            return;
        }

        NightDiagnosisCase finished = CurrentNightCase;
        NightVisit? visit = CurrentNightVisit;
        finished.Compare(picked, out int missed, out int wrong);
        int fixedCount = finished.CorrectCount(picked);
        int replicas = visit.HasValue && visit.Value.fixedReplicas > 0 ? visit.Value.fixedReplicas : finished.ReplicaCount(picked); // 시나리오가 확정 지급 수를 정했으면 그대로
        Debug.Log($"[GameManager] 밤 진단 결과: {finished.flower.DisplayName} / 정답 {string.Join(", ", finished.ailments.Select(NightDiagnosisUI.AilmentName))} / 체크 {string.Join(", ", picked.Select(NightDiagnosisUI.AilmentName))} → 놓친 {missed}, 틀린 {wrong}, 복제품 {replicas}개");
        if (replicas > 0 && CurrencyManager.Instance != null) CurrencyManager.Instance.AddFlowerStock(finished.flower.flowerID, replicas);
        pendingNotice = replicas > 0 ? $"{finished.flower.DisplayName} {NoticeToast.Highlight($"X{replicas}")}" : null; // 메인 화면이 열려 반응 대사를 할 때 획득 알림을 띄운다

        string reaction = ScriptedNightReaction(visit, fixedCount, finished.ailments.Count)
            ?? (fixedCount == 0 ? nightReactionNone : fixedCount == finished.ailments.Count ? nightReactionAll : nightReactionPartial);
        pendingCustomerLine = reaction.Replace("{flower}", finished.flower.DisplayName).Replace("{count}", replicas.ToString());
        PreviousNpc = visit.HasValue ? visit.Value.npc : null;
        pendingRejectResponseLine = replicas > 0 ? nightRejectWithReward : nightRejectNoReward;

        if (nightTimeUpPending) // 퍼즐을 하는 중에 밤 시간이 끝났다: 이 손님의 반응 대사까지 하고 밤을 끝낸다(다음 손님은 오지 않는다)
        {
            nightTimeUpPending = false;
            CarryOverNightVisits();
            CurrentNightCase = null;
        }
        else PrepareNextNightCase(); // 반응 대사 뒤에 올 손님(없으면 CurrentNightCase가 null이 되어 반응 대사 뒤에 밤이 끝난다)
        MainState = MainState.NightCustomer;
        SceneTransition.Go(SceneDayMain);
    }

    /// <summary>시나리오가 정한 NPC의 반응 대사(CSV의 `접두어_success/mistake/fail`). 모두 고침 = success, 일부 = mistake, 하나도 못 고침 = fail. 정해진 대사가 없으면 null(평소 반응 대사).</summary>
    private static string ScriptedNightReaction(NightVisit? visit, int fixedCount, int total)
    {
        if (!visit.HasValue) return null;

        return ScriptedReaction(visit.Value.reactionPrefix, fixedCount == 0 ? "fail" : fixedCount == total ? "success" : "mistake");
    }

    /// <summary>CSV의 `접두어_단계` 대화의 대사들. 접두어가 없거나 그 대화가 없으면 null(평소 반응 대사).</summary>
    private static string ScriptedReaction(string prefix, string tier)
    {
        if (string.IsNullOrEmpty(prefix) || ScenarioManager.Instance == null) return null;

        Dialogue dialogue = ScenarioManager.Instance.Dialogue.Get($"{prefix}_{tier}");
        return dialogue != null && dialogue.lines.Count > 0 ? string.Join("\n", dialogue.lines.Select(l => l.text)) : null;
    }

    /// <summary>다음 밤 손님을 만나러 메인 화면으로 간다. 만날 손님이 더 없으면 밤을 끝낸다.</summary>
    public void NextNightCustomer()
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

    /// <summary>밤 시간이 끝났을 때: 메인 화면에서 마지막 손님이 외치고 떠난다(밤 종료).
    /// 단, 밤 퍼즐을 하는 중이면(낮 퍼즐처럼) 그 퍼즐을 마치고 반응 대사까지 한 뒤에 밤이 끝난다(FinishNightDiagnosis).</summary>
    private void HandleNightTimeUp()
    {
        if (nightPuzzleInProgress)
        {
            nightTimeUpPending = true;
            return;
        }

        MainState = MainState.NightEnding;

        if (SceneManager.GetActiveScene().name == SceneDayMain) return; // 이미 메인 화면이면(밤 손님을 만나는 중) 그 자리의 MainUI가 같은 이벤트를 받아 바로 처리한다

        SceneTransition.Go(SceneDayMain);
    }

    private bool morningArrival; // 밤이 지나 새 날 낮이 열리는 중(메인 화면이 밤 → 낮으로 섞는다)

    /// <summary>새 날 낮 첫 메인 화면이 열릴 때 한 번 true(밤 → 낮 연출을 하라는 뜻). 꺼내 쓰면 지운다.</summary>
    public bool ConsumeMorningArrival()
    {
        bool arriving = morningArrival;
        morningArrival = false;
        return arriving;
    }

    /// <summary>밤이 끝나면(마지막 손님이 사라지면) 하루 결산 화면으로 간다. 밤 시간이 없는 날(1일차)은 결산 없이 바로 다음 날 낮으로 간다.
    /// 못 만난 정해진 손님(새 꽃)은 다음 밤 맨 앞으로 넘어간다.</summary>
    public void EndNight()
    {
        CarryOverNightVisits();
        if (nightFree)
        {
            StartNextDay();
            return;
        }
        SceneTransition.Go(SceneResult);
    }

    /// <summary>못 만난 정해진 방문(지금 만나던 중 마치지 못한 것과 줄 선 것)을 다음 밤으로 넘긴다.</summary>
    private void CarryOverNightVisits()
    {
        if (CurrentNightVisit.HasValue && !currentNightVisitDone) carriedNightVisits.Add(CurrentNightVisit.Value);
        carriedNightVisits.AddRange(scriptedNightVisits);
        scriptedNightVisits.Clear();
        CurrentNightVisit = null;
    }

    /// <summary>디버그용: 하루를 건너뛴다. 그날 밤에 새로 얻었을 정해진 꽃(아직 못 얻은 것만)은 복제품을, 그날 낮 대사의 포장지 선물은 재고를 지급하고, 다음 날 낮을 첫 방문자부터 연출과 함께 시작한다.</summary>
    public void DebugAdvanceDay()
    {
        DayScenarioData scenario = ScenarioManager.Instance != null ? ScenarioManager.Instance.ForDay(CurrencyManager.Instance.CurrentDay) : null;
        IEnumerable<NightVisit> skipped = carriedNightVisits.Concat(scriptedNightVisits).Concat(scenario != null ? scenario.nightVisits : System.Array.Empty<NightVisit>());
        foreach (NightVisit visit in skipped)
        {
            if (visit.flower == null || visit.fixedReplicas <= 0 || CurrencyManager.Instance.HasObtainedFlower(visit.flower.flowerID)) continue;
            CurrencyManager.Instance.AddFlowerStock(visit.flower.flowerID, visit.fixedReplicas);
        }

        // 건너뛴 낮 대사에서 받았을 포장지 선물: 다음 날부터 쓸 수 있게 지급된다(이미 받았어도 한 번 더 지급되는 것은 디버그라 그냥 둔다)
        if (scenario != null)
        {
            foreach (DayVisit visit in scenario.dayVisits)
            {
                Dialogue dialogue = ScenarioManager.Instance.Dialogue.Get(visit.dialogueId);
                if (dialogue != null) foreach (DialogueEffect gift in dialogue.EffectsOf(DialogueEffect.Gift)) ScenarioEffects.Apply(gift);
            }
        }

        carriedNightVisits.Clear();
        scriptedNightVisits.Clear();
        CurrentNightVisit = null;
        StartNextDay();
    }

    /// <summary>결산 화면에서 다음으로: 날짜를 넘기고 다음 날 낮을 시작한다.</summary>
    public void StartNextDay()
    {
        CurrencyManager.Instance.AdvanceDay();
        DayVisitIndex = 0; // 새 날은 방문자를 처음부터 센다
        morningArrival = true;
        BeginNewDay();
    }
}
