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

    public static void RaiseAscensionMoment(string description) => OnAscensionMoment?.Invoke(description);
    public static void RaiseMoneyChanged(int newAmount) => OnMoneyChanged?.Invoke(newAmount);
    public static void RaiseDayAdvanced(int newDay) => OnDayAdvanced?.Invoke(newDay);
    public static void RaiseDayTimeUp() => OnDayTimeUp?.Invoke();
}

/// <summary>낮 퍼즐 검증 결과</summary>
public struct PuzzleValidationResult
{
    public bool success;                         // 모든 태그 영역을 알맞게 채워 꽃다발을 완성했는가
    public int mostUsedColorId;                  // 가장 많이 사용된 색상 ID
    public int mostUsedFlowerId;                 // 가장 많이 사용된 꽃 ID
    public int colorBonus;                       // 인접 슬롯 색 조합 보너스 합계
    public bool requirementMet;                  // 고객 요구사항 충족 여부
    public int requirementBonus;                 // 고객 요구사항 보너스
    public int totalEarnings;                    // 색 조합 보너스 + 요구사항 보너스 (실패 시 0)
    public Dictionary<int, int> colorCounts;     // 색상ID -> 사용 개수 (전체)
    public Dictionary<int, int> flowerCounts;    // 꽃(blockID) -> 사용 개수 (전체)
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