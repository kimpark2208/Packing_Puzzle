using System.Collections.Generic;
using System.Linq;

/// <summary>금액이 가감된 이유. 선언 순서가 결과 표시 순서다(가산 → 감산).</summary>
public enum MoneyReason
{
    ColorCombo,          // 가산: 색 조합 보너스
    RequirementMet,      // 가산: 주문 요구조건 충족
    RequirementOver,     // 가산: 요구보다 더 많이 사용
    MoodGood,            // 가산: 손님 기분 좋음
    Collapse,            // 감산: 꽃다발 붕괴(횟수만큼)
    MoodBad,             // 감산: 손님 기분 나쁨
    RequirementMissed    // 감산: 주문 요구조건 미충족
}

/// <summary>
/// 퍼즐 한 판의 금액 가감 기록. 이유별로 합산한 금액만 들고 있다(규칙 판단도 지급도 하지 않는다).
/// </summary>
public class MoneyLedger
{
    private readonly Dictionary<MoneyReason, int> amounts = new();

    /// <summary>같은 이유는 금액이 합쳐진다. 0은 기록하지 않는다.</summary>
    public void Add(MoneyReason reason, int amount)
    {
        if (amount == 0) return;
        amounts[reason] = Get(reason) + amount;
    }

    public int Get(MoneyReason reason)
    {
        return amounts.TryGetValue(reason, out int v) ? v : 0;
    }

    public int Total => amounts.Values.Sum();
    public int Gains => amounts.Values.Where(v => v > 0).Sum();
    public int Losses => amounts.Values.Where(v => v < 0).Sum();

    /// <summary>기록된 이유와 금액(이유 선언 순서).</summary>
    public IEnumerable<KeyValuePair<MoneyReason, int>> Entries => amounts.OrderBy(kv => (int)kv.Key);
}
