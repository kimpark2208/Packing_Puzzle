using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 낮 퍼즐 검증 및 결과 계산
/// 배치된 블록들로부터 색상/꽃 데이터를 추출하고 매출을 계산
/// </summary>
public class PuzzleValidator : MonoBehaviour
{
    /// <summary>
    /// 퍼즐 검증 수행
    /// </summary>
    public static void ValidatePuzzle()
    {
        var gridManager = GridManager.Instance;
        if (gridManager == null)
        {
            Debug.LogError("[PuzzleValidator] GridManager를 찾을 수 없습니다!");
            return;
        }

        var result = new PuzzleValidationResult();

        // 1. 완벽도 판정
        bool isFull = gridManager.IsFull();
        bool hasOverlap = gridManager.HasAnyOverlap();

        result.isPerfect = isFull && !hasOverlap;
        result.baseScore = result.isPerfect ? 1000 : 500;

        // 2. 배치된 모든 블록 수집 및 색상/꽃 ID 추출
        var colorCounts = new Dictionary<int, int>();
        var flowerCounts = new Dictionary<int, int>();
        int totalBlockCount = 0;

        var allBlocks = GetAllPlacedBlocks(gridManager);

        foreach (var blockDrag in allBlocks)
        {
            if (blockDrag == null || blockDrag.blockData == null) continue;

            // 색상 ID (enum을 int로 변환)
            int colorId = (int)blockDrag.blockData.color;
            if (!colorCounts.ContainsKey(colorId))
                colorCounts[colorId] = 0;
            colorCounts[colorId]++;

            // 꽃 ID (blockID 사용)
            int flowerId = blockDrag.blockData.blockID;
            if (!flowerCounts.ContainsKey(flowerId))
                flowerCounts[flowerId] = 0;
            flowerCounts[flowerId]++;

            totalBlockCount++;
        }

        // 3. 최빈값 계산
        result.mostUsedColorId = GetMostUsedId(colorCounts);
        result.mostUsedFlowerId = GetMostUsedId(flowerCounts);

        // 4. 매출 계산
        int earnings = result.baseScore;

        // Phase 3: 고객 요구사항 보너스 확인
        var currencyManager = CurrencyManager.Instance;
        var requirement = currencyManager.CurrentRequirement;
        bool requirementMet = CustomerRequirementGenerator.IsRequirementMet(requirement, result);

        if (requirementMet)
        {
            earnings += requirement.bonusAmount;
            Debug.Log($"[PuzzleValidator] 고객 요구사항 달성! +{requirement.bonusAmount} 보너스");
        }
        else
        {
            Debug.Log($"[PuzzleValidator] 고객 요구사항 미달성");
        }

        result.totalEarnings = earnings;

        // 5. 결과 로깅
        Debug.Log($"[PuzzleValidator] 낮 퍼즐 검증 완료");
        Debug.Log($"  완벽: {result.isPerfect}");
        Debug.Log($"  기본 점수: {result.baseScore}");
        Debug.Log($"  최다 색상: ID {result.mostUsedColorId}");
        Debug.Log($"  최다 꽃: ID {result.mostUsedFlowerId}");
        Debug.Log($"  고객 요구사항: {requirement.description}");
        Debug.Log($"  요구사항 달성: {requirementMet}");
        Debug.Log($"  총 매출: {result.totalEarnings}");

        // 6. EventBus 발행
        EventBus.RaiseDayPuzzleComplete(result);

        // 7. CurrencyManager에 금액 추가
        CurrencyManager.Instance.AddMoney(result.totalEarnings);
    }

    /// <summary>
    /// GridManager에서 모든 배치된 블록 수집
    /// </summary>
    private static List<BlockDrag> GetAllPlacedBlocks(GridManager gridManager)
    {
        var blocks = gridManager.GetAllPlacedBlocks();
        return new List<BlockDrag>(blocks);
    }

    /// <summary>
    /// Dictionary에서 최빈값 ID 반환
    /// </summary>
    private static int GetMostUsedId(Dictionary<int, int> countDict)
    {
        if (countDict.Count == 0)
            return -1;

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
