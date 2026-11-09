using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 영업(낮 → 노을 → 밤)과 하루 결산 코어 루프 전체를 조율하는 게임 매니저(씬 흐름 담당).
/// 부트스트랩 씬(00Start)에 배치되어 씬 전환 간 유지된다. UI를 직접 찾아 부르지 않는다. 오늘의 주문, 가챠 풀은 UI가 필요할 때 읽어 간다.
///
/// 손님을 만나는 메인 화면은 씬 하나(01DayMain)이다. 하루 시간이 다 되면(주문을 하던 중이면 그 주문을 마무리한 뒤) 하루 결산(03Result)으로 간다.
/// 씬 이름 상수는 Assets/01Scenes 폴더 구조를 그대로 따른다.
/// </summary>
public partial class GameManager : Singleton<GameManager>
{
    public const string SceneDayMain = "01DayMain";
    public const string SceneDayPuzzle = "03DayPuzzle";
    public const string SceneResult = "03Result";

    [Header("손님 주문 대사의 템플릿과 확률(직설형/은유형 비율, 특수주문 확률 포함)")]
    [SerializeField] private OrderTemplateData orderTemplates;

    public DayPuzzleGenerator.DayOrder CurrentDayOrder { get; private set; }
    public List<FlowerData> ChosenGachaPool { get; private set; } = new(); // 꽃 선정에서 칸마다 채운 꽃(송이 수만큼 같은 꽃이 반복된다)

    /// <summary>지금 주문을 한 낮 손님의 모습. 퍼즐을 마치고 메인 화면이 다시 열려도 반응 대사를 같은 손님이 하도록 기억해 둔다(메인 화면은 열릴 때마다 새로 만들어진다).</summary>
    public CharacterAppearance? CustomerLook { get; set; }
    public bool CustomerUnhappy { get; set; } // 방금 주문을 한 손님이 결과(실수·실패)에 기분이 나빠졌다. 새 손님이 오면 MainUI가 끈다

    /// <summary>방금 주문을 한 정해진 NPC 손님(반응 대사를 할 때 그 모습을 보인다). 아니면 null.</summary>
    public NpcData PreviousNpc { get; private set; }

    /// <summary>방금 주문을 한 정해진 낮 손님의 아웃트로 대사 ID(꽃다발 반응 대사가 끝난 뒤에 한다). 없으면 null. 꺼내 쓰면 지워진다(ConsumeOutroDialogueId).</summary>
    public string PreviousOutroDialogueId { get; private set; }

    public string ConsumeOutroDialogueId()
    {
        string id = PreviousOutroDialogueId;
        PreviousOutroDialogueId = null;
        return id;
    }

    /// <summary>오늘 지금 만나는 방문자가 몇 번째인가(1부터. 방문자가 올 때마다 하나씩 늘고, 날이 바뀌면 0부터 다시 센다).</summary>
    public int DayVisitIndex { get; private set; }

    /// <summary>지금 방문자가 시나리오에 정해진 NPC면 그 방문, 아니면 null(무작위 손님).</summary>
    public DayVisit? CurrentScriptedVisit { get; private set; }

    private const string DefaultRejectResponseLine = CustomerSpeech.DefaultForceAcceptLine;

    private int reservationCallDay;  // 예약 전화를 이미 건 일차(하루에 한 번만 건다)
    private int pickupDay;           // 예약자를 이미 만난 일차(하루에 한 번만 온다)
    private readonly List<(int day, ReservationData data)> delayedReservationCalls = new(); // 예약을 못 지켜서 늦춰진 예약 전화(걸려 올 일차, 예약)

    private string pendingCustomerLine;
    private string pendingRejectResponseLine = DefaultRejectResponseLine;
    private bool orderInProgress; // 주문을 수락해 꽃 선택/퍼즐을 하는 중
    private bool dayEndPending; // 주문을 하는 중에 하루 시간이 끝나, 그 주문이 끝나면 하루 결산으로 가야 한다

    protected override void OnAwake()
    {
        EventBus.OnDayTimeUp += HandleDayTimeUp;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        EventBus.OnDayTimeUp -= HandleDayTimeUp;
    }

    // ========== 영업 ==========

    /// <summary>시작 화면(00Start)에서 터치했다: 로딩이 기니 로딩 씬을 거쳐 메인으로 간다. 첫 손님은 로딩 씬에서 정한다.</summary>
    public void StartGame()
    {
        dayIntroPending = true;
        SceneLoader.Load(SceneDayMain, StartGameWork());
    }

    private IEnumerator<float> StartGameWork()
    {
        yield return 0f;
        GrantStartingFlowers();
        GrantStartingDecorations();
        ApplyDailyProgression(CurrencyManager.Instance.CurrentDay);
        PrepareNewDay();
        yield return 1f;
    }

    public void BeginNewDay()
    {
        if (TimeIsUp && !HasPendingEvent) // 하루 시간이 끝났고 남은 이벤트도 없다: 다음 손님 없이 하루를 마친다
        {
            EndDayNow();
            return;
        }

        PrepareNewDay();
        SceneTransition.Go(SceneDayMain);
    }

    /// <summary>하루 시간이 이미 끝났는가(예약 꽃다발을 만드는 중이 아닐 때). 끝난 뒤에도 안 일어난 중간 이벤트는 차례로 일어난 다음 하루가 끝난다.</summary>
    public bool TimeIsUp => Craft == null && GameClock.Instance != null && GameClock.Instance.DayRemaining <= 0f;

    /// <summary>아직 안 일어난 중간 이벤트(지금 손님 뒤에 오기로 한 정해진 방문, 오늘 걸려 올 예약 전화)가 남았는가.</summary>
    public bool HasPendingEvent => NextPendingVisitIndex(DayVisitIndex + 1) > 0 || HasPendingReservationCall;

    private bool HasPendingReservationCall
    {
        get
        {
            int day = CurrencyManager.Instance.CurrentDay;
            if (reservationCallDay == day) return false;

            DayScenarioData scenario = ScenarioManager.Instance != null ? ScenarioManager.Instance.ForDay(day) : null;
            return (scenario != null && scenario.reservationCall != null) || delayedReservationCalls.Any(c => c.day == day);
        }
    }

    /// <summary>오늘 시나리오에서 순번이 from 이상인 정해진 방문 중 가장 앞선 순번. 없으면 0.</summary>
    private int NextPendingVisitIndex(int from)
    {
        DayScenarioData scenario = ScenarioManager.Instance != null ? ScenarioManager.Instance.ForDay(CurrencyManager.Instance.CurrentDay) : null;
        if (scenario == null) return 0;

        int next = 0;
        foreach (DayVisit visit in scenario.dayVisits)
        {
            if (visit.orderIndex >= from && (next == 0 || visit.orderIndex < next)) next = visit.orderIndex;
        }
        return next;
    }

    /// <summary>더 일어날 일 없이 하루를 마친다(밤 상점으로).</summary>
    public void EndDayNow()
    {
        orderInProgress = false;
        OpenShop();
    }

    /// <summary>새 손님(오늘의 주문)을 정하고 메인 화면이 손님을 보여 주도록 해 둔다.</summary>
    private void PrepareNewDay()
    {
        dayEndPending = false;
        orderInProgress = false;
        PreviousNpc = CurrentScriptedVisit.HasValue ? CurrentScriptedVisit.Value.npc : null; // 방금 주문을 한 손님(반응 대사 때 그 모습을 보인다)
        PreviousOutroDialogueId = CurrentScriptedVisit.HasValue ? CurrentScriptedVisit.Value.outroDialogueId : null;

        int day = CurrencyManager.Instance.CurrentDay;
        DayVisitIndex++;
        if (TimeIsUp)
        {
            int pending = NextPendingVisitIndex(DayVisitIndex);
            if (pending > 0) DayVisitIndex = pending; // 하루 시간이 끝났으면 무작위 손님은 건너뛰고 남은 정해진 방문으로 바로 간다
        }
        CurrentScriptedVisit = ScenarioManager.Instance != null ? ScenarioManager.Instance.DayVisitAt(day, DayVisitIndex) : null;

        CurrentDayOrder = ScriptedOrder(CurrentScriptedVisit, day) ?? DayPuzzleGenerator.GenerateOrder(day, orderTemplates);
        CurrencyManager.Instance.SetCurrentRequirement(CurrentDayOrder.requirement);
    }

    /// <summary>손님이 떠나고 다음 손님이 오기 전, 지금 예약 전화가 걸려 올 차례인가: 오늘 시나리오에 예약 전화가 있고, 시나리오가 정한 수(reservationCallAfterCustomers)만큼 손님을 마쳤고, 오늘 아직 안 걸었으면 그 예약을 돌려준다(그러면 오늘은 한 번 건 것으로 친다). 아니면 null.</summary>
    public ReservationData TakeReservationCall()
    {
        int day = CurrencyManager.Instance.CurrentDay;
        if (reservationCallDay == day) return null;

        DayScenarioData scenario = ScenarioManager.Instance != null ? ScenarioManager.Instance.ForDay(day) : null;
        ReservationData call = scenario != null ? scenario.reservationCall : null;
        int after = scenario != null ? scenario.reservationCallAfterCustomers : 1;
        if (call == null) // 시나리오에 없으면, 예약일에 준비를 못 해서 늦춰진 전화가 오늘 오기로 되어 있는지 본다
        {
            int delayed = delayedReservationCalls.FindIndex(c => c.day == day);
            if (delayed < 0) return null;

            call = delayedReservationCalls[delayed].data;
            if (!TimeIsUp && DayVisitIndex - 1 < after) return null; // 하루 시간이 끝났으면 손님 수와 상관없이 걸려 온다

            delayedReservationCalls.RemoveAt(delayed);
            reservationCallDay = day;
            return call;
        }

        if (!TimeIsUp && DayVisitIndex - 1 < after) return null; // DayVisitIndex는 이미 다음 손님을 가리키므로, 마친 손님 수는 하나 적다(하루 시간이 끝났으면 상관없이 건다)

        reservationCallDay = day;
        return call;
    }

    /// <summary>예약을 못 지켜서, 같은 예약 주문 전화가 days일 뒤에 다시 걸려 오게 한다.</summary>
    public void DelayReservationCall(ReservationData data, int days)
    {
        delayedReservationCalls.Add((CurrencyManager.Instance.CurrentDay + days, data));
    }

    /// <summary>오늘이 예약일인데 아직 예약자를 만나지 않았으면 그 예약을 돌려주고(오늘은 한 번 만난 것으로 친다), 아니면 null. 예약자는 하루의 첫 손님으로 온다.</summary>
    public ReservationRecord TakePickupReservation()
    {
        int day = CurrencyManager.Instance.CurrentDay;
        if (pickupDay == day) return null;

        ReservationRecord due = CurrencyManager.Instance.Reservations.FirstOrDefault(r => r.DueDay == day && !r.pickedUp);
        if (due == null) return null;

        pickupDay = day;
        return due;
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
            else flowers.Add((ObtainableFlower(flower), effect.amount));
        }
        if (flowers.Count == 0) return null;

        // 대체하면 같은 꽃이 두 번 나올 수 있어 합친다(예: 카네이션 1 + 장미 1 → 장미 2)
        flowers = flowers.GroupBy(f => f.flower).Select(g => (g.Key, g.Sum(f => f.count))).ToList();
        return DayPuzzleGenerator.GenerateFixedOrder(day, flowers, wrapperId, orderLine.text);
    }

    /// <summary>아직 얻지 못한 꽃은 주문할 수 없다: 같은 역할의 시작 꽃(획득 일차 0)으로 바꾼다(안개꽃 → 소국, 카네이션 → 장미). 바꿀 꽃이 없으면 그대로 둔다.</summary>
    private static FlowerData ObtainableFlower(FlowerData flower)
    {
        CurrencyManager wallet = CurrencyManager.Instance;
        if (wallet == null || wallet.HasObtainedFlower(flower.flowerID)) return flower;

        return DataRegistry.Instance.Flowers.FirstOrDefault(f => f != null && f.acquireDay <= 0 && f.flowerRole == flower.flowerRole) ?? flower;
    }

    /// <summary>플레이어가 포장지를 골랐을 때(꽃 선택 화면 상단). 포장지 단계의 태그 요구 레벨을 확정한다.</summary>
    public void ChooseWrapper(int wrapperId)
    {
        DayPuzzleGenerator.FinalizeForWrapper(CurrentDayOrder, wrapperId);
    }

    /// <summary>손님의 주문을 수락했다: 이때부터 퍼즐을 마칠 때까지가 한 주문이라, 그사이 하루 시간이 끝나도 주문을 마무리한 뒤에 하루 결산으로 간다.
    /// 꽃 선택 화면은 메인 씬 옆 페이지라서 씬을 바꾸지 않고, 플레이어가 스와이프해서 간다.</summary>
    public void AcceptOrder()
    {
        orderInProgress = true;
    }

    public void ConfirmFlowerSelection(List<FlowerData> chosen)
    {
        ChosenGachaPool = new List<FlowerData>(chosen);
        bouquetInProgress = Crafting; // 예약 꽃다발을 만드는 중에는 이 퍼즐을 마칠 때까지 하루 시간이 끝나도 기다린다
        SceneTransition.Go(SceneDayPuzzle);
    }

    /// <summary>낮 퍼즐이 끝나 다음 손님이 있는 메인으로 돌아간다(성공이든 손님이 화났든). 돌아가면 손님이 customerLine을 첫 대사로 말한다.
    /// rejectResponseLine은 "이건아니지"를 눌렀을 때 나오는 대사(기본값: "그래요라고 하세요").</summary>
    public void ReturnToDayMain(string customerLine, string rejectResponseLine = DefaultRejectResponseLine, ReactionTier tier = ReactionTier.Success)
    {
        bool timeUp = dayEndPending || TimeIsUp; // 주문을 하는 동안 하루 시간이 끝났다(이벤트를 앞당겨 하는 중이면 이미 끝나 있다)
        dayEndPending = false;
        if (timeUp && !HasPendingEvent && !CurrentScriptedVisit.HasValue) // 이 주문은 마무리했고 남은 이벤트도 없으니 이제 하루 결산으로 간다(정해진 NPC였다면 반응 대사와 아웃트로까지 하고 마친다)
        {
            EndDayNow();
            return;
        }

        CustomerUnhappy = tier == ReactionTier.Mistake || tier == ReactionTier.Fail;

        // 시나리오에 정해진 NPC(아직 다음 방문자로 넘어가기 전이라 CurrentScriptedVisit이 방금 주문한 손님이다)는 CSV의 반응 대사를 하고, 아니면 평소 반응 대사를 한다
        pendingCustomerLine = ScriptedReaction(CurrentScriptedVisit?.reactionPrefix, tier.ToString().ToLowerInvariant()) ?? customerLine;
        pendingRejectResponseLine = rejectResponseLine;
        PrepareNewDay(); // BeginNewDay가 아니라 직접 연다: 하루 시간이 끝났어도 방금 주문한 NPC의 반응 대사부터 하고, 끝나면 MainUI가 하루를 마친다
        SceneTransition.Go(SceneDayMain);
    }

    /// <summary>메인 진입 시 반응 대사가 예약되어 있으면 한 번 꺼내 쓰고 지운다.</summary>
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

    /// <summary>하루 시간이 끝나면 하루 결산으로 간다.
    /// 단, 주문을 수락해 꽃 선택/퍼즐을 하는 중(AcceptOrder 이후)이면 그 주문을 마무리한 뒤에 넘어간다(ReturnToDayMain).</summary>
    private void HandleDayTimeUp()
    {
        if (Craft != null)
        {
            HandleCraftTimeUp(); // 예약일: 결산이 아니라 예약자의 반응으로 간다
            return;
        }
        if (orderInProgress)
        {
            dayEndPending = true;
            return;
        }
        BeginDayEnding();
    }

    /// <summary>하루 시간이 끝났다: 아직 안 일어난 중간 이벤트가 있으면 그것부터 하고(다음 손님으로 앞당긴다), 없으면 밤 상점이 열린다(상점을 마치면 결산).</summary>
    private void BeginDayEnding()
    {
        if (TimeIsUp && HasPendingEvent) BeginNewDay(); // 남은 이벤트를 다음 손님으로 앞당긴다
        else EndDayNow(); // 예약 반응 뒤처럼 시계가 아직 0이 아니어도 여기서는 하루를 마친다
    }

    /// <summary>낮 퍼즐이 끝났을 때: 쓴 포장지(소모품) 한 장을 소모한다(재고는 0 아래로 내려가지 않는다).</summary>
    public void ConsumeUsedWrapper()
    {
        if (Crafting && Craft.WrapperId > 0) // 예약 꽃다발은 보관해 둔 포장지에서 한 장 쓴다
        {
            Craft.UseWrapper(1);
            return;
        }
        if (CurrencyManager.WrappersUnlimited) return; // 포장지 개수를 따지지 않는 동안은 줄지 않는다

        WrapperData wrapper = CurrentDayOrder != null ? CurrentDayOrder.wrapper : null;
        if (wrapper != null) CurrencyManager.Instance.AddWrapperStock(wrapper.wrapperId, -1);
    }

    /// <summary>예약 꽃다발을 마쳤을 때: 꽃다발에 쓴 필수 꾸미기 용품마다 보관해 둔 수에서 하나씩 뺀다(안 쓴 용품은 줄지 않는다).</summary>
    public void ConsumeUsedSupplies(DecorationPlacer placer)
    {
        if (!Crafting || placer == null) return;

        foreach (DecorationData supply in Craft.Supplies())
        {
            if (placer.IsUsed(supply)) Craft.UseSupply(supply.id, 1);
        }
    }

    /// <summary>낮 퍼즐이 끝났을 때: 실제로 칸에 놓은 꽃 한 송이마다 보유 수량을 1씩 소모한다(한 번만).</summary>
    public void ConsumeUsedFlowers(IEnumerable<FlowerData> used)
    {
        foreach (FlowerData flower in used)
        {
            if (Crafting && Craft.IsRequired(flower.flowerID)) Craft.Use(flower.flowerID, 1); // 예약 꽃다발의 필수 꽃은 보관해 둔 것에서 빠진다
            else CurrencyManager.Instance.AddFlowerStock(flower.flowerID, -1);
        }
        ChosenGachaPool = new List<FlowerData>();
    }

    /// <summary>CSV의 `접두어_단계` 대화의 대사들. 접두어가 없거나 그 대화가 없으면 null(평소 반응 대사).</summary>
    private static string ScriptedReaction(string prefix, string tier)
    {
        if (string.IsNullOrEmpty(prefix) || ScenarioManager.Instance == null) return null;

        Dialogue dialogue = ScenarioManager.Instance.Dialogue.Get($"{prefix}_{tier}");
        return dialogue != null && dialogue.lines.Count > 0 ? string.Join("\n", dialogue.lines.Select(l => l.text)) : null;
    }

    private bool debugSkipPending; // 디버그: 낮 퍼즐 씬이 열리면 바로 강제 제출한다

    /// <summary>
    /// 디버그용 낮 퍼즐 스킵. 퍼즐 씬이면 지금 놓인 꽃으로 강제 제출한다. 주문을 수락한 뒤(꽃 선택 화면 포함)에 메인 화면에서 누르면, 포장지를 아직 안 골랐으면 손님이 말한 포장지로 정하고
    /// 꽃을 고르지 않은 채 퍼즐 씬으로 넘어가 열리자마자 강제 제출한다. 주문을 수락하기 전에는 퍼즐이 없으니 아무 일도 없다.
    /// </summary>
    public void DebugSkipDayPuzzle()
    {
        if (SceneTransition.Busy) return;

        string scene = SceneManager.GetActiveScene().name;
        if (scene == SceneDayPuzzle)
        {
            FindFirstObjectByType<DayPuzzleUI>()?.DebugForceSubmit();
            return;
        }

        bool accepted = orderInProgress || Crafting;
        if (scene != SceneDayMain || !accepted || CurrentDayOrder == null) return;

        if (CurrentDayOrder.preset == null) ChooseWrapper(CurrentDayOrder.hintWrapperId); // 포장지를 아직 안 골랐으면 손님이 말한 포장지로
        debugSkipPending = true;
        ConfirmFlowerSelection(new List<FlowerData>());
    }

    /// <summary>낮 퍼즐 씬이 열렸을 때 한 번: 디버그 스킵으로 넘어온 것이면 true(바로 강제 제출한다).</summary>
    public bool ConsumeDebugSkip()
    {
        bool skip = debugSkipPending;
        debugSkipPending = false;
        return skip;
    }

    private bool morningArrival; // 하루가 끝나 새 날이 열리는 중(메인 화면이 밤 → 낮으로 섞는다)

    private bool dayIntroPending; // 하루가 막 시작돼 시계에 "N년차 계절"을 보여야 한다

    /// <summary>하루가 막 시작됐으면 한 번 true(시계의 DayLabel이 "N년차 계절"을 잠깐 보인다). 꺼내 쓰면 지운다.</summary>
    public bool ConsumeDayIntro()
    {
        bool pending = dayIntroPending;
        dayIntroPending = false;
        return pending;
    }

    /// <summary>새 날 첫 메인 화면이 열릴 때 한 번 true(밤 → 낮 연출을 하라는 뜻). 꺼내 쓰면 지운다.</summary>
    public bool ConsumeMorningArrival()
    {
        bool arriving = morningArrival;
        morningArrival = false;
        return arriving;
    }

    /// <summary>디버그용: 하루를 건너뛴다. 그날 낮 대사에서 받았을 포장지 선물은 재고를 지급하고, 다음 날을 첫 방문자부터 연출과 함께 시작한다.</summary>
    public void DebugAdvanceDay()
    {
        DayScenarioData scenario = ScenarioManager.Instance != null ? ScenarioManager.Instance.ForDay(CurrencyManager.Instance.CurrentDay) : null;
        if (scenario != null)
        {
            foreach (DayVisit visit in scenario.dayVisits)
            {
                Dialogue dialogue = ScenarioManager.Instance.Dialogue.Get(visit.dialogueId);
                if (dialogue != null) foreach (DialogueEffect gift in dialogue.EffectsOf(DialogueEffect.Gift)) ScenarioEffects.Apply(gift); // 이미 받았어도 한 번 더 지급되는 것은 디버그라 그냥 둔다
            }
        }
        StartNextDay();
    }

    /// <summary>결산 화면에서 다음으로: 날짜를 넘기고 다음 날을 시작한다.</summary>
    public void StartNextDay()
    {
        CurrencyManager.Instance.AdvanceDay();
        ApplyDailyProgression(CurrencyManager.Instance.CurrentDay);
        DayVisitIndex = 0; // 새 날은 방문자를 처음부터 센다
        morningArrival = true;
        dayIntroPending = true;
        BeginNewDay();
    }
}
