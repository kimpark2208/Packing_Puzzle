using System;

/// <summary>
/// 게임 플로우를 조율하는 중앙 이벤트 버스(정적 클래스)
/// 구독하는 곳이 있는 이벤트만 둔다. 새 구독이 생기면 이벤트와 Raise 메서드를 추가한다.
/// </summary>
public static class EventBus
{
    /// <summary>금액 변경 (변경된 금액)</summary>
    public static event Action<int> OnMoneyChanged;

    /// <summary>다음 날로 진행 (새로운 일수)</summary>
    public static event Action<int> OnDayAdvanced;

    /// <summary>하루 시간이 모두 소진됨</summary>
    public static event Action OnDayTimeUp;

    public static void RaiseMoneyChanged(int newAmount) => OnMoneyChanged?.Invoke(newAmount);
    public static void RaiseDayAdvanced(int newDay) => OnDayAdvanced?.Invoke(newDay);
    public static void RaiseDayTimeUp() => OnDayTimeUp?.Invoke();
}
