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
