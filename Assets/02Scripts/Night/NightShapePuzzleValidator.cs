using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 일반/잉여 스테이지용 목표 도형 판정기.
/// 정답 도형(targetCells)에 속한 칸만 유효하며, 벽을 제외한 목표 영역이 전부 채워지면 완료된다.
/// 다중 정답을 허용하는 스테이지 특성상, "정확히 이 모양대로"가 아니라
/// "목표 영역 안의 칸만 그릴 수 있고, 그 칸이 모두 채워지면 클리어"라는 느슨한 기준을 쓴다.
/// 프로토타입 단계라 벽은 아직 구현하지 않으므로 IsWallCell은 항상 false를 반환한다.
/// </summary>
public class NightShapePuzzleValidator : INightPuzzleValidator
{
    private readonly HashSet<Vector2Int> targetCells;

    public NightShapePuzzleValidator(IEnumerable<Vector2Int> targetCells)
    {
        this.targetCells = new HashSet<Vector2Int>(targetCells);
    }

    public bool IsWallCell(Vector2Int coord)
    {
        // TODO: 벽 스테이지 구현 시 벽 좌표 집합을 받아 여기서 판정한다.
        return false;
    }

    public bool IsCellInTarget(Vector2Int coord)
    {
        return targetCells.Contains(coord);
    }

    /// <summary>
    /// 벽을 제외한 목표 영역의 모든 칸이 채워졌으면 완료로 판정한다.
    /// </summary>
    public bool IsPuzzleComplete(bool[,] filled)
    {
        foreach (Vector2Int coord in targetCells)
        {
            if (IsWallCell(coord)) continue;

            if (!filled[coord.y, coord.x])
            {
                return false;
            }
        }

        return true;
    }
}
