using System.Collections.Generic;

/// <summary>규칙 목록을 차례로 적용해 장부를 만든다. 어떤 규칙이 있는지는 모른다(계산만 하고 지급·표시는 하지 않는다).</summary>
public class MoneyCalculator
{
    private readonly IReadOnlyList<IMoneyRule> rules;

    public MoneyCalculator(IReadOnlyList<IMoneyRule> rules)
    {
        this.rules = rules;
    }

    public MoneyLedger Calculate(PuzzleContext context)
    {
        var ledger = new MoneyLedger();
        foreach (IMoneyRule rule in rules) rule.Evaluate(context, ledger);
        return ledger;
    }
}
