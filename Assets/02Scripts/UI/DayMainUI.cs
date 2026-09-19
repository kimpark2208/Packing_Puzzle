using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 낮 메인(손님 주문) 화면. 좋은피자 위대한피자 스타일의 대사창 UI를 참고했다:
/// 손님 캐릭터 자리 + 말풍선(주문 힌트) + 응답 버튼 2개("다시 여쭤볼게요" / "포장 시작할게요").
/// 포장지 힌트가 모호하게 나온 날은 최대 2번까지 되물어서 점점 구체적인 힌트로 바꿀 수 있다.
/// 모든 UI는 이미지 없이 색상으로만 구분한다.
/// </summary>
public class DayMainUI : MonoBehaviour
{
    private const int MaxAsks = 2;

    private static readonly Color BgColor = new(0.98f, 0.93f, 0.90f);
    private static readonly Color BubbleColor = new(1f, 1f, 1f);
    private static readonly Color PortraitColor = new(0.85f, 0.75f, 0.80f);
    private static readonly Color AskButtonColor = new(0.88f, 0.88f, 0.88f);
    private static readonly Color AcceptButtonColor = new(0.95f, 0.55f, 0.55f);

    private DayPuzzleGenerator.DayOrder order;
    private int hintLevel;
    private int asksUsed;
    private Text bubbleText;
    private Button askButton;

    private void Start()
    {
        order = GameFlowController.Instance != null ? GameFlowController.Instance.CurrentDayOrder : null;
        hintLevel = (order != null && order.hintStartsVague) ? 0 : 2;
        asksUsed = 0;

        Canvas canvas = UIFactory.EnsureCanvas();
        RectTransform root = UIFactory.CreateFullStretchPanel("DayMainRoot", canvas.transform, BgColor);

        BuildTopBar(root);
        BuildPortrait(root);
        BuildSpeechBubble(root);
        BuildResponseButtons(root);

        RefreshBubbleText();
    }

    private void BuildTopBar(Transform parent)
    {
        var currency = CurrencyManager.Instance;
        string dayText = currency != null ? $"{currency.CurrentDay}일째" : "1일째";
        string moneyText = currency != null ? $"{currency.CurrentMoney}원" : "0원";

        RectTransform bar = CreateBox(parent, new Vector2(0, 0.92f), new Vector2(1, 1f));
        UIFactory.CreateText(bar, $"{dayText}        소지금 {moneyText}", 26, Color.black, TextAnchor.MiddleLeft);
    }

    private void BuildPortrait(Transform parent)
    {
        RectTransform portrait = CreateBox(parent, new Vector2(0.68f, 0.45f), new Vector2(0.95f, 0.88f));
        var img = portrait.gameObject.AddComponent<Image>();
        img.color = PortraitColor;
        UIFactory.CreateText(portrait, "손님", 24, Color.black);
    }

    private void BuildSpeechBubble(Transform parent)
    {
        RectTransform bubble = CreateBox(parent, new Vector2(0.05f, 0.45f), new Vector2(0.63f, 0.88f));
        var img = bubble.gameObject.AddComponent<Image>();
        img.color = BubbleColor;
        bubbleText = UIFactory.CreateText(bubble, "", 24, Color.black);
    }

    private void BuildResponseButtons(Transform parent)
    {
        askButton = UIFactory.CreateButton(parent, "다시 여쭤볼게요", AskButtonColor, Color.black);
        var askRect = (RectTransform)askButton.transform;
        askRect.anchorMin = new Vector2(0.05f, 0.28f);
        askRect.anchorMax = new Vector2(0.47f, 0.40f);
        askRect.offsetMin = Vector2.zero;
        askRect.offsetMax = Vector2.zero;
        askButton.onClick.AddListener(OnAskAgainClicked);

        Button acceptButton = UIFactory.CreateButton(parent, "포장 시작할게요", AcceptButtonColor, Color.white);
        var acceptRect = (RectTransform)acceptButton.transform;
        acceptRect.anchorMin = new Vector2(0.53f, 0.28f);
        acceptRect.anchorMax = new Vector2(0.95f, 0.40f);
        acceptRect.offsetMin = Vector2.zero;
        acceptRect.offsetMax = Vector2.zero;
        acceptButton.onClick.AddListener(() => GameFlowController.Instance.GoToFlowerSelect());
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

    private static RectTransform CreateBox(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject("Box", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }
}
