using UnityEngine;

/// <summary>
/// GameManager의 밤 상점: 하루 영업이 끝나면 하루 결산보다 먼저 같은 메인 씬(01DayMain)을 밤 모습으로 다시 열어 상점을 보여 주고, 상점을 마치면 하루 결산(03Result)으로 간다.
/// 방문 한 번의 진열과 리롤 횟수는 FlowerShop이 들고 있다(씬이 다시 열려도 방문이 끝날 때까지 같은 진열이다).
/// </summary>
public partial class GameManager
{
    /// <summary>지금 밤 상점 방문 중인가(메인 화면이 손님 대신 상점을 보여 준다).</summary>
    public bool ShopPending { get; private set; }

    /// <summary>이번 밤 상점 방문(방문 중이 아니면 null).</summary>
    public FlowerShop Shop { get; private set; }

    /// <summary>하루 영업이 끝났다: 밤 상점이 열린 메인 화면으로 간다.</summary>
    private void OpenShop()
    {
        CurrencyManager wallet = CurrencyManager.Instance;
        DataRegistry registry = DataRegistry.Instance;
        if (wallet == null || registry == null)
        {
            SceneTransition.Go(SceneResult); // 상점을 열 수 없으면(씬 단독 실행) 바로 결산으로 간다
            return;
        }

        Shop = new FlowerShop(registry.Flowers, wallet.HasObtainedFlower, wallet.CurrentDay);
        ShopPending = true;
        SceneTransition.Go(SceneDayMain);
    }

    /// <summary>상점을 마치고 하루 결산으로 간다.</summary>
    public void CloseShop()
    {
        ShopPending = false;
        Shop = null;
        SceneTransition.Go(SceneResult);
    }
}
