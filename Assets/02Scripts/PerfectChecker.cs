using System.Collections.Generic;

/// <summary>
/// 그리드에 배치된 블록들을 모아서, 모양별(blockID별) 개수가 레시피와 정확히 일치하는지 판정한다.
/// 칸의 위치나 배치 형태는 보지 않고, "어떤 모양이 몇 개 쓰였는가"만 비교한다.
/// 레시피는 DayPuzzleGenerator가 매일 절차적으로 생성하므로 런타임에 인자로 전달받는다.
/// </summary>
public static class PerfectChecker
{
    public struct CheckResult
    {
        public bool isPerfect;
        public Dictionary<int, int> requiredCounts;   // blockID -> 필요 개수
        public Dictionary<int, int> actualCounts;     // blockID -> 실제 개수
    }

    public static CheckResult CheckPerfect(GridManager grid, PuzzleRecipe recipe)
    {
        var result = new CheckResult
        {
            requiredCounts = recipe != null ? recipe.BuildRequiredCountMap() : new Dictionary<int, int>(),
            actualCounts = CountPlacedBlocksByID(grid)
        };

        result.isPerfect = recipe != null && result.requiredCounts.Count > 0 && AreCountsEqual(result.requiredCounts, result.actualCounts);
        return result;
    }

    private static Dictionary<int, int> CountPlacedBlocksByID(GridManager grid)
    {
        var counts = new Dictionary<int, int>();
        if (grid == null) return counts;

        BlockDrag[] placedBlocks = grid.GetComponentsInChildren<BlockDrag>();
        foreach (var block in placedBlocks)
        {
            if (block == null || block.blockData == null) continue;

            int id = block.blockData.blockID;
            if (!counts.ContainsKey(id)) counts[id] = 0;
            counts[id]++;
        }

        return counts;
    }

    private static bool AreCountsEqual(Dictionary<int, int> required, Dictionary<int, int> actual)
    {
        var allKeys = new HashSet<int>(required.Keys);
        allKeys.UnionWith(actual.Keys);

        foreach (int key in allKeys)
        {
            int req = required.TryGetValue(key, out int r) ? r : 0;
            int act = actual.TryGetValue(key, out int a) ? a : 0;
            if (req != act) return false;
        }

        return true;
    }
}
