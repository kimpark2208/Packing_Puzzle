using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 낮 퍼즐(손님 주문) 절차적 생성.
/// 손님 등장 시 보유한 포장지의 프리셋을 모두 모아 하나를 무작위로 고르고, 그 프리셋으로 풀 수 있는 주문과
/// 힌트(그 프리셋 포장지의 색 표현)를 만든다. 플레이어가 포장지를 고르면
/// 퍼즐에 쓸 프리셋을 확정한다. 손님이 말한 포장지를 고르면 주문을 풀 수 있는 프리셋이고,
/// 다른 포장지를 고르면 그 포장지의 프리셋 중 무작위라 못 풀 수도 있다(손님 말을 안 들은 대가).
/// </summary>
public static class DayPuzzleGenerator
{
    public class DayOrder
    {
        // ---- 손님이 등장하는 순간부터 알 수 있는 것 (포장지 선택 전) ----
        public int day;
        public OrderComposer.CustomerRequirement requirement;

        /// <summary>주문을 풀 수 있는 프리셋을 가진 포장지. 손님의 힌트가 이 포장지를 가리킨다. 플레이어가 반드시 이걸 골라야 하는 건 아니다.</summary>
        public int hintWrapperId;
        public int hintPresetIndex;    // hintWrapperId 포장지 안에서 주문을 풀 수 있는 프리셋 번호
        public string hintColorName;   // 명확한 힌트: "핑크"

        /// <summary>손님이 말하는 주문 대사 전체(꽃 부분 + 포장지 부분). 손님이 올 때 한 번 정해서, 대사가 다시 나와도 바뀌지 않는다.</summary>
        public string orderLine;

        /// <summary>되묻기를 누를 때마다 말하는 대사 전체(1회, 2회). 붙여서 다시 말하기(포장지는 바로 말한다)와 덧붙이는 말까지 들어 있다(OrderComposer가 만든다).</summary>
        public string[] reaskLines = System.Array.Empty<string>();
        public int reaskIndex; // 지금까지 보여 준 되묻기 대사 수

        // ---- 플레이어가 포장지를 고른 뒤에 확정되는 것 ----
        /// <summary>확정된 포장지 데이터. 확정 전이거나 찾을 수 없으면 null. 퍼즐을 만드는 쪽(보드, 가챠, 꽃 선택)은 번호로 다시 찾지 말고 이것을 쓴다.</summary>
        public WrapperData wrapper;

        /// <summary>확정된 프리셋. 확정 전이거나 번호가 맞지 않으면 null.</summary>
        public WrapperPresetData preset;
    }

    /// <summary>손님이 막 등장했을 때 호출. 아직 포장지는 정하지 않고 힌트만 준비한다. 주문을 어떻게 말할지(직설/은유, 템플릿)는 OrderComposer가 templates로 정한다.</summary>
    public static DayOrder GenerateOrder(int day, OrderTemplateData templates = null)
    {
        // 보유한 포장지의 (보드가 지원하는) 프리셋을 전부 모아서 무작위로 하나 고른다
        var pool = new List<(int wrapperId, int presetIndex)>();
        foreach (int id in CurrencyManager.Instance.UsableWrappers)
        {
            WrapperData data = DataRegistry.Instance != null ? DataRegistry.Instance.GetWrapper(id) : null;
            if (data == null) continue;
            for (int i = 0; i < data.presets.Count; i++)
            {
                if (IsPlayable(data.presets[i])) pool.Add((id, i));
            }
        }

        int hintWrapperId;
        int hintPresetIndex = -1;
        WrapperPresetData preset = null;
        string hintColorName;
        WrapperColor? hintColor = null; // 포장지 SO의 색 종류. 주문은 이 색으로 포장지를 말한다
        if (pool.Count > 0)
        {
            var pick = pool[Random.Range(0, pool.Count)];
            hintWrapperId = pick.wrapperId;
            hintPresetIndex = pick.presetIndex;
            WrapperData hintData = DataRegistry.Instance.GetWrapper(hintWrapperId);
            preset = hintData.presets[hintPresetIndex];
            hintColor = hintData.wrapperColor;
            hintColorName = hintData.ColorName; // 포장지 선택 화면에 보이는 색과 같은 이름
        }
        else
        {
            hintWrapperId = CurrencyManager.Instance.GetRandomOwnedWrapperID(); // 프리셋 데이터가 없으면 예전처럼 제한 없이
            hintColorName = ShopManager.Instance.GetColorHintName(hintWrapperId);
        }

        OrderComposer.Result composed = OrderComposer.Compose(day, preset, hintColor, hintColorName, templates);
        return new DayOrder
        {
            day = day,
            requirement = composed.requirement,
            hintWrapperId = hintWrapperId,
            hintPresetIndex = hintPresetIndex,
            hintColorName = hintColorName,
            orderLine = composed.line,
            reaskLines = composed.reaskLines,
        };
    }

    /// <summary>
    /// 시나리오에 정해진 주문(꽃과 송이 수, 포장지 번호)으로 낮 주문을 만든다. 손님의 힌트는 그 포장지를 가리키고, 프리셋은 그 포장지의 프리셋 중 요구한 꽃이 다 들어가는 것을 무작위로 고른다.
    /// wrapperId가 0이면 포장지는 말하지 않은 것이라 보유한 포장지 중에서 고른다. 꽃은 두 종까지만 쓴다(요구사항의 형태).
    /// </summary>
    public static DayOrder GenerateFixedOrder(int day, List<(FlowerData flower, int count)> flowers, int wrapperId, string orderLine)
    {
        if (flowers.Count > 2) Debug.LogWarning($"[DayPuzzleGenerator] 고정 주문의 꽃이 {flowers.Count}종이라 앞의 두 종만 씁니다.");

        var requirement = new OrderComposer.CustomerRequirement
        {
            requirementId = day,
            baseAmount = OrderComposer.BaseAmount,
            type = OrderComposer.RequirementType.Flower,
            flowerId = flowers[0].flower.flowerID,
            flowerCount = flowers[0].count,
            flowerId2 = -1,
        };
        requirement.minCount = requirement.flowerCount;
        if (flowers.Count > 1)
        {
            requirement.flowerId2 = flowers[1].flower.flowerID;
            requirement.flowerCount2 = flowers[1].count;
            requirement.minCount += requirement.flowerCount2;
        }

        var used = flowers.Take(2).ToList();
        bool Fits(WrapperPresetData p) => used.GroupBy(f => f.flower.flowerRole).All(g => g.Sum(f => f.count) <= p.CountByTag(g.Key));

        var pool = new List<(int wrapperId, int presetIndex)>();
        var fallback = new List<(int wrapperId, int presetIndex)>(); // 꽃이 다 들어가는 프리셋이 없을 때의 대비
        foreach (int id in wrapperId > 0 ? new[] { wrapperId } : CurrencyManager.Instance.UsableWrappers.ToArray())
        {
            WrapperData data = DataRegistry.Instance != null ? DataRegistry.Instance.GetWrapper(id) : null;
            if (data == null) continue;
            for (int i = 0; i < data.presets.Count; i++)
            {
                if (!IsPlayable(data.presets[i])) continue;
                fallback.Add((id, i));
                if (Fits(data.presets[i])) pool.Add((id, i));
            }
        }
        if (pool.Count == 0)
        {
            Debug.LogWarning($"[DayPuzzleGenerator] 고정 주문의 꽃이 다 들어가는 프리셋이 없어 아무 프리셋으로 진행합니다 (포장지 {wrapperId}).");
            pool = fallback;
        }

        int hintWrapperId = wrapperId > 0 ? wrapperId : CurrencyManager.Instance.GetRandomOwnedWrapperID();
        int hintPresetIndex = -1;
        if (pool.Count > 0)
        {
            var pick = pool[Random.Range(0, pool.Count)];
            hintWrapperId = pick.wrapperId;
            hintPresetIndex = pick.presetIndex;
        }

        WrapperData hint = DataRegistry.Instance != null ? DataRegistry.Instance.GetWrapper(hintWrapperId) : null;
        return new DayOrder
        {
            day = day,
            requirement = requirement,
            hintWrapperId = hintWrapperId,
            hintPresetIndex = hintPresetIndex,
            hintColorName = hint != null ? hint.ColorName : "",
            orderLine = orderLine,
            reaskLines = new[] { orderLine, orderLine }, // 되묻기는 같은 대사를 다시 말한다
        };
    }

    /// <summary>플레이어가 포장지를 골랐을 때 호출. 퍼즐에 쓸 프리셋을 확정한다.</summary>
    public static void FinalizeForWrapper(DayOrder order, int chosenWrapperId)
    {
        int presetIndex = chosenWrapperId == order.hintWrapperId ? order.hintPresetIndex : RandomPlayablePresetIndex(chosenWrapperId);

        order.wrapper = DataRegistry.Instance != null ? DataRegistry.Instance.GetWrapper(chosenWrapperId) : null;
        bool validIndex = order.wrapper != null && presetIndex >= 0 && presetIndex < order.wrapper.presets.Count;
        order.preset = validIndex ? order.wrapper.presets[presetIndex] : null;
    }

    /// <summary>해당 포장지의 보드가 지원하는 프리셋 중 무작위 번호. 없으면 -1.</summary>
    private static int RandomPlayablePresetIndex(int wrapperId)
    {
        WrapperData data = DataRegistry.Instance != null ? DataRegistry.Instance.GetWrapper(wrapperId) : null;
        if (data == null) return -1;

        var indices = new List<int>();
        for (int i = 0; i < data.presets.Count; i++)
        {
            if (IsPlayable(data.presets[i])) indices.Add(i);
        }
        return indices.Count > 0 ? indices[Random.Range(0, indices.Count)] : -1;
    }

    private static bool IsPlayable(WrapperPresetData preset)
    {
        return preset != null && preset.IsWellFormed && preset.MaxRing <= DayPuzzleController.SupportedRings;
    }
}
