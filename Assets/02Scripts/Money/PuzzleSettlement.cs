/// <summary>정산 결과: 금액 기록과 요구조건 충족 여부(손님 반응 대사에 쓴다).</summary>
public readonly struct SettlementResult
{
    public SettlementResult(MoneyLedger ledger, int payout, bool requirementMet)
    {
        Ledger = ledger;
        Payout = payout;
        RequirementMet = requirementMet;
    }

    public MoneyLedger Ledger { get; }

    /// <summary>실제로 지갑에 더해진 금액. 합계가 음수여도 돈을 깎지 않으므로 0 이상이다.</summary>
    public int Payout { get; }
    public bool RequirementMet { get; }
}

/// <summary>
/// 퍼즐 종료 정산의 순서만 맡는다: 상황 수집 → 규칙 적용 → 지급. 각 단계의 내용은 각자 별도 클래스가 한다.
/// 쓸 규칙은 생성자에서 조립한다. 규칙을 더하거나 빼려면 여기만 고친다.
/// </summary>
public class PuzzleSettlement
{
    private readonly IMoneyRule[] rules;

    public PuzzleSettlement(MoneyRuleSettings s)
    {
        rules = new IMoneyRule[]
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

    public SettlementResult Settle(PuzzleEndKind endKind, int collapseCount)
    {
        PuzzleContext context = PuzzleContextBuilder.Build(endKind, collapseCount);

        var ledger = new MoneyLedger();
        foreach (IMoneyRule rule in rules) rule.Evaluate(context, ledger);

        int payout = System.Math.Max(0, ledger.Total); // 손해가 나도 받을 돈이 없을 뿐, 가진 돈을 깎지는 않는다
        CurrencyManager.Instance.AddMoney(payout);
        return new SettlementResult(ledger, payout, context.RequirementMet);
    }
}
