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
/// 칸이 이웃한 꽃과 색 관계를 맺고(ColorRelation), 꽃마다 관계 합계로 상태가 정해지며,
/// 정산에는 꽃 한 송이당 평균 상태 점수 × 배율이 들어간다(기본값은 -20 전부 흐림 ~ +30 전부 빛남, 포장지 크기와 상관없다. 점수 값은 ScoreRuleSettings).
/// </summary>
public static class FlowerLight
{
    private const int BrightFrom = 3, ClearFrom = 1;
    private const int ShadeClearBrightNeighbors = 2; // 그늘 칸 둘레에 빛남 꽃이 이만큼 있으면 그늘이 사라진다

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
            if (cells.TryGetValue(n, out FlowerData.Color other)) sum += ColorRelation.GetScore(color, other);
        }
        return sum;
    }

    public static FlowerGlow GlowOf(int relationSum)
    {
        return relationSum >= BrightFrom ? FlowerGlow.Bright : relationSum >= ClearFrom ? FlowerGlow.Clear : FlowerGlow.Dim;
    }

    private static int GlowScore(FlowerGlow glow, ScoreRuleSettings settings)
    {
        return glow == FlowerGlow.Bright ? settings.flowerLightBrightScore : glow == FlowerGlow.Clear ? settings.flowerLightClearScore : settings.flowerLightDimScore;
    }

    /// <summary>
    /// 놓인 꽃마다 최종 상태와, 효과가 사라진 그늘 칸. 먼저 이웃과의 관계로 기본 상태를 구한다.
    /// 별 칸의 꽃은 한 단계 올린다. 그늘 칸의 꽃은 이웃의 영향을 받지 않고 계속 흐림이다.
    /// 다만 이웃한 꽃 중 빛남이 둘 이상이 되면 그늘이 사라져 일반 칸이 되고, 그 꽃은 기본 상태로 돌아온다(사라진 그늘 칸이 다른 그늘 칸의 조건을 채울 수 있어 더 변하지 않을 때까지 되풀이한다).
    /// 특수 칸은 그 칸의 꽃에게만 작용하고 이웃 꽃의 관계 합계는 바꾸지 않는다. specials가 null이면 특수 칸이 없는 것이다.
    /// </summary>
    public static (Dictionary<WrapperRegionPosition, FlowerGlow> glows, HashSet<WrapperRegionPosition> clearedShades) Resolve(
        IReadOnlyDictionary<WrapperRegionPosition, FlowerData.Color> cells,
        IReadOnlyDictionary<WrapperRegionPosition, SpecialCell> specials = null)
    {
        var glows = new Dictionary<WrapperRegionPosition, FlowerGlow>();
        var cleared = new HashSet<WrapperRegionPosition>();
        foreach (WrapperRegionPosition position in cells.Keys) glows[position] = GlowOf(RelationSum(position, cells));
        if (specials == null) return (glows, cleared);

        var baseGlows = new Dictionary<WrapperRegionPosition, FlowerGlow>(glows);
        var shades = new List<WrapperRegionPosition>();
        foreach (WrapperRegionPosition position in cells.Keys)
        {
            if (!specials.TryGetValue(position, out SpecialCell kind)) continue;
            if (kind == SpecialCell.Star) glows[position] = Raised(glows[position]);
            else if (kind == SpecialCell.Shade)
            {
                glows[position] = FlowerGlow.Dim;
                shades.Add(position);
            }
        }

        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (WrapperRegionPosition shade in shades)
            {
                if (cleared.Contains(shade) || BrightNeighborCount(shade, glows) < ShadeClearBrightNeighbors) continue;
                cleared.Add(shade);
                glows[shade] = baseGlows[shade];
                changed = true;
            }
        }
        return (glows, cleared);
    }

    private static int BrightNeighborCount(WrapperRegionPosition position, Dictionary<WrapperRegionPosition, FlowerGlow> glows)
    {
        int count = 0;
        foreach (WrapperRegionPosition n in Neighbors(position))
        {
            if (glows.TryGetValue(n, out FlowerGlow g) && g == FlowerGlow.Bright) count++;
        }
        return count;
    }

    private static FlowerGlow Raised(FlowerGlow glow) => glow == FlowerGlow.Dim ? FlowerGlow.Clear : FlowerGlow.Bright;

    /// <summary>정산에 넣는 '꽃의 빛' 점수: (꽃 상태 점수 합계 ÷ 꽃 수) × 배율을 반올림. 상태별 점수와 배율은 ScoreRuleSettings 에셋 값이다. 꽃이 없으면 0.</summary>
    public static int Score(IReadOnlyDictionary<WrapperRegionPosition, FlowerData.Color> cells,
        IReadOnlyDictionary<WrapperRegionPosition, SpecialCell> specials, ScoreRuleSettings settings)
    {
        if (cells.Count == 0) return 0;
        int total = 0;
        foreach (FlowerGlow glow in Resolve(cells, specials).glows.Values) total += GlowScore(glow, settings);
        return (int)Math.Round(total * (double)settings.flowerLightScale / cells.Count, MidpointRounding.AwayFromZero);
    }
}
