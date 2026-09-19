using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 일반/잉여 밤 퍼즐용 판정기. 하나 이상의 BlockData(꽃) 각각의 shapeGrid에서
/// 회전, 좌우 반전 및 그 조합으로 나오는 모든 고유한 형태를 미리 계산해 두고,
/// 플레이어가 드래그로 선택한 칸 집합이 그 중 어떤 것과 위치 무관하게(shape-only) 일치하는지 판정한다.
/// 위치는 중요하지 않다. 매 변형은 셀 집합을 좌상단이 (0,0)이 되도록 정규화한다.
/// 여러 꽃이 동시에 후보로 주어질 수 있다(잉여/조합 스테이지).
/// </summary>
public class NightShapePuzzleValidator : INightPuzzleValidator
{
    private readonly List<(BlockData block, List<HashSet<Vector2Int>> variants)> entries = new();
    private readonly bool[,] wallGrid;

    public NightShapePuzzleValidator(IEnumerable<BlockData> blocks, bool[,] wallGrid = null)
    {
        this.wallGrid = wallGrid;

        foreach (BlockData block in blocks)
        {
            if (block == null) continue;
            entries.Add((block, PolyominoUtil.GetUniqueVariants(block)));
        }
    }

    /// <summary>퍼즐 생성기가 필러로 벽 처리한 칸인지 여부. 벽 칸은 그릴 수 없고 완료 판정에서도 제외한다.</summary>
    public bool IsWallCell(Vector2Int coord)
    {
        if (wallGrid == null) return false;
        if (coord.y < 0 || coord.y >= wallGrid.GetLength(0)) return false;
        if (coord.x < 0 || coord.x >= wallGrid.GetLength(1)) return false;
        return wallGrid[coord.y, coord.x];
    }

    /// <summary>일반 스테이지는 벽을 제외한 보드 전체가 목표 범위다.</summary>
    public bool IsCellInTarget(Vector2Int coord) => !IsWallCell(coord);

    /// <summary>벽을 제외한 모든 칸이 채워졌는가.</summary>
    public bool IsPuzzleComplete(bool[,] filled)
    {
        int rows = filled.GetLength(0);
        int cols = filled.GetLength(1);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                Vector2Int coord = new Vector2Int(col, row);
                if (IsWallCell(coord)) continue;
                if (!filled[row, col]) return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 지금까지 선택된 셀 집합이, 어떤 꽃의 어떤 회전/반전 변형으로도 이어서 완성될 가능성이 있는지.
    /// </summary>
    public bool CanStillMatch(IReadOnlyCollection<Vector2Int> selectedCells)
    {
        if (selectedCells == null || selectedCells.Count == 0) return false;

        foreach (var entry in entries)
        {
            foreach (HashSet<Vector2Int> variant in entry.variants)
            {
                if (selectedCells.Count <= variant.Count && CanFitInsideVariant(selectedCells, variant))
                    return true;
            }
        }

        return false;
    }

    /// <summary>선택된 셀 집합과 정확히 일치하는 변형을 가진 꽃(BlockData)을 찾는다. 없으면 null.</summary>
    public BlockData GetExactMatchBlock(IReadOnlyCollection<Vector2Int> selectedCells)
    {
        if (selectedCells == null || selectedCells.Count == 0) return null;

        foreach (var entry in entries)
        {
            foreach (HashSet<Vector2Int> variant in entry.variants)
            {
                if (selectedCells.Count == variant.Count && CanFitInsideVariant(selectedCells, variant))
                    return entry.block;
            }
        }

        return null;
    }

    public bool IsExactMatch(IReadOnlyCollection<Vector2Int> selectedCells) => GetExactMatchBlock(selectedCells) != null;

    /// <summary>selectedCells가 variant 내부에 위치 이동만으로 정확히 포함될 수 있는지 확인한다.</summary>
    private static bool CanFitInsideVariant(IReadOnlyCollection<Vector2Int> selectedCells, HashSet<Vector2Int> variant)
    {
        foreach (Vector2Int selectedAnchor in selectedCells)
        {
            foreach (Vector2Int variantAnchor in variant)
            {
                Vector2Int offset = selectedAnchor - variantAnchor;
                bool fits = true;

                foreach (Vector2Int selected in selectedCells)
                {
                    if (!variant.Contains(selected - offset))
                    {
                        fits = false;
                        break;
                    }
                }

                if (fits) return true;
            }
        }

        return false;
    }
}
