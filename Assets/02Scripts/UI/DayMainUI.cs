using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 낮 메인(손님 주문) 화면. 손님 캐릭터 자리 + 말풍선(주문 힌트) + 응답 버튼 2개
/// ("다시 여쭤볼게요" / "포장 시작할게요") + 설정 팝업 + 카탈로그 팝업(전화 버튼으로 열림).
/// UI 요소는 전부 씬 하이라키에 미리 배치되어 있고, 이 스크립트는 참조만 들고 로직을 수행한다.
///
/// 첫째 날은 GameFlowController.Start()가 BeginNewDay()를 호출하면서 씬 로드까지 같은 프레임에
/// 동기적으로 발생시키기 때문에, 이 스크립트의 Start()가 CurrentDayOrder를 읽는 시점이
/// GameFlowController보다 먼저일 수도, 나중일 수도 있다(레이스). NightBoardController 때와 동일한
/// 문제라서 같은 해법을 쓴다: GameFlowController.OnSceneLoaded가 Initialize()를 직접 호출해서
/// 데이터를 넣어주고, Start()는 그게 아직 안 됐을 때만 스스로 채우는 폴백 역할만 한다.
/// </summary>
public class DayMainUI : MonoBehaviour
{
    private enum CatalogTab { Artifacts, Wrapper, Furniture }

    private const string RejectLine = "거절할순없으세요";
    private const string DefaultForceAcceptLine = "그래요라고 하세요";
    private const string TimeUpLine = "시간이 늦었네요 가봐야겠어요";
    private const string NightEndLine = "해가 뜨면 저는 죽어요!!";

    [SerializeField] private float TimeUpHideDelay = 5f;

    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text bubbleText;
    [SerializeField] private Button askButton;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button lanternButton;

    [Header("설정 팝업")]
    [SerializeField] private Button preferencesButton;
    [SerializeField] private GameObject preferencesPopup;
    [SerializeField] private Button preferencesCloseButton;

    [Header("카탈로그 팝업 (전화 버튼으로 열고 닫기)")]
    [SerializeField] private Button telephoneButton;
    [SerializeField] private GameObject catalogPopup;
    [SerializeField] private GameObject artifactsPage;
    [SerializeField] private GameObject wrapperPage;
    [SerializeField] private GameObject furniturePage;

    [Header("하루 시간 종료 연출")]
    [SerializeField] private GameObject bubbleContainer;
    [SerializeField] private GameObject characterRoot;
    [SerializeField] private Button artifactsIndexButton;
    [SerializeField] private Button wrapperIndexButton;
    [SerializeField] private Button furnitureIndexButton;

    [Header("하루 시간이 끝났을 때 낮 모습으로 바꿀 이미지 (밤 메인에서만 지정)")]
    [SerializeField] private Image characterImage;
    [SerializeField] private Sprite dayCharacterSprite;
    [SerializeField] private Image lanternImage;
    [SerializeField] private Sprite dayLanternSprite;
    [SerializeField] private float lanternDelay = 0.5f; // 전등을 누른 뒤 밤 요청 화면으로 넘어가기까지의 시간

    private Sprite nightLanternSprite; // 전등이 씬에 놓인 모습(밤). 낮 모습으로 바꾼 전등을 누르면 이걸로 되돌린다

    private DayPuzzleGenerator.DayOrder order;
    private bool initialized;
    private bool catalogOpen;
    private bool showingAngryLine;
    private string forceAcceptLine = DefaultForceAcceptLine;
    private bool closing; // 하루 시간이 끝나 손님이 퇴장하는 중
    private bool nightMode; // 밤 메인에서 밤 손님을 만나는 중(수락하면 밤 퍼즐로)
    private bool nightEnding; // 밤 시간이 끝나 밤 손님이 외치고 떠나는 중(사라지면 낮으로)

    public void Initialize(DayPuzzleGenerator.DayOrder dayOrder)
    {
        if (initialized) return;
        initialized = true;

        order = dayOrder;

        var currency = CurrencyManager.Instance;
        if (moneyText != null) moneyText.text = currency != null ? $"{currency.CurrentMoney}원" : "0원";

        if (askButton != null) askButton.onClick.AddListener(OnRejectClicked);
        if (acceptButton != null) acceptButton.onClick.AddListener(OnAcceptClicked);

        if (lanternButton != null)
        {
            lanternButton.interactable = false; // 하루 시간이 끝나고 손님이 사라진 뒤에야 누를 수 있다(HideCustomer)
            if (lanternImage != null) nightLanternSprite = lanternImage.sprite; // 씬에 놓인 모습(밤 전등)을 기억해 둔다
            lanternButton.onClick.AddListener(() =>
            {
                if (nightMode || nightEnding) return; // 밤 손님을 만나는 중에는 전등으로 밤을 다시 시작하지 않는다
                StartCoroutine(LightLanternThenProceed());
            });
        }

        if (preferencesButton != null) preferencesButton.onClick.AddListener(OpenPreferences);
        if (preferencesCloseButton != null) preferencesCloseButton.onClick.AddListener(ClosePreferences);
        if (preferencesPopup != null) preferencesPopup.SetActive(false);

        if (telephoneButton != null) telephoneButton.onClick.AddListener(ToggleCatalog);
        if (artifactsIndexButton != null) artifactsIndexButton.onClick.AddListener(() => SwitchCatalogTab(CatalogTab.Artifacts));
        if (wrapperIndexButton != null) wrapperIndexButton.onClick.AddListener(() => SwitchCatalogTab(CatalogTab.Wrapper));
        if (furnitureIndexButton != null) furnitureIndexButton.onClick.AddListener(() => SwitchCatalogTab(CatalogTab.Furniture));
        catalogOpen = false;
        if (catalogPopup != null) catalogPopup.SetActive(false);
        SwitchCatalogTab(CatalogTab.Wrapper);

        // 밤 시간이 끝나서 온 것이면: 마지막 손님이 외치고 떠난다(낮 시간 종료 때와 같은 연출).
        if (GameFlowController.Instance != null && GameFlowController.Instance.ConsumeNightEnd())
        {
            BeginNightEnd();
            return;
        }

        // 밤 요청을 마치고 온 밤 손님: 첫 퍼즐에 나올 꽃을 말하고, 수락하면 밤 퍼즐로 간다.
        string nightFlowers = GameFlowController.Instance != null ? GameFlowController.Instance.ConsumeNightCustomerFlowers() : null;
        if (nightFlowers != null)
        {
            nightMode = true;
            if (bubbleText != null)
            {
                bubbleText.text = $"저기요… 제 꽃이 <b>{nightFlowers}</b>인데요. 자꾸 시드네요. 이 꽃을 고쳐주시면 똑같이 만들어 드릴게요. 유령에게는 신비한 능력이 있거든요.";
            }
            return;
        }

        string angryLine = GameFlowController.Instance != null ? GameFlowController.Instance.ConsumePendingCustomerLine() : null;
        showingAngryLine = !string.IsNullOrEmpty(angryLine);
        if (showingAngryLine)
        {
            if (bubbleText != null) bubbleText.text = angryLine;
            forceAcceptLine = GameFlowController.Instance != null
                ? GameFlowController.Instance.ConsumePendingRejectResponseLine()
                : DefaultForceAcceptLine;
        }
        else
        {
            RefreshBubbleText();
        }

        bool timeUp = GameFlowController.Instance != null && GameFlowController.Instance.ConsumeDayTimeUp();
        if (timeUp)
        {
            BeginDayEnd();
        }
        else if (!showingAngryLine)
        {
            DayClock.Instance?.StartMood(); // 반응 대사 없이 바로 새 주문이 시작된다
        }
    }

    private void Start()
    {
        if (!initialized)
        {
            Initialize(GameFlowController.Instance != null ? GameFlowController.Instance.CurrentDayOrder : null);
        }
    }

    private void OnRejectClicked()
    {
        if (closing) { HideCustomer(); return; }
        if (bubbleText == null) return;
        bubbleText.text = showingAngryLine ? forceAcceptLine : RejectLine;
    }

    private void OnAcceptClicked()
    {
        if (closing) { HideCustomer(); return; }
        if (nightMode)
        {
            if (GameFlowController.Instance != null) GameFlowController.Instance.StartNightPuzzle();
            return;
        }
        if (showingAngryLine)
        {
            showingAngryLine = false;
            DayClock.Instance?.StartMood(); // 반응 대사가 끝나고 다음 주문이 시작되는 순간 기분 시간이 가득 찬다
            RefreshBubbleText();
            return;
        }

        if (GameFlowController.Instance != null) GameFlowController.Instance.GoToFlowerSelect();
    }

    /// <summary>전등을 누르면 밤 전등 모습으로 바뀌고, 잠시 뒤 밤 요청(부적) 화면으로 넘어간다.</summary>
    private System.Collections.IEnumerator LightLanternThenProceed()
    {
        lanternButton.interactable = false; // 기다리는 동안 다시 누르지 못한다
        if (lanternImage != null && nightLanternSprite != null) lanternImage.sprite = nightLanternSprite;

        yield return new WaitForSeconds(lanternDelay);
        if (GameFlowController.Instance != null) GameFlowController.Instance.ProceedToNightMain();
    }

    /// <summary>하루 시간이 끝났을 때: 아직 낮이므로 캐릭터와 전등을 낮 모습으로 바꾸고, 손님이 마무리 대사를 한 뒤 잠시 후(또는 응답 버튼을 누르면) 사라진다. 사라지면 전등을 누를 수 있다.</summary>
    public void BeginDayEnd()
    {
        if (closing) return;

        if (characterImage != null && dayCharacterSprite != null) characterImage.sprite = dayCharacterSprite;
        if (lanternImage != null && dayLanternSprite != null) lanternImage.sprite = dayLanternSprite;

        if (bubbleText != null) bubbleText.text = TimeUpLine;
        EndConversation();
    }

    /// <summary>밤 시간이 끝났을 때: 손님이 "해가 뜨면 저는 죽어요!!"라고 외치고, 낮 시간 종료 때와 같은 방식(응답 버튼을 누르거나 시간이 지나면)으로 사라진다. 사라지면 낮으로 넘어간다.</summary>
    public void BeginNightEnd()
    {
        nightMode = false;
        nightEnding = true;
        if (bubbleText != null) bubbleText.text = NightEndLine;
        EndConversation();
    }

    /// <summary>하루 시간이 끝난 손님: 대사를 한 뒤 잠시 후(또는 응답 버튼을 누르면 바로) 말풍선과 캐릭터가 사라지고 전등만 남는다.</summary>
    private void EndConversation()
    {
        closing = true; // 버튼은 그대로 두고(색 유지), 누르면 바로 퇴장한다
        StartCoroutine(HideCustomerAfterDelay());
    }

    private System.Collections.IEnumerator HideCustomerAfterDelay()
    {
        yield return new WaitForSeconds(TimeUpHideDelay);
        HideCustomer();
    }

    private void HideCustomer()
    {
        StopAllCoroutines();
        if (bubbleContainer != null) bubbleContainer.SetActive(false);
        if (characterRoot != null) characterRoot.SetActive(false);
        if (lanternButton != null && !nightEnding)
        {
            lanternButton.interactable = true; // 하루 시간 종료 손님이 사라졌으니 전등으로 밤을 시작할 수 있다
            TutorialOverlay.Play("night-start", new TutorialOverlay.Step((RectTransform)lanternButton.transform,
                $"해가 지면 {TutorialOverlay.Em("밤 영업")}이 시작돼요.\n손전등에 불이 켜지면 {TutorialOverlay.Em("특별한 손님")}들이 찾아와요."));
        }

        if (nightEnding && GameFlowController.Instance != null) GameFlowController.Instance.EndNight(); // 손님이 사라지면 하루 결산 화면으로
    }

    private void RefreshBubbleText()
    {
        if (bubbleText == null) return;

        if (order == null)
        {
            bubbleText.text = "손님이 아직 정하지 못한 것 같아요...";
            return;
        }

        bubbleText.text = CustomerDialogue.OrderLine(order);
    }

    private void OpenPreferences()
    {
        DayClock.Instance?.SetPaused(true);
        if (preferencesPopup != null) preferencesPopup.SetActive(true);
    }

    private void ClosePreferences()
    {
        DayClock.Instance?.SetPaused(false);
        if (preferencesPopup != null) preferencesPopup.SetActive(false);
    }

    private void ToggleCatalog()
    {
        catalogOpen = !catalogOpen;
        if (catalogPopup != null) catalogPopup.SetActive(catalogOpen);
    }

    private void SwitchCatalogTab(CatalogTab tab)
    {
        if (artifactsPage != null) artifactsPage.SetActive(tab == CatalogTab.Artifacts);
        if (wrapperPage != null) wrapperPage.SetActive(tab == CatalogTab.Wrapper);
        if (furniturePage != null) furniturePage.SetActive(tab == CatalogTab.Furniture);
    }
}
