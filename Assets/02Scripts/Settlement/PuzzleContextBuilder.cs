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
        int used = 0;
        if (requirement.type == CustomerRequirementGenerator.RequirementType.Color)
        {
            counts.colorCounts.TryGetValue(requirement.targetId, out used);
        }
        else
        {
            foreach (int flowerId in requirement.FlowerTargets()) // 말한 꽃마다 한 송이 이상: 쓴 꽃 종류 수가 요구(minCount)를 채워야 한다
            {
                if (counts.flowerCounts.TryGetValue(flowerId, out int n) && n > 0) used++;
            }
        }

        float mood = GameClock.Instance != null ? GameClock.Instance.MoodRatio : 1f;

        return new PuzzleContext(endKind, placedCells, specialCells, requirement, used, mood, collapseCount);
    }
}
