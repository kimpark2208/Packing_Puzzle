/// <summary>
/// 점수 가감 규칙 하나. 상황(컨텍스트)을 보고 해당되면 장부에 점수 항목을 더한다.
/// 규칙 하나가 클래스 하나이고 서로를 모른다. 새 규칙은 이것을 구현해 PuzzleSettlement의 규칙 목록에 추가한다.
/// </summary>
public interface IScoreRule
{
    void Evaluate(PuzzleContext context, ScoreLedger ledger);
}
