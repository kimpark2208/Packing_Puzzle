/// <summary>손님이 주문을 말하는 대사를 만든다(표시 전용). 주문에 따라 달라지는 값은 굵게 보여 준다.</summary>
public static class CustomerDialogue
{
    public static string OrderLine(DayPuzzleGenerator.DayOrder order)
    {
        var req = order.requirement;
        string wrapper = $"<b>{order.hintColorName}색</b>";

        if (req.type == CustomerRequirementGenerator.RequirementType.Color)
        {
            string color = $"<b>{ColorPalette.ToKoreanName((FlowerData.Color)req.targetId)}색</b>";
            return $"친구 생일이에요. 친구가… {color} 꽃을 좋아했던가? {color} 꽃 <b>{req.minCount}개</b>는 꼭 넣어주세요. 포장지는 {wrapper}이 좋겠어요!";
        }

        FlowerData flower = BlockRegistry.Instance != null ? BlockRegistry.Instance.GetById(req.targetId) : null;
        string name = flower != null && !string.IsNullOrEmpty(flower.flowerName) ? flower.flowerName : $"꽃 {req.targetId}";
        string count = req.minCount == 1 ? "하나" : $"{req.minCount}개";
        return $"고백을 하려고요. 사랑을 뜻하는 <b>{name}</b>{KoreanParticle.IGa(name)} <b>{count}</b>는 들어가야겠죠! 포장지는 {wrapper}으로 부탁드려요.";
    }
}
