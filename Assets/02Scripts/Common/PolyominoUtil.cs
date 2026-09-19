using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// BlockData.shapeGrid로부터 회전/반전 변형 집합을 계산하는 공용 유틸리티.
/// NightShapePuzzleValidator와 절차적 생성기(낮/밤)가 동일한 로직을 공유하기 위해 분리했다.
/// 모든 변형은 좌상단이 (0,0)이 되도록 정규화된 Vector2Int(x=col, y=row) 좌표 집합이다.
/// </summary>
public static class PolyominoUtil
{
    public static HashSet<Vector2Int> ReadShape(BlockData blockData)
    {
        HashSet<Vector2Int> result = new();
        if (blockData == null || blockData.shapeGrid == null) return result;

        for (int row = 0; row < blockData.shapeGrid.Length; row++)
        {
            bool[] cols = blockData.shapeGrid[row].cols;
            if (cols == null) continue;

            for (int col = 0; col < cols.Length; col++)
            {
                if (cols[col]) result.Add(new Vector2Int(col, row));
            }
        }

        return Normalize(result);
    }

    /// <summary>회전(0/90/180/270) x 반전(0/1) 조합 중 서로 겹치지 않는 고유한 형태만 반환.</summary>
    public static List<HashSet<Vector2Int>> GetUniqueVariants(BlockData blockData)
    {
        return GetUniqueVariants(ReadShape(blockData));
    }

    public static List<HashSet<Vector2Int>> GetUniqueVariants(HashSet<Vector2Int> original)
    {
        var variants = new List<HashSet<Vector2Int>>();
        HashSet<Vector2Int> current = original;

        for (int i = 0; i < 4; i++)
        {
            AddIfUnique(variants, current);
            AddIfUnique(variants, FlipHorizontal(current));
            current = RotateClockwise(current);
        }

        return variants;
    }

    private static void AddIfUnique(List<HashSet<Vector2Int>> variants, HashSet<Vector2Int> candidate)
    {
        HashSet<Vector2Int> normalized = Normalize(candidate);
        if (variants.Any(existing => existing.SetEquals(normalized))) return;
        variants.Add(normalized);
    }

    public static HashSet<Vector2Int> RotateClockwise(HashSet<Vector2Int> cells)
    {
        HashSet<Vector2Int> rotated = new();
        foreach (Vector2Int cell in cells)
        {
            rotated.Add(new Vector2Int(-cell.y, cell.x));
        }
        return Normalize(rotated);
    }

    public static HashSet<Vector2Int> FlipHorizontal(HashSet<Vector2Int> cells)
    {
        HashSet<Vector2Int> flipped = new();
        foreach (Vector2Int cell in cells)
        {
            flipped.Add(new Vector2Int(-cell.x, cell.y));
        }
        return Normalize(flipped);
    }

    public static HashSet<Vector2Int> Normalize(IEnumerable<Vector2Int> cells)
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
}
