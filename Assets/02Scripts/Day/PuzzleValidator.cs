using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 낮 퍼즐 검증 및 결과 계산.
/// 성공(모든 태그 영역 완성) / 실패(리롤 소진 + 미완성) 두 경로가 있다.
/// </summary>
public static class PuzzleValidator
{
    public static PuzzleValidationResult ValidateSuccess()
    {
        var result = BuildBaseResult();
        result.success = true;

        var board = WrapperBoardController.Instance;
        result.colorBonus = board != null ? board.ComputeColorBonus() : 0;

        var currencyManager = CurrencyManager.Instance;
        var requirement = currencyManager != null ? currencyManager.CurrentRequirement : default;
        result.requirementMet = CustomerRequirementGenerator.IsRequirementMet(requirement, result);
        result.requirementBonus = result.requirementMet ? requirement.bonusAmount : 0;

        result.totalEarnings = result.colorBonus + result.requirementBonus;

        Debug.Log($"[PuzzleValidator] 꽃다발 완성! 색 조합 보너스: {result.colorBonus}, 요구사항 보너스: {result.requirementBonus}, 총 매출: {result.totalEarnings}");

        EventBus.RaiseDayPuzzleComplete(result);
        currencyManager?.AddMoney(result.totalEarnings);

        return result;
    }

    public static PuzzleValidationResult ValidateFailure()
    {
        var result = BuildBaseResult();
        result.success = false;
        result.totalEarnings = 0;

        Debug.Log("[PuzzleValidator] 꽃다발 실패작 완성 (리롤 소진, 미완성 영역 존재)");

        EventBus.RaiseDayPuzzleComplete(result);
        return result;
    }

    private static PuzzleValidationResult BuildBaseResult()
    {
        var result = new PuzzleValidationResult
        {
            colorCounts = new Dictionary<int, int>(),
            flowerCounts = new Dictionary<int, int>()
        };

        var board = WrapperBoardController.Instance;
        if (board == null) return result;

        foreach (var slot in board.AllSlots)
        {
            foreach (var flower in slot.PlacedFlowers)
            {
                int colorId = (int)flower.color;
                result.colorCounts.TryGetValue(colorId, out int c);
                result.colorCounts[colorId] = c + 1;

                int flowerId = flower.blockID;
                result.flowerCounts.TryGetValue(flowerId, out int f);
                result.flowerCounts[flowerId] = f + 1;
            }
        }

        result.mostUsedColorId = GetMostUsedId(result.colorCounts);
        result.mostUsedFlowerId = GetMostUsedId(result.flowerCounts);

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
