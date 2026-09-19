using System.Collections.Generic;

/// <summary>
/// Donald Knuth의 Dancing Links(Algorithm X)를 이용한 진짜 Exact Cover 솔버.
/// 기존 ExactCoverMatrix는 커버 카운터만 증감시키는 흉내였을 뿐 실제로 행/열을 제거하지 않아
/// 백트래킹이 정확하지 않았다. 이 클래스는 이중 연결 리스트 기반의 표준 DLX 구현이다.
///
/// Primary 컬럼: 반드시 정확히 한 번 커버되어야 하는 열 (필수 조건).
/// Secondary 컬럼: 최대 한 번만 커버될 수 있지만 커버되지 않아도 되는 열 (선택 조건).
///   - 밤 퍼즐: 그리드의 모든 칸이 Primary (빈틈없이 채워야 함).
///   - 낮 퍼즐 생성기: "이 블록을 정확히 N개 사용" 슬롯이 Primary, 그리드 칸은 Secondary
///     (레시피 블록들은 겹치면 안 되지만 그리드를 전부 채울 필요는 없음).
/// </summary>
public class DancingLinks
{
    private class Node
    {
        public Node Left, Right, Up, Down;
        public ColumnNode Column;
        public int RowId; // 이 노드가 속한 행이 나타내는 사용자 정의 ID (배치 정보 등)
    }

    private class ColumnNode : Node
    {
        public int Size;      // 이 열에 남아있는(연결된) 노드 수
        public int Index;     // 열 인덱스
        public bool IsPrimary;
    }

    private readonly ColumnNode root = new ColumnNode { Index = -1 };
    private readonly ColumnNode[] columns;
    private readonly List<List<Node>> rowsOfNodes = new();

    /// <param name="totalColumns">전체 열 개수</param>
    /// <param name="primaryCount">앞에서부터 primaryCount개는 Primary, 나머지는 Secondary로 취급</param>
    public DancingLinks(int totalColumns, int primaryCount)
    {
        columns = new ColumnNode[totalColumns];
        ColumnNode prev = root;

        for (int i = 0; i < totalColumns; i++)
        {
            var col = new ColumnNode { Index = i, Size = 0, IsPrimary = i < primaryCount };
            col.Up = col.Down = col;
            columns[i] = col;

            if (col.IsPrimary)
            {
                // Primary 컬럼만 root를 도는 수평 원형 리스트에 연결한다.
                // Secondary 컬럼은 수직(Up/Down) 리스트만 가지며 탐색 대상에서 제외된다.
                col.Left = prev;
                col.Right = root;
                prev.Right = col;
                root.Left = col;
                prev = col;
            }
            else
            {
                col.Left = col.Right = col;
            }
        }
    }

    /// <summary>
    /// 행 하나를 추가한다. rowId는 해결책에서 이 행을 식별하기 위한 사용자 정의 값
    /// (예: "어떤 블록을 어디에 어떤 회전으로 배치했는가"의 인덱스).
    /// </summary>
    public void AddRow(int rowId, IReadOnlyList<int> columnIndices)
    {
        if (columnIndices.Count == 0) return;

        Node first = null;
        Node prev = null;
        var nodesInRow = new List<Node>();

        foreach (int colIndex in columnIndices)
        {
            ColumnNode col = columns[colIndex];
            var node = new Node { Column = col, RowId = rowId };

            // 세로로 컬럼 리스트 맨 아래에 삽입
            node.Down = col;
            node.Up = col.Up;
            col.Up.Down = node;
            col.Up = node;
            col.Size++;

            // 가로로 같은 행의 이전 노드와 연결
            if (first == null)
            {
                first = node;
                node.Left = node.Right = node;
            }
            else
            {
                node.Left = prev;
                node.Right = first;
                prev.Right = node;
                first.Left = node;
            }

            prev = node;
            nodesInRow.Add(node);
        }

        rowsOfNodes.Add(nodesInRow);
    }

    private static void CoverColumn(ColumnNode col)
    {
        col.Right.Left = col.Left;
        col.Left.Right = col.Right;

        for (Node row = col.Down; row != col; row = row.Down)
        {
            for (Node node = row.Right; node != row; node = node.Right)
            {
                node.Down.Up = node.Up;
                node.Up.Down = node.Down;
                node.Column.Size--;
            }
        }
    }

    private static void UncoverColumn(ColumnNode col)
    {
        for (Node row = col.Up; row != col; row = row.Up)
        {
            for (Node node = row.Left; node != row; node = node.Left)
            {
                node.Column.Size++;
                node.Down.Up = node;
                node.Up.Down = node;
            }
        }

        col.Right.Left = col;
        col.Left.Right = col;
    }

    private ColumnNode ChoosePrimaryColumn()
    {
        ColumnNode best = null;
        for (ColumnNode c = (ColumnNode)root.Right; c != root; c = (ColumnNode)c.Right)
        {
            if (best == null || c.Size < best.Size) best = c;
        }
        return best;
    }

    /// <summary>
    /// 해 하나를 찾아 사용된 행들의 rowId 리스트로 반환한다. 해가 없으면 null.
    /// </summary>
    public List<int> SolveOne()
    {
        var solution = new List<int>();
        return Search(solution) ? solution : null;
    }

    private bool Search(List<int> solution)
    {
        if (root.Right == root) return true; // 모든 Primary 컬럼이 커버됨

        ColumnNode col = ChoosePrimaryColumn();
        if (col.Size == 0) return false; // 이 컬럼을 만족시킬 행이 없음 -> 실패

        CoverColumn(col);

        for (Node row = col.Down; row != col; row = row.Down)
        {
            solution.Add(row.RowId);

            for (Node node = row.Right; node != row; node = node.Right)
                CoverColumn(node.Column);

            if (Search(solution)) return true;

            for (Node node = row.Left; node != row; node = node.Left)
                UncoverColumn(node.Column);

            solution.RemoveAt(solution.Count - 1);
        }

        UncoverColumn(col);
        return false;
    }

    /// <summary>
    /// cap개에 도달하면 즉시 멈추고 지금까지 찾은 해의 개수를 반환한다.
    /// 유일해 검증용: CountSolutions(2) == 1 이면 정확히 하나의 해만 존재한다는 뜻이다.
    /// </summary>
    public int CountSolutions(int cap)
    {
        return CountSearch(cap);
    }

    private int CountSearch(int cap)
    {
        if (root.Right == root) return 1;

        ColumnNode col = ChoosePrimaryColumn();
        if (col.Size == 0) return 0;

        CoverColumn(col);

        int count = 0;
        for (Node row = col.Down; row != col && count < cap; row = row.Down)
        {
            for (Node node = row.Right; node != row; node = node.Right)
                CoverColumn(node.Column);

            count += CountSearch(cap - count);

            for (Node node = row.Left; node != row; node = node.Left)
                UncoverColumn(node.Column);
        }

        UncoverColumn(col);
        return count;
    }
}
