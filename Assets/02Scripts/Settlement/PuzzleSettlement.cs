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

    /// <summary>지급할 금액(기본 금액 × 배율). 지갑에 더하는 일은 정산을 부른 쪽이 한다.</summary>
    public int Payout { get; }
    public bool RequirementMet { get; }
}

/// <summary>
/// 퍼즐 종료 정산: 상황 수집(PuzzleContext.Capture) → 점수 가감 규칙으로 장부에 기록 → 점수 구간 배율로 지급액 계산. 지갑에 더하지는 않는다.
/// 점수 규칙은 아래 Score 한 곳에 모여 있다. 수치는 ScoreRuleSettings 에셋에서 조정한다.
/// </summary>
public class PuzzleSettlement
{
    private readonly ScoreRuleSettings settings;

    public PuzzleSettlement(ScoreRuleSettings settings)
    {
        this.settings = settings;
    }

    public SettlementResult Settle(PuzzleEndKind endKind, int collapseCount)
    {
        PuzzleContext context = PuzzleContext.Capture(endKind, collapseCount);

        var ledger = new ScoreLedger();
        Score(context, ledger);

        int score = ScoreTier.Clamp(ledger.Total);
        float multiplier = ScoreTier.Multiplier(ScoreTier.Tick(score));
        int baseAmount = context.Requirement.baseAmount;
        int payout = Mathf.RoundToInt(baseAmount * multiplier);

        return new SettlementResult(ledger, score, baseAmount, multiplier, payout, context.RequirementMet);
    }

    /// <summary>점수 가감 규칙. 상황(context)을 보고 해당되는 항목을 장부에 더한다.</summary>
    private void Score(PuzzleContext context, ScoreLedger ledger)
    {
        // 가산/감산: 꽃의 빛(꽃마다 이웃 칸과의 색 관계로 정해진 상태 점수의 평균 × 10, -20~+30)
        ledger.Add(ScoreReason.FlowerLight, FlowerLight.Score(context.PlacedCells, context.SpecialCells));

        // 주문 요구조건: 채웠으면 가산(더 쓴 만큼 보너스), 못 채웠으면 크게 감산(다른 항목을 모두 잘해도 기본 보상 정도에 그치게)
        if (context.RequirementMet)
        {
            ledger.Add(ScoreReason.RequirementMet, settings.requirementMetScore);

            int extra = context.RequirementUsed - context.Requirement.minCount;
            if (extra > 0) ledger.Add(ScoreReason.RequirementOver, extra * settings.overAchievePerExtra);
        }
        else
        {
            ledger.Add(ScoreReason.RequirementMissed, -settings.requirementMissedPenalty);
        }

        // 퍼즐이 끝난 순간 손님 기분
        if (context.MoodRatio >= settings.goodMoodRatio) ledger.Add(ScoreReason.MoodGood, settings.goodMoodBonus);
        if (context.MoodRatio <= settings.badMoodRatio) ledger.Add(ScoreReason.MoodBad, -settings.badMoodPenalty);

        // 꽃다발이 무너질 때마다 붕괴 1회당 감산
        if (context.CollapseCount > 0) ledger.Add(ScoreReason.Collapse, -settings.collapsePenalty * context.CollapseCount);
    }
}
