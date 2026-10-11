using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 낮 메인(손님 주문) 화면. 손님 캐릭터 자리 + 말풍선(주문 힌트) + 응답 버튼 2개
/// ("다시 여쭤볼게요" / "포장 시작할게요") + 설정 팝업 + 카탈로그 팝업(전화 버튼으로 열림).
/// UI 요소는 전부 씬 하이라키에 미리 배치되어 있고, 이 스크립트는 참조만 들고 로직을 수행한다.
///
/// 오늘의 주문(CurrentDayOrder)은 GameManager가 씬을 로드하기 전에 이미 정해 두므로, Start()에서 스스로 읽어 온다.
/// 밤 시간이 끝나면 EventBus.OnNightTimeUp을 직접 받아 밤 손님의 마지막 대사를 처리한다(GameManager가 이 UI를 찾아 부르지 않는다).
/// </summary>
public class DayMainUI : MonoBehaviour
{
    [SerializeField] private float timeUpHideDelay = 5f;

    [SerializeField] private TMP_Text bubbleText;
    [SerializeField] private Button askButton;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button lanternButton;

    [Header("카탈로그 팝업 (전화 버튼으로 열고 닫기)")]
    [SerializeField] private Button telephoneButton;
    [SerializeField] private GameObject catalogPopup;

    [Header("하루 시간 종료 연출")]
    [SerializeField] private GameObject bubbleContainer;
    [SerializeField] private GameObject characterRoot;

    [Header("하루 시간이 끝났을 때 낮 모습으로 바꿀 이미지 (밤 메인에서만 지정)")]
    [SerializeField] private Image characterImage;
    [SerializeField] private Sprite dayCharacterSprite;
    [SerializeField] private Image lanternImage;
    [SerializeField] private Sprite dayLanternSprite;
    [SerializeField] private float lanternDelay = 0.5f; // 전등을 누른 뒤 밤 요청 화면으로 넘어가기까지의 시간

    private Sprite nightLanternSprite; // 전등이 씬에 놓인 모습(밤). 낮 모습으로 바꾼 전등을 누르면 이걸로 되돌린다

    private DayPuzzleGenerator.DayOrder order;
    private bool showingAngryLine;
    private string forceAcceptLine = CustomerSpeech.DefaultForceAcceptLine;
    private CustomerSpeech speech; // 대사 출력은 이쪽에 맡기고, 여기서는 어떤 상황인지만 알린다
    private CustomerSpeech Speech => speech ??= new CustomerSpeech(bubbleText);
    private bool closing; // 하루 시간이 끝나 손님이 퇴장하는 중
    private bool nightMode; // 밤 메인에서 밤 손님을 만나는 중(수락하면 밤 퍼즐로)
    private bool nightEnding; // 밤 시간이 끝나 밤 손님이 외치고 떠나는 중(사라지면 낮으로)

    private void Initialize(DayPuzzleGenerator.DayOrder dayOrder)
    {
        order = dayOrder;

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

        if (telephoneButton != null && catalogPopup != null) telephoneButton.onClick.AddListener(() => catalogPopup.SetActive(!catalogPopup.activeSelf)); // 팝업의 닫기 버튼도 스스로 끄므로 열림 상태를 따로 들고 있지 않는다
        if (catalogPopup != null) catalogPopup.SetActive(false);

        // 밤 시간이 끝나서 온 것이면: 마지막 손님이 외치고 떠난다(낮 시간 종료 때와 같은 연출).
        if (GameManager.Instance != null && GameManager.Instance.ConsumeNightEnd())
        {
            BeginNightEnd();
            return;
        }

        // 밤 요청을 마치고 온 밤 손님: 첫 퍼즐에 나올 꽃을 말하고, 수락하면 밤 퍼즐로 간다.
        string nightFlowers = GameManager.Instance != null ? GameManager.Instance.ConsumeNightCustomerFlowers() : null;
        if (nightFlowers != null)
        {
            nightMode = true;
            Speech.ShowNightCustomer(nightFlowers);
            return;
        }

        string angryLine = GameManager.Instance != null ? GameManager.Instance.ConsumePendingCustomerLine() : null;
        showingAngryLine = !string.IsNullOrEmpty(angryLine);
        if (showingAngryLine)
        {
            Speech.Show(angryLine);
            forceAcceptLine = GameManager.Instance != null
                ? GameManager.Instance.ConsumePendingRejectResponseLine()
                : CustomerSpeech.DefaultForceAcceptLine;
        }
        else
        {
            Speech.ShowOrder(order);
        }

        bool timeUp = GameManager.Instance != null && GameManager.Instance.ConsumeDayTimeUp();
        if (timeUp)
        {
            BeginDayEnd();
        }
        else
        {
            GameClock.Instance?.ResetMood(); // 새 손님: 기분 시간은 가득 차 있고, 주문을 수락해야 줄어들기 시작한다
        }
    }

    private void OnEnable()
    {
        EventBus.OnNightTimeUp += BeginNightEnd;
    }

    private void OnDisable()
    {
        EventBus.OnNightTimeUp -= BeginNightEnd;
    }

    private void Start()
    {
        Initialize(GameManager.Instance != null ? GameManager.Instance.CurrentDayOrder : null);
    }

    private void OnRejectClicked()
    {
        if (closing) { HideCustomer(); return; }
        if (showingAngryLine) Speech.Show(forceAcceptLine);
        else Speech.ShowReject();
    }

    private void OnAcceptClicked()
    {
        if (closing) { HideCustomer(); return; }
        if (nightMode)
        {
            if (GameManager.Instance != null) GameManager.Instance.StartNightPuzzle();
            return;
        }
        if (showingAngryLine)
        {
            showingAngryLine = false;
            Speech.ShowOrder(order);
            return;
        }

        GameClock.Instance?.StartMood(); // 주문을 수락하는 순간부터 손님 기분 시간이 줄어든다
        if (GameManager.Instance != null) GameManager.Instance.GoToFlowerSelect();
    }

    /// <summary>전등을 누르면 밤 전등 모습으로 바뀌고, 잠시 뒤 밤 요청(부적) 화면으로 넘어간다.</summary>
    private System.Collections.IEnumerator LightLanternThenProceed()
    {
        lanternButton.interactable = false; // 기다리는 동안 다시 누르지 못한다
        if (lanternImage != null && nightLanternSprite != null) lanternImage.sprite = nightLanternSprite;

        yield return new WaitForSeconds(lanternDelay);
        if (GameManager.Instance != null) GameManager.Instance.ProceedToNightMain();
    }

    /// <summary>하루 시간이 끝났을 때: 아직 낮이므로 캐릭터와 전등을 낮 모습으로 바꾸고, 손님이 마무리 대사를 한 뒤 잠시 후(또는 응답 버튼을 누르면) 사라진다. 사라지면 전등을 누를 수 있다.</summary>
    public void BeginDayEnd()
    {
        if (closing) return;

        if (characterImage != null && dayCharacterSprite != null) characterImage.sprite = dayCharacterSprite;
        if (lanternImage != null && dayLanternSprite != null) lanternImage.sprite = dayLanternSprite;

        Speech.ShowTimeUp();
        EndConversation();
    }

    /// <summary>밤 시간이 끝났을 때: 손님이 "해가 뜨면 저는 죽어요!!"라고 외치고, 낮 시간 종료 때와 같은 방식(응답 버튼을 누르거나 시간이 지나면)으로 사라진다. 사라지면 낮으로 넘어간다.</summary>
    private void BeginNightEnd()
    {
        nightMode = false;
        nightEnding = true;
        Speech.ShowNightEnd();
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
        yield return new WaitForSeconds(timeUpHideDelay);
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
        }

        if (nightEnding && GameManager.Instance != null) GameManager.Instance.EndNight(); // 손님이 사라지면 하루 결산 화면으로
    }
}
