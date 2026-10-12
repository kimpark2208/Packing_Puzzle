using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인(손님 주문) 화면. 낮 손님/낮 종료/밤 손님/밤 종료를 이 씬 하나에서 보여 준다(MainState).
/// 손님 캐릭터 자리 + 말풍선(주문 힌트) + 응답 버튼 2개("다시 여쭤볼게요" / "포장 시작할게요") + 설정 팝업 + 카탈로그 팝업(전화 버튼으로 열림).
/// 낮 손님의 주문을 수락하면 응답 버튼이 사라지고, 잠시 뒤 옆 페이지의 꽃 선택(FlowerSelectUI)으로 자동으로 넘어간다(한 번 가면 돌아올 수 없다).
/// UI 요소는 전부 씬 하이라키에 미리 배치되어 있고, 이 스크립트는 인스펙터 참조 없이 이름으로 찾아서(SceneFind) 로직을 수행한다.
///
/// 어떤 상황인지는 GameManager가 씬을 열기 전에 정해 두므로(MainState, 오늘의 주문 CurrentDayOrder) Start()에서 스스로 읽어 온다.
/// 화면 모습은 같은 오브젝트의 MainLook이 바꾼다(낮 손님은 낮, 낮 종료는 노을, 밤 손님과 밤 종료는 밤).
/// 밤 손님을 만나는 중에 밤 시간이 끝나면 EventBus.OnNightTimeUp을 직접 받아 그 자리에서 밤 종료로 넘어간다(GameManager가 이 UI를 찾아 부르지 않는다).
/// </summary>
public class DayMainUI : MonoBehaviour
{
    // 씬에서 찾을 오브젝트 이름. 이름을 바꾸면 여기도 같이 바꾼다.
    private const string BubbleTextName = "BubbleText";
    private const string RejectButtonName = "RejectBTN";
    private const string AcceptButtonName = "AcceptBTN";
    private const string TelephoneName = "CounterTelephone"; // 낮/밤 모습이 같은 오브젝트 하나를 쓴다(MainLook이 그림만 바꾼다)
    private const string CatalogPopupName = "CatalogPopup";
    private const string BubbleContainerName = "BubbleContainer";
    private static readonly string[] CharacterRootNames = { "DayCharacterArea", "NightCharacterArea" };
    private const string LanternToggleName = "LanternToggle";
    private const string ShelfRootName = "ShelfRoot"; // 메인 옆 페이지: 낮 손님이면 꽃 선택, 낮 종료면 꽃 선반(부적)
    private const string CharmPillName = "CharmUse"; // 부적 화면에만 나오는 "부적 n / 6" 표시

    [SerializeField] private float timeUpHideDelay = 5f;
    [SerializeField] private float lanternDelay = 0.5f; // 전등을 켠 뒤 밤 요청 화면으로 넘어가기까지의 시간
    [SerializeField] private float acceptToSelectDelay = 1f; // 낮 주문을 수락한 뒤 꽃 선택 화면으로 자동으로 넘어가기까지의 시간(초)
    [SerializeField] private Sprite nightLanternSprite; // 전등을 켜면 바뀌는 밤 전등 모습

    private MainState state;
    private DayPuzzleGenerator.DayOrder order;
    private bool showingAngryLine; // 낮 손님: 이전 손님의 반응 대사 중
    private string forceAcceptLine = CustomerSpeech.DefaultForceAcceptLine;
    private CustomerSpeech speech; // 대사 출력은 이쪽에 맡기고, 여기서는 어떤 상황인지만 알린다
    private bool leaving; // 마무리 대사를 하고 퇴장하는 중(낮 종료/밤 종료)

    private GameObject catalogPopup;
    private GameObject bubbleContainer;
    private GameObject[] characterRoots;
    private Toggle lantern; // 켜면 밤이 시작된다(낮 종료 손님이 떠난 뒤, 카운터 화면에서만 켤 수 있다)
    private SwipeParallax swipe;     // 카운터 ↔ 옆 페이지 이동(낮 손님: 수락하면 꽃 선택으로 자동으로 한 방향, 낮 종료: 꽃 선반과 스와이프로 왕복)
    private NightRequestUI shelf;    // 꽃 선반: 부적을 붙인 꽃을 들고 있다(낮 종료)
    private GameObject rejectButton;
    private GameObject acceptButton;
    private bool customerGone;      // 낮 종료 손님이 떠났다(그제서야 스와이프와 등불이 열린다)
    private bool nightStarting;      // 등불을 켜서 밤으로 넘어가는 중

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
        state = GameManager.Instance != null ? GameManager.Instance.MainState : MainState.DayCustomer;
        order = GameManager.Instance != null ? GameManager.Instance.CurrentDayOrder : null;
        Initialize();
    }

    private void Initialize()
    {
        var scene = gameObject.scene;
        speech = new CustomerSpeech(SceneFind.One<TMP_Text>(scene, BubbleTextName));
        bubbleContainer = SceneFind.One<Transform>(scene, BubbleContainerName)?.gameObject;
        catalogPopup = SceneFind.One<Transform>(scene, CatalogPopupName)?.gameObject;
        characterRoots = System.Array.ConvertAll(CharacterRootNames, n => SceneFind.One<Transform>(scene, n)?.gameObject);

        Button reject = SceneFind.One<Button>(scene, RejectButtonName);
        Button accept = SceneFind.One<Button>(scene, AcceptButtonName);
        rejectButton = reject != null ? reject.gameObject : null;
        acceptButton = accept != null ? accept.gameObject : null;
        reject?.onClick.AddListener(OnRejectClicked);
        accept?.onClick.AddListener(OnAcceptClicked);

        GetComponent<MainLook>()?.Apply(state);

        // 옆 페이지(ShelfRoot)는 낮 손님(꽃 선택)과 낮 종료(꽃 선반)에만 켜 둔다(다른 상황에서는 스와이프로도 갈 수 없다). 둘은 같은 선반을 쓰므로 상황에 맞는 쪽만 켠다.
        GameObject shelfRoot = SceneFind.One<Transform>(scene, ShelfRootName)?.gameObject;
        bool hasSidePage = state == MainState.DayCustomer || state == MainState.DayEnding;
        if (shelfRoot != null) shelfRoot.SetActive(hasSidePage);
        shelf = shelfRoot != null ? shelfRoot.GetComponentInChildren<NightRequestUI>(true) : null;
        if (shelf != null) shelf.gameObject.SetActive(state == MainState.DayEnding);
        FlowerSelectUI flowerSelect = shelfRoot != null ? shelfRoot.GetComponentInChildren<FlowerSelectUI>(true) : null;
        if (flowerSelect != null) flowerSelect.gameObject.SetActive(state == MainState.DayCustomer);
        SceneFind.One<Transform>(scene, CharmPillName)?.gameObject.SetActive(state == MainState.DayEnding);
        swipe = FindFirstObjectByType<SwipeParallax>();
        if (swipe != null)
        {
            swipe.Enabled = false;
            swipe.Arrived += OnSwipeArrived;
        }

        lantern = SceneFind.One<Toggle>(scene, LanternToggleName);
        if (lantern != null)
        {
            lantern.gameObject.SetActive(state == MainState.DayEnding || IsNight); // 전등은 낮이 끝난 시점과 밤 동안에만 나타난다(낮 영업 중에는 숨긴다)
            lantern.SetIsOnWithoutNotify(false);
            lantern.interactable = false; // 낮 종료 손님이 떠난 뒤에야 켤 수 있다(HideCustomer). 밤에는 켜진 모습을 보여 주기만 한다
            if (IsNight && nightLanternSprite != null && lantern.targetGraphic is Image nightImage) nightImage.sprite = nightLanternSprite;
            lantern.onValueChanged.AddListener(OnLanternChanged);
        }

        // 전화기를 누르면 카탈로그가 열리고 닫힌다. 팝업의 닫기 버튼도 스스로 끄므로 열림 상태를 따로 들고 있지 않는다.
        if (catalogPopup != null)
        {
            foreach (Button telephone in SceneFind.All<Button>(scene, TelephoneName)) telephone.onClick.AddListener(() => catalogPopup.SetActive(!catalogPopup.activeSelf));
            catalogPopup.SetActive(false);
        }

        switch (state)
        {
            case MainState.DayCustomer: BeginDayCustomer(); break;
            case MainState.DayEnding: BeginDayEnd(); break;
            case MainState.NightCustomer: BeginNightCustomer(); break;
            case MainState.NightEnding: BeginNightEnd(); break;
        }
    }

    private void Update()
    {
        if (state != MainState.DayEnding || nightStarting) return;

        // 낮 종료가 되면 손님이 아직 말하는 중이어도 스와이프로 선반을 오갈 수 있다(카탈로그가 열려 있는 동안은 막는다).
        // 등불은 낮 종료 손님이 떠난 뒤, 카운터 화면에 있을 때만 켤 수 있다.
        if (swipe != null) swipe.Enabled = !(catalogPopup != null && catalogPopup.activeSelf);
        if (lantern != null) lantern.interactable = customerGone && (swipe == null || swipe.IsAtHome);
    }

    /// <summary>꽃 선택 페이지에 도착하면 카운터로 돌아올 수 없다(주문은 이미 수락했다).</summary>
    private void OnSwipeArrived(int page)
    {
        if (state == MainState.DayCustomer && page >= 1) swipe.LowestPage = page;
    }

    /// <summary>낮 손님: 주문을 말한다. 이전 손님의 반응 대사가 있으면 그것부터 말한다.</summary>
    private void BeginDayCustomer()
    {
        string angryLine = GameManager.Instance != null ? GameManager.Instance.ConsumePendingCustomerLine() : null;
        showingAngryLine = !string.IsNullOrEmpty(angryLine);
        if (showingAngryLine)
        {
            speech.Show(angryLine);
            forceAcceptLine = GameManager.Instance != null
                ? GameManager.Instance.ConsumePendingRejectResponseLine()
                : CustomerSpeech.DefaultForceAcceptLine;
        }
        else
        {
            speech.ShowOrder(order);
            GameClock.Instance?.ResetMood(); // 새 손님: 기분 시간은 가득 차 있고, 주문을 수락해야 줄어들기 시작한다
        }
        // 이전 손님의 반응 대사 중이면 기분 시간은 끝난 그대로 멈춰 둔다(다음 손님이 올 때 채운다: OnAcceptClicked)
    }

    /// <summary>낮 종료: 하루 시간이 끝났다. 손님이 마무리 대사를 한 뒤 잠시 후(또는 응답 버튼을 누르면) 사라지고, 사라지면 전등을 켤 수 있다.</summary>
    private void BeginDayEnd()
    {
        if (GameManager.Instance != null) // 이전 손님의 반응 대사가 남아 있으면 지운다(다음 날로 넘어가지 않게)
        {
            GameManager.Instance.ConsumePendingCustomerLine();
            GameManager.Instance.ConsumePendingRejectResponseLine();
        }
        speech.ShowTimeUp();
        EndConversation();
    }

    /// <summary>밤 손님: 지금 풀 퍼즐에 나올 꽃을 말하고, 수락하면 밤 퍼즐로 간다. 풀 퍼즐이 더 없으면 밤을 끝낸다.</summary>
    private void BeginNightCustomer()
    {
        string flowers = GameManager.Instance != null ? GameManager.Instance.NightCustomerFlowerNames() : null;
        if (flowers == null)
        {
            if (GameManager.Instance != null) GameManager.Instance.EndNight();
            return;
        }
        speech.ShowNightCustomer(flowers);
    }

    /// <summary>밤 종료: 밤 시간이 끝났다. 손님이 "해가 뜨면 저는 죽어요!!"라고 외치고, 낮 종료와 같은 방식으로 사라진다. 사라지면 하루 결산으로 간다.</summary>
    private void BeginNightEnd()
    {
        state = MainState.NightEnding;
        speech.ShowNightEnd();
        EndConversation();
    }

    /// <summary>밤 손님을 만나는 중에 밤 시간이 끝났다: 그 자리에서 밤 종료로 넘어간다.</summary>
    private void OnNightTimeUp()
    {
        if (IsNight) BeginNightEnd();
    }

    private void OnRejectClicked()
    {
        if (leaving) { HideCustomer(); return; }
        if (showingAngryLine) speech.Show(forceAcceptLine);
        else speech.ShowReject();
    }

    private void OnAcceptClicked()
    {
        if (leaving) { HideCustomer(); return; }
        if (state == MainState.NightCustomer)
        {
            if (GameManager.Instance != null) GameManager.Instance.StartNightPuzzle();
            return;
        }
        if (showingAngryLine)
        {
            showingAngryLine = false;
            speech.ShowOrder(order);
            GameClock.Instance?.ResetMood(); // 반응 대사가 끝나고 다음 손님이 왔다: 기분 시간이 가득 찬다(수락하면 줄어들기 시작한다)
            return;
        }

        GameClock.Instance?.StartMood(); // 주문을 수락하는 순간부터 손님 기분 시간이 줄어든다
        if (GameManager.Instance != null) GameManager.Instance.AcceptOrder();
        if (rejectButton != null) rejectButton.SetActive(false); // 수락한 뒤에는 응답 버튼이 필요 없다
        if (acceptButton != null) acceptButton.SetActive(false);
        StartCoroutine(GoToFlowerSelectAfterDelay());
    }

    /// <summary>수락한 뒤 잠시 있다가 꽃 선택 페이지로 넘어간다(도착하면 돌아올 수 없다: OnSwipeArrived).</summary>
    private System.Collections.IEnumerator GoToFlowerSelectAfterDelay()
    {
        yield return new WaitForSeconds(acceptToSelectDelay);
        if (swipe != null) swipe.MoveTo(1);
    }

    /// <summary>전등을 켜면 밤 전등 모습으로 바뀌고(빛은 토글의 켜짐 그림), 잠시 뒤 밤이 시작된다(부적을 붙인 꽃의 밤 손님이 찾아온다).</summary>
    private void OnLanternChanged(bool on)
    {
        if (!on || state != MainState.DayEnding) return;
        nightStarting = true;
        lantern.interactable = false; // 켠 뒤에는 다시 건드리지 못한다
        if (swipe != null) swipe.Enabled = false;
        if (nightLanternSprite != null && lantern.targetGraphic is Image lanternImage) lanternImage.sprite = nightLanternSprite;
        StartCoroutine(StartNightAfterDelay());
    }

    private System.Collections.IEnumerator StartNightAfterDelay()
    {
        yield return new WaitForSeconds(lanternDelay);
        if (GameManager.Instance != null) GameManager.Instance.StartNight(shelf != null ? new List<int>(shelf.CharmedFlowerIds) : new List<int>());
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
        StopAllCoroutines();
        if (bubbleContainer != null) bubbleContainer.SetActive(false);
        foreach (GameObject root in characterRoots) if (root != null) root.SetActive(false);

        if (state == MainState.DayEnding) customerGone = true; // 낮 종료 손님이 사라졌으니 선반으로 스와이프하고, 전등을 켜서 밤을 시작할 수 있다(Update)
        if (state == MainState.NightEnding && GameManager.Instance != null) GameManager.Instance.EndNight(); // 손님이 사라지면 하루 결산 화면으로
    }
}
