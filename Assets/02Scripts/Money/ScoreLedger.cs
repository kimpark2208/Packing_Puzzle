using System.Collections.Generic;
using System.Linq;

/// <summary>점수가 가감된 이유. 선언 순서가 같은 부호끼리의 표시 순서다.</summary>
public enum ScoreReason
{
    ColorCombo,          // 가산: 색 조합
    RequirementMet,      // 가산: 주문 요구조건 충족
    RequirementOver,     // 가산: 요구보다 더 많이 사용
    MoodGood,            // 가산: 손님 기분 좋음
    Collapse,            // 감산: 꽃다발 붕괴(횟수만큼)
    MoodBad,             // 감산: 손님 기분 나쁨
    RequirementMissed    // 감산: 주문 요구조건 미충족
}

/// <summary>
/// 퍼즐 한 판의 점수 가감 기록. 이유별로 합산한 점수만 들고 있다(규칙 판단도 지급도 하지 않는다).
/// </summary>
public class ScoreLedger
{
    private readonly Dictionary<ScoreReason, int> points = new();

    /// <summary>같은 이유는 점수가 합쳐진다. 0은 기록하지 않는다.</summary>
    public void Add(ScoreReason reason, int amount)
    {
        if (amount == 0) return;
        points[reason] = Get(reason) + amount;
    }

    public int Get(ScoreReason reason)
    {
        return points.TryGetValue(reason, out int v) ? v : 0;
    }

    public int Total => points.Values.Sum();

    /// <summary>기록된 이유와 점수(이유 선언 순서).</summary>
    public IEnumerable<KeyValuePair<ScoreReason, int>> Entries => points.OrderBy(kv => (int)kv.Key);
}
