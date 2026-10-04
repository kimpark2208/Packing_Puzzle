using System.Collections.Generic;
using System.Linq;

/// <summary>장부를 결과 팝업에 보여 줄 문구로 바꾼다(표시 전용).</summary>
public static class SettlementFormatter
{
    private static string Label(MoneyReason reason)
    {
        switch (reason)
        {
            case MoneyReason.ColorCombo: return "색 조합 보너스";
            case MoneyReason.RequirementMet: return "요구사항 달성";
            case MoneyReason.RequirementOver: return "요구사항 초과 달성";
            case MoneyReason.MoodGood: return "손님 기분 좋음";
            case MoneyReason.Collapse: return "꽃다발 붕괴";
            case MoneyReason.MoodBad: return "손님 기분 나쁨";
            case MoneyReason.RequirementMissed: return "요구사항 미달성";
            default: return reason.ToString();
        }
    }

    /// <summary>
    /// 보너스요소(가산)와 감점요소(감산)를 구분해 사유별 금액을 적는다. 해당 항목이 없는 쪽은 줄째로 생략한다.
    /// 예) 보너스요소 / 색 조합 보너스 +56 / 감점요소 / 꽃다발 붕괴 -100
    /// </summary>
    public static string Breakdown(MoneyLedger ledger)
    {
        var lines = new List<string>();
        AddSection(lines, "보너스요소", ledger.Entries.Where(e => e.Value > 0));
        AddSection(lines, "감점요소", ledger.Entries.Where(e => e.Value < 0));
        return string.Join("\n", lines);
    }

    private static void AddSection(List<string> lines, string header, IEnumerable<KeyValuePair<MoneyReason, int>> entries)
    {
        var list = entries.ToList();
        if (list.Count == 0) return;

        lines.Add(header);
        lines.AddRange(list.Select(e => $"{Label(e.Key)} {Signed(e.Value)}"));
    }

    private static string Signed(int value)
    {
        return value > 0 ? $"+{value}" : value.ToString();
    }
}
