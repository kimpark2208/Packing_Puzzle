// 점수 가감 규칙들. 규칙 하나가 클래스 하나이고, 서로를 모른다. 새 규칙은 IScoreRule을 구현해 PuzzleSettlement의 규칙 목록에 추가한다.

/// <summary>[가산/감산] 꽃의 빛: 꽃마다 이웃 칸과의 색 관계로 정해진 상태 점수의 평균 × 10(-20~+30).</summary>
public class FlowerLightRule : IScoreRule
{
    public void Evaluate(PuzzleContext context, ScoreLedger ledger)
    {
        ledger.Add(ScoreReason.FlowerLight, FlowerLight.Score(context.PlacedCells));
    }
}

/// <summary>[가산] 요구조건을 채웠으면 정해진 점수.</summary>
public class RequirementMetRule : IScoreRule
{
    private readonly int score;

    public RequirementMetRule(int score)
    {
        this.score = score;
    }

    public void Evaluate(PuzzleContext context, ScoreLedger ledger)
    {
        if (!context.RequirementMet) return;
        ledger.Add(ScoreReason.RequirementMet, score);
    }
}

/// <summary>[가산] 요구 개수보다 더 쓴 꽃 1송이당 점수.</summary>
public class RequirementOverRule : IScoreRule
{
    private readonly int perExtra;

    public RequirementOverRule(int perExtra)
    {
        this.perExtra = perExtra;
    }

    public void Evaluate(PuzzleContext context, ScoreLedger ledger)
    {
        int extra = context.RequirementUsed - context.Requirement.minCount;
        if (extra > 0) ledger.Add(ScoreReason.RequirementOver, extra * perExtra);
    }
}

/// <summary>[가산] 퍼즐이 끝난 순간 손님 기분이 기준 이상이면 보너스.</summary>
public class CustomerMoodGoodRule : IScoreRule
{
    private readonly float goodRatio;
    private readonly int bonus;

    public CustomerMoodGoodRule(float goodRatio, int bonus)
    {
        this.goodRatio = goodRatio;
        this.bonus = bonus;
    }

    public void Evaluate(PuzzleContext context, ScoreLedger ledger)
    {
        if (context.MoodRatio >= goodRatio) ledger.Add(ScoreReason.MoodGood, bonus);
    }
}

/// <summary>[감산] 꽃다발이 무너질 때마다 붕괴 1회당 점수.</summary>
public class CollapsePenaltyRule : IScoreRule
{
    private readonly int penalty;

    public CollapsePenaltyRule(int penalty)
    {
        this.penalty = penalty;
    }

    public void Evaluate(PuzzleContext context, ScoreLedger ledger)
    {
        if (context.CollapseCount > 0) ledger.Add(ScoreReason.Collapse, -penalty * context.CollapseCount);
    }
}

/// <summary>[감산] 퍼즐이 끝난 순간 손님 기분이 기준 이하일 때.</summary>
public class CustomerMoodBadRule : IScoreRule
{
    private readonly float badRatio;
    private readonly int penalty;

    public CustomerMoodBadRule(float badRatio, int penalty)
    {
        this.badRatio = badRatio;
        this.penalty = penalty;
    }

    public void Evaluate(PuzzleContext context, ScoreLedger ledger)
    {
        if (context.MoodRatio <= badRatio) ledger.Add(ScoreReason.MoodBad, -penalty);
    }
}

/// <summary>[감산] 요구조건을 못 채웠을 때 정해진 점수. 크게 정해서 다른 항목을 모두 잘해도 기본 보상 정도에 그친다.</summary>
public class RequirementMissedRule : IScoreRule
{
    private readonly int penalty;

    public RequirementMissedRule(int penalty)
    {
        this.penalty = penalty;
    }

    public void Evaluate(PuzzleContext context, ScoreLedger ledger)
    {
        if (context.RequirementMet) return;
        ledger.Add(ScoreReason.RequirementMissed, -penalty);
    }
}
