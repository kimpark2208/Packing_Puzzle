using TMPro;

/// <summary>
/// 손님 말풍선에 대사를 띄운다. 대사 문구와 "어떤 상황에 어떤 대사"를 여기서 정하고,
/// MainUI는 상황만 알려 준다. 주문 대사의 내용은 주문(DayOrder.orderLine)이 이미 들고 있다.
/// </summary>
public class CustomerSpeech
{
    public const string DefaultForceAcceptLine = "그래요라고 하세요"; // 화난 손님에게 "이건아니지"를 눌렀을 때의 기본 대사

    private const string RejectLine = "거절할순없으세요";
    private const string NoOrderLine = "손님이 아직 정하지 못한 것 같아요...";

    private readonly TMP_Text bubbleText;
    private readonly TypewriterText typewriter; // 말풍선에 붙어 있으면 대사를 한 글자씩 보여 준다(없으면 한 번에)

    public CustomerSpeech(TMP_Text bubbleText)
    {
        this.bubbleText = bubbleText;
        typewriter = bubbleText != null ? bubbleText.GetComponent<TypewriterText>() : null;
    }

    /// <summary>대사가 한 글자씩 드러나는 중인가.</summary>
    public bool IsTyping => typewriter != null && typewriter.IsTyping;

    public void Show(string line)
    {
        if (typewriter != null) typewriter.Play(line);
        else if (bubbleText != null) bubbleText.text = line;
    }


    /// <summary>오늘의 주문을 말한다(주문이 아직 없으면 그 사정을 말한다).</summary>
    public void ShowOrder(DayPuzzleGenerator.DayOrder order)
    {
        Show(order == null ? NoOrderLine : order.orderLine);
    }

    public void ShowReject() => Show(RejectLine);

    /// <summary>"다시 여쭤볼게요": 누를 때마다 주문에 만들어 둔 되묻기 대사(1회, 2회)를 차례로 말한다. 더 없으면 거절 대사를 말한다.</summary>
    public void ShowReask(DayPuzzleGenerator.DayOrder order)
    {
        if (order != null && order.reaskIndex < order.reaskLines.Length) Show(order.reaskLines[order.reaskIndex++]);
        else ShowReject();
    }

}
