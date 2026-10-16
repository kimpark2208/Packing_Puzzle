using TMPro;

/// <summary>
/// 손님 말풍선에 대사를 띄운다(낮 메인/밤 메인 공용). 대사 문구와 "어떤 상황에 어떤 대사"를 여기서 정하고,
/// MainUI는 상황만 알려 준다. 주문 대사의 내용은 CustomerDialogue가 만든다.
/// </summary>
public class CustomerSpeech
{
    public const string DefaultForceAcceptLine = "그래요라고 하세요"; // 화난 손님에게 "이건아니지"를 눌렀을 때의 기본 대사

    private const string RejectLine = "거절할순없으세요";
    private const string TimeUpLine = "시간이 늦었네요 가봐야겠어요";
    private const string NightEndLine = "해가 뜨면 저는 죽어요!!";
    private const string NoOrderLine = "손님이 아직 정하지 못한 것 같아요...";

    private readonly TMP_Text bubbleText;
    private readonly TypewriterText typewriter; // 말풍선에 붙어 있으면 대사를 한 글자씩 보여 준다(없으면 한 번에)

    public CustomerSpeech(TMP_Text bubbleText)
    {
        this.bubbleText = bubbleText;
        typewriter = bubbleText != null ? bubbleText.GetComponent<TypewriterText>() : null;
    }

    public void Show(string line)
    {
        if (typewriter != null) typewriter.Play(line);
        else if (bubbleText != null) bubbleText.text = line;
    }


    /// <summary>오늘의 주문을 말한다(주문이 아직 없으면 그 사정을 말한다).</summary>
    public void ShowOrder(DayPuzzleGenerator.DayOrder order)
    {
        Show(order == null ? NoOrderLine : CustomerDialogue.OrderLine(order));
    }

    public void ShowReject() => Show(RejectLine);

    public void ShowTimeUp() => Show(TimeUpLine);

    public void ShowNightEnd() => Show(NightEndLine);

    /// <summary>밤 손님이 첫 퍼즐에 나올 꽃을 말한다.</summary>
    public void ShowNightCustomer(string flowerNames)
    {
        Show($"저기요… 제 꽃이 <b>{flowerNames}</b>인데요. 자꾸 시드네요. 이 꽃을 고쳐주시면 똑같이 만들어 드릴게요. 유령에게는 신비한 능력이 있거든요.");
    }
}
