using System;
using System.Collections.Generic;

/// <summary>꽃의 상태. 이웃 꽃과의 관계 점수 합계로 정해진다.</summary>
public enum FlowerGlow
{
    Dim,    // 흐림: 관계 합계 0
    Clear,  // 선명: 1~2
    Bright  // 빛남: 3 이상
}

/// <summary>
/// 꽃의 빛 계산(순수 계산, 보드·UI를 모른다).
/// 칸이 이웃한 꽃과 색 관계를 맺고(ColorCompatDatabase), 꽃마다 관계 합계로 상태가 정해지며,
/// 정산에는 꽃 한 송이당 평균 상태 점수 × 10이 들어간다(-20 전부 흐림 ~ +30 전부 빛남, 포장지 크기와 상관없다).
/// </summary>
public static class FlowerLight
{
    private const int DimScore = -2, ClearScore = 1, BrightScore = 3;
    private const int BrightFrom = 3, ClearFrom = 1;
    private const int ScoreScale = 10;

    /// <summary>
    /// 칸의 이웃: 같은 링의 바로 옆 두 칸(A상↔A하, B상↔B하, A상↔B상, A하↔B하), 같은 방향의 안쪽·바깥쪽 링 칸.
    /// 중앙은 1링의 모든 칸과 붙어 있다. 포장지에 없는 링의 칸도 돌려주므로, 놓인 꽃만 가진 표에서 찾아 쓴다.
    /// </summary>
    public static IEnumerable<WrapperRegionPosition> Neighbors(WrapperRegionPosition p)
    {
        if (p == WrapperRegionPosition.Center)
        {
            for (int k = 0; k < 4; k++) yield return (WrapperRegionPosition)(1 + k);
            yield break;
        }

        int ring = p.Ring();
        int slot = ((int)p - 1) % 4;       // 0 A상, 1 A하, 2 B상, 3 B하
        int first = 1 + (ring - 1) * 4;    // 이 링의 첫 칸 번호
        yield return (WrapperRegionPosition)(first + (slot ^ 1));
        yield return (WrapperRegionPosition)(first + (slot ^ 2));
        yield return ring == 1 ? WrapperRegionPosition.Center : (WrapperRegionPosition)(first - 4 + slot);
        if (first + 4 + slot <= (int)WrapperRegionPosition.B_4Ring_Lower) yield return (WrapperRegionPosition)(first + 4 + slot);
    }

    /// <summary>한 칸의 꽃이 놓인 이웃 꽃들과 맺은 관계 점수 합계(이웃이 비어 있으면 그만큼 빠진다).</summary>
    public static int RelationSum(WrapperRegionPosition position, IReadOnlyDictionary<WrapperRegionPosition, FlowerData.Color> cells)
    {
        int sum = 0;
        if (!cells.TryGetValue(position, out FlowerData.Color color)) return 0;
        foreach (WrapperRegionPosition n in Neighbors(position))
        {
            if (cells.TryGetValue(n, out FlowerData.Color other)) sum += ColorCompatDatabase.GetBonus(color, other);
        }
        return sum;
    }

    public static FlowerGlow GlowOf(int relationSum)
    {
        return relationSum >= BrightFrom ? FlowerGlow.Bright : relationSum >= ClearFrom ? FlowerGlow.Clear : FlowerGlow.Dim;
    }

    private static int GlowScore(FlowerGlow glow)
    {
        return glow == FlowerGlow.Bright ? BrightScore : glow == FlowerGlow.Clear ? ClearScore : DimScore;
    }

    /// <summary>정산에 넣는 '꽃의 빛' 점수: (꽃 상태 점수 합계 ÷ 꽃 수) × 10을 반올림. 꽃이 없으면 0.</summary>
    public static int Score(IReadOnlyDictionary<WrapperRegionPosition, FlowerData.Color> cells)
    {
        if (cells.Count == 0) return 0;
        int total = 0;
        foreach (WrapperRegionPosition position in cells.Keys) total += GlowScore(GlowOf(RelationSum(position, cells)));
        return (int)Math.Round(total * (double)ScoreScale / cells.Count, MidpointRounding.AwayFromZero);
    }
}
