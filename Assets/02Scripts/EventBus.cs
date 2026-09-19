using System;
using UnityEngine;

/// <summary>
/// 게임 플로우를 조율하는 중앙 이벤트 버스 (싱글톤)
/// 낮 퍼즐 완료, 낮→밤 전환, 밤 퍼즐 생성, 일수 진행 등의 이벤트를 발행
/// </summary>
public class EventBus : MonoBehaviour
{
    public static EventBus Instance { get; private set; }

    // ========== 낮 퍼즐 이벤트 ==========

    /// <summary>
    /// 낮 퍼즐 완료 시 발행
    /// (검증 결과: 색상, 꽃, 완벽도, 매출)
    /// </summary>
    public static event Action<PuzzleValidationResult> OnDayPuzzleComplete;

    /// <summary>
    /// 낮 시간 종료, 밤 퍼즐 생성 시작
    /// (요청한 꽃 목록)
    /// </summary>
    public static event Action<int[]> OnDayEnded;

    // ========== 밤 퍼즐 이벤트 ==========

    /// <summary>
    /// 절차적 밤 퍼즐 생성 완료
    /// (생성된 퍼즐 데이터)
    /// </summary>
    public static event Action<NightPuzzleData[]> OnNightPuzzlesGenerated;

    /// <summary>
    /// 밤 퍼즐 완료, 꽃 획득
    /// (획득한 꽃 ID)
    /// </summary>
    public static event Action<int> OnNightPuzzleComplete;

    // ========== 재화 이벤트 ==========

    /// <summary>
    /// 금액 변경
    /// (변경된 금액)
    /// </summary>
    public static event Action<int> OnMoneyChanged;

    /// <summary>
    /// 포장지 언락
    /// (언락된 포장지 ID)
    /// </summary>
    public static event Action<int> OnWrapperUnlocked;

    /// <summary>
    /// 꽃 획득
    /// (획득한 꽃 ID)
    /// </summary>
    public static event Action<int> OnFlowerObtained;

    // ========== 일수 진행 이벤트 ==========

    /// <summary>
    /// 다음 날로 진행
    /// (새로운 일수)
    /// </summary>
    public static event Action<int> OnDayAdvanced;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ========== 이벤트 발행 메서드 ==========

    public static void RaiseDayPuzzleComplete(PuzzleValidationResult result)
    {
        OnDayPuzzleComplete?.Invoke(result);
    }

    public static void RaiseDayEnded(int[] requestedFlowerIds)
    {
        OnDayEnded?.Invoke(requestedFlowerIds);
    }

    public static void RaiseNightPuzzlesGenerated(NightPuzzleData[] puzzles)
    {
        OnNightPuzzlesGenerated?.Invoke(puzzles);
    }

    public static void RaiseNightPuzzleComplete(int flowerObtainedId)
    {
        OnNightPuzzleComplete?.Invoke(flowerObtainedId);
    }

    public static void RaiseMoneyChanged(int newAmount)
    {
        OnMoneyChanged?.Invoke(newAmount);
    }

    public static void RaiseWrapperUnlocked(int wrapperId)
    {
        OnWrapperUnlocked?.Invoke(wrapperId);
    }

    public static void RaiseFlowerObtained(int flowerId)
    {
        OnFlowerObtained?.Invoke(flowerId);
    }

    public static void RaiseDayAdvanced(int newDay)
    {
        OnDayAdvanced?.Invoke(newDay);
    }
}

/// <summary>
/// 낮 퍼즐 검증 결과
/// </summary>
public struct PuzzleValidationResult
{
    public int mostUsedColorId;      // 가장 많이 사용된 색상 ID
    public int mostUsedFlowerId;     // 가장 많이 사용된 꽃 ID
    public bool isPerfect;           // 모든 칸이 채워졌는가
    public int baseScore;            // 기본 점수 (완벽: 1000, 불완벽: 500)
    public int totalEarnings;        // 손님 요구사항 포함 최종 매출
}

/// <summary>
/// 절차적으로 생성된 밤 퍼즐 데이터
/// </summary>
public struct NightPuzzleData
{
    public int puzzleId;                // 퍼즐 고유 ID
    public int gridSize;                // 그리드 크기 (5x5, 8x8 등)
    public int[] requiredFlowerIds;     // 필요한 꽃 ID 목록
    public bool[,] filledGrid;          // 배치된 그리드 (true = 채워짐)
    public int[,] placedFlowerIds;      // 각 셀의 꽃 ID (-1 = 빈칸, -2 = 벽)
}
