using System.Collections.Generic;
using UnityEngine;

/// <summary>퍼즐이 어떻게 끝났는가.</summary>
public enum PuzzleEndKind
{
    Completed,     // 모든 칸을 채워 완성
    ForcedSubmit   // 강제 제출(디버그)
}

/// <summary>
/// 정산에 필요한 값만 모은 읽기 전용 스냅샷. 점수 규칙은 보드/시계/지갑 같은 싱글톤을 직접 보지 않고 이것만 본다.
/// 싱글톤에서 값을 읽어 오는 곳은 Capture 하나뿐이다.
/// </summary>
public readonly struct PuzzleContext
{
    public PuzzleContext(PuzzleEndKind endKind, IReadOnlyDictionary<WrapperRegionPosition, FlowerData.Color> placedCells,
        IReadOnlyDictionary<WrapperRegionPosition, SpecialCell> specialCells,
        OrderComposer.CustomerRequirement requirement, int requirementUsed, float moodRatio, int collapseCount, bool reservation = false, int missingSupplies = 0)
    {
        Reservation = reservation;
        MissingSupplies = missingSupplies;
        EndKind = endKind;
        PlacedCells = placedCells;
        SpecialCells = specialCells;
        Requirement = requirement;
        RequirementUsed = requirementUsed;
        MoodRatio = moodRatio;
        CollapseCount = collapseCount;
    }

    /// <summary>보드·주문·손님 기분 시계에서 값을 읽어 스냅샷을 만든다. 놓인 꽃은 색별/꽃별로 세어 요구사항을 몇 송이(몇 종) 채웠는지 구한다.</summary>
    public static PuzzleContext Capture(PuzzleEndKind endKind, int collapseCount)
    {
        var board = DayPuzzleController.Instance;
        var placedCells = board != null
            ? board.PlacedCells()
            : new Dictionary<WrapperRegionPosition, FlowerData.Color>();
        var specialCells = board != null
            ? board.SpecialCells()
            : new Dictionary<WrapperRegionPosition, SpecialCell>();

        var requirement = CurrencyManager.Instance != null ? CurrencyManager.Instance.CurrentRequirement : default;

        var colorCounts = new Dictionary<int, int>();   // 색상ID -> 사용 개수
        var flowerCounts = new Dictionary<int, int>();  // 꽃(flowerID) -> 사용 개수
        if (board != null)
        {
            foreach (var slot in board.AllSlots)
            {
                foreach (var flower in slot.PlacedFlowers)
                {
                    colorCounts[(int)flower.color] = colorCounts.GetValueOrDefault((int)flower.color) + 1;
                    flowerCounts[flower.flowerID] = flowerCounts.GetValueOrDefault(flower.flowerID) + 1;
                }
            }
        }

        int used = 0;
        if (requirement.type == OrderComposer.RequirementType.Color)
        {
            used = colorCounts.GetValueOrDefault((int)requirement.color);
        }
        else
        {
            foreach (var target in requirement.FlowerTargets()) // 말한 꽃마다 요구 송이 수까지만 센다(그 합이 요구(minCount)를 채워야 한다)
            {
                used += System.Math.Min(flowerCounts.GetValueOrDefault(target.flowerId), target.count);
            }
        }

        float mood = GameClock.Instance != null ? GameClock.Instance.MoodRatio : 1f;

        // 예약 꽃다발: 손님이 없어 기분 시간을 보지 않고, 필수 꾸미기 용품을 꽃다발에 놓았는지 센다
        ReservationCraft craft = GameManager.Instance != null && GameManager.Instance.Crafting ? GameManager.Instance.Craft : null;
        int missingSupplies = 0;
        if (craft != null)
        {
            var placer = Object.FindFirstObjectByType<DecorationPlacer>();
            foreach (DecorationData supply in craft.Supplies()) if (placer == null || !placer.Contains(supply)) missingSupplies++;
        }
        return new PuzzleContext(endKind, placedCells, specialCells, requirement, used, mood, collapseCount, craft != null, missingSupplies);
    }

    public PuzzleEndKind EndKind { get; }

    /// <summary>꽃이 놓인 칸과 그 꽃의 색(빈 칸은 없다).</summary>
    public IReadOnlyDictionary<WrapperRegionPosition, FlowerData.Color> PlacedCells { get; }

    /// <summary>이번 퍼즐의 특수 칸(별 칸/그늘 칸). 특수가 아닌 칸은 없다.</summary>
    public IReadOnlyDictionary<WrapperRegionPosition, SpecialCell> SpecialCells { get; }

    public OrderComposer.CustomerRequirement Requirement { get; }

    /// <summary>요구사항의 대상(색 또는 꽃)을 몇 송이 썼는가.</summary>
    public int RequirementUsed { get; }

    /// <summary>퍼즐이 끝난 순간 손님 기분 시간의 남은 비율(0~1).</summary>
    public float MoodRatio { get; }

    /// <summary>이 주문을 만드는 동안 꽃다발이 무너진 횟수(붕괴하면 퍼즐을 처음부터 다시 한다).</summary>
    public int CollapseCount { get; }

    /// <summary>예약 꽃다발을 만드는 중인가(손님 기분이 점수에 들어가지 않는다).</summary>
    public bool Reservation { get; }

    /// <summary>꽃다발에 놓지 않은 예약 필수 꾸미기 용품의 수.</summary>
    public int MissingSupplies { get; }

    public bool RequirementMet => RequirementUsed >= Requirement.minCount;
}
