using System.Collections.Generic;
using UnityEngine;

/// <summary>특수 칸 종류. 별 칸은 놓인 꽃의 상태를 한 단계 올리고, 그늘 칸은 한 단계 내린다(FlowerLight가 계산한다).</summary>
public enum SpecialCell
{
    None,
    Star,
    Shade
}

/// <summary>퍼즐이 만들어진 뒤 특수 칸을 정한다(순수 계산). 종류마다 확률로 만들지 판정하고, 만들기로 했으면 칸 하나를 무작위로 고른다.</summary>
public static class SpecialCellRoller
{
    /// <summary>별 칸을 먼저 정하고, 그늘 칸은 별 칸이 아닌 칸에서만 고른다. 특수가 아닌 칸은 결과에 없다.</summary>
    public static Dictionary<WrapperRegionPosition, SpecialCell> Roll(IEnumerable<WrapperRegionPosition> positions,
        float starChance, float shadeChance)
    {
        var free = new List<WrapperRegionPosition>(positions);
        var result = new Dictionary<WrapperRegionPosition, SpecialCell>();
        TryPlace(free, result, SpecialCell.Star, starChance);
        TryPlace(free, result, SpecialCell.Shade, shadeChance);
        return result;
    }

    private static void TryPlace(List<WrapperRegionPosition> free, Dictionary<WrapperRegionPosition, SpecialCell> result,
        SpecialCell kind, float chance)
    {
        if (free.Count == 0 || Random.value >= chance) return;
        int index = Random.Range(0, free.Count);
        result[free[index]] = kind;
        free.RemoveAt(index);
    }
}
