using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 꽃잎 결손 퍼즐 생성. 꽃마다 정해진 격자 모양(region) 안에서 막힌 칸(벽)을 뭉치게 뽑고, 남은 칸을 블록 두 종으로 빈틈없이 채울 수 있는지 확인한다.
/// 블록은 큰 것 하나와 작은 것 하나를 고른다. 막힌 칸 수와 뭉치는 정도는 Settings로 조절한다.
/// 어떤 블록으로도 덮을 수 없는 칸(좁은 틈 등)은 저절로 막힌 칸이 되고, 그 수도 막힌 칸 수에 들어간다.
/// 채울 수 있는지는 레거시 밤 퍼즐 생성기(ProceduralNightPuzzleGenerator)의 DLX 검사를 그대로 쓴다. 유일해는 따지지 않고, 두 블록을 모두 한 번 이상 써서 채울 수만 있으면 된다.
/// </summary>
public static class PetalLossPuzzleGenerator
{
    [System.Serializable]
    public class Settings
    {
        [Min(0), Tooltip("막힌 칸 수의 목표(어떤 블록으로도 못 덮는 칸이 저절로 막히는 것까지 포함).")] public int wallCount = 10;
        [Min(0), Tooltip("막힌 칸 수가 목표에서 이만큼까지 벗어나도 된다(칸 수가 블록 크기로 나눠떨어져야 해서 목표 그대로는 안 될 때가 있다).")] public int wallCountTolerance = 2;
        [Min(0f), Tooltip("막힌 칸이 뭉치는 정도. 0이면 완전히 무작위이고, 클수록 이미 막힌 칸의 이웃(대각선 포함)이 막히기 쉽다. 이웃한 막힌 칸이 하나 늘 때마다 뽑힐 가중치가 이만큼 더해진다.")] public float clusterWeight = 4f;
        [Min(2), Tooltip("이 칸 수 이상이면 큰 블록, 미만이면 작은 블록으로 나눈다.")] public int bigBlockMinCells = 4;
    }

    public class Puzzle
    {
        public bool[,] region;          // 격자 모양(칸이 있는 자리)
        public bool[,] walls;           // 막힌 칸
        public List<BlockData> blocks;  // 이번 퍼즐의 블록(큰 것, 작은 것 순서)

        /// <summary>그릴 수 없는 칸(칸이 없거나 막힌 칸).</summary>
        public bool[,] Unavailable() => PetalLossPuzzleGenerator.Unavailable(region, walls);
    }

    private const int BlockPickAttempts = 6;       // 칸을 못 채우는 블록 조합이면 블록을 다시 고르는 횟수
    private const int WallAttemptsPerCount = 150;  // 막힌 칸 수 하나당 배치를 뽑아 보는 횟수
    private const long TimeBudgetMs = 600;         // 퍼즐 하나를 만드는 데 쓸 수 있는 최대 시간(메인 스레드 멈춤 방지)

    /// <summary>region 모양 안에서 퍼즐을 만든다. 블록이 모자라면 null.</summary>
    public static Puzzle Generate(bool[,] region, IReadOnlyList<BlockData> allBlocks, Settings settings)
    {
        int size = region.GetLength(0);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        List<BlockData> blocks = null;

        for (int pick = 0; pick < BlockPickAttempts && stopwatch.ElapsedMilliseconds < TimeBudgetMs; pick++)
        {
            blocks = PickBlocks(allBlocks, settings.bigBlockMinCells);
            if (blocks == null) return null;

            bool[,] forced = ForcedWalls(region, blocks);
            Puzzle puzzle = SearchWalls(region, forced, blocks, settings, stopwatch.ElapsedMilliseconds + TimeBudgetMs / BlockPickAttempts, stopwatch);
            if (puzzle != null) return puzzle;
        }

        Debug.LogWarning("[PetalLossPuzzleGenerator] 막힌 칸 배치를 무작위로 못 찾아, 블록을 직접 놓아 보는 예비 방법으로 만듭니다(막힌 칸 수가 목표와 다를 수 있습니다).");
        return BuildFallback(region, blocks ?? PickBlocks(allBlocks, settings.bigBlockMinCells));
    }

    /// <summary>큰 블록 하나와 작은 블록 하나를 고른다. 어느 한쪽이 없으면 아무 블록 둘을 고른다. 블록이 둘보다 적으면 null.</summary>
    public static List<BlockData> PickBlocks(IReadOnlyList<BlockData> allBlocks, int bigMinCells)
    {
        var valid = allBlocks.Where(b => b != null && b.CellCount > 0).ToList();
        if (valid.Count < 2) return null;

        var big = valid.Where(b => b.CellCount >= bigMinCells).ToList();
        var small = valid.Where(b => b.CellCount < bigMinCells).ToList();
        if (big.Count > 0 && small.Count > 0) return new List<BlockData> { big[Random.Range(0, big.Count)], small[Random.Range(0, small.Count)] };

        return valid.OrderBy(_ => Random.value).Take(2).ToList();
    }

    /// <summary>region 안에서 blocks 어느 것으로도 덮을 수 없는 칸(덮는 칸이 막히면 또 덮을 수 없게 되는 칸까지)을 찾아 막힌 칸으로 돌려준다.</summary>
    private static bool[,] ForcedWalls(bool[,] region, List<BlockData> blocks)
    {
        int size = region.GetLength(0);
        var variants = blocks.SelectMany(b => PolyominoUtil.GetUniqueVariants(b)).ToList();
        var forced = new bool[size, size];

        bool changed = true;
        while (changed)
        {
            changed = false;
            var coverable = new bool[size, size];
            foreach (HashSet<Vector2Int> variant in variants)
            {
                for (int row = 0; row < size; row++)
                {
                    for (int col = 0; col < size; col++)
                    {
                        if (!variant.All(v => IsOpen(region, forced, col + v.x, row + v.y))) continue;
                        foreach (Vector2Int v in variant) coverable[row + v.y, col + v.x] = true;
                    }
                }
            }

            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                {
                    if (!region[row, col] || forced[row, col] || coverable[row, col]) continue;
                    forced[row, col] = true;
                    changed = true;
                }
            }
        }
        return forced;
    }

    private static bool IsOpen(bool[,] region, bool[,] walls, int col, int row)
    {
        int size = region.GetLength(0);
        return col >= 0 && col < size && row >= 0 && row < size && region[row, col] && !walls[row, col];
    }

    /// <summary>목표에 가까운 막힌 칸 수부터, 뭉치게 뽑은 막힌 칸 배치로 두 블록을 모두 써서 채울 수 있는지 살핀다. 찾으면 퍼즐, 못 찾으면(또는 시간이 다 되면) null.</summary>
    private static Puzzle SearchWalls(bool[,] region, bool[,] forced, List<BlockData> blocks, Settings settings, long deadline, System.Diagnostics.Stopwatch stopwatch)
    {
        int size = region.GetLength(0);
        int regionCells = Count(region);
        int forcedCount = Count(forced);
        int mandatory = blocks.Sum(b => b.CellCount);
        var cellCounts = blocks.Select(b => b.CellCount).Distinct().ToList();

        int low = Mathf.Max(forcedCount, settings.wallCount - settings.wallCountTolerance);
        int high = settings.wallCount + settings.wallCountTolerance;
        var counts = Enumerable.Range(low, Mathf.Max(0, high - low + 1))
            .Where(w => w <= regionCells && ProceduralNightPuzzleGenerator.CanFillUsingAll(regionCells - w, mandatory, cellCounts))
            .OrderBy(w => Mathf.Abs(w - settings.wallCount) + Random.value * 0.5f)
            .ToList();

        // 목표 범위 안에서 블록 칸 수로 나눠떨어지는 수가 없으면(또는 막혀야 하는 칸이 이미 더 많으면) 범위 밖에서 가장 가까운 수를 쓴다
        if (counts.Count == 0)
        {
            counts = Enumerable.Range(forcedCount, regionCells - forcedCount + 1)
                .Where(w => ProceduralNightPuzzleGenerator.CanFillUsingAll(regionCells - w, mandatory, cellCounts))
                .OrderBy(w => Mathf.Abs(w - settings.wallCount))
                .Take(2)
                .ToList();
        }

        foreach (int wallTotal in counts)
        {
            for (int attempt = 0; attempt < WallAttemptsPerCount; attempt++)
            {
                if (stopwatch.ElapsedMilliseconds > deadline) return null;

                bool[,] walls = PickClusteredWalls(region, forced, wallTotal - forcedCount, settings.clusterWeight);
                if (!ProceduralNightPuzzleGenerator.HasTilingUsingAll(size, blocks, Unavailable(region, walls))) continue;

                return new Puzzle { region = region, walls = walls, blocks = blocks };
            }
        }
        return null;
    }

    /// <summary>forced(이미 막힌 칸)에 더해 extra칸을 더 막는다. 첫 칸은 무작위이고, 그다음부터는 이미 막힌 칸의 이웃(8방향)일수록 뽑힐 가중치가 크다.</summary>
    private static bool[,] PickClusteredWalls(bool[,] region, bool[,] forced, int extra, float clusterWeight)
    {
        int size = region.GetLength(0);
        var walls = (bool[,])forced.Clone();

        for (int i = 0; i < extra; i++)
        {
            var candidates = new List<Vector2Int>();
            var weights = new List<float>();
            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                {
                    if (!region[row, col] || walls[row, col]) continue;

                    candidates.Add(new Vector2Int(col, row));
                    weights.Add(1f + clusterWeight * WallNeighbors(walls, col, row));
                }
            }
            if (candidates.Count == 0) break;

            float roll = Random.value * weights.Sum();
            int chosen = 0;
            for (; chosen < candidates.Count - 1; chosen++)
            {
                roll -= weights[chosen];
                if (roll <= 0f) break;
            }
            walls[candidates[chosen].y, candidates[chosen].x] = true;
        }
        return walls;
    }

    private static int WallNeighbors(bool[,] walls, int col, int row)
    {
        int size = walls.GetLength(0);
        int count = 0;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;

                int c = col + dx, r = row + dy;
                if (c >= 0 && c < size && r >= 0 && r < size && walls[r, c]) count++;
            }
        }
        return count;
    }

    /// <summary>그릴 수 없는 칸 표(칸이 없거나 막힌 칸이 true).</summary>
    public static bool[,] Unavailable(bool[,] region, bool[,] walls)
    {
        int size = region.GetLength(0);
        var result = new bool[size, size];
        for (int row = 0; row < size; row++)
            for (int col = 0; col < size; col++)
                result[row, col] = !region[row, col] || walls[row, col];
        return result;
    }

    /// <summary>예비 방법: 블록을 한 종씩 먼저 놓고, 남은 칸에 어느 블록이든 들어가는 대로 놓아 본 뒤, 못 채운 칸을 막힌 칸으로 한다. 직접 놓아 본 배치라 반드시 풀 수 있다.</summary>
    private static Puzzle BuildFallback(bool[,] region, List<BlockData> blocks)
    {
        int size = region.GetLength(0);
        bool[,] covered = Unavailable(region, new bool[size, size]);
        var variantsByBlock = blocks.Select(b => PolyominoUtil.GetUniqueVariants(b).OrderBy(_ => Random.value).ToList()).ToList();

        var cells = Enumerable.Range(0, size * size).OrderBy(_ => Random.value).ToList();
        foreach (var variants in variantsByBlock)
        {
            foreach (int index in cells)
                if (TryPlace(covered, variants, index % size, index / size)) break;
        }

        var all = variantsByBlock.SelectMany(v => v).OrderBy(_ => Random.value).ToList();
        foreach (int index in cells) TryPlace(covered, all, index % size, index / size);

        var walls = new bool[size, size];
        for (int row = 0; row < size; row++)
            for (int col = 0; col < size; col++)
                walls[row, col] = region[row, col] && !covered[row, col];

        return new Puzzle { region = region, walls = walls, blocks = blocks };
    }

    /// <summary>variants 중 하나를 (col, row) 칸이 덮이도록 놓아 본다. 놓았으면 true.</summary>
    private static bool TryPlace(bool[,] covered, List<HashSet<Vector2Int>> variants, int col, int row)
    {
        int size = covered.GetLength(0);
        if (covered[row, col]) return false;

        foreach (HashSet<Vector2Int> variant in variants)
        {
            foreach (Vector2Int anchor in variant)
            {
                Vector2Int origin = new Vector2Int(col - anchor.x, row - anchor.y);
                bool fits = variant.All(v =>
                {
                    int c = origin.x + v.x, r = origin.y + v.y;
                    return c >= 0 && c < size && r >= 0 && r < size && !covered[r, c];
                });
                if (!fits) continue;

                foreach (Vector2Int v in variant) covered[origin.y + v.y, origin.x + v.x] = true;
                return true;
            }
        }
        return false;
    }

    private static int Count(bool[,] grid)
    {
        int count = 0;
        foreach (bool cell in grid) if (cell) count++;
        return count;
    }
}
