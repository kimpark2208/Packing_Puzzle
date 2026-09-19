using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 절차적 밤 퍼즐 생성 (Dancing Links 기반)
/// Phase 1: 요청한 꽃으로 메인 퍼즐 생성
/// Phase 2: 3-5개의 보너스/잉여 퍼즐 생성
/// </summary>
public class ProceduralNightPuzzleGenerator : MonoBehaviour
{
    private static int nextPuzzleId = 1;

    /// <summary>
    /// 퍼즐 생성 메인 메서드
    /// </summary>
    public static void GeneratePuzzles(int gridSize, int[] requestedFlowerIds)
    {
        Debug.Log($"[ProceduralNightPuzzleGenerator] 퍼즐 생성 시작: gridSize={gridSize}, 요청 꽃 {requestedFlowerIds.Length}개");

        var puzzles = new List<NightPuzzleData>();

        // Phase 1: 요청 꽃으로 메인 퍼즐 생성
        var mainPuzzle = GenerateMainPuzzle(gridSize, requestedFlowerIds);
        if (mainPuzzle.HasValue)
        {
            puzzles.Add(mainPuzzle.Value);
            Debug.Log($"[ProceduralNightPuzzleGenerator] 메인 퍼즐 생성 성공 (ID: {mainPuzzle.Value.puzzleId})");
        }
        else
        {
            Debug.LogWarning("[ProceduralNightPuzzleGenerator] 메인 퍼즐 생성 실패, 보너스 퍼즐로 대체");
        }

        // Phase 2: 보너스 퍼즐 생성 (3-5개)
        int bonusCount = UnityEngine.Random.Range(3, 6);
        for (int i = 0; i < bonusCount; i++)
        {
            var bonusPuzzle = GenerateBonusPuzzle(gridSize);
            if (bonusPuzzle.HasValue)
            {
                puzzles.Add(bonusPuzzle.Value);
                Debug.Log($"[ProceduralNightPuzzleGenerator] 보너스 퍼즐 생성 (ID: {bonusPuzzle.Value.puzzleId})");
            }
        }

        // 이벤트 발행
        EventBus.RaiseNightPuzzlesGenerated(puzzles.ToArray());
        Debug.Log($"[ProceduralNightPuzzleGenerator] 총 {puzzles.Count}개 퍼즐 생성 완료");
    }

    /// <summary>
    /// 요청 꽃 기반 메인 퍼즐 생성
    /// </summary>
    private static NightPuzzleData? GenerateMainPuzzle(int gridSize, int[] requestedFlowerIds)
    {
        var matrix = BuildExactCoverMatrix(gridSize, requestedFlowerIds);
        var solution = FindExactCover(matrix);

        if (solution != null && solution.Count > 0)
        {
            return BuildPuzzleFromSolution(gridSize, solution, requestedFlowerIds);
        }

        // 실패 시 Filler로 폴백
        return GenerateWithFillerBlock(gridSize, requestedFlowerIds);
    }

    /// <summary>
    /// 랜덤 보너스 퍼즐 생성
    /// </summary>
    private static NightPuzzleData? GenerateBonusPuzzle(int gridSize)
    {
        var obtainedFlowers = CurrencyManager.Instance.GetObtainedFlowerIds();
        if (obtainedFlowers.Count == 0)
        {
            Debug.LogWarning("[ProceduralNightPuzzleGenerator] 보유 꽃이 없어 보너스 퍼즐 생성 불가");
            return null;
        }

        // 랜덤 꽃 조합 선택
        int randomCount = UnityEngine.Random.Range(2, Math.Min(5, obtainedFlowers.Count + 1));
        var randomFlowers = obtainedFlowers
            .OrderBy(_ => UnityEngine.Random.value)
            .Take(randomCount)
            .ToArray();

        var matrix = BuildExactCoverMatrix(gridSize, randomFlowers);
        var solution = FindExactCover(matrix);

        if (solution != null && solution.Count > 0)
        {
            return BuildPuzzleFromSolution(gridSize, solution, randomFlowers);
        }

        return GenerateWithFillerBlock(gridSize, randomFlowers);
    }

    /// <summary>
    /// 정확한 커버 행렬 생성
    /// 각 행: 특정 꽃의 특정 변형과 배치 위치
    /// 각 열: 그리드의 각 셀
    /// </summary>
    private static ExactCoverMatrix BuildExactCoverMatrix(int gridSize, int[] flowerIds)
    {
        var matrix = new ExactCoverMatrix();
        matrix.SetColumnCount(gridSize * gridSize);

        foreach (int flowerId in flowerIds)
        {
            // TODO: 실제 구현에서는 FlowerDatabase에서 꽃 변형 정보를 가져옴
            // 현재는 placeholder L-tetromino 사용
            var shapes = GetFlowerShapes(flowerId);

            foreach (var shape in shapes)
            {
                // 그리드 모든 위치에서 이 변형을 배치할 수 있는지 확인
                for (int row = 0; row < gridSize; row++)
                {
                    for (int col = 0; col < gridSize; col++)
                    {
                        if (CanPlaceShape(shape, row, col, gridSize))
                        {
                            var cellIndices = GetCellIndicesForShape(shape, row, col, gridSize);
                            matrix.AddRow(flowerId, row, col, shape, cellIndices);
                        }
                    }
                }
            }
        }

        return matrix;
    }

    /// <summary>
    /// 꽃의 모든 변형 반환 (회전/반전)
    /// </summary>
    private static List<ExactCoverMatrix.Shape> GetFlowerShapes(int flowerId)
    {
        // Placeholder: L-tetromino 변형
        var shapes = new List<ExactCoverMatrix.Shape>();

        // L-tetromino 원본: ##
        //                   #
        //                   #
        var shape1 = new ExactCoverMatrix.Shape(
            new[] { true, true, true, false, true, false, true, false },
            2, 4
        );
        shapes.Add(shape1);

        // 90도 회전
        var shape2 = new ExactCoverMatrix.Shape(
            new[] { true, true, true, true, false, false, false, true },
            4, 2
        );
        shapes.Add(shape2);

        // 180도 회전
        var shape3 = new ExactCoverMatrix.Shape(
            new[] { false, true, false, true, true, false, true, true },
            2, 4
        );
        shapes.Add(shape3);

        // 270도 회전
        var shape4 = new ExactCoverMatrix.Shape(
            new[] { true, false, false, false, true, true, true, true },
            4, 2
        );
        shapes.Add(shape4);

        return shapes;
    }

    /// <summary>
    /// 그리드 내에 도형을 배치할 수 있는지 확인
    /// </summary>
    private static bool CanPlaceShape(ExactCoverMatrix.Shape shape, int startRow, int startCol, int gridSize)
    {
        if (startRow + shape.height > gridSize || startCol + shape.width > gridSize)
            return false;

        return true;
    }

    /// <summary>
    /// 도형이 점유하는 셀 인덱스 목록
    /// </summary>
    private static List<int> GetCellIndicesForShape(ExactCoverMatrix.Shape shape, int startRow, int startCol, int gridSize)
    {
        var indices = new List<int>();

        for (int i = 0; i < shape.height; i++)
        {
            for (int j = 0; j < shape.width; j++)
            {
                if (shape.cells[i * shape.width + j])
                {
                    int cellIndex = (startRow + i) * gridSize + (startCol + j);
                    indices.Add(cellIndex);
                }
            }
        }

        return indices;
    }

    /// <summary>
    /// Dancing Links로 정확한 커버 찾기
    /// </summary>
    private static List<int> FindExactCover(ExactCoverMatrix matrix)
    {
        var solution = new List<int>();

        if (SolveExactCover(matrix, solution))
        {
            return solution;
        }

        return null;
    }

    /// <summary>
    /// Dancing Links 재귀 해결 (백트래킹)
    /// </summary>
    private static bool SolveExactCover(ExactCoverMatrix matrix, List<int> solution)
    {
        // 모든 열이 커버되었으면 성공
        if (matrix.GetColumnCount() == 0)
            return true;

        // 가장 작은 열 선택 (최소 분기 휴리스틱)
        int columnIndex = matrix.GetSmallestColumn();
        if (columnIndex == -1)
            return false;

        var rows = matrix.GetRowsForColumn(columnIndex);

        foreach (int rowIndex in rows)
        {
            solution.Add(rowIndex);

            var row = matrix.GetRow(rowIndex);
            foreach (int col in row.columnsInThisRow)
            {
                matrix.CoverRow(rowIndex);
            }

            if (SolveExactCover(matrix, solution))
            {
                return true;
            }

            // 백트래킹
            solution.RemoveAt(solution.Count - 1);
            foreach (int col in row.columnsInThisRow)
            {
                matrix.UncoverRow(rowIndex);
            }
        }

        return false;
    }

    /// <summary>
    /// 해결책으로부터 퍼즐 데이터 생성
    /// </summary>
    private static NightPuzzleData BuildPuzzleFromSolution(int gridSize, List<int> solution, int[] flowerIds)
    {
        var puzzle = new NightPuzzleData
        {
            puzzleId = nextPuzzleId++,
            gridSize = gridSize,
            requiredFlowerIds = flowerIds,
            filledGrid = new bool[gridSize, gridSize],
            placedFlowerIds = new int[gridSize, gridSize]
        };

        // 초기화
        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                puzzle.filledGrid[i, j] = false;
                puzzle.placedFlowerIds[i, j] = -1;
            }
        }

        // 해결책 적용 (placeholder)
        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                puzzle.filledGrid[i, j] = true;
                puzzle.placedFlowerIds[i, j] = flowerIds[i % flowerIds.Length];
            }
        }

        return puzzle;
    }

    /// <summary>
    /// Filler 블록으로 퍼즐 생성 (정확한 커버 실패 시 폴백)
    /// </summary>
    private static NightPuzzleData GenerateWithFillerBlock(int gridSize, int[] flowerIds)
    {
        Debug.Log("[ProceduralNightPuzzleGenerator] Filler 블록으로 폴백");

        var puzzle = new NightPuzzleData
        {
            puzzleId = nextPuzzleId++,
            gridSize = gridSize,
            requiredFlowerIds = flowerIds,
            filledGrid = new bool[gridSize, gridSize],
            placedFlowerIds = new int[gridSize, gridSize]
        };

        // 전체 그리드 채우기
        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                puzzle.filledGrid[i, j] = true;
                puzzle.placedFlowerIds[i, j] = flowerIds[i % flowerIds.Length];
            }
        }

        return puzzle;
    }
}
