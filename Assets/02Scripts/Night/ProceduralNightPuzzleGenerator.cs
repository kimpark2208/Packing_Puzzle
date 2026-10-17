using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 절차적 밤 퍼즐 생성 (진짜 Dancing Links/Algorithm X 기반).
/// 밤 퍼즐은 플레이어가 직접 셀을 그려서 채우는 방식이므로(NightPuzzleController),
/// 이 생성기는 "정답 배치"를 미리 계산해서 넣어주는 것이 아니라
/// 주어진 꽃 조합만으로 보드를 빈틈없이 유일하게 채울 수 있는지를 검증하고,
/// 불가능하면 최소한의 칸을 벽(필러)으로 막아 나머지가 유일하게 채워지도록 만든다.
/// 퍼즐 하나에 주어지는 꽃은 최대 MaxBlocksPerStage종이고, 주어진 꽃은 모두 한 번 이상 써서 채울 수 있는 배치만 만든다
/// (플레이어도 주어진 꽃을 모두 써야 완료로 친다: NightShapePuzzleValidator.AreAllBlocksUsed).
/// </summary>
public static class ProceduralNightPuzzleGenerator
{
    /// <summary>퍼즐 하나에 주어지는 꽃 종류의 최대 수.</summary>
    public const int MaxBlocksPerStage = 2;

    private static int nextPuzzleId = 1;
    private const int WallSearchAttemptsPerCount = 3000;
    private const int MaxSolutionsToInspect = 300; // 모든 꽃을 쓴 해를 찾느라 살펴볼 해의 최대 개수
    private const long TimeBudgetMs = 700; // 스테이지 하나당 벽 탐색에 쓸 수 있는 최대 시간 (메인 스레드 프리징 방지)
    private static readonly System.Random Rng = new();

    /// <summary>보유한 꽃 중 무작위로 고른 (최대 MaxBlocksPerStage종) 꽃으로 잉여 스테이지를 만든다. 보유한 꽃이 없으면 false.</summary>
    public static bool TryGenerateBonusStage(int gridSize, List<int> obtainedFlowerIds, out NightPuzzleData stage)
    {
        int[] combo = PickRandomDistinct(obtainedFlowerIds, Mathf.Min(MaxBlocksPerStage, obtainedFlowerIds != null ? obtainedFlowerIds.Count : 0));
        if (combo.Length == 0)
        {
            stage = default;
            return false;
        }

        stage = GenerateStage(gridSize, PickBlocks(), combo, isBonusStage: true);
        return true;
    }

    /// <summary>퍼즐에 쓸 블록 MaxBlocksPerStage개를 블록 목록에서 무작위로 고른다(꽃과는 무관).</summary>
    public static List<BlockData> PickBlocks()
    {
        return BlockRegistry.Instance != null ? BlockRegistry.Instance.PickRandom(MaxBlocksPerStage) : new List<BlockData>();
    }

    /// <summary>주어진 꽃(최대 MaxBlocksPerStage종)을 모두 한 번 이상 써서 보드를 채울 수 있는 스테이지를 만든다.</summary>
    public static NightPuzzleData GenerateStage(int gridSize, List<BlockData> blocks, int[] flowerIds, bool isBonusStage)
    {
        blocks = blocks.Where(b => b != null).ToList();
        int[] blockIds = blocks.Select(b => b.blockID).ToArray();

        bool[,] wall = new bool[gridSize, gridSize];

        if (blocks.Count == 0)
        {
            Debug.LogWarning("[ProceduralNightPuzzleGenerator] 사용할 블록 데이터가 없어 스테이지를 생성할 수 없습니다.");
            return MakeData(gridSize, blockIds, flowerIds, isBonusStage, wall);
        }

        var cellCounts = blocks.Select(b => b.CellCount).Where(c => c > 0).Distinct().ToList();
        int mandatoryCells = blocks.Sum(b => b.CellCount); // 주어진 꽃을 하나씩은 반드시 쓰므로 그만큼은 이미 채워진 셈
        int totalCells = gridSize * gridSize;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 칸 수 조합으로 애초에 나눠떨어지지 않으면(예: 5x5=25칸을 3칸짜리 I자 하나로만 못 채움)
        // DLX를 아예 돌리지 않는다 - 이 사전 필터가 없으면 절대 못 찾을 답을 수백 번 재시도하며 멈춰버린다.
        if (CanFillUsingAll(totalCells, mandatoryCells, cellCounts) && HasUniqueTilingUsingAll(gridSize, blocks, wall))
        {
            return MakeData(gridSize, blockIds, flowerIds, isBonusStage, wall);
        }

        // 꽃 조합만으로 유일하게 완전히 채울 수 없다면 최소한의 칸을 벽(=임의의 필러)으로 막아본다.
        // 벽은 꽃 블록과 무관하게 아무 크기나 될 수 있으므로 1칸 단위로 늘려가며 시도한다.
        int maxWallCells = Mathf.Min(totalCells / 2, 20);

        for (int wallCellCount = 1; wallCellCount <= maxWallCells; wallCellCount++)
        {
            if (stopwatch.ElapsedMilliseconds > TimeBudgetMs) break;

            int remaining = totalCells - wallCellCount;
            if (!CanFillUsingAll(remaining, mandatoryCells, cellCounts)) continue; // 이 필러 개수로도 나눠떨어지지 않으면 스킵

            if (TryFindWallPlacement(gridSize, blocks, wallCellCount, stopwatch, requireUnique: true, out bool[,] foundWall))
            {
                wall = foundWall;
                Debug.Log($"[ProceduralNightPuzzleGenerator] 필러 {wallCellCount}칸으로 유일해 확보");
                return MakeData(gridSize, blockIds, flowerIds, isBonusStage, wall);
            }
        }

        // S-테트로미노처럼 모양이 빡빡한 조각은 "유일한" 타일링을 만드는 벽 배치를 찾기가 매우 어렵다.
        // 유일성을 못 찾았다고 필러 없이 반환하면, 칸 수가 안 나눠떨어지는 경우(예: 25칸/4칸) 절대 완성 불가능한
        // 퍼즐이 되어버린다. 그래서 유일성을 포기하고 "풀리기만 하는" 벽 배치라도 다시 찾는다.
        // 위 유일해 탐색이 시간예산을 이미 다 써버렸을 수 있으니, 이 2차 탐색은 별도의 시간예산을 새로 받는다.
        stopwatch = System.Diagnostics.Stopwatch.StartNew();

        for (int wallCellCount = 1; wallCellCount <= maxWallCells; wallCellCount++)
        {
            if (stopwatch.ElapsedMilliseconds > TimeBudgetMs) break;

            int remaining = totalCells - wallCellCount;
            if (!CanFillUsingAll(remaining, mandatoryCells, cellCounts)) continue;

            if (TryFindWallPlacement(gridSize, blocks, wallCellCount, stopwatch, requireUnique: false, out bool[,] foundWall))
            {
                wall = foundWall;
                Debug.Log($"[ProceduralNightPuzzleGenerator] 유일해는 못 찾았지만 필러 {wallCellCount}칸으로 풀리는 배치 확보 (정답이 여러 개일 수 있음)");
                return MakeData(gridSize, blockIds, flowerIds, isBonusStage, wall);
            }
        }

        // 무작위 탐색이 시간예산 안에 못 찾을 수도 있으니(특히 꽃 하나만 선택한 일반 퍼즐처럼 모양이 빡빡한 경우),
        // 마지막 예비 수단으로 블록을 실제로 하나씩 그리디하게 놓아서 만드는 배치를 쓴다.
        // 이건 "실제로 놓아본" 배치라 반드시 풀 수 있음이 보장된다(운에 의존하지 않음).
        wall = BuildGuaranteedWallFallback(gridSize, blocks);
        Debug.LogWarning("[ProceduralNightPuzzleGenerator] 무작위 탐색 실패, 예비(그리디) 배치로 대체합니다.");
        return MakeData(gridSize, blockIds, flowerIds, isBonusStage, wall);
    }

    /// <summary>
    /// blocks의 변형들을 왼쪽 위부터 순서대로 실제로 하나씩 놓아서 그리드를 채운다. 주어진 꽃은 하나씩 먼저 놓아
    /// 모두 한 번 이상 쓰이게 하고, 나머지 칸을 채운다. 어떤 변형도 들어맞지 않는 칸만 필러(벽)로 남긴다.
    /// 직접 놓아본 배치이므로 항상 풀 수 있음이 보장된다.
    /// </summary>
    private static bool[,] BuildGuaranteedWallFallback(int gridSize, List<BlockData> blocks)
    {
        bool[,] covered = new bool[gridSize, gridSize];

        var variantsByBlock = blocks
            .Select(block => PolyominoUtil.GetUniqueVariants(block).Select(variant => variant.OrderBy(v => v.y).ThenBy(v => v.x).ToList()).ToList())
            .ToList();

        // 1) 꽃마다 하나씩 먼저 놓는다(맨 앞에서부터 들어가는 자리).
        foreach (var variants in variantsByBlock)
        {
            if (!TryPlaceAnywhere(covered, gridSize, variants)) Debug.LogWarning("[ProceduralNightPuzzleGenerator] 예비 배치에서 꽃 하나를 놓을 자리를 못 찾았습니다.");
        }

        // 2) 남은 칸을 어떤 꽃이든 들어가는 대로 채운다.
        var allVariants = variantsByBlock.SelectMany(v => v).ToList();
        for (int row = 0; row < gridSize; row++)
        {
            for (int col = 0; col < gridSize; col++)
            {
                if (covered[row, col]) continue;
                TryPlaceAt(covered, gridSize, allVariants, row, col);
                // 어떤 변형도 안 맞으면 이 칸은 그냥 필러로 남는다(covered[row, col]가 false로 유지됨).
            }
        }

        bool[,] wall = new bool[gridSize, gridSize];
        for (int r = 0; r < gridSize; r++)
            for (int c = 0; c < gridSize; c++)
                wall[r, c] = !covered[r, c];

        return wall;
    }

    /// <summary>variants 중 하나를 (row, col)에 읽는 순서상 첫 칸이 오도록 놓아 본다. 놓았으면 true.</summary>
    private static bool TryPlaceAt(bool[,] covered, int gridSize, List<List<Vector2Int>> variants, int row, int col)
    {
        foreach (var variant in variants)
        {
            Vector2Int anchor = variant[0]; // 읽는 순서상 이 변형의 첫 칸
            var targetCells = new List<Vector2Int>();
            bool fits = true;

            foreach (Vector2Int offset in variant)
            {
                int r = row + (offset.y - anchor.y);
                int c = col + (offset.x - anchor.x);

                if (r < 0 || r >= gridSize || c < 0 || c >= gridSize || covered[r, c])
                {
                    fits = false;
                    break;
                }
                targetCells.Add(new Vector2Int(c, r));
            }

            if (!fits) continue;

            foreach (Vector2Int cell in targetCells) covered[cell.y, cell.x] = true;
            return true;
        }
        return false;
    }

    /// <summary>variants 중 하나를 그리드 어디든 들어가는 첫 자리(왼쪽 위부터)에 놓는다. 놓았으면 true.</summary>
    private static bool TryPlaceAnywhere(bool[,] covered, int gridSize, List<List<Vector2Int>> variants)
    {
        for (int row = 0; row < gridSize; row++)
        {
            for (int col = 0; col < gridSize; col++)
            {
                if (!covered[row, col] && TryPlaceAt(covered, gridSize, variants, row, col)) return true;
            }
        }
        return false;
    }

    /// <summary>주어진 꽃을 하나씩은 반드시 쓰고(mandatoryCells칸) 남는 칸을 꽃들의 칸 수 조합으로 채울 수 있는지.</summary>
    private static bool CanFillUsingAll(int target, int mandatoryCells, List<int> cellCounts)
    {
        return CanReachExactSum(target - mandatoryCells, cellCounts);
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

    private static bool TryFindWallPlacement(int gridSize, List<BlockData> blocks, int wallCellCount, System.Diagnostics.Stopwatch stopwatch, bool requireUnique, out bool[,] wall)
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

            bool valid = requireUnique
                ? HasUniqueTilingUsingAll(gridSize, blocks, candidate)
                : HasTilingUsingAll(gridSize, blocks, candidate);

            if (valid)
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
    /// 방법이 정확히 하나만 존재하고, 그 방법이 주어진 꽃을 모두 한 번 이상 쓰는지 검증한다.
    /// (0개=불가능, 2개 이상=유일하지 않음 → 둘 다 실패로 취급)
    /// </summary>
    private static bool HasUniqueTilingUsingAll(int gridSize, List<BlockData> blocks, bool[,] wallGrid)
    {
        var dlx = BuildExactCoverInstance(gridSize, blocks, wallGrid, out int primaryColumnCount);
        if (primaryColumnCount == 0) return false;

        bool usesAll = false;
        int solutions = dlx.EnumerateSolutions(2, solution =>
        {
            usesAll = UsesEveryBlock(solution, blocks.Count);
            return false; // 해가 둘인지 보려면 첫 해에서 멈추지 않는다
        });
        return solutions == 1 && usesAll;
    }

    /// <summary>유일성은 따지지 않고, 이 벽 배치로 주어진 꽃을 모두 한 번 이상 써서 완전히 채울 수 있는 방법이 하나라도 있는지만 검사한다.</summary>
    private static bool HasTilingUsingAll(int gridSize, List<BlockData> blocks, bool[,] wallGrid)
    {
        var dlx = BuildExactCoverInstance(gridSize, blocks, wallGrid, out int primaryColumnCount);
        if (primaryColumnCount == 0) return false;

        bool found = false;
        dlx.EnumerateSolutions(MaxSolutionsToInspect, solution => found = UsesEveryBlock(solution, blocks.Count));
        return found;
    }

    /// <summary>해(행 목록, 각 행의 RowId는 꽃의 번호)가 모든 꽃을 한 번 이상 쓰는가.</summary>
    private static bool UsesEveryBlock(IReadOnlyList<int> solution, int blockCount)
    {
        for (int block = 0; block < blockCount; block++)
        {
            bool used = false;
            for (int i = 0; i < solution.Count; i++)
            {
                if (solution[i] == block)
                {
                    used = true;
                    break;
                }
            }
            if (!used) return false;
        }
        return true;
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

        for (int blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
        {
            foreach (HashSet<Vector2Int> variant in PolyominoUtil.GetUniqueVariants(blocks[blockIndex]))
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
                            dlx.AddRow(blockIndex, columns); // RowId = 이 칸 묶음이 어느 꽃인지(해에서 꽃 사용 여부를 보려고)
                        }
                    }
                }
            }
        }

        return dlx;
    }

    private static NightPuzzleData MakeData(int gridSize, int[] blockIds, int[] flowerIds, bool isBonusStage, bool[,] wall)
    {
        return new NightPuzzleData
        {
            puzzleId = nextPuzzleId++,
            gridSize = gridSize,
            requiredBlockIds = blockIds,
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
