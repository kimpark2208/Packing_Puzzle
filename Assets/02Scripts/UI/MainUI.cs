using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인(손님 주문) 화면. 낮 손님/낮 종료/밤 손님/밤 종료를 이 씬 하나에서 보여 준다(MainState).
/// 손님 캐릭터 자리 + 말풍선(주문 힌트) + 응답 버튼 2개("다시 여쭤볼게요" / "포장 시작할게요") + 설정 팝업 + 카탈로그 팝업(전화 버튼으로 열림).
/// 낮 손님의 주문을 수락하면 응답 버튼이 사라지고, 잠시 뒤 옆 페이지의 꽃 선택(FlowerSelectUI)으로 자동으로 넘어간다(한 번 가면 돌아올 수 없다).
/// UI 요소는 전부 씬 하이라키에 미리 배치되어 있고, 이 스크립트는 인스펙터 참조 없이 이름(SceneObjectNames)으로 찾아서(SceneFind) 로직을 수행한다.
///
/// 어떤 상황인지는 GameManager가 씬을 열기 전에 정해 두므로(MainState, 오늘의 주문 CurrentDayOrder) Start()에서 스스로 읽어 온다.
/// 화면 모습은 같은 오브젝트의 MainLook이 바꾼다(낮 손님은 낮, 낮 종료는 노을, 밤 손님과 밤 종료는 밤).
/// 밤 손님을 만나는 중에 밤 시간이 끝나면 EventBus.OnNightTimeUp을 직접 받아 그 자리에서 밤 종료로 넘어간다(GameManager가 이 UI를 찾아 부르지 않는다).
/// </summary>
public class MainUI : MonoBehaviour
{
    [SerializeField] private float timeUpHideDelay = 5f;
    [SerializeField] private float lanternDelay = 0.5f; // 전등을 켠 뒤 밤 요청 화면으로 넘어가기까지의 시간
    [SerializeField, Min(0f), Tooltip("이전 손님이 나간 뒤 새 손님이 들어오기까지 쉬는 시간(초).")] private float customerSwapDelay = 0.3f;
    [SerializeField, Tooltip("주문을 수락한 뒤 꽃 선택으로 저절로 넘어갈 때(스와이프 시작) 나는 소리.")] private AudioClip forcedSwipeSound;
    [SerializeField, Range(0f, 1f)] private float forcedSwipeVolume = 0.8f;
    [SerializeField] private float acceptToSelectDelay = 1f; // 낮 주문을 수락한 뒤 꽃 선택 화면으로 자동으로 넘어가기까지의 시간(초)
    [SerializeField, Min(0.1f), Tooltip("시나리오가 낮을 건너뛸 때(1일차)와 예약자의 반응이 끝난 뒤 남은 시간이 빨리 감기는 시간(초).")] private float timeSkipSeconds = 3f;
    [SerializeField, Min(0f), Tooltip("예약 꽃다발을 만들 때 메인 화면이 열리고 포장지 선택 페이지로 넘어가기까지의 시간(초).")] private float craftSwipeDelay = 0.4f;
    [SerializeField, Min(0f), Tooltip("낮을 건너뛸 때 전등이 나타난 뒤 저절로 켜지기까지 시간(초).")] private float lanternLightDelay = 0.3f;

    [Header("소리")]
    [SerializeField, Tooltip("낮 손님 동안 나오는 배경음악(낮 퍼즐 씬과 같은 곡이면 이어서 나온다).")] private AudioClip dayBgm;
    [SerializeField, Tooltip("낮 종료(부적 화면) 동안 나오는 배경음악.")] private AudioClip dayEndBgm;
    [SerializeField, Tooltip("밤 동안 나오는 배경음악(밤 퍼즐 씬과 같은 곡이면 이어서 나온다).")] private AudioClip nightBgm;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.4f;
    [SerializeField, Tooltip("전등을 켜면 나오는 소리. 이 소리가 끝난 뒤에 밤이 시작된다.")] private AudioClip nightStartJingle;
    [SerializeField, Range(0f, 1f)] private float nightStartJingleVolume = 0.7f;

    private MainState state;
    private DayPuzzleGenerator.DayOrder order;
    private bool showingReaction; // 이전 손님의 반응 대사 중(낮 손님의 화난 대사, 밤 손님의 수리 반응). 거절은 거절 대사, 수락은 손님이 나가고 다음 손님이 온다
    private string forceAcceptLine = CustomerSpeech.DefaultForceAcceptLine;
    private CustomerSpeech speech; // 대사 출력은 이쪽에 맡기고, 여기서는 어떤 상황인지만 알린다
    private DialoguePlayer dialoguePlayer; // 시나리오에 정해진 NPC 방문의 대사(CSV)를 말풍선에 재생한다
    private bool leaving; // 마무리 대사를 하고 퇴장하는 중(낮 종료/밤 종료)

    private GameObject catalogPopup;
    private GameObject calendarPopup; // 예약확인(달력) 화면
    private GameObject telephoneBar; // 전화기를 누르면 뜨는 메뉴 바(안에 상품주문 버튼이 카탈로그를 연다)
    private bool phoneUnlocked = true; // 전화기를 쓸 수 있는가(시나리오의 해금 일차부터)
    private GameObject bubbleContainer;
    private GameObject[] characterRoots;
    private CharacterGenerator character; // 낮 손님의 모습(주문마다 무작위로 바뀐다)
    private Toggle lantern; // 켜면 밤이 시작된다(낮 종료 손님이 떠난 뒤, 카운터 화면에서만 켤 수 있다)
    private LanternLight lanternLight; // 전등의 켜짐/꺼짐 모습(켜진 전등 그림 + 불빛)
    private SwipeParallax swipe;     // 카운터 ↔ 옆 페이지 이동(낮 손님: 수락하면 꽃 선택으로 자동으로 한 방향, 낮 종료: 꽃 선반과 스와이프로 왕복)
    private NightRequestUI shelf;    // 꽃 선반: 부적을 붙인 꽃을 들고 있다(낮 종료)
    private GameObject rejectButton;
    private GameObject acceptButton;
    private bool customerGone;      // 낮 종료 손님이 떠났다(그제서야 스와이프와 등불이 열린다)
    private bool nightStarting;      // 등불을 켜서 밤으로 넘어가는 중
    private bool hidingCustomer;     // 손님이 사라지는 중(한 번만 처리한다)
    private MainLook mainLook;       // 화면 모습(낮/노을/밤)과 섞기
    private bool npcPaused;          // NPC와 대화하느라 시간을 멈춰 두었는가(PauseForNpc)
    private bool changingCustomer;   // 손님이 바뀌는 중(말풍선 -> 나가는 모션 -> 들어오는 모션 -> 말풍선). 그동안은 응답 버튼을 무시한다

    private static GameManager Game => GameManager.Instance;

    private bool IsNight => state == MainState.NightCustomer || state == MainState.NightEnding;

    private void OnEnable()
    {
        EventBus.OnNightTimeUp += OnNightTimeUp;
    }

    private void OnDisable()
    {
        EventBus.OnNightTimeUp -= OnNightTimeUp;
        if (swipe != null) swipe.Arrived -= OnSwipeArrived;
        PauseForNpc(false);
    }

    /// <summary>NPC와 대화하는 동안(들어온 뒤 대사, 주문을 수락하기 전, 꽃다발 반응 대사와 친밀 대사) 하루/밤 시간과 손님 기분 시간이 가지 않게 한다. 켜진 채 다시 켜도 한 번만 센다.</summary>
    private void PauseForNpc(bool on)
    {
        if (on == npcPaused) return;

        npcPaused = on;
        GameClock.Instance?.SetPaused(on);
    }

    private void Start()
    {
        state = Game != null ? Game.MainState : MainState.DayCustomer;
        order = Game != null ? Game.CurrentDayOrder : null;
        Initialize();
    }

    private void Initialize()
    {
        FindSceneObjects();

        mainLook = GetComponent<MainLook>();
        mainLook?.Apply(state);
        bool morning = state == MainState.DayCustomer && Game != null && Game.ConsumeMorningArrival(); // 밤이 지나 새 날이 열렸다
        if (morning) mainLook?.PlayMorningBlend(); // 비워진 밤 시계에 낮 시계가 채워지며 배경이 밤 → 낮으로 섞인다
        Bgm.Play(state == MainState.DayCustomer ? dayBgm : state == MainState.DayEnding ? dayEndBgm : nightBgm, bgmVolume);

        SetUpSidePage();
        SetUpLantern();
        SetUpCatalog();
        if (IsNight) FindFirstObjectByType<OrderPopup>(FindObjectsInactive.Include)?.SetTabVisible(false); // 주문서는 낮 손님의 주문이라 밤에는 숨긴다

        switch (state)
        {
            case MainState.DayCustomer:
                if (morning) StartCoroutine(BeginDayCustomerAfterMorning());
                else BeginDayCustomer();
                break;
            case MainState.DayEnding: BeginDayEnd(); break;
            case MainState.NightCustomer: BeginNightCustomer(); break;
            case MainState.NightEnding: BeginNightEnd(); break;
        }
    }

    /// <summary>씬에서 이 화면이 쓰는 오브젝트를 이름으로 찾아 두고 응답 버튼을 잇는다.</summary>
    private void FindSceneObjects()
    {
        var scene = gameObject.scene;
        speech = new CustomerSpeech(SceneFind.One<TMP_Text>(scene, SceneObjectNames.BubbleText));
        bubbleContainer = SceneFind.One<Transform>(scene, SceneObjectNames.BubbleContainer)?.gameObject;
        catalogPopup = SceneFind.One<Transform>(scene, SceneObjectNames.CatalogPopup)?.gameObject;
        calendarPopup = SceneFind.One<Transform>(scene, SceneObjectNames.ReservationCalendar)?.gameObject;
        characterRoots = System.Array.ConvertAll(SceneObjectNames.CharacterRoots, n => SceneFind.One<Transform>(scene, n)?.gameObject);
        character = SceneFind.One<CharacterGenerator>(scene, SceneObjectNames.DayCharacter);

        Button reject = SceneFind.One<Button>(scene, SceneObjectNames.RejectButton);
        Button accept = SceneFind.One<Button>(scene, SceneObjectNames.AcceptButton);
        rejectButton = reject != null ? reject.gameObject : null;
        acceptButton = accept != null ? accept.gameObject : null;
        reject?.onClick.AddListener(OnRejectClicked);
        accept?.onClick.AddListener(OnAcceptClicked);
        dialoguePlayer = new DialoguePlayer(speech, rejectButton, acceptButton);
    }

    /// <summary>옆 페이지(꽃 선택/꽃 선반)와 스와이프를 상황에 맞게 준비한다.</summary>
    private void SetUpSidePage()
    {
        var scene = gameObject.scene;
        // 옆 페이지(ShelfRoot)는 낮 손님(꽃 선택)과 낮 종료(꽃 선반)에만 켜 둔다(다른 상황에서는 스와이프로도 갈 수 없다). 둘은 같은 선반을 쓰므로 상황에 맞는 쪽만 켠다.
        GameObject shelfRoot = SceneFind.One<Transform>(scene, SceneObjectNames.ShelfRoot)?.gameObject;
        bool hasSidePage = state == MainState.DayCustomer || state == MainState.DayEnding;
        if (shelfRoot != null) shelfRoot.SetActive(hasSidePage);
        if (shelfRoot != null && state == MainState.DayEnding) // 낮 종료의 옆 페이지는 부적을 붙이는 꽃 선반(FlowerArea)이다. 포장지 선택(WrapperArea)은 낮 손님의 것이라 켜 둔 채 저장돼 있어도 끈다
        {
            shelfRoot.transform.Find("WrapperArea")?.gameObject.SetActive(false);
            shelfRoot.transform.Find("FlowerArea")?.gameObject.SetActive(true);
        }
        shelf = shelfRoot != null ? shelfRoot.GetComponentInChildren<NightRequestUI>(true) : null;
        if (shelf != null) shelf.gameObject.SetActive(state == MainState.DayEnding);
        FlowerSelectUI flowerSelect = shelfRoot != null ? shelfRoot.GetComponentInChildren<FlowerSelectUI>(true) : null;
        if (flowerSelect != null) flowerSelect.gameObject.SetActive(state == MainState.DayCustomer);
        SceneFind.One<Transform>(scene, SceneObjectNames.CharmPill)?.gameObject.SetActive(state == MainState.DayEnding);
        swipe = FindFirstObjectByType<SwipeParallax>();
        if (swipe != null)
        {
            swipe.Enabled = false;
            swipe.Arrived += OnSwipeArrived;
        }
    }

    /// <summary>전등(낮 종료와 밤에만 나타난다)을 상황에 맞게 준비한다.</summary>
    private void SetUpLantern()
    {
        lantern = SceneFind.One<Toggle>(gameObject.scene, SceneObjectNames.LanternToggle);
        if (lantern != null)
        {
            lantern.gameObject.SetActive(state == MainState.DayEnding || IsNight); // 전등은 낮이 끝난 시점과 밤 동안에만 나타난다(낮 영업 중에는 숨긴다)
            lantern.SetIsOnWithoutNotify(false);
            lantern.interactable = false; // 낮 종료 손님이 떠난 뒤에야 켤 수 있다(HideCustomer). 밤에는 켜진 모습을 보여 주기만 한다
            lanternLight = lantern.GetComponentInParent<LanternLight>(true);
            if (IsNight) lanternLight?.SetLit(true); // 밤에는 전등이 켜진 모습이다
            lantern.onValueChanged.AddListener(OnLanternChanged);
        }
    }

    /// <summary>전화기를 누르면 전화기 바가 뜨고(다시 누르면 닫힌다), 바의 상품주문 버튼을 눌러야 카탈로그가 열리게 잇는다. 팝업의 닫기 버튼도 스스로 끄므로 열림 상태를 따로 들고 있지 않는다.</summary>
    private void SetUpCatalog()
    {
        if (catalogPopup == null) return;

        var scene = gameObject.scene;
        telephoneBar = SceneFind.One<Transform>(scene, SceneObjectNames.TelephoneBar)?.gameObject;
        int day = CurrencyManager.Instance != null ? CurrencyManager.Instance.CurrentDay : 1;
        ScenarioManager scenario = ScenarioManager.Instance; // 없으면(씬 단독 실행) 모두 해금
        phoneUnlocked = scenario == null || scenario.IsPhoneUnlocked(day);
        foreach (Button telephone in SceneFind.All<Button>(scene, SceneObjectNames.Telephone))
        {
            telephone.onClick.AddListener(ToggleTelephoneBar);
            if (!phoneUnlocked && telephone.TryGetComponent(out PressSound press)) press.enabled = false; // 전화기를 쓸 수 없는 동안은 누르는 소리도 나지 않는다
        }
        SceneFind.One<Button>(scene, SceneObjectNames.CatalogButton)?.onClick.AddListener(OpenCatalog);
        Button booking = SceneFind.One<Button>(scene, SceneObjectNames.BookingButton);
        if (booking != null)
        {
            booking.interactable = scenario == null || scenario.IsCalendarUnlocked(day); // 달력은 해금된 날부터
            booking.onClick.AddListener(OpenCalendar);
        }
        telephoneBar?.SetActive(false);
        catalogPopup.SetActive(false);
        calendarPopup?.SetActive(false);
    }

    /// <summary>지금 가구 상인의 대사를 재생하는 중인가.</summary>
    private bool MerchantTalking
    {
        get
        {
            DayVisit? visit = Game != null ? Game.CurrentScriptedVisit : null;
            return dialoguePlayer != null && dialoguePlayer.Playing && visit.HasValue && visit.Value.dialogueId != null && visit.Value.dialogueId.StartsWith("merchant");
        }
    }

    private bool PopupOpen => (catalogPopup != null && catalogPopup.activeSelf) || (calendarPopup != null && calendarPopup.activeSelf);

    /// <summary>
    /// 대사를 시작한다. 말풍선과 응답 버튼을 숨겨 두었다가, 씬 전환의 페이드가 다 걷힌 뒤(걷힐 게 없으면 한 프레임 뒤)에 보이고 대사(와 말소리)를 시작한다.
    /// </summary>
    private void SayAfterReveal(System.Action say)
    {
        SetBubble(false);
        StartCoroutine(SayAfterRevealRoutine(say));
    }

    private System.Collections.IEnumerator SayAfterRevealRoutine(System.Action say)
    {
        while (SceneTransition.Busy) yield return null;
        SetBubble(true);
        say();
    }

    /// <summary>전화기를 누르면 전화기 바가 뜨고, 다시 누르면 닫힌다. 카탈로그가 열려 있는 동안은 바를 띄우지 않는다.</summary>
    private void ToggleTelephoneBar()
    {
        if (!phoneUnlocked || telephoneBar == null || PopupOpen || ReservationCall.Busy || MerchantTalking) return; // 가구 상인이 말하는 동안에는 전화기를 쓰지 않는다(상인이 떠난 뒤 구재민이 말할 때부터 쓴다) // 전화기가 해금되기 전과 예약 전화가 울리는 동안(받는 일은 ReservationCall이 한다)에는 메뉴 바를 열지 않는다

        telephoneBar.SetActive(!telephoneBar.activeSelf);
    }

    /// <summary>바의 상품주문 버튼: 카탈로그가 아래에서 올라오며 열린다(닫기는 팝업의 닫기 버튼). 열리면 전화기 바는 닫는다.</summary>
    private void OpenCatalog()
    {
        telephoneBar?.SetActive(false);
        if (!catalogPopup.activeSelf) catalogPopup.SetActive(true);
    }

    /// <summary>바의 예약확인 버튼: 달력 화면이 열린다(닫기는 화면의 닫기 버튼). 열리면 전화기 바는 닫는다.</summary>
    private void OpenCalendar()
    {
        telephoneBar?.SetActive(false);
        if (calendarPopup != null && !calendarPopup.activeSelf) calendarPopup.SetActive(true);
    }

    private void Update()
    {
        if (state != MainState.DayEnding || nightStarting) return;

        // 낮 종료가 되면 손님이 아직 말하는 중이어도 스와이프로 선반을 오갈 수 있다(카탈로그가 열려 있는 동안은 막는다).
        // 등불은 낮 종료 손님이 떠난 뒤, 카운터 화면에 있을 때만 켤 수 있고, 선반(부적) 화면으로 가면 사라진다.
        if (swipe != null) swipe.Enabled = !PopupOpen;
        if (lantern != null)
        {
            bool atHome = swipe == null || swipe.IsAtHome;
            lantern.gameObject.SetActive(atHome);
            lantern.interactable = customerGone && atHome;
        }
    }

    /// <summary>꽃 선택 페이지에 도착하면 카운터로 돌아올 수 없다(주문은 이미 수락했다).</summary>
    private void OnSwipeArrived(int page)
    {
        if (state != MainState.DayCustomer || page < 1) return;

        swipe.LowestPage = page;
        ColorWheelHud.Show(); // 컬러휠은 꽃 선택 페이지에 도착한 뒤부터 보인다
    }

    /// <summary>밤 → 낮 연출이 끝날 때까지 손님은 나타나지 않고, 끝나면 첫 손님이 온다.</summary>
    private System.Collections.IEnumerator BeginDayCustomerAfterMorning()
    {
        SetBubble(false);
        if (character != null) character.gameObject.SetActive(false);
        while (mainLook != null && mainLook.IsMorningBlending) yield return null;
        BeginDayCustomer();
    }

    /// <summary>낮 손님: 주문을 말한다. 이전 손님의 반응 대사가 있으면 그것부터 말한다.</summary>
    private void BeginDayCustomer()
    {
        if (Game != null && Game.Craft != null) // 예약일: 손님은 더 오지 않고 예약 꽃다발만 만든다
        {
            StartCoroutine(Game.Crafting ? CraftingRoutine() : ReservationReactionRoutine());
            return;
        }

        string angryLine = Game != null ? Game.ConsumePendingCustomerLine() : null;
        showingReaction = !string.IsNullOrEmpty(angryLine);
        if (!showingReaction && Game != null && calendarPopup != null)
        {
            ReservationRecord due = Game.TakePickupReservation(); // 오늘이 예약일이면 예약자가 하루의 첫 손님으로 온다
            if (due != null)
            {
                StartCoroutine(PickupRoutine(due));
                return;
            }
        }
        if (!showingReaction && TryBeginScriptedVisit()) return;

        if (showingReaction)
        {
            ShowPreviousNpcOrCustomer(); // 반응 대사는 방금 주문을 한 그 손님이 한다
            if (Game != null && Game.PreviousNpc != null) PauseForNpc(true); // NPC의 반응 대사(와 이어지는 친밀 대사)를 듣는 동안은 시간이 가지 않는다
            SayAfterReveal(() => speech.Show(angryLine));
            forceAcceptLine = GameManager.Instance != null
                ? GameManager.Instance.ConsumePendingRejectResponseLine()
                : CustomerSpeech.DefaultForceAcceptLine;
        }
        else
        {
            StartCoroutine(NextCustomerRoutine(false)); // 새 손님: 들어오는 모션 -> 말풍선과 주문. 기분 시간은 가득 차 있고, 주문을 수락해야 줄어들기 시작한다
        }
        // 이전 손님의 반응 대사 중이면 기분 시간은 끝난 그대로 멈춰 둔다(다음 손님이 올 때 채운다: OnAcceptClicked)
    }

    /// <summary>
    /// 시나리오에 정해진 NPC 방문이면 그 NPC의 대사(CSV)를 재생한다. 정해진 방문이 아니거나 대사를 쓸 수 없으면 false(평소처럼 무작위 손님).
    /// 주문 효과가 있는 대사는 주문 줄 앞까지만 재생하고, 주문 줄은 평소처럼 주문으로 말한다(GameManager가 그 대사의 꽃·포장지로 주문을 만들어 둔다. 수락/되묻기는 평소와 같다).
    /// previousLeaves: 이전 손님(반응 대사를 한 손님)이 먼저 나가야 하는가.
    /// </summary>
    private bool TryBeginScriptedVisit(bool previousLeaves = false)
    {
        if (IsNight || Game == null || ScenarioManager.Instance == null) return false;

        DayVisit? visit = Game.CurrentScriptedVisit;
        if (visit == null) return false;

        Dialogue dialogue = ScenarioManager.Instance.Dialogue.Get(visit.Value.dialogueId);
        if (dialogue == null)
        {
            Debug.LogWarning($"[MainUI] 대화 '{visit.Value.dialogueId}'가 CSV에 없어 무작위 손님으로 진행합니다.");
            return false;
        }
        StartCoroutine(ScriptedDayVisitRoutine(visit.Value.npc, dialogue, previousLeaves));
        return true;
    }

    /// <summary>정해진 낮 NPC 방문: 들어온다 -> 대화를 재생한다 -> 나간다 -> (시간경과 효과가 있으면 밤으로, 아니면 다음 방문자). 주문이 있는 방문은 나가지 않고 주문을 말한다.</summary>
    private System.Collections.IEnumerator ScriptedDayVisitRoutine(NpcData npc, Dialogue dialogue, bool previousLeaves)
    {
        int orderLineIndex = dialogue.lines.FindLastIndex(l => l.effects.Any(e => e.kind == DialogueEffect.Order));
        if (orderLineIndex >= 0 && order != null)
        {
            var before = new Dialogue { id = dialogue.id, lines = dialogue.lines.GetRange(0, orderLineIndex) }; // 주문 줄 앞까지
            yield return TalkScripted(npc, before, previousLeaves, leaveAfter: false);

            SetAnswerButtons(true);
            speech.ShowOrder(order); // 주문 줄: 평소의 주문과 같이 말하고 수락/되묻기를 받는다
            GameClock.Instance?.ResetMood();
            yield break;
        }

        yield return TalkScripted(npc, dialogue, previousLeaves, leaveAfter: true);

        if (dialogue.EffectsOf(DialogueEffect.TimeSkip).Any()) yield return TimeSkipToNight();
        else Game.BeginNewDay();
    }

    /// <summary>
    /// NPC 하나와 정해진 대화를 한다: (이전 손님이 있으면 나가고) NPC가 들어오고 -> 대화를 재생하고 -> leaveAfter면 나간다. 대화하는 동안은 시간이 흐르지 않는다.
    /// 작은 NPC는 작은 캐릭터(SmallChar)에, 큰 NPC는 큰 캐릭터(BigChar)에 그림을 보인다(그림이 없는 NPC는 무작위 손님 모습).
    /// </summary>
    private System.Collections.IEnumerator TalkScripted(NpcData npc, Dialogue dialogue, bool previousLeaves, bool leaveAfter)
    {
        changingCustomer = true;
        SetBubble(false);
        if (SceneTransition.Busy)
        {
            if (character != null) character.gameObject.SetActive(false); // 페이드가 걷히는 동안은 숨겼다가, 다 걷히면 들어오는 모션과 함께 나타낸다
            while (SceneTransition.Busy) yield return null;
        }

        PanelMotion motion = character != null ? character.GetComponent<PanelMotion>() : null;
        float seconds = motion != null ? motion.Seconds : 0f;
        if (previousLeaves && character != null)
        {
            PanelMotion.Close(character.gameObject);
            yield return GameTime.Wait(seconds + customerSwapDelay);
        }

        if (npc != null && npc.portrait != null)
        {
            ShowNpc(npc);
            if (character != null) character.gameObject.SetActive(true);
            motion?.PlayShow();
        }
        else ShowNewCustomer(arrive: true); // 그림이 없는 NPC는 전용 모습이 생기기 전까지 무작위 손님 모습
        yield return GameTime.Wait(seconds + 0.1f);

        SetBubble(true);
        changingCustomer = false;
        PauseForNpc(true);
        yield return dialoguePlayer.Play(dialogue);
        if (!leaveAfter) yield break; // 주문을 말하러 이어지는 방문은 주문을 수락할 때까지 시간이 멈춘 채로 있다(OnAcceptClicked)

        PauseForNpc(false);

        changingCustomer = true;
        SetBubble(false);
        if (character != null) PanelMotion.Close(character.gameObject);
        yield return GameTime.Wait(seconds + customerSwapDelay);
    }

    /// <summary>
    /// NPC의 그림을 캐릭터 자리에 보인다. 체구가 작은 NPC는 작은 캐릭터(SmallChar)에, 큰 NPC는 큰 캐릭터(BigChar)에 그림을 넣는다.
    /// </summary>
    private void ShowNpc(NpcData npc)
    {
        ShowCharacterStyle(npc.characterType, npc.portrait);
    }

    /// <summary>
    /// 낮을 건너뛰고 밤으로 간다(1일차): 시계가 빠르게 감기며 낮이 노을이 되면 → 전등이 (꺼진 채) 나타나고 → 잠시 뒤 전등이 켜지며 → 밤 휘슬이 울리고, 노을 → 밤으로 섞인 뒤 밤이 시작된다.
    /// 전등을 켜는 평소 흐름(OnLanternChanged)과 같은 효과와 소리를 낸다.
    /// </summary>
    private System.Collections.IEnumerator TimeSkipToNight()
    {
        if (GameClock.Instance != null)
        {
            yield return GameClock.Instance.FastForwardDay(timeSkipSeconds);
            mainLook?.Apply(MainState.DayEnding); // 노을 모습으로 고정한다(시계가 밤 모습이 되면 낮 → 노을 섞기가 낮으로 되돌아가 낮 배경이 잠깐 보이는 것을 막는다)
            GameClock.Instance.ShowNightLook(); // 시계가 밤 모습이 되고, 시계 옆 HUD의 전등 영역이 나타난다(ClockGauge)
            yield return null;
        }

        if (lantern != null) // 낮에는 숨겨 둔 전등이 꺼진 채 나타난다
        {
            lantern.gameObject.SetActive(true);
            lantern.interactable = false;
            lantern.SetIsOnWithoutNotify(false);
        }
        lanternLight?.SetLit(false);
        yield return GameTime.Wait(lanternLightDelay);

        lantern?.SetIsOnWithoutNotify(true); // 저절로 켜진다
        lanternLight?.SetLit(true);
        if (lantern != null && lantern.TryGetComponent(out PressSound click)) click.Play(); // 전등을 누를 때의 딸깍 소리
        Bgm.Stop();
        Sfx.Play(nightStartJingle, nightStartJingleVolume); // 밤 휘슬

        float blend = mainLook != null ? mainLook.PlayNightBlend() : 0f;
        float jingle = nightStartJingle != null ? nightStartJingle.length : 0f;
        yield return GameTime.Wait(Mathf.Max(lanternDelay, blend, jingle)); // 밤 휘슬이 끝날 때까지 기다린다
        Game.StartNight(new List<int>());
    }

    /// <summary>
    /// 캐릭터 자리에 어느 모습을 보일지 정한다: npcType이 null이면 무작위 손님(NomalChar, 부위 조합), Small이면 작은 NPC(SmallChar), Big이면 큰 NPC(BigChar).
    /// SmallChar·BigChar에는 portrait 그림을 넣고, 나머지는 끈다.
    /// </summary>
    private void ShowCharacterStyle(NpcCharacterType? npcType, Sprite portrait)
    {
        if (character == null) return;

        Transform random = character.transform.Find(SceneObjectNames.NormalChar);
        Transform small = character.transform.Find(SceneObjectNames.SmallChar);
        Transform big = character.transform.Find(SceneObjectNames.BigChar);
        if (random != null) random.gameObject.SetActive(npcType == null);
        if (small != null) small.gameObject.SetActive(npcType == NpcCharacterType.Small);
        if (big != null) big.gameObject.SetActive(npcType == NpcCharacterType.Big);

        Transform shown = npcType == NpcCharacterType.Small ? small : npcType == NpcCharacterType.Big ? big : null;
        if (shown != null && shown.TryGetComponent(out Image image)) image.sprite = portrait;
    }

    /// <summary>새 손님이 왔다: 무작위 캐릭터로 바꾸고, 반응 대사와 낮 종료 대사를 같은 손님이 하도록 기억해 둔다.</summary>
    private void ShowNewCustomer(bool arrive = false)
    {
        if (character == null) return;

        ShowCharacterStyle(null, null); // 무작위 손님은 부위를 조합한 캐릭터(NomalChar)다
        character.Randomize(IsNight); // 밤 손님은 피부가 파란 귀신, 낮 손님은 그 피부를 뺀 사람이다
        if (Game != null) Game.CustomerLook = character.Current;
        if (!arrive) return;

        character.gameObject.SetActive(true); // 나갔던 손님 자리를 다시 켠다
        if (character.TryGetComponent(out PanelMotion motion)) motion.PlayShow(); // 새 손님은 아래에서 올라오며 들어온다
    }

    /// <summary>다음 손님이 오는 순서: (이전 손님이 있으면) 말풍선이 사라진다 -> 이전 손님이 나간다 -> 새 손님이 들어온다 -> 말풍선과 주문이 뜬다.</summary>
    private System.Collections.IEnumerator NextCustomerRoutine(bool previousLeaves)
    {
        changingCustomer = true;
        SetBubble(false);
        if (SceneTransition.Busy)
        {
            if (character != null) character.gameObject.SetActive(false); // 페이드가 걷히는 동안은 손님을 숨겼다가, 다 걷히면 들어오는 모션과 함께 나타낸다
            while (SceneTransition.Busy) yield return null;
        }

        PanelMotion motion = character != null ? character.GetComponent<PanelMotion>() : null;
        float seconds = motion != null ? motion.Seconds : 0f;
        if (previousLeaves && character != null)
        {
            PanelMotion.Close(character.gameObject);
            yield return GameTime.Wait(seconds + customerSwapDelay); // 나가는 모션이 끝나고 잠시 쉰 뒤 새 손님이 들어온다
        }

        ShowNewCustomer(arrive: true);
        yield return GameTime.Wait(seconds + 0.1f); // 들어오는 모션은 한 프레임 뒤에 시작한다

        SetBubble(true);
        if (IsNight)
        {
            speech.ShowNightCustomer(Game != null ? Game.NightCustomerFlowerName() : null, Game != null && Game.NightCustomerBringsNewFlower);
        }
        else
        {
            speech.ShowOrder(order);
            GameClock.Instance?.ResetMood();
        }
        changingCustomer = false;
    }

    /// <summary>말풍선과 응답 버튼을 함께 보이고 숨긴다.</summary>
    private void SetBubble(bool on)
    {
        if (bubbleContainer != null) bubbleContainer.SetActive(on);
        SetAnswerButtons(on);
    }

    private void SetAnswerButtons(bool on)
    {
        if (rejectButton != null) rejectButton.SetActive(on);
        if (acceptButton != null) acceptButton.SetActive(on);
    }

    /// <summary>방금 주문을 한 손님의 모습을 보여 준다(기억해 둔 것이 없으면 새 손님).</summary>
    private void ShowPreviousCustomer()
    {
        if (character == null) return;

        ShowCharacterStyle(null, null); // 메인 씬은 NPC 캐릭터(SmallChar·BigChar)가 켜진 채 열릴 수 있으므로, 무작위 손님 모습으로 되돌린다
        if (Game != null && Game.CustomerLook is CharacterAppearance look) character.Apply(look);
        else ShowNewCustomer();
    }

    /// <summary>낮 종료: 하루 시간이 끝났다. 손님이 마무리 대사를 한 뒤 잠시 후(또는 응답 버튼을 누르면) 사라지고, 사라지면 전등을 켤 수 있다.</summary>
    private void BeginDayEnd()
    {
        if (GameManager.Instance != null) // 이전 손님의 반응 대사가 남아 있으면 지운다(다음 날로 넘어가지 않게)
        {
            GameManager.Instance.ConsumePendingCustomerLine();
            GameManager.Instance.ConsumePendingRejectResponseLine();
        }
        if (Game != null && Game.ConsumeSkipEndingCustomer()) // 예약일이 끝났다: 마무리 손님 없이 바로 선반과 전등을 쓸 수 있다
        {
            SetBubble(false);
            foreach (GameObject root in characterRoots) if (root != null) root.SetActive(false);
            customerGone = true;
            return;
        }
        ShowPreviousCustomer();
        SayAfterReveal(() =>
        {
            speech.ShowTimeUp();
            EndConversation();
        });
    }

    /// <summary>밤 손님: 새 손님(파란 피부 귀신)이 들어와 자기 꽃 이야기를 하고, 수락하면 꽃을 진단하러 밤 퍼즐 씬으로 간다. 만날 손님이 더 없으면 밤을 끝낸다.</summary>
    private void BeginNightCustomer()
    {
        string reaction = Game != null ? Game.ConsumePendingCustomerLine() : null;
        if (!string.IsNullOrEmpty(reaction)) // 방금 꽃을 고친 손님이 고쳐진 정도에 맞는 반응 대사를 말한다
        {
            showingReaction = true;
            ShowPreviousNpcOrCustomer();
            if (Game.PreviousNpc != null) PauseForNpc(true); // NPC의 반응 대사를 듣는 동안은 밤 시간이 가지 않는다
            string notice = Game.ConsumePendingNotice();
            SayAfterReveal(() =>
            {
                speech.Show(reaction);
                if (!string.IsNullOrEmpty(notice)) NoticeToast.Show(notice); // 받은 꽃(복제품) 알림
            });
            forceAcceptLine = Game.ConsumePendingRejectResponseLine();
            return;
        }

        if (Game == null || !Game.HasNightCustomer)
        {
            Game?.EndNight();
            return;
        }
        if (TryBeginScriptedNightVisit(previousLeaves: false)) return;
        StartCoroutine(NextCustomerRoutine(false));
    }

    /// <summary>
    /// 시나리오에 정해진 밤 방문(예: 1일차 세이렌의 소개, 6일차 세이렌의 델피니움)에 대사(CSV)가 있으면 그 대화를 재생한다. 대사가 없으면 false(평소 밤 손님 대사).
    /// 대화만 하는 방문은 대화가 끝나면 손님이 나가고 다음 손님으로, 수리를 부탁하는 방문은 대화가 끝나면 바로 밤 퍼즐로 간다.
    /// </summary>
    private bool TryBeginScriptedNightVisit(bool previousLeaves)
    {
        if (Game == null || ScenarioManager.Instance == null) return false;

        NightVisit? visit = Game.CurrentNightVisit;
        if (visit == null) return false;

        Dialogue dialogue = ScenarioManager.Instance.Dialogue.Get(visit.Value.dialogueId);
        if (dialogue == null)
        {
            if (!visit.Value.dialogueOnly) return false; // 수리 손님은 평소 밤 손님 대사로
            Debug.LogWarning($"[MainUI] 대화만 하는 밤 방문의 대화 '{visit.Value.dialogueId}'가 CSV에 없어 건너뜁니다.");
            Game.NextNightCustomer();
            return true;
        }

        StartCoroutine(ScriptedNightVisitRoutine(visit.Value, dialogue, previousLeaves));
        return true;
    }

    private System.Collections.IEnumerator ScriptedNightVisitRoutine(NightVisit visit, Dialogue dialogue, bool previousLeaves)
    {
        yield return TalkScripted(visit.npc, dialogue, previousLeaves, leaveAfter: visit.dialogueOnly);

        if (visit.dialogueOnly) Game.NextNightCustomer();
        else Game.StartNightPuzzle();
    }

    /// <summary>방금 주문·수리를 맡긴 손님의 모습을 보여 준다(그림이 있는 NPC면 그 그림, 아니면 방금 그 손님).</summary>
    private void ShowPreviousNpcOrCustomer()
    {
        NpcData npc = Game != null ? Game.PreviousNpc : null;
        if (npc != null && npc.portrait != null)
        {
            ShowNpc(npc);
            if (character != null) character.gameObject.SetActive(true);
        }
        else ShowPreviousCustomer();
    }

    /// <summary>밤 종료: 밤 시간이 끝났다. 손님이 "해가 뜨면 저는 죽어요!!"라고 외치고, 낮 종료와 같은 방식으로 사라진다. 사라지면 하루 결산으로 간다.</summary>
    private void BeginNightEnd()
    {
        state = MainState.NightEnding;
        showingReaction = false;
        if (Game != null) // 이전 손님의 반응 대사가 남아 있으면 지운다
        {
            Game.ConsumePendingCustomerLine();
            Game.ConsumePendingRejectResponseLine();
        }
        if (character != null) character.Randomize(true); // 밤 종료 손님도 파란 피부 귀신
        SayAfterReveal(() =>
        {
            speech.ShowNightEnd();
            EndConversation();
        });
    }

    /// <summary>밤 손님을 만나는 중에 밤 시간이 끝났다: 그 자리에서 밤 종료로 넘어간다.</summary>
    private void OnNightTimeUp()
    {
        if (IsNight) BeginNightEnd();
    }

    private void OnRejectClicked()
    {
        if (dialoguePlayer != null && dialoguePlayer.Playing) { dialoguePlayer.Answer(); return; }
        if (changingCustomer) return;
        if (leaving) { HideCustomer(); return; }
        if (showingReaction) speech.Show(forceAcceptLine);
        else speech.ShowReask(order);
    }

    private void OnAcceptClicked()
    {
        if (dialoguePlayer != null && dialoguePlayer.Playing) { dialoguePlayer.Answer(); return; }
        if (changingCustomer) return;
        if (leaving) { HideCustomer(); return; }
        if (state == MainState.NightCustomer)
        {
            if (!showingReaction)
            {
                Game?.StartNightPuzzle();
                return;
            }

            showingReaction = false; // 반응 대사가 끝났다: 다음 손님이 있으면 이 손님이 나가고 다음 손님이 오고, 없으면 이 손님이 나가며 밤이 끝난다
            PauseForNpc(false);
            if (Game != null && Game.HasNightCustomer)
            {
                if (!TryBeginScriptedNightVisit(previousLeaves: true)) StartCoroutine(NextCustomerRoutine(true));
            }
            else
            {
                state = MainState.NightEnding;
                HideCustomer();
            }
            return;
        }
        if (showingReaction)
        {
            showingReaction = false;
            StartCoroutine(NextVisitRoutine()); // 반응 대사가 끝났으니 이 손님이 나가고 다음 손님이 온다(기분 시간은 말풍선이 뜰 때 가득 찬다)
            return;
        }

        PauseForNpc(false); // NPC와의 대화(주문 포함)는 끝났다: 이제부터 시간이 간다
        GameClock.Instance?.StartMood(); // 주문을 수락하는 순간부터 손님 기분 시간이 줄어든다
        Game?.AcceptOrder();
        SetAnswerButtons(false); // 수락한 뒤에는 응답 버튼이 필요 없다
        StartCoroutine(GoToFlowerSelectAfterDelay());
    }

    private static readonly string[] PickupReadyAnswers = { "네!", "그래요" };
    private static readonly string[] PickupNotReadyAnswers = { "뭐라고요?", "네?" };

    /// <summary>
    /// 예약일: 예약자가 찾아와 준비가 됐냐고 묻는다. 준비 상태에 따라 답 버튼이 다르다(준비됨 = 네!/그래요, 안 됨 = 뭐라고요?/네?).
    /// 필요한 꽃은 가지고 있으면서 수납하지 않았다면, 예약자가 말한 뒤 답 버튼 없이 1초 기다렸다가 "달력 화면으로 갈까요?"를 묻는다. 거절하면 미완료로 답하고, 수락하면 달력에서 수납한 뒤 준비가 됐는지 다시 보고 답한다.
    /// 준비됐다고 답하면 예약은 제작 대기가 된다(제작 시스템은 아직 없다). 준비 안 됐다고 답하면 예약금의 2배를 돌려주고 예약이 없어지며, 같은 예약 전화가 5일 뒤에 다시 온다.
    /// 이 모든 동안 시간이 가지 않는다. 끝나면 예약자가 나가고 평소의 첫 손님이 온다.
    /// </summary>
    private System.Collections.IEnumerator PickupRoutine(ReservationRecord record)
    {
        CurrencyManager currency = CurrencyManager.Instance;
        ReservationData data = record.data;
        changingCustomer = true;
        SetBubble(false);
        PauseForNpc(true);
        if (SceneTransition.Busy)
        {
            if (character != null) character.gameObject.SetActive(false); // 페이드가 걷히는 동안은 숨겼다가, 다 걷히면 들어오는 모션과 함께 나타낸다
            while (SceneTransition.Busy) yield return null;
        }

        PanelMotion motion = character != null ? character.GetComponent<PanelMotion>() : null;
        float seconds = motion != null ? motion.Seconds : 0f;
        ShowNewCustomer(arrive: true); // 예약자는 전용 그림이 생기기 전까지 무작위 손님 모습
        yield return GameTime.Wait(seconds + 0.1f);
        SetBubble(true);
        changingCustomer = false;

        CurrencyManager.PickupState state = currency.GetPickupState(record);
        if (state == CurrencyManager.PickupState.NeedsStoring)
        {
            SetAnswerButtons(false);
            speech.Show(data.pickupLine); // 답 버튼 없이 말만 한다
            while (speech.IsTyping) yield return null;
            yield return GameTime.Wait(1f);

            ReservationCalendar calendar = calendarPopup.GetComponent<ReservationCalendar>();
            bool went = false;
            yield return calendar.PickupStoreFlow(record, w => went = w);
            state = went ? currency.GetPickupState(record) : CurrencyManager.PickupState.NotReady; // 거절하면 미완료로 답한다
            if (state != CurrencyManager.PickupState.Ready) state = CurrencyManager.PickupState.NotReady; // 달력에서 다 수납하지 않고 닫았어도 미완료다
        }

        bool ready = state == CurrencyManager.PickupState.Ready;
        var ask = new Dialogue { id = "reservation_pickup_ask", lines = { new DialogueLine { text = data.pickupLine, answers = ready ? PickupReadyAnswers : PickupNotReadyAnswers } } };
        yield return dialoguePlayer.Play(ask);
        var reply = new Dialogue { id = "reservation_pickup_reply", lines = { new DialogueLine { text = ready ? data.pickupReadyReply : data.pickupFailReply } } };
        yield return dialoguePlayer.Play(reply);

        if (!ready)
        {
            int fee = data.deposit * CurrencyManager.NoShowFeeMultiplier;
            currency.FailReservation(record);
            Game.DelayReservationCall(data, CurrencyManager.NoShowDelayDays);
            NoticeToast.Show($"예약 취소 {NoticeToast.Highlight($"-{fee:N0}")}");
        }

        changingCustomer = true;
        SetBubble(false);
        if (character != null) PanelMotion.Close(character.gameObject);
        yield return GameTime.Wait(seconds + customerSwapDelay);
        PauseForNpc(false);
        if (ready)
        {
            Game.BeginReservationCraft(record); // 준비됐다: 오늘은 예약 꽃다발만 만든다
            yield break;
        }
        if (!TryBeginScriptedVisit(previousLeaves: false)) StartCoroutine(NextCustomerRoutine(false)); // 평소의 첫 손님
    }

    /// <summary>
    /// 예약 꽃다발을 만드는 중에 메인 화면이 열렸다(꽃다발 하나를 마칠 때마다 이 화면으로 돌아온다): 손님 없이 바로 포장지 선택 페이지로 넘어간다.
    /// 시간은 흐른다(손님 기분 시간은 쓰지 않는다).
    /// </summary>
    private System.Collections.IEnumerator CraftingRoutine()
    {
        changingCustomer = true;
        SetBubble(false);
        if (character != null) character.gameObject.SetActive(false);
        while (SceneTransition.Busy) yield return null;

        yield return GameTime.Wait(craftSwipeDelay);
        Sfx.Play(forcedSwipeSound, forcedSwipeVolume);
        if (swipe != null) swipe.MoveTo(1);
    }

    /// <summary>
    /// 예약 꽃다발을 다 만들었거나 시간이 끝났다: 예약자가 다시 나타나 평균 점수 등급에 맞는 말을 하고, 보상(보상금 × 등급 배율)을 준 뒤 나간다.
    /// 그러고 나서 남은 하루 시간이 빠르게 흘러 낮 종료로 넘어간다. 이 동안 시간은 가지 않는다.
    /// </summary>
    private System.Collections.IEnumerator ReservationReactionRoutine()
    {
        ReservationCraft craft = Game.Craft;
        changingCustomer = true;
        SetBubble(false);
        PauseForNpc(true);
        if (character != null) character.gameObject.SetActive(false);
        while (SceneTransition.Busy) yield return null;

        PanelMotion motion = character != null ? character.GetComponent<PanelMotion>() : null;
        float seconds = motion != null ? motion.Seconds : 0f;
        ShowPreviousCustomer(); // 찾아왔던 그 예약자
        if (character != null) character.gameObject.SetActive(true);
        motion?.PlayShow();
        yield return GameTime.Wait(seconds + 0.1f);
        SetBubble(true);
        changingCustomer = false;

        ReservationGrade grade = craft.Grade;
        var reaction = new Dialogue { id = "reservation_result", lines = { new DialogueLine { text = craft.Data.GradeLine(grade) } } };
        yield return dialoguePlayer.Play(reaction);

        int payout = Game.CollectReservationReward();
        NoticeToast.Show($"예약 보상 {NoticeToast.Highlight($"+{payout:N0}")} ({grade}등급)");

        changingCustomer = true;
        SetBubble(false);
        if (character != null) PanelMotion.Close(character.gameObject);
        yield return GameTime.Wait(seconds + customerSwapDelay);

        GameClock clock = GameClock.Instance;
        if (clock != null && clock.DayRemaining > 0.5f) yield return clock.FastForwardDay(timeSkipSeconds); // 남은 시간은 빠르게 흘러간다
        PauseForNpc(false);
        Game.EndReservationDay();
    }

    /// <summary>
    /// 반응 대사가 끝난 뒤: 이전 손님이 나가고 다음 손님이 온다. 단, 오늘 예약 전화가 걸려 올 차례면 이전 손님이 나간 뒤 전화가 울리고, 전화가 끝나야(받아서 수락·거절을 했거나, 못 받고 끊겼거나) 다음 손님이 온다.
    /// </summary>
    private System.Collections.IEnumerator NextVisitRoutine()
    {
        string bondId = Game != null ? Game.ConsumeBondDialogueId() : null; // 정해진 손님의 친밀 대사는 꽃다발 반응 대사 바로 뒤에 한다
        Dialogue bond = !string.IsNullOrEmpty(bondId) && ScenarioManager.Instance != null ? ScenarioManager.Instance.Dialogue.Get(bondId) : null;
        if (bond != null)
        {
            PauseForNpc(true);
            yield return dialoguePlayer.Play(bond);
        }
        PauseForNpc(false); // 반응 대사와 친밀 대사가 끝났다

        ReservationData reservation = ReservationCall.Instance != null && Game != null ? Game.TakeReservationCall() : null;
        bool called = reservation != null;
        if (called)
        {
            changingCustomer = true;
            SetBubble(false);
            PanelMotion motion = character != null ? character.GetComponent<PanelMotion>() : null;
            if (character != null) PanelMotion.Close(character.gameObject);
            yield return GameTime.Wait((motion != null ? motion.Seconds : 0f) + customerSwapDelay);

            ReservationCall.Instance.Begin(reservation);
            yield return null;
            while (ReservationCall.Busy) yield return null;
            yield return GameTime.Wait(customerSwapDelay);
        }

        if (!TryBeginScriptedVisit(previousLeaves: !called)) StartCoroutine(NextCustomerRoutine(!called));
    }

    /// <summary>수락한 뒤 잠시 있다가 꽃 선택 페이지로 넘어간다(도착하면 돌아올 수 없다: OnSwipeArrived).</summary>
    private System.Collections.IEnumerator GoToFlowerSelectAfterDelay()
    {
        yield return new WaitForSeconds(acceptToSelectDelay);
        Sfx.Play(forcedSwipeSound, forcedSwipeVolume);
        if (swipe != null) swipe.MoveTo(1);
    }

    /// <summary>전등을 켜면 밤 전등 모습으로 바뀌고(빛은 토글의 켜짐 그림), 잠시 뒤 밤이 시작된다(부적을 붙인 꽃의 밤 손님이 찾아온다).</summary>
    private void OnLanternChanged(bool on)
    {
        if (!on || state != MainState.DayEnding) return;
        nightStarting = true;
        lantern.interactable = false; // 켠 뒤에는 다시 건드리지 못한다
        if (swipe != null) swipe.Enabled = false;
        lanternLight?.SetLit(true);
        Bgm.Stop();
        Sfx.Play(nightStartJingle, nightStartJingleVolume);
        StartCoroutine(StartNightAfterDelay());
    }

    private System.Collections.IEnumerator StartNightAfterDelay()
    {
        float blend = mainLook != null ? mainLook.PlayNightBlend() : 0f; // 전등을 켜면 노을 → 밤으로 확 섞이고, 다 섞인 뒤에 밤이 시작된다
        float jingle = nightStartJingle != null ? nightStartJingle.length : 0f; // 전등 소리가 끝날 때까지 기다린다
        yield return new WaitForSeconds(Mathf.Max(lanternDelay, blend, jingle));
        Game?.StartNight(shelf != null ? new List<int>(shelf.CharmedFlowerIds) : new List<int>());
    }

    /// <summary>마무리 대사를 한 손님: 잠시 후(또는 응답 버튼을 누르면 바로) 말풍선과 캐릭터가 사라진다.</summary>
    private void EndConversation()
    {
        leaving = true; // 버튼은 그대로 두고(색 유지), 누르면 바로 퇴장한다
        StartCoroutine(HideCustomerAfterDelay());
    }

    private System.Collections.IEnumerator HideCustomerAfterDelay()
    {
        yield return new WaitForSeconds(timeUpHideDelay);
        HideCustomer();
    }

    private void HideCustomer()
    {
        if (hidingCustomer) return;
        hidingCustomer = true;
        StopAllCoroutines();
        if (bubbleContainer != null) bubbleContainer.SetActive(false);
        StartCoroutine(HideCustomerRoutine());
    }

    /// <summary>손님은 페이드와 함께 사라지고(PanelMotion), 다 사라진 뒤에 다음 일을 한다.</summary>
    private System.Collections.IEnumerator HideCustomerRoutine()
    {
        float wait = 0f;
        foreach (GameObject root in characterRoots)
        {
            if (root == null || !root.activeSelf) continue;
            if (root.TryGetComponent(out PanelMotion motion)) wait = Mathf.Max(wait, motion.Seconds);
            PanelMotion.Close(root);
        }
        if (wait > 0f) yield return GameTime.Wait(wait);

        if (state == MainState.DayEnding) customerGone = true; // 낮 종료 손님이 사라졌으니 선반으로 스와이프하고, 전등을 켜서 밤을 시작할 수 있다(Update)
        if (state == MainState.NightEnding && GameManager.Instance != null) GameManager.Instance.EndNight(); // 손님이 사라지면 하루 결산 화면으로
    }
}
