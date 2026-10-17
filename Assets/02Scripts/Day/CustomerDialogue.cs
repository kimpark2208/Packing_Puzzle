/// <summary>손님이 주문을 말하는 대사(표시 전용). 대사는 주문이 만들어질 때 OrderComposer가 한 번 정해 두고, 여기서는 그것을 돌려준다.</summary>
public static class CustomerDialogue
{
    public static string OrderLine(DayPuzzleGenerator.DayOrder order)
    {
        return string.IsNullOrEmpty(order.orderLine) ? "" : order.orderLine;
    }
}
