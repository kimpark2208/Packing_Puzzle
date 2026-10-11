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
