using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 낮 퍼즐 검증 및 결과 계산.
/// 배치된 블록들로부터 색상/꽃 데이터를 추출하고 매출을 계산한다.
/// </summary>
public static class PuzzleValidator
{
    public static PuzzleValidationResult ValidatePuzzle()
    {
        var gridManager = GridManager.Instance;
        var result = new PuzzleValidationResult
        {
            colorCounts = new Dictionary<int, int>(),
            flowerCounts = new Dictionary<int, int>()
        };

        if (gridManager == null)
        {
            Debug.LogError("[PuzzleValidator] GridManager를 찾을 수 없습니다!");
            return result;
        }

        // 1. 완벽도 판정 (겹침 없이 그리드 전체가 채워졌는가)
        bool isFull = gridManager.IsFull();
        bool hasOverlap = gridManager.HasAnyOverlap();

        result.isPerfect = isFull && !hasOverlap;
        result.baseScore = result.isPerfect ? 1000 : 500;

        // 2. 배치된 모든 블록 수집 및 색상/꽃 ID 별 전체 개수 집계
        var allBlocks = gridManager.GetAllPlacedBlocks();

        foreach (var blockDrag in allBlocks)
        {
            if (blockDrag == null || blockDrag.blockData == null) continue;

            int colorId = (int)blockDrag.blockData.color;
            result.colorCounts.TryGetValue(colorId, out int c);
            result.colorCounts[colorId] = c + 1;

            int flowerId = blockDrag.blockData.blockID;
            result.flowerCounts.TryGetValue(flowerId, out int f);
            result.flowerCounts[flowerId] = f + 1;
        }

        result.mostUsedColorId = GetMostUsedId(result.colorCounts);
        result.mostUsedFlowerId = GetMostUsedId(result.flowerCounts);

        // 진행 중엔 색 블록으로, 제출 후엔 꽃 아이콘 꽃다발로 보이도록 전환한다.
        foreach (var blockDrag in allBlocks)
        {
            blockDrag?.SetFlowerMode(true);
        }

        // 3. 완벽한 꽃다발 판정 (배치한 블록 모양별 개수가 오늘의 정답 레시피와 정확히 일치하는가)
        var recipe = CurrencyManager.Instance != null ? CurrencyManager.Instance.CurrentDayRecipe : null;
        var perfectResult = PerfectChecker.CheckPerfect(gridManager, recipe);
        result.isPerfectBouquet = perfectResult.isPerfect;

        // 4. 매출 계산
        int earnings = result.baseScore;

        if (result.isPerfectBouquet)
        {
            earnings += 500;
            Debug.Log("[PuzzleValidator] 완벽한 꽃다발! +500 보너스");
        }

        var currencyManager = CurrencyManager.Instance;
        var requirement = currencyManager != null ? currencyManager.CurrentRequirement : default;
        bool requirementMet = CustomerRequirementGenerator.IsRequirementMet(requirement, result);

        if (requirementMet)
        {
            earnings += requirement.bonusAmount;
            Debug.Log($"[PuzzleValidator] 고객 요구사항 달성! +{requirement.bonusAmount} 보너스");
        }
        else
        {
            Debug.Log("[PuzzleValidator] 고객 요구사항 미달성");
        }

        result.scoreBeforePenalty = earnings;
        result.targetScore = currencyManager != null ? currencyManager.CurrentTargetScore : 0;
        result.targetScoreMet = earnings >= result.targetScore;

        // 목표 점수(손님이 기대하는 최소 결과)에 못 미치면 매출이 줄어든다.
        result.totalEarnings = result.targetScoreMet ? earnings : Mathf.RoundToInt(earnings * 0.5f);

        Debug.Log($"[PuzzleValidator] 낮 퍼즐 검증 완료 - 완벽: {result.isPerfect}, 완벽한 꽃다발: {result.isPerfectBouquet}, 목표점수: {result.targetScore} (달성: {result.targetScoreMet}), 총 매출: {result.totalEarnings}");

        // 5. EventBus 발행 및 재화 반영
        EventBus.RaiseDayPuzzleComplete(result);
        currencyManager?.AddMoney(result.totalEarnings);

        return result;
    }

    private static int GetMostUsedId(Dictionary<int, int> countDict)
    {
        int maxId = -1;
        int maxCount = 0;

        foreach (var kvp in countDict)
        {
            if (kvp.Value > maxCount)
            {
                maxCount = kvp.Value;
                maxId = kvp.Key;
            }
        }

        return maxId;
    }
}
