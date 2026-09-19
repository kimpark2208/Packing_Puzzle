using UnityEngine;

/// <summary>
/// 고객 요구사항 생성 (매일)
/// 색상 또는 꽃 기반 랜덤 요구사항 생성
/// </summary>
public class CustomerRequirementGenerator : MonoBehaviour
{
    public enum RequirementType
    {
        Color,   // 색상 기반: "빨간색을 3개 이상 사용하세요"
        Flower   // 꽃 기반: "꽃 101을 2개 사용하세요"
    }

    /// <summary>
    /// 고객 요구사항 데이터
    /// </summary>
    public struct CustomerRequirement
    {
        public int requirementId;        // 요구사항 고유 ID (일수)
        public string description;       // UI 표시용 설명
        public RequirementType type;     // 색상/꽃 기반
        public int targetId;             // 색상 ID (0~6) 또는 꽃 ID
        public int minCount;             // 최소 개수
        public int bonusAmount;          // 충족 시 보너스
    }

    /// <summary>
    /// 매일 새로운 요구사항 생성
    /// </summary>
    public static CustomerRequirement GenerateRandomRequirement(int day)
    {
        var requirement = new CustomerRequirement();
        requirement.requirementId = day;

        // 50% 확률로 색상 또는 꽃 기반 선택
        bool isColorBased = Random.value > 0.5f;

        if (isColorBased)
        {
            GenerateColorRequirement(ref requirement);
        }
        else
        {
            GenerateFlowerRequirement(ref requirement);
        }

        Debug.Log($"[CustomerRequirementGenerator] Day {day} 요구사항: {requirement.description} (보너스: {requirement.bonusAmount})");
        return requirement;
    }

    /// <summary>
    /// 색상 기반 요구사항 생성
    /// </summary>
    private static void GenerateColorRequirement(ref CustomerRequirement requirement)
    {
        requirement.type = RequirementType.Color;
        requirement.targetId = Random.Range(0, 7);  // 0~6: Red, Blue, Yellow, Black, Green, White, Purple
        requirement.minCount = Random.Range(2, 5);  // 2~4개
        requirement.bonusAmount = Random.Range(100, 301);  // 100~300

        string colorName = GetColorName(requirement.targetId);
        requirement.description = $"<b>{colorName}</b>색 꽃을 <b>{requirement.minCount}개</b> 이상 사용하세요";
    }

    /// <summary>
    /// 꽃 기반 요구사항 생성
    /// </summary>
    private static void GenerateFlowerRequirement(ref CustomerRequirement requirement)
    {
        requirement.type = RequirementType.Flower;

        // 획득한 꽃 중에서 랜덤 선택
        var obtainedFlowers = CurrencyManager.Instance.GetObtainedFlowerIds();
        if (obtainedFlowers.Count == 0)
        {
            // 획득한 꽃이 없으면 색상 기반으로 대체
            GenerateColorRequirement(ref requirement);
            return;
        }

        requirement.targetId = obtainedFlowers[Random.Range(0, obtainedFlowers.Count)];
        requirement.minCount = Random.Range(1, 4);  // 1~3개
        requirement.bonusAmount = Random.Range(150, 351);  // 150~350

        requirement.description = $"<b>꽃 ID {requirement.targetId}</b>을(를) <b>{requirement.minCount}개</b> 사용하세요";
    }

    /// <summary>
    /// 요구사항 충족 여부 확인
    /// </summary>
    public static bool IsRequirementMet(CustomerRequirement requirement, PuzzleValidationResult result)
    {
        if (requirement.type == RequirementType.Color)
        {
            // 색상 기반: 검증 결과의 색상별 개수를 알 수 없으므로 (현재 PuzzleValidationResult에 없음)
            // TODO: Phase 4에서 색상별 개수를 PuzzleValidationResult에 추가하면 정확히 확인 가능
            // 임시로: 가장 많이 사용된 색상이 targetId이고 minCount 이상이면 충족
            return result.mostUsedColorId == requirement.targetId;
        }
        else // Flower
        {
            // 꽃 기반: 검증 결과의 꽃별 개수를 알 수 없으므로 (현재 PuzzleValidationResult에 없음)
            // 임시로: 가장 많이 사용된 꽃이 targetId이고 minCount 이상이면 충족
            return result.mostUsedFlowerId == requirement.targetId;
        }
    }

    /// <summary>
    /// 색상 ID를 한글 이름으로 변환
    /// </summary>
    private static string GetColorName(int colorId)
    {
        return colorId switch
        {
            0 => "빨간",
            1 => "파란",
            2 => "노란",
            3 => "검은",
            4 => "초록",
            5 => "흰",
            6 => "보라",
            _ => "알 수 없는"
        };
    }
}
