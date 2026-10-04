using System.Collections.Generic;

/// <summary>퍼즐이 어떻게 끝났는가.</summary>
public enum PuzzleEndKind
{
    Completed,     // 모든 칸을 채워 완성
    ForcedSubmit   // 강제 제출(디버그)
}

/// <summary>
/// 정산에 필요한 값만 모은 읽기 전용 스냅샷. 규칙은 보드/시계/지갑 같은 싱글톤을 직접 보지 않고 이것만 본다.
/// </summary>
public readonly struct PuzzleContext
{
    public PuzzleContext(PuzzleEndKind endKind, IReadOnlyList<FlowerData.Color> placedColors,
        CustomerRequirementGenerator.CustomerRequirement requirement, int requirementUsed, float moodRatio, int collapseCount)
    {
        EndKind = endKind;
        PlacedColors = placedColors;
        Requirement = requirement;
        RequirementUsed = requirementUsed;
        MoodRatio = moodRatio;
        CollapseCount = collapseCount;
    }

    public PuzzleEndKind EndKind { get; }

    /// <summary>꽃을 놓은 순서대로의 색.</summary>
    public IReadOnlyList<FlowerData.Color> PlacedColors { get; }

    public CustomerRequirementGenerator.CustomerRequirement Requirement { get; }

    /// <summary>요구사항의 대상(색 또는 꽃)을 몇 송이 썼는가.</summary>
    public int RequirementUsed { get; }

    /// <summary>퍼즐이 끝난 순간 손님 기분 시간의 남은 비율(0~1).</summary>
    public float MoodRatio { get; }

    /// <summary>이 주문을 만드는 동안 꽃다발이 무너진 횟수(붕괴하면 퍼즐을 처음부터 다시 한다).</summary>
    public int CollapseCount { get; }

    public bool RequirementMet => RequirementUsed >= Requirement.minCount;
}
