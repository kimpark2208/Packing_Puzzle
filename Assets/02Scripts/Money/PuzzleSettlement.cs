using UnityEngine;

/// <summary>정산 결과: 점수 기록, 합계 점수(-100~+100), 퍼즐 기본 금액, 구간 배율, 실제 지급액, 요구조건 충족 여부(손님 반응 대사에 쓴다).</summary>
public readonly struct SettlementResult
{
    public SettlementResult(ScoreLedger ledger, int score, int baseAmount, float multiplier, int payout, bool requirementMet)
    {
        Ledger = ledger;
        Score = score;
        BaseAmount = baseAmount;
        Multiplier = multiplier;
        Payout = payout;
        RequirementMet = requirementMet;
    }

    public ScoreLedger Ledger { get; }

    /// <summary>점수 합계를 -100~+100으로 자른 값. 결과창 게이지가 이 값을 보여 준다.</summary>
    public int Score { get; }

    /// <summary>이 퍼즐의 기본 금액(주문에 정해진다).</summary>
    public int BaseAmount { get; }

    /// <summary>점수 구간의 재화 배율.</summary>
    public float Multiplier { get; }

    /// <summary>실제로 지갑에 더해진 금액(기본 금액 × 배율).</summary>
    public int Payout { get; }
    public bool RequirementMet { get; }
}

/// <summary>
/// 퍼즐 종료 정산의 순서만 맡는다: 상황 수집 → 규칙으로 점수 기록 → 점수 구간 배율로 지급액 계산 → 지급.
/// 각 단계의 내용은 각자 별도 클래스가 한다. 쓸 규칙은 생성자에서 조립한다. 규칙을 더하거나 빼려면 여기만 고친다.
/// </summary>
public class PuzzleSettlement
{
    private readonly IScoreRule[] rules;

    public PuzzleSettlement(ScoreRuleSettings s)
    {
        rules = new IScoreRule[]
        {
            // 가산
            new FlowerLightRule(),
            new RequirementMetRule(s.requirementMetScore),
            new RequirementOverRule(s.overAchievePerExtra),
            new CustomerMoodGoodRule(s.goodMoodRatio, s.goodMoodBonus),
            // 감산
            new CollapsePenaltyRule(s.collapsePenalty),
            new CustomerMoodBadRule(s.badMoodRatio, s.badMoodPenalty),
            new RequirementMissedRule(s.requirementMissedPenalty),
        };
    }

    public SettlementResult Settle(PuzzleEndKind endKind, int collapseCount)
    {
        PuzzleContext context = PuzzleContextBuilder.Build(endKind, collapseCount);

        var ledger = new ScoreLedger();
        foreach (IScoreRule rule in rules) rule.Evaluate(context, ledger);

        int score = ScoreTier.Clamp(ledger.Total);
        float multiplier = ScoreTier.Multiplier(ScoreTier.Tick(score));
        int baseAmount = context.Requirement.baseAmount;
        int payout = Mathf.RoundToInt(baseAmount * multiplier);

        CurrencyManager.Instance.AddMoney(payout);
        return new SettlementResult(ledger, score, baseAmount, multiplier, payout, context.RequirementMet);
    }
}
