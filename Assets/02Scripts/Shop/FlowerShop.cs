using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 밤 상점 한 번의 방문(게임 개편 2 기획서 3장). 진열대에는 꽃이 최대 6종류 놓이고, 리롤은 방문당 2번 할 수 있다.
/// 진열 가능한 꽃은 이미 획득한 꽃과, 신규 꽃 등장 판정에 붙은 꽃이다.
/// 신규 꽃은 획득 일차(FlowerData.acquireDay)부터 25%에서 시작해 하루마다 25%씩 늘고 4일째에 100%가 된다.
/// 등장 판정은 방문을 시작할 때와 리롤할 때마다 새로 한다(기획서는 방문당 한 번이지만 리롤하면 다시 하기로 했다).
/// 판정에 붙은 신규 꽃은 그 진열에 반드시 나온다.
/// 화면(ShopUI)을 모르는 순수 논리라서, 방문이 시작될 때 만들어 GameManager가 방문이 끝날 때까지 들고 있는다.
/// </summary>
public class FlowerShop
{
    public const int ShelfSize = 6;
    public const int RerollsPerVisit = 2;
    private const float ChancePerDay = 0.25f;
    public const int BetaSnapdragonId = 1;  // 금어초(Flower_01_Snapdragon). 신규 꽃 구매 튜토리얼이 이 꽃을 사게 한다
    private const int BetaSnapdragonDay = 7; // 베타에서 금어초가 확정으로 나오는 밤(기획서 3장)

    private readonly List<FlowerData> flowers;
    private readonly Func<int, bool> isObtained;
    private readonly int day;
    private readonly List<FlowerData> pool = new();       // 지금 진열 가능한 꽃(획득한 꽃 + 이번 판정에 붙은 신규 꽃)
    private readonly List<FlowerData> newFlowers = new(); // 그중 이번 판정에 붙은 신규 꽃
    private readonly List<FlowerData> shelf = new();
    private readonly HashSet<int> soldOut = new(); // 지금 진열에서 이미 산 꽃(진열마다 한 번만 살 수 있고, 리롤하면 품절이 풀린다)

    public IReadOnlyList<FlowerData> Shelf => shelf;

    public bool IsSoldOut(FlowerData flower) => soldOut.Contains(flower.flowerID);
    /// <summary>이번 진열 판정에 붙은 신규 꽃인가(NEW 뱃지 표시용). 리롤하면 판정을 다시 해서 값이 바뀐다.</summary>
    public bool IsNew(FlowerData flower) => newFlowers.Contains(flower);
    public int RerollsLeft { get; private set; } = RerollsPerVisit;

    /// <summary>
    /// 지금 꼭 사야 하는 꽃(없으면 null). 신규 꽃 구매 튜토리얼: 시작 꽃 말고는 한 번도 사 본 적이 없을 때 처음 진열대에 나온 신규 꽃, 베타 강제: 7일차 밤의 금어초.
    /// 살 돈이 없으면 막히지 않게 null이다. 사야 하는 동안은 상점을 마칠 수 없다.
    /// </summary>
    public FlowerData ForcedFlower(CurrencyManager wallet)
    {
        FlowerData flower = day == BetaSnapdragonDay ? shelf.FirstOrDefault(f => f.flowerID == BetaSnapdragonId && !wallet.HasObtainedFlower(f.flowerID)) : null;
        if (flower == null && !flowers.Any(f => f.acquireDay > 0 && wallet.HasObtainedFlower(f.flowerID))) flower = shelf.FirstOrDefault(IsNew);
        return flower != null && wallet.CurrentMoney >= flower.shopPrice ? flower : null;
    }

    public FlowerShop(IEnumerable<FlowerData> allFlowers, Func<int, bool> isObtained, int day)
    {
        flowers = allFlowers.Where(f => f != null).ToList();
        this.isObtained = isObtained;
        this.day = day;
        Fill();
    }

    /// <summary>획득하지 않은 꽃이 day일차 상점에 나올 확률: 획득 일차부터 25%, 하루마다 25%씩 늘어 4일째 100%.</summary>
    public static float AppearChance(FlowerData flower, int day)
    {
        if (flower.flowerID == BetaSnapdragonId && day <= BetaSnapdragonDay) return day == BetaSnapdragonDay ? 1f : 0f; // 베타 고정: 금어초는 7일차 밤에 100% 나오고 그 전에는 나오지 않는다
        return day < flower.acquireDay ? 0f : Mathf.Clamp01(ChancePerDay * (day - flower.acquireDay + 1));
    }

    /// <summary>신규 꽃 등장 판정을 다시 하고 진열대를 새로 채운다. 리롤을 다 썼으면 false.</summary>
    public bool Reroll()
    {
        if (RerollsLeft <= 0) return false;

        RerollsLeft--;
        Fill();
        return true;
    }

    /// <summary>꽃 한 묶음을 산다(한 진열에 꽃마다 한 번, 산 꽃은 품절). 돈이 모자라거나 품절이면 false. firstTime은 이 꽃을 처음 얻었는가.</summary>
    public bool TryBuy(FlowerData flower, CurrencyManager wallet, out bool firstTime)
    {
        firstTime = !wallet.HasObtainedFlower(flower.flowerID);
        if (IsSoldOut(flower) || !wallet.TrySpendMoney(flower.shopPrice)) return false;

        wallet.AddFlowerStock(flower.flowerID, flower.shopBundle);
        soldOut.Add(flower.flowerID);
        return true;
    }

    /// <summary>진열 가능한 꽃을 판정으로 정하고(획득한 꽃 + 신규 꽃), 신규 꽃을 먼저 놓은 뒤 나머지 칸을 무작위로 채운다.</summary>
    private void Fill()
    {
        soldOut.Clear();
        pool.Clear();
        newFlowers.Clear();
        foreach (FlowerData flower in flowers)
        {
            if (isObtained(flower.flowerID))
            {
                pool.Add(flower);
            }
            else if (UnityEngine.Random.value < AppearChance(flower, day))
            {
                pool.Add(flower);
                newFlowers.Add(flower);
            }
        }

        shelf.Clear();
        shelf.AddRange(newFlowers.Take(ShelfSize));
        shelf.AddRange(pool.Where(f => !shelf.Contains(f)).OrderBy(_ => UnityEngine.Random.value).Take(ShelfSize - shelf.Count));
    }
}
