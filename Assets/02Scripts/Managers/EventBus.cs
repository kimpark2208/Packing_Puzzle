using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 플로우를 조율하는 중앙 이벤트 버스 (싱글톤)
/// 구독하는 곳이 있는 이벤트만 둔다. 새 구독이 생기면 이벤트와 Raise 메서드를 추가한다.
/// </summary>
public class EventBus : Singleton<EventBus>
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

    public static void RaiseAscensionMoment(string description) => OnAscensionMoment?.Invoke(description);
    public static void RaiseMoneyChanged(int newAmount) => OnMoneyChanged?.Invoke(newAmount);
    public static void RaiseDayAdvanced(int newDay) => OnDayAdvanced?.Invoke(newDay);
    public static void RaiseDayTimeUp() => OnDayTimeUp?.Invoke();
    public static void RaiseNightTimeUp() => OnNightTimeUp?.Invoke();
}

/// <summary>절차적으로 생성된 밤 퍼즐 데이터</summary>
public struct NightPuzzleData
{
    public int puzzleId;                // 퍼즐 고유 ID
    public int gridSize;                // 그리드 크기 (5x5, 8x8 등)
    public int[] requiredFlowerIds;     // 이 스테이지에서 사용 가능한 꽃 ID 목록
    public bool isBonusStage;           // 요청 스테이지가 아닌 잉여시간 스테이지인지
    public bool[,] wallGrid;            // true = 벽(그릴 수 없는 칸, 임의 필러가 채운 자리)
}