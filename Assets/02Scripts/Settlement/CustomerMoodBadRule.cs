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
