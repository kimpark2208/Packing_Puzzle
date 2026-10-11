/// <summary>[가산/감산] 꽃의 빛: 꽃마다 이웃 칸과의 색 관계로 정해진 상태 점수의 평균 × 10(-20~+30).</summary>
public class FlowerLightRule : IScoreRule
{
    public void Evaluate(PuzzleContext context, ScoreLedger ledger)
    {
        ledger.Add(ScoreReason.FlowerLight, FlowerLight.Score(context.PlacedCells, context.SpecialCells));
    }
}
