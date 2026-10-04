using System.Linq;

/// <summary>보드·주문·손님 기분 시계에서 값을 읽어 PuzzleContext를 만드는 유일한 접점.</summary>
public static class PuzzleContextBuilder
{
    public static PuzzleContext Build(PuzzleEndKind endKind, int collapseCount)
    {
        var board = WrapperBoardController.Instance;
        var placedColors = board != null
            ? board.PlacementHistory.Select(f => f.color).ToList()
            : new System.Collections.Generic.List<FlowerData.Color>();

        var requirement = CurrencyManager.Instance != null ? CurrencyManager.Instance.CurrentRequirement : default;
        var counts = PuzzleCounter.CollectCounts();
        var usedCounts = requirement.type == CustomerRequirementGenerator.RequirementType.Color ? counts.colorCounts : counts.flowerCounts;
        usedCounts.TryGetValue(requirement.targetId, out int used);

        float mood = DayClock.Instance != null ? DayClock.Instance.MoodRatio : 1f;

        return new PuzzleContext(endKind, placedColors, requirement, used, mood, collapseCount);
    }
}
