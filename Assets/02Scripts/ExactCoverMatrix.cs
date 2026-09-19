using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dancing Links 알고리즘을 위한 정확한 커버 문제(Exact Cover Problem) 행렬
/// 각 행은 특정 꽃 변형과 배치 위치를 나타내고, 각 열은 그리드의 각 셀을 나타낸다
/// </summary>
public class ExactCoverMatrix
{
    /// <summary>
    /// 행 데이터: 꽃 변형 하나가 그리드에 배치될 때의 정보
    /// </summary>
    public struct Row
    {
        public List<int> columnsInThisRow;  // 이 행이 차지하는 열 인덱스 목록
        public int flowerId;                 // 이 행이 나타내는 꽃 ID
        public int startRow;                 // 그리드상 시작 행
        public int startCol;                 // 그리드상 시작 열
        public Shape shape;                  // 꽃 변형 모양
    }

    /// <summary>
    /// 열 데이터: 각 그리드 셀 또는 꽃 필수 조건을 나타낸다
    /// </summary>
    public class ColumnData
    {
        public List<int> rowIndices;        // 이 열을 포함하는 행들의 인덱스
        public int coverCount;              // 선택된 행의 개수

        public ColumnData()
        {
            rowIndices = new List<int>();
            coverCount = 0;
        }
    }

    /// <summary>
    /// 꽃 변형 모양
    /// </summary>
    [Serializable]
    public struct Shape
    {
        public bool[] cells;  // 바운딩 박스 내 셀 점유 정보 (true = 점유)
        public int width;
        public int height;

        public Shape(bool[] cells, int width, int height)
        {
            this.cells = cells;
            this.width = width;
            this.height = height;
        }
    }

    private List<Row> rows = new();
    private Dictionary<int, ColumnData> columns = new();
    private int columnCount = 0;

    /// <summary>
    /// 행 추가
    /// </summary>
    public void AddRow(int flowerId, int startRow, int startCol, Shape shape, List<int> cellIndices)
    {
        var row = new Row
        {
            flowerId = flowerId,
            startRow = startRow,
            startCol = startCol,
            shape = shape,
            columnsInThisRow = new List<int>(cellIndices)
        };
        rows.Add(row);

        foreach (var colIndex in cellIndices)
        {
            if (!columns.ContainsKey(colIndex))
                columns[colIndex] = new ColumnData { rowIndices = new List<int>(), coverCount = 0 };

            columns[colIndex].rowIndices.Add(rows.Count - 1);
        }
    }

    /// <summary>
    /// 전체 열 개수 설정
    /// </summary>
    public void SetColumnCount(int count)
    {
        columnCount = count;
    }

    /// <summary>
    /// 행 개수 반환
    /// </summary>
    public int GetRowCount() => rows.Count;

    /// <summary>
    /// 열 개수 반환
    /// </summary>
    public int GetColumnCount() => columnCount;

    /// <summary>
    /// 특정 열을 포함하는 행 목록
    /// </summary>
    public List<int> GetRowsForColumn(int columnIndex)
    {
        return columns.ContainsKey(columnIndex) ? columns[columnIndex].rowIndices : new List<int>();
    }

    /// <summary>
    /// 가장 적은 행을 가진 열을 반환 (최소 분기 휴리스틱)
    /// </summary>
    public int GetSmallestColumn()
    {
        int minRows = int.MaxValue;
        int resultColumn = -1;

        foreach (var kvp in columns)
        {
            int rowCount = kvp.Value.rowIndices.Count;
            if (rowCount > 0 && rowCount < minRows)
            {
                minRows = rowCount;
                resultColumn = kvp.Key;
            }
        }

        return resultColumn;
    }

    /// <summary>
    /// 특정 행을 선택 (Dancing Links - Cover)
    /// </summary>
    public void CoverRow(int rowIndex)
    {
        var row = rows[rowIndex];
        foreach (var colIndex in row.columnsInThisRow)
        {
            if (columns.ContainsKey(colIndex))
            {
                columns[colIndex].coverCount++;
            }
        }
    }

    /// <summary>
    /// 특정 행의 선택 취소 (Dancing Links - Uncover)
    /// </summary>
    public void UncoverRow(int rowIndex)
    {
        var row = rows[rowIndex];
        foreach (var colIndex in row.columnsInThisRow)
        {
            if (columns.ContainsKey(colIndex))
            {
                columns[colIndex].coverCount--;
            }
        }
    }

    /// <summary>
    /// 특정 행의 정보 반환
    /// </summary>
    public Row GetRow(int rowIndex)
    {
        return rows[rowIndex];
    }

    /// <summary>
    /// 모든 행 반환 (읽기 전용)
    /// </summary>
    public IReadOnlyList<Row> GetAllRows() => rows.AsReadOnly();

#if UNITY_EDITOR
    public void PrintMatrix()
    {
        Debug.Log($"[ExactCoverMatrix] 행: {rows.Count}, 열: {columnCount}");
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var colStr = string.Join(",", row.columnsInThisRow);
            Debug.Log($"  행 {i}: 꽃 {row.flowerId}, 위치 ({row.startRow},{row.startCol}), 열 [{colStr}]");
        }
    }
#endif
}
