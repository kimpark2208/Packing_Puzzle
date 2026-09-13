using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 일반/잉여 밤 퍼즐용 블럭 모양 판정기.
/// BlockData의 shapeGrid에서 원본, 회전, 좌우 반전 형태를 만들고,
/// 플레이어가 드래그한 셀 집합이 그중 한 형태가 될 수 있는지/정확히 일치하는지 판정한다.
/// 위치는 중요하지 않다. 비교 전에 각 셀 집합을 좌상단 (0, 0) 기준으로 정규화한다.
/// </summary>
public class NightShapePuzzleValidator : INightPuzzleValidator
{
    private readonly List<HashSet<Vector2Int>> shapeVariants = new();

    public NightShapePuzzleValidator(BlockData blockData)
    {
        if (blockData == null) return;

        HashSet<Vector2Int> original = ReadShape(blockData);
        AddAllUniqueVariants(original);
    }

    /// <summary>
    /// 프로토타입에서는 벽을 사용하지 않는다.
    /// TODO: 벽 좌표 집합을 생성자로 받도록 확장한다.
    /// </summary>
    public bool IsWallCell(Vector2Int coord)
    {
        return false;
    }

    /// <summary>
    /// 이 일반 퍼즐은 고정된 목표 영역이 없다.
    /// 드래그 전체 모양을 보고 CanStillMatch/IsExactMatch로 판정한다.
    /// </summary>
    public bool IsCellInTarget(Vector2Int coord)
    {
        return true;
    }

    /// <summary>
    /// 일반 퍼즐 완료 조건: 벽을 제외한 보드의 모든 칸이 채워졌는가.
    /// </summary>
    public bool IsPuzzleComplete(bool[,] filled)
    {
        int rows = filled.GetLength(0);
        int cols = filled.GetLength(1);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                if (IsWallCell(new Vector2Int(col, row))) continue;
                if (!filled[row, col]) return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 현재 선택된 셀들이, 어떤 회전/반전 변형을 보드의 어느 위치에 놓았을 때
    /// 그 안에 포함될 수 있는지 판정한다.
    /// </summary>
    public bool CanStillMatch(IReadOnlyCollection<Vector2Int> selectedCells)
    {
        if (selectedCells == null || selectedCells.Count == 0) return false;

        foreach (HashSet<Vector2Int> variant in shapeVariants)
        {
            if (selectedCells.Count > variant.Count) continue;
            if (CanFitInsideVariant(selectedCells, variant)) return true;
        }

        return false;
    }

    /// <summary>
    /// 현재 선택된 셀들이, 어떤 회전/반전 변형과 위치까지 포함하여 완전히 일치하는지 판정한다.
    /// </summary>
    public bool IsExactMatch(IReadOnlyCollection<Vector2Int> selectedCells)
    {
        if (selectedCells == null || selectedCells.Count == 0) return false;

        foreach (HashSet<Vector2Int> variant in shapeVariants)
        {
            if (selectedCells.Count != variant.Count) continue;
            if (CanFitInsideVariant(selectedCells, variant)) return true;
        }

        return false;
    }

    private static HashSet<Vector2Int> ReadShape(BlockData blockData)
    {
        HashSet<Vector2Int> result = new();

        if (blockData.shapeGrid == null) return result;

        for (int row = 0; row < blockData.shapeGrid.Length; row++)
        {
            bool[] cols = blockData.shapeGrid[row].cols;
            if (cols == null) continue;

            for (int col = 0; col < cols.Length; col++)
            {
                if (cols[col])
                {
                    result.Add(new Vector2Int(col, row));
                }
            }
        }

        return Normalize(result);
    }

    private void AddAllUniqueVariants(HashSet<Vector2Int> original)
    {
        HashSet<Vector2Int> current = original;

        for (int i = 0; i < 4; i++)
        {
            AddVariantIfUnique(current);
            AddVariantIfUnique(FlipHorizontal(current));
            current = RotateClockwise(current);
        }
    }

    private void AddVariantIfUnique(HashSet<Vector2Int> candidate)
    {
        HashSet<Vector2Int> normalized = Normalize(candidate);

        foreach (HashSet<Vector2Int> existing in shapeVariants)
        {
            if (existing.SetEquals(normalized)) return;
        }

        shapeVariants.Add(normalized);
    }

    private static HashSet<Vector2Int> RotateClockwise(HashSet<Vector2Int> cells)
    {
        HashSet<Vector2Int> rotated = new();

        foreach (Vector2Int cell in cells)
        {
            // (x, y)를 원점 기준 시계방향 90도 회전: (x, y) -> (-y, x)
            rotated.Add(new Vector2Int(-cell.y, cell.x));
        }

        return Normalize(rotated);
    }

    private static HashSet<Vector2Int> FlipHorizontal(HashSet<Vector2Int> cells)
    {
        HashSet<Vector2Int> flipped = new();

        foreach (Vector2Int cell in cells)
        {
            // y축 대칭: (x, y) -> (-x, y)
            flipped.Add(new Vector2Int(-cell.x, cell.y));
        }

        return Normalize(flipped);
    }

    private static HashSet<Vector2Int> Normalize(IEnumerable<Vector2Int> cells)
    {
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        List<Vector2Int> copied = new();

        foreach (Vector2Int cell in cells)
        {
            copied.Add(cell);
            minX = Mathf.Min(minX, cell.x);
            minY = Mathf.Min(minY, cell.y);
        }

        HashSet<Vector2Int> normalized = new();

        foreach (Vector2Int cell in copied)
        {
            normalized.Add(new Vector2Int(cell.x - minX, cell.y - minY));
        }

        return normalized;
    }

    /// <summary>
    /// selectedCells가 variant를 평행이동한 도형 안에 포함될 수 있는지 확인한다.
    /// 선택 셀 하나를 variant의 각 칸에 맞춰 보는 방식으로 모든 가능한 위치를 검사한다.
    /// </summary>
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
