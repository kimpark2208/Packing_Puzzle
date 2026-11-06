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

    private readonly List<FlowerData> flowers;
    private readonly Func<int, bool> isObtained;
    private readonly int day;
    private readonly List<FlowerData> pool = new();       // 지금 진열 가능한 꽃(획득한 꽃 + 이번 판정에 붙은 신규 꽃)
    private readonly List<FlowerData> newFlowers = new(); // 그중 이번 판정에 붙은 신규 꽃
    private readonly List<FlowerData> shelf = new();
    private readonly HashSet<int> soldOut = new(); // 지금 진열에서 이미 산 꽃(진열마다 한 번만 살 수 있고, 리롤하면 품절이 풀린다)

    public IReadOnlyList<FlowerData> Shelf => shelf;

    public bool IsSoldOut(FlowerData flower) => soldOut.Contains(flower.flowerID);
    public int RerollsLeft { get; private set; } = RerollsPerVisit;

    /// <summary>리롤 버튼을 보여 주는가: 항상 보인다(기획서는 진열 가능한 꽃이 6종류 이하면 숨기지만, 리롤로 신규 꽃 판정을 다시 하므로 보이게 했다).</summary>
    public bool RerollAvailable => true;

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
        return day < flower.acquireDay ? 0f : Mathf.Clamp01(ChancePerDay * (day - flower.acquireDay + 1));
    }

    /// <summary>신규 꽃 등장 판정을 다시 하고 진열대를 새로 채운다. 리롤을 다 썼거나 버튼이 없는 상태면 false.</summary>
    public bool Reroll()
    {
        if (!RerollAvailable || RerollsLeft <= 0) return false;

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
