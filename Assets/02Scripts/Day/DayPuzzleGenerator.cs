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
        public CustomerRequirementGenerator.CustomerRequirement requirement;

        /// <summary>주문을 풀 수 있는 프리셋을 가진 포장지. 손님의 힌트가 이 포장지를 가리킨다. 플레이어가 반드시 이걸 골라야 하는 건 아니다.</summary>
        public int hintWrapperId;
        public int hintPresetIndex;    // hintWrapperId 포장지 안에서 주문을 풀 수 있는 프리셋 번호
        public string hintColorName;   // 명확한 힌트: "핑크"

        // ---- 플레이어가 포장지를 고른 뒤에 확정되는 것 ----
        public bool isFinalized;
        public int wrapperId;
        public int presetIndex;        // 퍼즐에 쓸 프리셋 번호 (-1이면 보드 기본값)
    }

    /// <summary>손님이 막 등장했을 때 호출. 아직 포장지는 정하지 않고 힌트만 준비한다.</summary>
    public static DayOrder GenerateOrder(int day)
    {
        // 보유한 포장지의 (보드가 지원하는) 프리셋을 전부 모아서 무작위로 하나 고른다
        var pool = new List<(int wrapperId, int presetIndex)>();
        foreach (int id in CurrencyManager.Instance.OwnedWrappers)
        {
            WrapperData data = WrapperRegistry.Instance != null ? WrapperRegistry.Instance.GetById(id) : null;
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
        if (pool.Count > 0)
        {
            var pick = pool[Random.Range(0, pool.Count)];
            hintWrapperId = pick.wrapperId;
            hintPresetIndex = pick.presetIndex;
            WrapperData hintData = WrapperRegistry.Instance.GetById(hintWrapperId);
            preset = hintData.presets[hintPresetIndex];
            hintColorName = hintData.colorName; // 포장지 선택 화면에 보이는 색과 같은 이름
        }
        else
        {
            hintWrapperId = CurrencyManager.Instance.GetRandomOwnedWrapperID(); // 프리셋 데이터가 없으면 예전처럼 제한 없이
            hintColorName = ShopManager.Instance.GetColorHintName(hintWrapperId);
        }

        return new DayOrder
        {
            day = day,
            requirement = CustomerRequirementGenerator.GenerateRandomRequirement(day, preset),
            hintWrapperId = hintWrapperId,
            hintPresetIndex = hintPresetIndex,
            hintColorName = hintColorName,
        };
    }

    /// <summary>플레이어가 포장지를 골랐을 때 호출. 퍼즐에 쓸 프리셋을 확정한다.</summary>
    public static void FinalizeForWrapper(DayOrder order, int chosenWrapperId)
    {
        order.wrapperId = chosenWrapperId;
        order.presetIndex = chosenWrapperId == order.hintWrapperId ? order.hintPresetIndex : RandomPlayablePresetIndex(chosenWrapperId);
        order.isFinalized = true;
    }

    /// <summary>해당 포장지의 보드가 지원하는 프리셋 중 무작위 번호. 없으면 -1.</summary>
    private static int RandomPlayablePresetIndex(int wrapperId)
    {
        WrapperData data = WrapperRegistry.Instance != null ? WrapperRegistry.Instance.GetById(wrapperId) : null;
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
        return preset != null && preset.MaxRing <= WrapperBoardController.SupportedRings;
    }
}
