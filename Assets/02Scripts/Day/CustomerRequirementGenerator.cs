using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 고객 요구사항 생성 (매일)
/// 색상 또는 꽃 기반 랜덤 요구사항 생성
/// 프리셋이 주어지면 그 프리셋으로 풀 수 있는 요구사항만 만든다(칸당 꽃 한 송이이므로 역할별 칸 수가 상한).
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

    /// <param name="preset">null이면 제한 없이 만든다(씬 단독 실행 등).</param>
    public static CustomerRequirement GenerateRandomRequirement(int day, WrapperPresetData preset = null)
    {
        var requirement = new CustomerRequirement { requirementId = day };

        bool colorFirst = Random.value > 0.5f;
        bool made = colorFirst
            ? TryGenerateColor(ref requirement, preset) || TryGenerateFlower(ref requirement, preset)
            : TryGenerateFlower(ref requirement, preset) || TryGenerateColor(ref requirement, preset);
        if (!made) TryGenerateColor(ref requirement, null); // 풀 수 있는 요구사항이 없으면 제한 없이 만든다(시작 꽃이 모든 역할을 덮으므로 거의 없다)

        Debug.Log($"[CustomerRequirementGenerator] Day {day} 요구사항: {requirement.description} (보너스: {requirement.bonusAmount})");
        return requirement;
    }

    private static bool TryGenerateColor(ref CustomerRequirement requirement, WrapperPresetData preset)
    {
        var colors = new List<int>();
        for (int c = 0; c < 7; c++)  // 0~6: Red, Blue, Yellow, Black, Green, White, Purple
        {
            if (ColorCapacity(preset, (FlowerData.Color)c) >= 2) colors.Add(c);
        }
        if (colors.Count == 0) return false;

        requirement.type = RequirementType.Color;
        requirement.targetId = colors[Random.Range(0, colors.Count)];
        int capacity = ColorCapacity(preset, (FlowerData.Color)requirement.targetId);
        requirement.minCount = Random.Range(2, Mathf.Min(4, capacity) + 1);  // 2~4개
        requirement.bonusAmount = Random.Range(100, 301);  // 100~300

        string colorName = ColorPalette.ToKoreanName((FlowerData.Color)requirement.targetId);
        requirement.description = $"<b>{colorName}</b>색 꽃을 <b>{requirement.minCount}개</b> 이상 사용하세요";
        return true;
    }

    private static bool TryGenerateFlower(ref CustomerRequirement requirement, WrapperPresetData preset)
    {
        var flowers = ObtainedFlowers().Where(f => FlowerCapacity(preset, f) >= 1).ToList();
        if (flowers.Count == 0) return false;

        FlowerData flower = flowers[Random.Range(0, flowers.Count)];
        requirement.type = RequirementType.Flower;
        requirement.targetId = flower.blockID;
        requirement.minCount = Random.Range(1, Mathf.Min(3, FlowerCapacity(preset, flower)) + 1);  // 1~3개
        requirement.bonusAmount = Random.Range(150, 351);  // 150~350

        string flowerName = !string.IsNullOrEmpty(flower.flowerName) ? flower.flowerName : $"꽃 ID {flower.blockID}";
        requirement.description = $"<b>{flowerName}</b>을(를) <b>{requirement.minCount}개</b> 사용하세요";
        return true;
    }

    /// <summary>플레이어가 획득한 꽃(FlowerData) 목록.</summary>
    private static List<FlowerData> ObtainedFlowers()
    {
        if (CurrencyManager.Instance == null || BlockRegistry.Instance == null) return new List<FlowerData>();
        return CurrencyManager.Instance.GetObtainedFlowerIds()
            .Select(id => BlockRegistry.Instance.GetById(id))
            .Where(f => f != null)
            .ToList();
    }

    /// <summary>프리셋에서 이 꽃을 최대 몇 송이 놓을 수 있나 (그 꽃의 역할 칸 수).</summary>
    private static int FlowerCapacity(WrapperPresetData preset, FlowerData flower)
    {
        return preset == null ? int.MaxValue : preset.CountByTag(flower.flowerRole);
    }

    /// <summary>프리셋에서 이 색 꽃을 최대 몇 송이 놓을 수 있나 (그 색 꽃이 있는 역할들의 칸 수 합).</summary>
    private static int ColorCapacity(WrapperPresetData preset, FlowerData.Color color)
    {
        if (preset == null) return int.MaxValue;
        return ObtainedFlowers().Where(f => f.color == color).Select(f => f.flowerRole).Distinct().Sum(role => preset.CountByTag(role));
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
