using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 낮 퍼즐(손님 주문) 절차적 생성.
/// 포장지를 랜덤 선택하고, 보유한 꽃 중 최대 블록을 하나 이상 포함해 3종(가능한 만큼)을 고른 뒤,
/// Dancing Links로 "정확히 이 개수만큼 겹치지 않게 배치 가능한가"를 검증해 정답 레시피를 만든다.
/// 이 레시피는 그리드 전체를 채우라는 뜻이 아니라, 완성 후 "완벽한 꽃다발" 보너스 판정 기준이다
/// (겹치지만 않으면 되고, 나머지 칸은 플레이어가 가챠로 뽑은 다른 조각으로 채운다).
/// </summary>
public static class DayPuzzleGenerator
{
    public class DayOrder
    {
        // ---- 손님이 등장하는 순간부터 알 수 있는 것 (포장지 선택 전) ----
        public int day;
        public CustomerRequirementGenerator.CustomerRequirement requirement;

        /// <summary>손님이 속으로 원하는 포장지(힌트용). 플레이어가 반드시 이걸 골라야 하는 건 아니다.</summary>
        public int hintWrapperId;
        public string hintColorName;   // 명확한 힌트: "핑크"
        public string hintSizeWord;    // 모호한 힌트: "가장 작은"
        public bool hintStartsVague;   // true면 처음엔 색 대신 모호한 크기 표현으로 시작

        // ---- 플레이어가 포장지를 고른 뒤에 확정되는 것 ----
        public bool isFinalized;
        public int wrapperId;
        public int gridSize;
        public int targetScore;
        public List<BlockData> recipeBlocks = new();
        public List<int> recipeCounts = new();
        public PuzzleRecipe recipe;
    }

    private const int ComboSearchTrialsCap = 6; // 팩토리얼 폭주 방지: 블록 3종 * 각 1~3개 = 최대 27가지, 그대로 다 시도해도 충분히 빠름

    /// <summary>손님이 막 등장했을 때 호출. 아직 포장지는 정하지 않고 힌트만 준비한다.</summary>
    public static DayOrder GenerateOrder(int day)
    {
        int hintWrapperId = CurrencyManager.Instance.GetRandomOwnedWrapperID();

        return new DayOrder
        {
            day = day,
            requirement = CustomerRequirementGenerator.GenerateRandomRequirement(day),
            hintWrapperId = hintWrapperId,
            hintColorName = ShopManager.Instance.GetColorHintName(hintWrapperId),
            hintSizeWord = ShopManager.Instance.GetSizeHintWord(hintWrapperId),
            hintStartsVague = Random.value > 0.5f
        };
    }

    /// <summary>플레이어가 포장지를 골랐을 때 호출. 그 포장지 기준으로 정답 레시피와 목표 점수를 확정한다.</summary>
    public static void FinalizeForWrapper(DayOrder order, int chosenWrapperId)
    {
        order.wrapperId = chosenWrapperId;
        order.gridSize = ShopManager.Instance.GetGridSize(chosenWrapperId);
        int tier = ShopManager.Instance.GetTier(chosenWrapperId);

        var obtainedIds = CurrencyManager.Instance.GetObtainedFlowerIds();
        var candidates = BlockDatabase.Instance.GetObtainedBlocksForWrapperTier(obtainedIds, tier);

        order.targetScore = 400 + tier * 250 + Random.Range(0, 150);

        if (candidates.Count == 0)
        {
            order.recipe = BuildRecipeAsset(order.recipeBlocks, order.recipeCounts);
            order.isFinalized = true;
            return;
        }

        BlockData largest = candidates.OrderByDescending(b => b.CellCount).First();
        var others = candidates.Where(b => b != largest).OrderBy(_ => Random.value).ToList();

        order.recipeBlocks.Add(largest);
        order.recipeBlocks.AddRange(others.Take(2));

        order.recipeCounts = FindFeasibleCounts(order.gridSize, order.recipeBlocks);
        order.recipe = BuildRecipeAsset(order.recipeBlocks, order.recipeCounts);
        order.isFinalized = true;
    }

    private static List<int> FindFeasibleCounts(int gridSize, List<BlockData> blocks)
    {
        foreach (var combo in ShuffledCombosOf123(blocks.Count))
        {
            int totalCells = 0;
            for (int i = 0; i < blocks.Count; i++) totalCells += combo[i] * blocks[i].CellCount;
            if (totalCells > gridSize * gridSize) continue;

            if (HasNonOverlappingPlacement(gridSize, blocks, combo))
            {
                return combo;
            }
        }

        // 폴백: 배치 가능성을 찾지 못하면 전부 1개씩 (그래도 겹치지만 않으면 최소한의 답은 됨)
        return blocks.Select(_ => 1).ToList();
    }

    /// <summary>
    /// Dancing Links (Primary=사용 슬롯 N개, Secondary=그리드 칸)로
    /// "블록별로 정확히 counts[i]개를, 서로 겹치지 않게 그리드 안에 배치할 수 있는가"를 확인한다.
    /// </summary>
    private static bool HasNonOverlappingPlacement(int gridSize, List<BlockData> blocks, List<int> counts)
    {
        int totalSlots = counts.Sum();
        if (totalSlots == 0) return true;

        int totalCells = gridSize * gridSize;
        var dlx = new DancingLinks(totalSlots + totalCells, totalSlots);

        int slotBase = 0;
        for (int i = 0; i < blocks.Count; i++)
        {
            for (int slot = 0; slot < counts[i]; slot++)
            {
                int slotColumn = slotBase + slot;

                foreach (HashSet<Vector2Int> variant in PolyominoUtil.GetUniqueVariants(blocks[i]))
                {
                    int maxRowOffset = variant.Max(v => v.y);
                    int maxColOffset = variant.Max(v => v.x);

                    for (int r = 0; r + maxRowOffset < gridSize; r++)
                    {
                        for (int c = 0; c + maxColOffset < gridSize; c++)
                        {
                            var cols = new List<int> { slotColumn };
                            foreach (Vector2Int off in variant)
                            {
                                cols.Add(totalSlots + (r + off.y) * gridSize + (c + off.x));
                            }
                            dlx.AddRow(0, cols); // rowId는 존재 여부만 필요하므로 사용하지 않는다
                        }
                    }
                }
            }
            slotBase += counts[i];
        }

        return dlx.SolveOne() != null;
    }

    private static List<List<int>> ShuffledCombosOf123(int blockCount)
    {
        var combos = new List<List<int>>();

        void Recurse(List<int> cur)
        {
            if (cur.Count == blockCount)
            {
                combos.Add(new List<int>(cur));
                return;
            }
            for (int v = 1; v <= 3; v++)
            {
                cur.Add(v);
                Recurse(cur);
                cur.RemoveAt(cur.Count - 1);
            }
        }

        Recurse(new List<int>());
        return combos.OrderBy(_ => Random.value).Take(Mathf.Max(ComboSearchTrialsCap, combos.Count)).ToList();
    }

    private static PuzzleRecipe BuildRecipeAsset(List<BlockData> blocks, List<int> counts)
    {
        var recipe = ScriptableObject.CreateInstance<PuzzleRecipe>();
        recipe.requiredBlocks = new List<PuzzleRecipe.RequiredBlock>();

        for (int i = 0; i < blocks.Count; i++)
        {
            recipe.requiredBlocks.Add(new PuzzleRecipe.RequiredBlock
            {
                blockData = blocks[i],
                requiredCount = counts[i]
            });
        }

        return recipe;
    }
}
