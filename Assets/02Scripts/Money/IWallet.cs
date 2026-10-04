/// <summary>돈을 더하거나 빼는 지갑. 정산이 구체적인 CurrencyManager가 아니라 이것에 의존한다.</summary>
public interface IWallet
{
    void AddMoney(int amount);
}
