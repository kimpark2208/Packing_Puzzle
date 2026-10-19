using TMPro;

/// <summary>
/// 손님 말풍선에 대사를 띄운다(낮 메인/밤 메인 공용). 대사 문구와 "어떤 상황에 어떤 대사"를 여기서 정하고,
/// MainUI는 상황만 알려 준다. 주문 대사의 내용은 주문(DayOrder.orderLine)이 이미 들고 있다.
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

    public void ShowTimeUp() => Show(TimeUpLine);

    public void ShowNightEnd() => Show(NightEndLine);

    /// <summary>시나리오가 새 꽃을 처음 얻게 해 주는 밤 손님(1·2일차 모브 등)의 대사(둘 중 하나를 무작위로).</summary>
    private static readonly string[] NewFlowerLines =
    {
        "저기요... 제가 여기로 이사온지 얼마 안됐는데... 제 꽃이 이 근처에서는 수리하는 곳이 없는 것 같더라고요. [꽃이름]이에요. 고쳐주실 수 있나요?",
        "저기... 처음 뵙겠습니다. 눈을 떠 보니 곁에 [꽃이름]이 있었는데, 어째 시들시들하네요. 고쳐 주실 수 있나요?",
    };

    /// <summary>밤 손님이 자기 꽃(flowerName) 이야기를 한다. newFlower면 새 꽃을 얻는 손님의 대사, 아니면 평소 밤 손님의 대사.</summary>
    public void ShowNightCustomer(string flowerName, bool newFlower = false)
    {
        if (!newFlower)
        {
            Show($"저기요… 제 꽃이 <b>{flowerName}</b>인데요. 자꾸 시드네요. 이 꽃을 고쳐주시면 똑같이 만들어 드릴게요. 유령에게는 신비한 능력이 있거든요.");
            return;
        }

        string line = NewFlowerLines[UnityEngine.Random.Range(0, NewFlowerLines.Length)];
        Show(KoreanParticle.Replace(line, "꽃이름", flowerName, $"<b>{flowerName}</b>"));
    }
}
