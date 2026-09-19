using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 낮 메인(손님 주문) 화면. 좋은피자 위대한피자 스타일의 대사창 UI를 참고했다:
/// 손님 캐릭터 자리 + 말풍선(주문 힌트) + 응답 버튼 2개("다시 여쭤볼게요" / "포장 시작할게요").
/// 포장지 힌트가 모호하게 나온 날은 최대 2번까지 되물어서 점점 구체적인 힌트로 바꿀 수 있다.
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
    private const int MaxAsks = 2;

    [SerializeField] private Text topBarText;
    [SerializeField] private Text bubbleText;
    [SerializeField] private Button askButton;
    [SerializeField] private Button acceptButton;

    private DayPuzzleGenerator.DayOrder order;
    private int hintLevel;
    private int asksUsed;
    private bool initialized;

    public void Initialize(DayPuzzleGenerator.DayOrder dayOrder)
    {
        if (initialized) return;
        initialized = true;

        order = dayOrder;
        hintLevel = (order != null && order.hintStartsVague) ? 0 : 2;
        asksUsed = 0;

        var currency = CurrencyManager.Instance;
        string dayText = currency != null ? $"{currency.CurrentDay}일째" : "1일째";
        string moneyText = currency != null ? $"{currency.CurrentMoney}원" : "0원";
        topBarText.text = $"{dayText}        소지금 {moneyText}";

        askButton.onClick.AddListener(OnAskAgainClicked);
        acceptButton.onClick.AddListener(() => GameFlowController.Instance.GoToFlowerSelect());

        RefreshBubbleText();
    }

    private void Start()
    {
        if (!initialized)
        {
            Initialize(GameFlowController.Instance != null ? GameFlowController.Instance.CurrentDayOrder : null);
        }
    }

    private void OnAskAgainClicked()
    {
        if (asksUsed >= MaxAsks || hintLevel >= 2) return;

        asksUsed++;
        hintLevel = Mathf.Min(2, hintLevel + 1);
        RefreshBubbleText();
    }

    private void RefreshBubbleText()
    {
        if (order == null)
        {
            bubbleText.text = "손님이 아직 정하지 못한 것 같아요...";
            return;
        }

        string wrapperPhrase = hintLevel switch
        {
            0 => "",
            1 => order.hintSizeWord + " ",
            _ => order.hintColorName + "색 "
        };

        bubbleText.text = $"{wrapperPhrase}포장지에 {order.requirement.description}";

        bool canAskMore = asksUsed < MaxAsks && hintLevel < 2;
        askButton.interactable = canAskMore;
    }
}
