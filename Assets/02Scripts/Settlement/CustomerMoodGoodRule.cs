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
