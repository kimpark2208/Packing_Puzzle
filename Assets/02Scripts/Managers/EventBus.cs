using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 플로우를 조율하는 중앙 이벤트 버스 (싱글톤)
/// 낮 퍼즐 완료, 낮→밤 전환, 밤 퍼즐 생성, 일수 진행 등의 이벤트를 발행
/// </summary>
public class EventBus : Singleton<EventBus>
{
    // ========== 낮 퍼즐 이벤트 ==========

    /// <summary>낮 퍼즐 완료 시 발행 (검증 결과: 색상, 꽃, 완벽도, 매출)</summary>
    public static event Action<PuzzleValidationResult> OnDayPuzzleComplete;

    /// <summary>낮 시간 종료, 밤 퍼즐 생성 시작 (요청한 꽃 목록)</summary>
    public static event Action<int[]> OnDayEnded;

    // ========== 밤 퍼즐 이벤트 ==========

    /// <summary>절차적 밤 퍼즐 생성 완료 (생성된 퍼즐 데이터)</summary>
    public static event Action<NightPuzzleData[]> OnNightPuzzlesGenerated;

    /// <summary>밤 퍼즐 완료, 꽃 획득 (획득한 꽃 ID)</summary>
    public static event Action<int> OnNightPuzzleComplete;

    /// <summary>이번 밤에 준비된 모든 퍼즐(메인+잉여)을 다 풀었을 때</summary>
    public static event Action OnAllNightPuzzlesFinished;

    /// <summary>손님이 성불하는 연출 타이밍 (표시할 문구)</summary>
    public static event Action<string> OnAscensionMoment;

    // ========== 재화 이벤트 ==========

    /// <summary>금액 변경 (변경된 금액)</summary>
    public static event Action<int> OnMoneyChanged;

    /// <summary>포장지 언락 (언락된 포장지 ID)</summary>
    public static event Action<int> OnWrapperUnlocked;

    /// <summary>꽃 획득 (획득한 꽃 ID)</summary>
    public static event Action<int> OnFlowerObtained;

    // ========== 일수 진행 이벤트 ==========

    /// <summary>다음 날로 진행 (새로운 일수)</summary>
    public static event Action<int> OnDayAdvanced;

    // ========== 이벤트 발행 메서드 ==========

    public static void RaiseDayPuzzleComplete(PuzzleValidationResult result) => OnDayPuzzleComplete?.Invoke(result);
    public static void RaiseDayEnded(int[] requestedFlowerIds) => OnDayEnded?.Invoke(requestedFlowerIds);
    public static void RaiseNightPuzzlesGenerated(NightPuzzleData[] puzzles) => OnNightPuzzlesGenerated?.Invoke(puzzles);
    public static void RaiseNightPuzzleComplete(int flowerObtainedId) => OnNightPuzzleComplete?.Invoke(flowerObtainedId);
    public static void RaiseAllNightPuzzlesFinished() => OnAllNightPuzzlesFinished?.Invoke();
    public static void RaiseAscensionMoment(string description) => OnAscensionMoment?.Invoke(description);
    public static void RaiseMoneyChanged(int newAmount) => OnMoneyChanged?.Invoke(newAmount);
    public static void RaiseWrapperUnlocked(int wrapperId) => OnWrapperUnlocked?.Invoke(wrapperId);
    public static void RaiseFlowerObtained(int flowerId) => OnFlowerObtained?.Invoke(flowerId);
    public static void RaiseDayAdvanced(int newDay) => OnDayAdvanced?.Invoke(newDay);
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
