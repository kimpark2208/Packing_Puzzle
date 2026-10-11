using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 꽃 선택 화면의 선택 규칙(UI를 모르는 순수 계산).
/// - 역할마다 고를 수 있는 종류는 그 프리셋에서 그 역할의 칸 수까지이고, 전체 한도는 그 합이다(표시되는 분모와 실제로 고를 수 있는 수가 같다).
/// - 칸이 있는 역할은 최소 1종을 골라야 한다.
/// - 꽃은 놓는 만큼 재고에서 빠지므로, 한 역할의 고른 꽃들의 재고를 합쳐 그 역할의 칸 수 이상이어야 완성할 수 있다.
/// </summary>
public class FlowerSelection
{
    private readonly Dictionary<FlowerData.FlowerRole, int> roleLimits;
    private readonly Func<FlowerData, int> stockOf;
    private readonly HashSet<FlowerData> picked = new();

    /// <param name="roleLimits">역할별 칸 수(고를 수 있는 종류의 상한). 칸이 없는 역할은 0이거나 없다.</param>
    public FlowerSelection(Dictionary<FlowerData.FlowerRole, int> roleLimits, Func<FlowerData, int> stockOf)
    {
        this.roleLimits = roleLimits;
        this.stockOf = stockOf;
    }

    public int TotalLimit => roleLimits.Values.Sum();
    public int Total => picked.Count;
    public IReadOnlyCollection<FlowerData> Picked => picked;

    public int Limit(FlowerData.FlowerRole role) => roleLimits.TryGetValue(role, out int n) ? n : 0;
    public int Count(FlowerData.FlowerRole role) => picked.Count(f => f.flowerRole == role);
    public bool Contains(FlowerData flower) => picked.Contains(flower);

    private IEnumerable<FlowerData.FlowerRole> UsedRoles => roleLimits.Where(kv => kv.Value > 0).Select(kv => kv.Key);

    /// <summary>지금 이 꽃을 새로 담을 수 있는가(재고, 역할 한도).</summary>
    public bool CanAdd(FlowerData flower)
    {
        FlowerData.FlowerRole role = flower.flowerRole;
        return Limit(role) > 0 && stockOf(flower) > 0 && Count(role) < Limit(role);
    }

    /// <summary>꽃을 눌렀을 때: 담겨 있으면 비우고, 비어 있으면 담는다. 한도가 1종인 역할이 차 있으면 바꿔 담는다. 선택이 바뀌었으면 true.</summary>
    public bool Toggle(FlowerData flower)
    {
        if (picked.Remove(flower)) return true;

        FlowerData.FlowerRole role = flower.flowerRole;
        if (Limit(role) == 1 && Count(role) == 1 && stockOf(flower) > 0)
        {
            picked.RemoveWhere(f => f.flowerRole == role);
            picked.Add(flower);
            return true;
        }

        if (!CanAdd(flower)) return false;
        picked.Add(flower);
        return true;
    }

    /// <summary>칸이 있는 역할마다 최소 1종을 골랐고, 그 역할 꽃들의 재고가 그 역할의 칸 수를 채우는가.</summary>
    public bool IsValid => UsedRoles.All(r => Count(r) >= 1 && !IsShortOfStock(r));

    /// <summary>고른 꽃의 재고를 다 합쳐도 그 역할의 칸 수에 모자란 역할이 있는가(고른 뒤에야 알 수 있다).</summary>
    public bool HasStockShortfall => UsedRoles.Any(r => Count(r) >= 1 && IsShortOfStock(r));

    private bool IsShortOfStock(FlowerData.FlowerRole role)
    {
        return picked.Where(f => f.flowerRole == role).Sum(stockOf) < Limit(role);
    }
}
