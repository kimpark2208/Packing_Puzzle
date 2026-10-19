using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 줄기 꺾임 퍼즐 생성. 8×8 격자의 서로 반대쪽 모서리 칸 두 개(점 1, 점 2)를 정하고, 블록 두 종(큰 것 하나, 작은 것 하나)을 이어 붙여 두 점을 잇는 길을 먼저 만든다.
/// 길은 최단 경로일 필요가 없고 같은 블록을 여러 번 써도 되며, 두 블록을 모두 한 번 이상 쓴다. 그다음 길이 아닌 칸에 가시를 무작위로 놓는다.
/// 가시는 길 칸에 놓지 않으므로 만든 길이 항상 정답이다(풀 수 있음이 보장된다). 유일해는 따지지 않는다.
/// </summary>
public static class StemBendPuzzleGenerator
{
    public const int Size = FlowerData.PetalGridSize;

    [System.Serializable]
    public class Settings
    {
        [Min(0), Tooltip("가시 수의 목표. 길이 아닌 칸에 무작위로 놓는다.")] public int thornCount = 10;
        [Min(0), Tooltip("가시 수가 목표에서 이만큼까지 벗어날 수 있다(매번 목표 ± 이 범위에서 무작위).")] public int thornCountTolerance = 2;
        [Min(2), Tooltip("이 칸 수 이상이면 큰 블록, 미만이면 작은 블록으로 나눈다(꽃잎 결손 퍼즐과 같다).")] public int bigBlockMinCells = 4;
    }

    /// <summary>정답 길을 이루는 블록 하나(어느 블록을 어느 칸들에 놓았는지).</summary>
    public class Placement
    {
        public BlockData block;
        public List<Vector2Int> cells;
    }

    public class Puzzle
    {
        public bool[,] thorns;              // 가시 칸
        public Vector2Int dotA;             // 점 1 칸(열 x, 줄 y)
        public Vector2Int dotB;             // 점 2 칸
        public List<BlockData> blocks;      // 이번 퍼즐의 블록(큰 것, 작은 것 순서)
        public List<Placement> solution;    // 만들어 둔 정답 길(가시는 이 칸들을 피해 놓였다)
    }

    private const int Attempts = 200;        // 두 블록을 다 써서 두 점을 잇는 길을 못 만들었을 때 다시 해 보는 횟수
    private const int MaxPlacements = 16;    // 길 하나에 쓰는 블록 수의 상한
    private const float Wander = 2.5f;       // 다음 블록을 고를 때 목표 점까지의 거리에 더해지는 무작위 폭(클수록 구불구불한 길)

    /// <summary>퍼즐을 만든다. 블록이 모자라거나 길을 못 만들면 null.</summary>
    public static Puzzle Generate(IReadOnlyList<BlockData> allBlocks, Settings settings)
    {
        List<BlockData> blocks = PetalLossPuzzleGenerator.PickBlocks(allBlocks, settings.bigBlockMinCells);
        if (blocks == null) return null;

        var variants = blocks.Select(b => PolyominoUtil.GetUniqueVariants(b)).ToList();
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            // 반대쪽 모서리 두 쌍(우상단-좌하단, 좌상단-우하단) 중 무작위, 어느 쪽이 점 1인지도 무작위
            int last = Size - 1;
            bool flip = Random.value < 0.5f;
            Vector2Int a = new(flip ? 0 : last, 0), b = new(flip ? last : 0, last);
            if (Random.value < 0.5f) (a, b) = (b, a);

            List<Placement> solution = BuildPath(blocks, variants, a, b);
            if (solution == null) continue;

            return new Puzzle { thorns = PlaceThorns(solution, settings), dotA = a, dotB = b, blocks = blocks, solution = solution };
        }
        return null;
    }

    /// <summary>start 칸에서 시작해 블록을 이웃하게 이어 붙여 goal 칸까지 닿는 길을 만든다. 두 블록을 다 쓰지 못했거나 상한을 넘으면 null.</summary>
    private static List<Placement> BuildPath(List<BlockData> blocks, List<List<HashSet<Vector2Int>>> variants, Vector2Int start, Vector2Int goal)
    {
        var covered = new bool[Size, Size];
        var solution = new List<Placement>();
        var used = new HashSet<int>();

        while (solution.Count < MaxPlacements)
        {
            // 아직 안 쓴 블록이 있으면 절반쯤은 그 블록을 먼저 고른다
            int pick = Random.Range(0, blocks.Count);
            if (used.Count < blocks.Count && Random.value < 0.5f) pick = Enumerable.Range(0, blocks.Count).First(i => !used.Contains(i));

            Placement placement = PickPlacement(blocks[pick], variants[pick], covered, start, goal, solution.Count == 0);
            if (placement == null) return null;

            foreach (Vector2Int cell in placement.cells) covered[cell.y, cell.x] = true;
            solution.Add(placement);
            used.Add(pick);

            if (covered[goal.y, goal.x]) return used.Count == blocks.Count ? solution : null;
        }
        return null;
    }

    /// <summary>block을 놓을 수 있는 자리(첫 블록은 start를 덮는 자리, 그다음은 이미 놓인 칸과 변이 닿는 자리) 중 goal에 가까운 쪽을 무작위를 섞어 고른다.</summary>
    private static Placement PickPlacement(BlockData block, List<HashSet<Vector2Int>> blockVariants, bool[,] covered, Vector2Int start, Vector2Int goal, bool first)
    {
        Placement best = null;
        float bestScore = float.MaxValue;

        foreach (HashSet<Vector2Int> variant in blockVariants)
        {
            for (int row = 0; row < Size; row++)
            {
                for (int col = 0; col < Size; col++)
                {
                    var origin = new Vector2Int(col, row);
                    bool fits = true, touches = false;
                    int nearest = int.MaxValue;
                    foreach (Vector2Int v in variant)
                    {
                        Vector2Int cell = origin + v;
                        if (cell.x >= Size || cell.y >= Size || covered[cell.y, cell.x]) { fits = false; break; }

                        touches |= first ? cell == start : IsTouching(covered, cell);
                        nearest = Mathf.Min(nearest, Mathf.Abs(cell.x - goal.x) + Mathf.Abs(cell.y - goal.y));
                    }
                    if (!fits || !touches) continue;

                    float score = nearest + Random.value * Wander;
                    if (score >= bestScore) continue;

                    bestScore = score;
                    best = new Placement { block = block, cells = variant.Select(v => origin + v).ToList() };
                }
            }
        }
        return best;
    }

    private static bool IsTouching(bool[,] covered, Vector2Int cell)
    {
        return (cell.x > 0 && covered[cell.y, cell.x - 1]) || (cell.x < Size - 1 && covered[cell.y, cell.x + 1])
            || (cell.y > 0 && covered[cell.y - 1, cell.x]) || (cell.y < Size - 1 && covered[cell.y + 1, cell.x]);
    }

    /// <summary>길이 아닌 칸 중에서 목표 ± 허용 오차만큼 무작위로 골라 가시를 놓는다.</summary>
    private static bool[,] PlaceThorns(List<Placement> solution, Settings settings)
    {
        var onPath = new HashSet<Vector2Int>(solution.SelectMany(p => p.cells));
        var free = new List<Vector2Int>();
        for (int row = 0; row < Size; row++)
            for (int col = 0; col < Size; col++)
                if (!onPath.Contains(new Vector2Int(col, row))) free.Add(new Vector2Int(col, row));

        int count = Mathf.Clamp(settings.thornCount + Random.Range(-settings.thornCountTolerance, settings.thornCountTolerance + 1), 0, free.Count);
        var thorns = new bool[Size, Size];
        foreach (Vector2Int cell in free.OrderBy(_ => Random.value).Take(count)) thorns[cell.y, cell.x] = true;
        return thorns;
    }
}
