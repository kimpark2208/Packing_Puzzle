using UnityEngine;

/// <summary>
/// 낮 퍼즐(손님 주문) 절차적 생성.
/// 손님 등장 시 힌트(포장지 색/크기 표현)만 준비하고, 플레이어가 포장지를 고르면
/// 그 포장지의 단계(tier)에 맞는 태그 요구 레벨(WrapperLevelDatabase)을 확정한다.
/// </summary>
public static class DayPuzzleGenerator
{
    public class DayOrder
    {
        // ---- 손님이 등장하는 순간부터 알 수 있는 것 (포장지 선택 전) ----
        public int day;
        public CustomerRequirementGenerator.CustomerRequirement requirement;

        /// <summary>손님이 속으로 원하는 포장지(힌트용). 플레이어가 반드시 이걸 골라야 하는 건 아니다.</summary>
        public int hintWrapperId;
        public string hintColorName;   // 명확한 힌트: "핑크"
        public string hintSizeWord;    // 모호한 힌트: "가장 작은"
        public bool hintStartsVague;   // true면 처음엔 색 대신 모호한 크기 표현으로 시작

        // ---- 플레이어가 포장지를 고른 뒤에 확정되는 것 ----
        public bool isFinalized;
        public int wrapperId;
        public int tier;
        public WrapperLevelDatabase.LevelDef level;
    }

    /// <summary>손님이 막 등장했을 때 호출. 아직 포장지는 정하지 않고 힌트만 준비한다.</summary>
    public static DayOrder GenerateOrder(int day)
    {
        int hintWrapperId = CurrencyManager.Instance.GetRandomOwnedWrapperID();

        return new DayOrder
        {
            day = day,
            requirement = CustomerRequirementGenerator.GenerateRandomRequirement(day),
            hintWrapperId = hintWrapperId,
            hintColorName = ShopManager.Instance.GetColorHintName(hintWrapperId),
            hintSizeWord = ShopManager.Instance.GetSizeHintWord(hintWrapperId),
            hintStartsVague = Random.value > 0.5f
        };
    }

    /// <summary>플레이어가 포장지를 골랐을 때 호출. 그 포장지 단계의 태그 요구 레벨을 확정한다.</summary>
    public static void FinalizeForWrapper(DayOrder order, int chosenWrapperId)
    {
        order.wrapperId = chosenWrapperId;
        order.tier = ShopManager.Instance.GetTier(chosenWrapperId);
        order.level = WrapperLevelDatabase.GetLevel(order.tier);
        order.isFinalized = true;
    }
}
