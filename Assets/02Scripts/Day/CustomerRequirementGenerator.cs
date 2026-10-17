using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 주문의 요구사항(플레이어가 퍼즐에서 채워야 하는 조건)과 그 재료(획득한 꽃, 프리셋이 받아 줄 수 있는 개수).
/// 주문을 어떤 말로 할지는 OrderComposer가 정한다.
/// 프리셋이 주어지면 그 프리셋으로 풀 수 있는 요구사항만 만든다(칸당 꽃 한 송이이므로 역할별 칸 수가 상한).
/// </summary>
public static class CustomerRequirementGenerator
{
    public enum RequirementType
    {
        Color,   // 색상 기반: "빨간색 꽃을 3개 이상 사용하세요"
        Flower   // 꽃 기반: "금어초(와 소국)를 하나 이상씩 사용하세요"
    }

    public struct CustomerRequirement
    {
        public int requirementId;        // 요구사항 고유 ID (일수)
        public string description;       // UI 표시용 설명
        public RequirementType type;     // 색상/꽃 기반
        public int targetId;             // 색상 ID (0~6) 또는 꽃 ID
        public int targetId2;            // 꽃 기반에서 두 번째 꽃 ID(없으면 -1). 색상 기반에서는 쓰지 않는다
        public int minCount;             // 색상 기반: 최소 개수. 꽃 기반: 말한 꽃의 종류 수(종류마다 한 송이 이상)
        public int baseAmount;           // 이 퍼즐의 기본 금액(정산 점수 구간의 배율이 곱해진다)

        /// <summary>꽃 기반에서 요구한 꽃 ID들(한 종이면 하나, 두 종이면 둘).</summary>
        public IEnumerable<int> FlowerTargets()
        {
            yield return targetId;
            if (targetId2 >= 0) yield return targetId2;
        }
    }

    /// <summary>퍼즐 기본 금액(임시: 모든 주문이 같다). 100 = 초반에 0~25점 꽃다발 1개를 판매한 정도.</summary>
    public const int BaseAmount = 100;

    /// <summary>플레이어가 획득한 꽃(FlowerData) 목록.</summary>
    public static List<FlowerData> ObtainedFlowers()
    {
        if (CurrencyManager.Instance == null || FlowerRegistry.Instance == null) return new List<FlowerData>();
        return CurrencyManager.Instance.GetObtainedFlowerIds()
            .Select(id => FlowerRegistry.Instance.GetById(id))
            .Where(f => f != null)
            .ToList();
    }

    /// <summary>프리셋에서 이 꽃을 최대 몇 송이 놓을 수 있나 (그 꽃의 역할 칸 수).</summary>
    public static int FlowerCapacity(WrapperPresetData preset, FlowerData flower)
    {
        return preset == null ? int.MaxValue : preset.CountByTag(flower.flowerRole);
    }

    /// <summary>프리셋에서 이 색 꽃을 최대 몇 송이 놓을 수 있나 (그 색 꽃이 있는 역할들의 칸 수 합).</summary>
    public static int ColorCapacity(WrapperPresetData preset, FlowerData.Color color)
    {
        if (preset == null) return int.MaxValue;
        return ObtainedFlowers().Where(f => f.color == color).Select(f => f.flowerRole).Distinct().Sum(role => preset.CountByTag(role));
    }

    /// <summary>꽃 두 종을 한 송이씩 놓을 수 있나: 서로 다른 역할이면 각 역할에 한 칸씩, 같은 역할이면 그 역할에 두 칸이 있어야 한다.</summary>
    public static bool CanPlacePair(WrapperPresetData preset, FlowerData a, FlowerData b)
    {
        if (preset == null) return true;
        if (a.flowerRole == b.flowerRole) return preset.CountByTag(a.flowerRole) >= 2;
        return FlowerCapacity(preset, a) >= 1 && FlowerCapacity(preset, b) >= 1;
    }
}
