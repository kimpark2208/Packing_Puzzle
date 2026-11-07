using System;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

/// <summary>
/// 버킷에 보여 줄 꽃을 뽑는다(UI를 모르는 순수 계산).
/// - 꽃 선정 화면에서 고른 꽃 목록이 그대로 꽃 공급이다(exact): 한 꽃은 고른 송이 수만큼만 나오고, 놓은 만큼 줄어든다.
///   공급은 포장지 칸 수와 같으므로, 모든 칸을 채우면 공급도 딱 떨어진다. 공급이 많이 남은 꽃일수록 더 자주 뽑힌다.
/// - exact가 아니면(씬 단독 실행의 시험용 풀) 수량 제한 없이 종류마다 같은 확률로 뽑는다.
/// - 한 번에 뽑는 꽃은 서로 다른 종류이고, 피하라고 한 꽃(방금 보여 준 꽃)은 가능하면 피한다.
/// </summary>
public class FlowerPool
{
    private readonly List<FlowerData> flowers; // 종류(중복 없음)
    private readonly Dictionary<FlowerData, int> supply = new(); // 꽃 → 고른 송이 수
    private readonly Dictionary<FlowerData, int> placed = new();
    private readonly bool exact;

    /// <param name="chosen">고른 꽃 목록(같은 꽃이 송이 수만큼 반복된다).</param>
    /// <param name="exact">고른 송이 수까지만 뽑는가.</param>
    public FlowerPool(IEnumerable<FlowerData> chosen, bool exact = true)
    {
        this.exact = exact;
        foreach (FlowerData flower in chosen)
        {
            supply.TryGetValue(flower, out int n);
            supply[flower] = n + 1;
        }
        flowers = supply.Keys.ToList();
    }

    public bool IsEmpty => flowers.Count == 0;

    /// <summary>더 뽑을 수 있는 꽃이 남아 있는가(고른 송이를 다 놓았으면 false). 수량 제한 없는 풀은 꽃이 있으면 항상 true.</summary>
    public bool HasRemaining => flowers.Any(f => Remaining(f) > 0);

    /// <summary>꽃 하나를 놓았다(공급에서 빠진다).</summary>
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
        if (!exact) return int.MaxValue;
        placed.TryGetValue(flower, out int n);
        return supply[flower] - n;
    }

    private int Weight(FlowerData flower) => exact ? Math.Max(1, Remaining(flower)) : 1;

    /// <summary>
    /// 꽃을 count송이까지 뽑는다. 뽑을 수 있는 꽃이 모자라면 그만큼만 돌려준다.
    /// 고른 송이 수까지만 뽑는 풀(exact)은 남은 송이 하나하나에서 뽑아서 같은 꽃이 여러 송이 나올 수 있다(꽃 선정에서 같은 꽃을 여러 칸에 채웠을 수 있다).
    /// 수량 제한 없는 풀은 서로 다른 종류만 뽑는다. 피하라고 한 꽃(방금 보여 준 꽃)은 어느 쪽이든 가능하면 피한다.
    /// </summary>
    public List<FlowerData> Draw(int count, ICollection<FlowerData> avoid = null)
    {
        if (exact) return DrawUnits(count, avoid);

        List<FlowerData> available = flowers.Where(f => Remaining(f) > 0).ToList();
        var picks = new List<FlowerData>();

        PickInto(picks, available.Where(f => avoid == null || !avoid.Contains(f)).ToList(), count);
        PickInto(picks, available.Where(f => !picks.Contains(f)).ToList(), count); // 새 꽃이 모자라면 방금 보여 준 꽃으로 채운다
        return picks;
    }

    /// <summary>남은 송이를 한 송이씩 늘어놓고 섞어서 count송이를 뽑는다. 피할 꽃이 아닌 송이를 먼저 뽑고, 모자라면 피할 꽃의 남은 송이로 채운다.</summary>
    private List<FlowerData> DrawUnits(int count, ICollection<FlowerData> avoid)
    {
        List<FlowerData> units = flowers.SelectMany(f => Enumerable.Repeat(f, Remaining(f))).OrderBy(_ => Random.value).ToList();
        var picks = units.Where(f => avoid == null || !avoid.Contains(f)).Take(count).ToList();
        if (picks.Count >= count) return picks;

        // 피할 꽃의 송이를 더해도 같은 송이를 두 번 뽑지 않도록, 이미 뽑은 만큼 빼고 채운다
        var left = new List<FlowerData>(units);
        foreach (FlowerData picked in picks) left.Remove(picked);
        picks.AddRange(left.Take(count - picks.Count));
        return picks;
    }

    private void PickInto(List<FlowerData> picks, List<FlowerData> candidates, int count)
    {
        while (picks.Count < count && candidates.Count > 0)
        {
            int roll = Random.Range(0, candidates.Sum(Weight));
            FlowerData pick = candidates[candidates.Count - 1];
            foreach (FlowerData candidate in candidates)
            {
                roll -= Weight(candidate);
                if (roll < 0)
                {
                    pick = candidate;
                    break;
                }
            }
            picks.Add(pick);
            candidates.Remove(pick);
        }
    }
}
