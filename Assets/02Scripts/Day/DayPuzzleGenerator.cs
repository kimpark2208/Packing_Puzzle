using System.Collections.Generic;
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

        /// <summary>되묻기에 대한 대사들(1차, 2차). 특수주문이 있는 꽃일 때만 있다.</summary>
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
        foreach (int id in CurrencyManager.Instance.OwnedWrappers)
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
