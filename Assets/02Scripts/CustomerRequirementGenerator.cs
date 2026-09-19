using UnityEngine;

/// <summary>
/// 고객 요구사항 생성 (매일)
/// 색상 또는 꽃 기반 랜덤 요구사항 생성
/// </summary>
public static class CustomerRequirementGenerator
{
    public enum RequirementType
    {
        Color,   // 색상 기반: "빨간색을 3개 이상 사용하세요"
        Flower   // 꽃 기반: "꽃 101을 2개 사용하세요"
    }

    public struct CustomerRequirement
    {
        public int requirementId;        // 요구사항 고유 ID (일수)
        public string description;       // UI 표시용 설명
        public RequirementType type;     // 색상/꽃 기반
        public int targetId;             // 색상 ID (0~6) 또는 꽃 ID
        public int minCount;             // 최소 개수
        public int bonusAmount;          // 충족 시 보너스
    }

    public static CustomerRequirement GenerateRandomRequirement(int day)
    {
        var requirement = new CustomerRequirement { requirementId = day };

        bool isColorBased = Random.value > 0.5f;
        if (isColorBased) GenerateColorRequirement(ref requirement);
        else GenerateFlowerRequirement(ref requirement);

        Debug.Log($"[CustomerRequirementGenerator] Day {day} 요구사항: {requirement.description} (보너스: {requirement.bonusAmount})");
        return requirement;
    }

    private static void GenerateColorRequirement(ref CustomerRequirement requirement)
    {
        requirement.type = RequirementType.Color;
        requirement.targetId = Random.Range(0, 7);  // 0~6: Red, Blue, Yellow, Black, Green, White, Purple
        requirement.minCount = Random.Range(2, 5);  // 2~4개
        requirement.bonusAmount = Random.Range(100, 301);  // 100~300

        string colorName = ColorPalette.ToKoreanName((BlockData.Color)requirement.targetId);
        requirement.description = $"<b>{colorName}</b>색 꽃을 <b>{requirement.minCount}개</b> 이상 사용하세요";
    }

    private static void GenerateFlowerRequirement(ref CustomerRequirement requirement)
    {
        requirement.type = RequirementType.Flower;

        var obtainedFlowers = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetObtainedFlowerIds() : new System.Collections.Generic.List<int>();
        if (obtainedFlowers.Count == 0)
        {
            GenerateColorRequirement(ref requirement);
            return;
        }

        requirement.targetId = obtainedFlowers[Random.Range(0, obtainedFlowers.Count)];
        requirement.minCount = Random.Range(1, 4);  // 1~3개
        requirement.bonusAmount = Random.Range(150, 351);  // 150~350

        requirement.description = $"<b>꽃 ID {requirement.targetId}</b>을(를) <b>{requirement.minCount}개</b> 사용하세요";
    }

    /// <summary>
    /// 요구사항 충족 여부 확인. PuzzleValidationResult의 전체 색상/꽃별 개수를 사용해 minCount까지 정확히 검사한다.
    /// </summary>
    public static bool IsRequirementMet(CustomerRequirement requirement, PuzzleValidationResult result)
    {
        var counts = requirement.type == RequirementType.Color ? result.colorCounts : result.flowerCounts;
        if (counts == null) return false;

        counts.TryGetValue(requirement.targetId, out int used);
        return used >= requirement.minCount;
    }
}
