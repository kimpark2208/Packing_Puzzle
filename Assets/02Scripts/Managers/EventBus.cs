using System;
using System.Collections.Generic;

/// <summary>
/// 게임 플로우를 조율하는 중앙 이벤트 버스(정적 클래스)
/// 구독하는 곳이 있는 이벤트만 둔다. 새 구독이 생기면 이벤트와 Raise 메서드를 추가한다.
/// </summary>
public static class EventBus
{
    /// <summary>손님이 성불하는 연출 타이밍 (표시할 문구)</summary>
    public static event Action<string> OnAscensionMoment;

    /// <summary>금액 변경 (변경된 금액)</summary>
    public static event Action<int> OnMoneyChanged;

    /// <summary>다음 날로 진행 (새로운 일수)</summary>
    public static event Action<int> OnDayAdvanced;

    /// <summary>하루 시간이 모두 소진됨</summary>
    public static event Action OnDayTimeUp;

    /// <summary>밤 시간이 모두 소진됨</summary>
    public static event Action OnNightTimeUp;

    /// <summary>밤 퍼즐 씬이 열려 풀 퍼즐이 정해졌다(보드가 받아 판을 만든다)</summary>
    public static event Action<NightPuzzleData, List<FlowerData>> OnNightStageReady;

    /// <summary>밤 퍼즐을 풀었다 (획득한 꽃 ID들)</summary>
    public static event Action<List<int>> OnNightStageCompleted;

    /// <summary>밤 퍼즐 결과를 보여 달라 (꽃 이름 → 늘어난 수량)</summary>
    public static event Action<IEnumerable<KeyValuePair<string, int>>> OnNightStageResult;

    /// <summary>밤 퍼즐 결과 팝업에서 확인을 눌렀다</summary>
    public static event Action OnNightResultConfirmed;

    public static void RaiseNightStageReady(NightPuzzleData data, List<FlowerData> allowedBlocks) => OnNightStageReady?.Invoke(data, allowedBlocks);
    public static void RaiseNightStageCompleted(List<int> obtainedFlowerIds) => OnNightStageCompleted?.Invoke(obtainedFlowerIds);
    public static void RaiseNightResultConfirmed() => OnNightResultConfirmed?.Invoke();

    /// <summary>결과를 보여 줄 쪽이 있으면 알리고 true, 받는 쪽이 없으면 false.</summary>
    public static bool RaiseNightStageResult(IEnumerable<KeyValuePair<string, int>> gains)
    {
        if (OnNightStageResult == null) return false;
        OnNightStageResult.Invoke(gains);
        return true;
    }

    public static void RaiseAscensionMoment(string description) => OnAscensionMoment?.Invoke(description);
    public static void RaiseMoneyChanged(int newAmount) => OnMoneyChanged?.Invoke(newAmount);
    public static void RaiseDayAdvanced(int newDay) => OnDayAdvanced?.Invoke(newDay);
    public static void RaiseDayTimeUp() => OnDayTimeUp?.Invoke();
    public static void RaiseNightTimeUp() => OnNightTimeUp?.Invoke();
}
