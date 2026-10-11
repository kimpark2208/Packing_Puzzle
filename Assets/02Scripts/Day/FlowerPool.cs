using System;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

/// <summary>
/// 꽃 선택에서 고른 꽃들로 버킷에 보여 줄 꽃을 뽑는다(UI를 모르는 순수 계산).
/// - 역할은 프리셋에서 그 역할의 칸 수에 비례해서 고른다(칸이 많은 역할이 더 자주 나온다). 그 역할의 꽃 중에서는 같은 확률이다.
/// - 이번 퍼즐에서 놓은 만큼을 재고에서 빼고, 재고가 다 떨어진 꽃은 더 뽑지 않는다.
/// - 한 번에 뽑는 꽃은 서로 다른 종류이고, 피하라고 한 꽃(방금 보여 준 꽃)은 가능하면 피한다.
/// </summary>
public class FlowerPool
{
    private readonly List<FlowerData> flowers;
    private readonly Dictionary<FlowerData.FlowerRole, int> roleWeights = new();
    private readonly Func<FlowerData, int> stockOf;
    private readonly Dictionary<FlowerData, int> placed = new();

    /// <param name="preset">역할 비율을 정하는 프리셋. null이면 역할마다 같은 비율로 본다.</param>
    /// <param name="stockOf">꽃의 보유 수량. null이면 재고 제한이 없다.</param>
    public FlowerPool(IEnumerable<FlowerData> flowers, WrapperPresetData preset, Func<FlowerData, int> stockOf)
    {
        this.flowers = flowers.ToList();
        this.stockOf = stockOf;
        foreach (FlowerData.FlowerRole role in Enum.GetValues(typeof(FlowerData.FlowerRole)))
            roleWeights[role] = preset != null ? preset.CountByTag(role) : 1;
    }

    public bool IsEmpty => flowers.Count == 0;

    /// <summary>꽃 하나를 놓았다(재고에서 빠진다).</summary>
    public void MarkPlaced(FlowerData flower)
    {
        placed.TryGetValue(flower, out int n);
        placed[flower] = n + 1;
    }

    /// <summary>붕괴로 퍼즐을 처음부터 다시 할 때: 놓은 기록을 지운다(놓은 꽃은 소모되지 않았다).</summary>
    public void ResetPlaced()
    {
        placed.Clear();
    }

    private int Remaining(FlowerData flower)
    {
        if (stockOf == null) return int.MaxValue;
        placed.TryGetValue(flower, out int n);
        return stockOf(flower) - n;
    }

    /// <summary>서로 다른 꽃을 count송이까지 뽑는다. 뽑을 수 있는 꽃이 모자라면 그만큼만 돌려준다.</summary>
    public List<FlowerData> Draw(int count, ICollection<FlowerData> avoid = null)
    {
        List<FlowerData> available = flowers.Where(f => Remaining(f) > 0).ToList();
        var picks = new List<FlowerData>();

        PickInto(picks, available.Where(f => avoid == null || !avoid.Contains(f)).ToList(), count);
        PickInto(picks, available.Where(f => !picks.Contains(f)).ToList(), count); // 새 꽃이 모자라면 방금 보여 준 꽃으로 채운다
        return picks;
    }

    private void PickInto(List<FlowerData> picks, List<FlowerData> candidates, int count)
    {
        while (picks.Count < count && candidates.Count > 0)
        {
            FlowerData.FlowerRole role = PickRole(candidates);
            List<FlowerData> ofRole = candidates.Where(f => f.flowerRole == role).ToList();
            FlowerData pick = ofRole[Random.Range(0, ofRole.Count)];
            picks.Add(pick);
            candidates.Remove(pick);
        }
    }

    /// <summary>후보가 있는 역할 중에서 프리셋의 칸 수 비율로 하나 고른다. 모든 후보 역할의 칸이 0이면 후보가 있는 역할을 같은 확률로 고른다.</summary>
    private FlowerData.FlowerRole PickRole(List<FlowerData> candidates)
    {
        List<FlowerData.FlowerRole> roles = candidates.Select(f => f.flowerRole).Distinct().ToList();
        int total = roles.Sum(r => roleWeights[r]);
        if (total <= 0) return roles[Random.Range(0, roles.Count)];

        int roll = Random.Range(0, total);
        foreach (FlowerData.FlowerRole role in roles)
        {
            roll -= roleWeights[role];
            if (roll < 0) return role;
        }
        return roles[roles.Count - 1];
    }
}
