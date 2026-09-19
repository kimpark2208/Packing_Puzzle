using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 절차적 밤 퍼즐 생성 (진짜 Dancing Links/Algorithm X 기반).
/// 밤 퍼즐은 플레이어가 직접 셀을 그려서 채우는 방식이므로(NightBoardController),
/// 이 생성기는 "정답 배치"를 미리 계산해서 넣어주는 것이 아니라
/// 주어진 꽃 조합만으로 보드를 빈틈없이 유일하게 채울 수 있는지를 검증하고,
/// 불가능하면 최소한의 칸을 벽(필러)으로 막아 나머지가 유일하게 채워지도록 만든다.
/// </summary>
public static class ProceduralNightPuzzleGenerator
{
    private static int nextPuzzleId = 1;
    private const int WallSearchAttemptsPerCount = 40;
    private const long TimeBudgetMs = 700; // 스테이지 하나당 벽 탐색에 쓸 수 있는 최대 시간 (메인 스레드 프리징 방지)
    private static readonly System.Random Rng = new();

    /// <summary>
    /// 하루 밤에 플레이할 퍼즐 큐를 만든다.
    /// 요청한 꽃이 있으면 메인 스테이지 1개를 최우선으로 넣고, 남는 시간은 보유 꽃 조합의 잉여 스테이지로 채운다.
    /// </summary>
    public static List<NightPuzzleData> GenerateNightQueue(int gridSize, int[] requestedFlowerIds, List<int> obtainedFlowerIds, int bonusStageCount)
    {
        var queue = new List<NightPuzzleData>();

        if (requestedFlowerIds != null && requestedFlowerIds.Length > 0)
        {
            queue.Add(GenerateStage(gridSize, requestedFlowerIds, isBonusStage: false));
        }

        for (int i = 0; i < bonusStageCount; i++)
        {
            int comboSize = Mathf.Clamp(Rng.Next(2, 4), 1, Mathf.Max(1, obtainedFlowerIds.Count));
            int[] combo = PickRandomDistinct(obtainedFlowerIds, comboSize);
            if (combo.Length == 0) break;

            queue.Add(GenerateStage(gridSize, combo, isBonusStage: true));
        }

        Debug.Log($"[ProceduralNightPuzzleGenerator] 밤 퍼즐 {queue.Count}개 생성 완료");
        return queue;
    }

    private static NightPuzzleData GenerateStage(int gridSize, int[] flowerIds, bool isBonusStage)
    {
        var blocks = flowerIds
            .Select(id => BlockDatabase.Instance != null ? BlockDatabase.Instance.GetById(id) : null)
            .Where(b => b != null)
            .ToList();

        bool[,] wall = new bool[gridSize, gridSize];

        if (blocks.Count == 0)
        {
            Debug.LogWarning("[ProceduralNightPuzzleGenerator] 사용할 블록 데이터가 없어 스테이지를 생성할 수 없습니다.");
            return MakeData(gridSize, flowerIds, isBonusStage, wall);
        }

        var cellCounts = blocks.Select(b => b.CellCount).Where(c => c > 0).Distinct().ToList();
        int totalCells = gridSize * gridSize;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 칸 수 조합으로 애초에 나눠떨어지지 않으면(예: 5x5=25칸을 3칸짜리 I자 하나로만 못 채움)
        // DLX를 아예 돌리지 않는다 - 이 사전 필터가 없으면 절대 못 찾을 답을 수백 번 재시도하며 멈춰버린다.
        if (CanReachExactSum(totalCells, cellCounts) && HasUniqueExactTiling(gridSize, blocks, wall))
        {
            return MakeData(gridSize, flowerIds, isBonusStage, wall);
        }

        // 꽃 조합만으로 유일하게 완전히 채울 수 없다면 최소한의 칸을 벽(=임의의 필러)으로 막아본다.
        // 벽은 꽃 블록과 무관하게 아무 크기나 될 수 있으므로 1칸 단위로 늘려가며 시도한다.
        int maxWallCells = Mathf.Min(totalCells / 2, 20);

        for (int wallCellCount = 1; wallCellCount <= maxWallCells; wallCellCount++)
        {
            if (stopwatch.ElapsedMilliseconds > TimeBudgetMs) break;

            int remaining = totalCells - wallCellCount;
            if (!CanReachExactSum(remaining, cellCounts)) continue; // 이 필러 개수로도 나눠떨어지지 않으면 스킵

            if (TryFindWallPlacement(gridSize, blocks, wallCellCount, stopwatch, out bool[,] foundWall))
            {
                wall = foundWall;
                Debug.Log($"[ProceduralNightPuzzleGenerator] 필러 {wallCellCount}칸으로 유일해 확보");
                return MakeData(gridSize, flowerIds, isBonusStage, wall);
            }
        }

        Debug.LogWarning("[ProceduralNightPuzzleGenerator] 유일해를 찾지 못해 필러 없이 스테이지를 반환합니다 (완전 타일링 불가능할 수 있음).");
        return MakeData(gridSize, flowerIds, isBonusStage, wall);
    }

    /// <summary>target을 cellCounts 값들의 음이 아닌 정수 조합(중복 사용 가능)으로 만들 수 있는지 (동전 교환 DP).</summary>
    private static bool CanReachExactSum(int target, List<int> cellCounts)
    {
        if (target < 0) return false;
        if (target == 0) return true;
        if (cellCounts == null || cellCounts.Count == 0) return false;

        var reachable = new bool[target + 1];
        reachable[0] = true;

        for (int s = 1; s <= target; s++)
        {
            foreach (int c in cellCounts)
            {
                if (c <= s && reachable[s - c])
                {
                    reachable[s] = true;
                    break;
                }
            }
        }

        return reachable[target];
    }

    private static bool TryFindWallPlacement(int gridSize, List<BlockData> blocks, int wallCellCount, System.Diagnostics.Stopwatch stopwatch, out bool[,] wall)
    {
        int totalCells = gridSize * gridSize;

        for (int attempt = 0; attempt < WallSearchAttemptsPerCount; attempt++)
        {
            if (stopwatch.ElapsedMilliseconds > TimeBudgetMs) break;

            var wallIndices = SampleRandomDistinctIndices(totalCells, wallCellCount);
            bool[,] candidate = new bool[gridSize, gridSize];
            foreach (int idx in wallIndices)
            {
                candidate[idx / gridSize, idx % gridSize] = true;
            }

            if (HasUniqueExactTiling(gridSize, blocks, candidate))
            {
                wall = candidate;
                return true;
            }
        }

        wall = null;
        return false;
    }

    /// <summary>
    /// Dancing Links로 "벽이 아닌 모든 칸을 blocks의 회전/반전 변형만으로, 겹침 없이 정확히 한 번씩" 덮는
    /// 방법이 정확히 하나만 존재하는지 검증한다. (0개=불가능, 2개 이상=유일하지 않음 → 둘 다 실패로 취급)
    /// </summary>
    private static bool HasUniqueExactTiling(int gridSize, List<BlockData> blocks, bool[,] wallGrid)
    {
        var dlx = BuildExactCoverInstance(gridSize, blocks, wallGrid, out int primaryColumnCount);
        if (primaryColumnCount == 0) return false;
        return dlx.CountSolutions(2) == 1;
    }

    private static DancingLinks BuildExactCoverInstance(int gridSize, List<BlockData> blocks, bool[,] wallGrid, out int primaryColumnCount)
    {
        var cellToColumn = new Dictionary<int, int>();
        int columnIndex = 0;

        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                if (wallGrid[r, c]) continue;
                cellToColumn[r * gridSize + c] = columnIndex++;
            }
        }

        primaryColumnCount = columnIndex;
        var dlx = new DancingLinks(primaryColumnCount, primaryColumnCount);

        int rowId = 0;
        foreach (BlockData block in blocks)
        {
            foreach (HashSet<Vector2Int> variant in PolyominoUtil.GetUniqueVariants(block))
            {
                int maxRowOffset = variant.Max(v => v.y);
                int maxColOffset = variant.Max(v => v.x);

                for (int startRow = 0; startRow + maxRowOffset < gridSize; startRow++)
                {
                    for (int startCol = 0; startCol + maxColOffset < gridSize; startCol++)
                    {
                        var columns = new List<int>();
                        bool fits = true;

                        foreach (Vector2Int offset in variant)
                        {
                            int rr = startRow + offset.y;
                            int cc = startCol + offset.x;

                            if (wallGrid[rr, cc]) { fits = false; break; }
                            columns.Add(cellToColumn[rr * gridSize + cc]);
                        }

                        if (fits)
                        {
                            dlx.AddRow(rowId++, columns);
                        }
                    }
                }
            }
        }

        return dlx;
    }

    private static NightPuzzleData MakeData(int gridSize, int[] flowerIds, bool isBonusStage, bool[,] wall)
    {
        return new NightPuzzleData
        {
            puzzleId = nextPuzzleId++,
            gridSize = gridSize,
            requiredFlowerIds = flowerIds,
            isBonusStage = isBonusStage,
            wallGrid = wall
        };
    }

    private static int[] PickRandomDistinct(List<int> source, int count)
    {
        if (source == null || source.Count == 0) return System.Array.Empty<int>();
        return source.OrderBy(_ => Rng.Next()).Take(Mathf.Min(count, source.Count)).ToArray();
    }

    private static List<int> SampleRandomDistinctIndices(int range, int count)
    {
        return Enumerable.Range(0, range).OrderBy(_ => Rng.Next()).Take(count).ToList();
    }
}
