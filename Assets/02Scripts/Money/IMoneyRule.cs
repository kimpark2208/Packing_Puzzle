/// <summary>금액 가감 규칙 하나. 상황(컨텍스트)을 보고 해당되면 장부에 항목을 더한다.</summary>
public interface IMoneyRule
{
    void Evaluate(PuzzleContext context, MoneyLedger ledger);
}
