/// <summary>보드·주문·손님 기분 시계에서 값을 읽어 PuzzleContext를 만드는 유일한 접점.</summary>
public static class PuzzleContextBuilder
{
    public static PuzzleContext Build(PuzzleEndKind endKind, int collapseCount)
    {
        var board = DayPuzzleController.Instance;
        var placedCells = board != null
            ? board.PlacedCells()
            : new System.Collections.Generic.Dictionary<WrapperRegionPosition, FlowerData.Color>();

        var specialCells = board != null
            ? board.SpecialCells()
            : new System.Collections.Generic.Dictionary<WrapperRegionPosition, SpecialCell>();

        var requirement = CurrencyManager.Instance != null ? CurrencyManager.Instance.CurrentRequirement : default;
        var counts = PuzzleCounter.CollectCounts();
        var usedCounts = requirement.type == CustomerRequirementGenerator.RequirementType.Color ? counts.colorCounts : counts.flowerCounts;
        usedCounts.TryGetValue(requirement.targetId, out int used);

        float mood = GameClock.Instance != null ? GameClock.Instance.MoodRatio : 1f;

        return new PuzzleContext(endKind, placedCells, specialCells, requirement, used, mood, collapseCount);
    }
}
