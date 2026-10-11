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
