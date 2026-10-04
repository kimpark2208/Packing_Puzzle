// 금액 가감 규칙들. 규칙 하나가 클래스 하나이고, 서로를 모른다. 새 규칙은 IMoneyRule을 구현해 PuzzleSettlement의 규칙 목록에 추가한다.

/// <summary>[가산] 놓은 순서대로 이웃한 두 꽃의 색 조합 점수 합.</summary>
public class ColorComboRule : IMoneyRule
{
    public void Evaluate(PuzzleContext context, MoneyLedger ledger)
    {
        int sum = 0;
        for (int i = 0; i < context.PlacedColors.Count - 1; i++)
        {
            sum += ColorCompatDatabase.GetBonus(context.PlacedColors[i], context.PlacedColors[i + 1]);
        }
        ledger.Add(MoneyReason.ColorCombo, sum);
    }
}

/// <summary>[가산] 요구조건을 채웠으면 주문에 정해진 보너스.</summary>
public class RequirementMetRule : IMoneyRule
{
    public void Evaluate(PuzzleContext context, MoneyLedger ledger)
    {
        if (!context.RequirementMet) return;
        ledger.Add(MoneyReason.RequirementMet, context.Requirement.bonusAmount);
    }
}

/// <summary>[가산] 요구 개수보다 더 쓴 꽃 1송이당 금액.</summary>
public class RequirementOverRule : IMoneyRule
{
    private readonly int perExtra;

    public RequirementOverRule(int perExtra)
    {
        this.perExtra = perExtra;
    }

    public void Evaluate(PuzzleContext context, MoneyLedger ledger)
    {
        int extra = context.RequirementUsed - context.Requirement.minCount;
        if (extra > 0) ledger.Add(MoneyReason.RequirementOver, extra * perExtra);
    }
}

/// <summary>[가산] 퍼즐이 끝난 순간 손님 기분이 기준 이상이면 보너스.</summary>
public class CustomerMoodGoodRule : IMoneyRule
{
    private readonly float goodRatio;
    private readonly int bonus;

    public CustomerMoodGoodRule(float goodRatio, int bonus)
    {
        this.goodRatio = goodRatio;
        this.bonus = bonus;
    }

    public void Evaluate(PuzzleContext context, MoneyLedger ledger)
    {
        if (context.MoodRatio >= goodRatio) ledger.Add(MoneyReason.MoodGood, bonus);
    }
}

/// <summary>[감산] 꽃다발이 무너질 때마다 붕괴 1회당 금액.</summary>
public class CollapsePenaltyRule : IMoneyRule
{
    private readonly int penalty;

    public CollapsePenaltyRule(int penalty)
    {
        this.penalty = penalty;
    }

    public void Evaluate(PuzzleContext context, MoneyLedger ledger)
    {
        if (context.CollapseCount > 0) ledger.Add(MoneyReason.Collapse, -penalty * context.CollapseCount);
    }
}

/// <summary>[감산] 퍼즐이 끝난 순간 손님 기분이 기준 이하일 때.</summary>
public class CustomerMoodBadRule : IMoneyRule
{
    private readonly float badRatio;
    private readonly int penalty;

    public CustomerMoodBadRule(float badRatio, int penalty)
    {
        this.badRatio = badRatio;
        this.penalty = penalty;
    }

    public void Evaluate(PuzzleContext context, MoneyLedger ledger)
    {
        if (context.MoodRatio <= badRatio) ledger.Add(MoneyReason.MoodBad, -penalty);
    }
}

/// <summary>[감산] 요구조건을 못 채웠을 때, 주문 보너스의 일정 비율.</summary>
public class RequirementMissedRule : IMoneyRule
{
    private readonly float ratio;

    public RequirementMissedRule(float ratio)
    {
        this.ratio = ratio;
    }

    public void Evaluate(PuzzleContext context, MoneyLedger ledger)
    {
        if (context.RequirementMet) return;
        ledger.Add(MoneyReason.RequirementMissed, -UnityEngine.Mathf.RoundToInt(context.Requirement.bonusAmount * ratio));
    }
}
