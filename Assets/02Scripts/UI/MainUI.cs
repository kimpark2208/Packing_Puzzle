using System.Collections.Generic;
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
    private bool leaving; // 마무리 대사를 하고 퇴장하는 중(낮 종료/밤 종료)

    private GameObject catalogPopup;
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
        Bgm.Play(state == MainState.DayCustomer ? dayBgm : state == MainState.DayEnding ? dayEndBgm : nightBgm, bgmVolume);

        SetUpSidePage();
        SetUpLantern();
        SetUpCatalog();
        if (IsNight) FindFirstObjectByType<OrderPopup>(FindObjectsInactive.Include)?.SetTabVisible(false); // 주문서는 낮 손님의 주문이라 밤에는 숨긴다

        switch (state)
        {
            case MainState.DayCustomer: BeginDayCustomer(); break;
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
        characterRoots = System.Array.ConvertAll(SceneObjectNames.CharacterRoots, n => SceneFind.One<Transform>(scene, n)?.gameObject);
        character = SceneFind.One<CharacterGenerator>(scene, SceneObjectNames.DayCharacter);

        Button reject = SceneFind.One<Button>(scene, SceneObjectNames.RejectButton);
        Button accept = SceneFind.One<Button>(scene, SceneObjectNames.AcceptButton);
        rejectButton = reject != null ? reject.gameObject : null;
        acceptButton = accept != null ? accept.gameObject : null;
        reject?.onClick.AddListener(OnRejectClicked);
        accept?.onClick.AddListener(OnAcceptClicked);
    }

    /// <summary>옆 페이지(꽃 선택/꽃 선반)와 스와이프를 상황에 맞게 준비한다.</summary>
    private void SetUpSidePage()
    {
        var scene = gameObject.scene;
        // 옆 페이지(ShelfRoot)는 낮 손님(꽃 선택)과 낮 종료(꽃 선반)에만 켜 둔다(다른 상황에서는 스와이프로도 갈 수 없다). 둘은 같은 선반을 쓰므로 상황에 맞는 쪽만 켠다.
        GameObject shelfRoot = SceneFind.One<Transform>(scene, SceneObjectNames.ShelfRoot)?.gameObject;
        bool hasSidePage = state == MainState.DayCustomer || state == MainState.DayEnding;
        if (shelfRoot != null) shelfRoot.SetActive(hasSidePage);
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

    /// <summary>전화기를 누르면 카탈로그가 열리고 닫히게 잇는다. 팝업의 닫기 버튼도 스스로 끄므로 열림 상태를 따로 들고 있지 않는다.</summary>
    private void SetUpCatalog()
    {
        if (catalogPopup == null) return;

        foreach (Button telephone in SceneFind.All<Button>(gameObject.scene, SceneObjectNames.Telephone)) telephone.onClick.AddListener(ToggleCatalog);
        catalogPopup.SetActive(false);
    }

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

    /// <summary>전화기를 누르면 카탈로그가 아래에서 올라오며 열리고, 다시 누르면 반대로 내려가며 닫힌다.</summary>
    private void ToggleCatalog()
    {
        if (catalogPopup.activeSelf) PanelMotion.Close(catalogPopup);
        else catalogPopup.SetActive(true);
    }

    private void Update()
    {
        if (state != MainState.DayEnding || nightStarting) return;

        // 낮 종료가 되면 손님이 아직 말하는 중이어도 스와이프로 선반을 오갈 수 있다(카탈로그가 열려 있는 동안은 막는다).
        // 등불은 낮 종료 손님이 떠난 뒤, 카운터 화면에 있을 때만 켤 수 있고, 선반(부적) 화면으로 가면 사라진다.
        if (swipe != null) swipe.Enabled = !(catalogPopup != null && catalogPopup.activeSelf);
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

    /// <summary>낮 손님: 주문을 말한다. 이전 손님의 반응 대사가 있으면 그것부터 말한다.</summary>
    private void BeginDayCustomer()
    {
        string angryLine = Game != null ? Game.ConsumePendingCustomerLine() : null;
        showingReaction = !string.IsNullOrEmpty(angryLine);
        if (showingReaction)
        {
            ShowPreviousCustomer(); // 반응 대사는 방금 주문을 한 그 손님이 한다
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

    /// <summary>새 손님이 왔다: 무작위 캐릭터로 바꾸고, 반응 대사와 낮 종료 대사를 같은 손님이 하도록 기억해 둔다.</summary>
    private void ShowNewCustomer(bool arrive = false)
    {
        if (character == null) return;

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
            yield return new WaitForSecondsRealtime(seconds + customerSwapDelay); // 나가는 모션이 끝나고 잠시 쉰 뒤 새 손님이 들어온다
        }

        ShowNewCustomer(arrive: true);
        yield return new WaitForSecondsRealtime(seconds + 0.1f); // 들어오는 모션은 한 프레임 뒤에 시작한다

        SetBubble(true);
        if (IsNight)
        {
            speech.ShowNightCustomer(Game != null ? Game.NightCustomerFlowerName() : null);
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
            ShowPreviousCustomer();
            SayAfterReveal(() => speech.Show(reaction));
            forceAcceptLine = Game.ConsumePendingRejectResponseLine();
            return;
        }

        if (Game == null || Game.NightCustomerFlowerName() == null)
        {
            Game?.EndNight();
            return;
        }
        StartCoroutine(NextCustomerRoutine(false));
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
        if (changingCustomer) return;
        if (leaving) { HideCustomer(); return; }
        if (showingReaction) speech.Show(forceAcceptLine);
        else speech.ShowReask(order);
    }

    private void OnAcceptClicked()
    {
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
            if (Game != null && Game.NightCustomerFlowerName() != null)
            {
                StartCoroutine(NextCustomerRoutine(true));
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
            StartCoroutine(NextCustomerRoutine(true)); // 반응 대사가 끝났으니 이 손님이 나가고 다음 손님이 온다(기분 시간은 말풍선이 뜰 때 가득 찬다)
            return;
        }

        GameClock.Instance?.StartMood(); // 주문을 수락하는 순간부터 손님 기분 시간이 줄어든다
        Game?.AcceptOrder();
        SetAnswerButtons(false); // 수락한 뒤에는 응답 버튼이 필요 없다
        StartCoroutine(GoToFlowerSelectAfterDelay());
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
        if (wait > 0f) yield return new WaitForSecondsRealtime(wait);

        if (state == MainState.DayEnding) customerGone = true; // 낮 종료 손님이 사라졌으니 선반으로 스와이프하고, 전등을 켜서 밤을 시작할 수 있다(Update)
        if (state == MainState.NightEnding && GameManager.Instance != null) GameManager.Instance.EndNight(); // 손님이 사라지면 하루 결산 화면으로
    }
}
