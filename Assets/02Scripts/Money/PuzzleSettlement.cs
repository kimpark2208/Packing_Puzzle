/// <summary>정산 결과: 금액 기록과 요구조건 충족 여부(손님 반응 대사에 쓴다).</summary>
public readonly struct SettlementResult
{
    public SettlementResult(MoneyLedger ledger, int payout, bool requirementMet)
    {
        Ledger = ledger;
        Payout = payout;
        RequirementMet = requirementMet;
    }

    public MoneyLedger Ledger { get; }

    /// <summary>실제로 지갑에 더해진 금액. 합계가 음수여도 돈을 깎지 않으므로 0 이상이다.</summary>
    public int Payout { get; }
    public bool RequirementMet { get; }
}

/// <summary>퍼즐 종료 정산의 순서만 맡는다: 상황 수집 → 계산 → 지급. 각 단계의 내용은 각자 별도 클래스가 한다.</summary>
public class PuzzleSettlement
{
    private readonly MoneyCalculator calculator;
    private readonly IWallet wallet;

    public PuzzleSettlement(MoneyCalculator calculator, IWallet wallet)
    {
        this.calculator = calculator;
        this.wallet = wallet;
    }

    public SettlementResult Settle(PuzzleEndKind endKind, int collapseCount)
    {
        PuzzleContext context = PuzzleContextBuilder.Build(endKind, collapseCount);
        MoneyLedger ledger = calculator.Calculate(context);
        int payout = System.Math.Max(0, ledger.Total); // 손해가 나도 받을 돈이 없을 뿐, 가진 돈을 깎지는 않는다
        wallet.AddMoney(payout);
        return new SettlementResult(ledger, payout, context.RequirementMet);
    }
}
