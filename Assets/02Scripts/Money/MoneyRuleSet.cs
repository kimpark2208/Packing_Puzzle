using System.Collections.Generic;

/// <summary>쓸 규칙들을 설정 수치로 조립하는 곳. 규칙을 더하거나 빼려면 여기만 고친다.</summary>
public static class MoneyRuleSet
{
    public static List<IMoneyRule> Create(MoneyRuleSettings s)
    {
        return new List<IMoneyRule>
        {
            // 가산
            new ColorComboRule(),
            new RequirementMetRule(),
            new RequirementOverRule(s.overAchievePerExtra),
            new CustomerMoodGoodRule(s.goodMoodRatio, s.goodMoodBonus),
            // 감산
            new CollapsePenaltyRule(s.collapsePenalty),
            new CustomerMoodBadRule(s.badMoodRatio, s.badMoodPenalty),
            new RequirementMissedRule(s.missedPenaltyRatio),
        };
    }
}
