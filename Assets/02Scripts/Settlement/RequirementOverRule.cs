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
