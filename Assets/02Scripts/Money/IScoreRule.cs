/// <summary>점수 가감 규칙 하나. 상황(컨텍스트)을 보고 해당되면 장부에 점수 항목을 더한다.</summary>
public interface IScoreRule
{
    void Evaluate(PuzzleContext context, ScoreLedger ledger);
}
