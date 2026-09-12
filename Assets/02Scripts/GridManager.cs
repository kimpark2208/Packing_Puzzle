using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("그리드 크기 (행, 열)")]
    public int rows = 5;
    public int cols = 5;

    [Header("겹침 표시 색상")]
    public Color overlapColor = new Color(1f, 0.2f, 0.2f, 0.85f);

    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private float cellWidth;
    private float cellHeight;

    public float CellWidth => cellWidth;
    public float CellHeight => cellHeight;

    // 한 칸에 여러 블록이 겹칠 수 있으므로 리스트로 관리
    private List<BlockDrag>[,] cellOwners;

    void Awake()
    {
        Instance = this;
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();

        cellOwners = new List<BlockDrag>[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                cellOwners[r, c] = new List<BlockDrag>();

        RecalculateCellSize();
    }

    public void RecalculateCellSize()
    {
        Rect rect = rectTransform.rect;
        cellWidth = rect.width / cols;
        cellHeight = rect.height / rows;
    }

    public bool ScreenPointToCell(Vector2 screenPoint, Camera eventCamera, out int row, out int col)
    {
        row = -1; col = -1;

        Camera cam = (parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            ? null
            : eventCamera;

        Vector2 localPoint;
        bool converted = RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, cam, out localPoint);
        if (!converted) return false;

        Rect rect = rectTransform.rect;

        float xFromLeft = localPoint.x - rect.xMin;
        float yFromTop = rect.yMax - localPoint.y;

        col = Mathf.FloorToInt(xFromLeft / cellWidth);
        row = Mathf.FloorToInt(yFromTop / cellHeight);

        if (row < 0 || row >= rows || col < 0 || col >= cols)
            return false;

        return true;
    }

    // 그리드 범위 안에 들어가는지만 확인한다. 다른 블록과 겹치는 것은 더 이상 배치 실패 사유가 아니다.
    public bool CanPlace(BlockRow[] shapeGrid, Vector2Int anchor, int anchorRow, int anchorCol)
    {
        for (int r = 0; r < shapeGrid.Length; r++)
        {
            bool[] rowCols = shapeGrid[r].cols;
            for (int c = 0; c < rowCols.Length; c++)
            {
                if (!rowCols[c]) continue;
                int targetRow = anchorRow + (r - anchor.y);
                int targetCol = anchorCol + (c - anchor.x);
                if (targetRow < 0 || targetRow >= rows || targetCol < 0 || targetCol >= cols) return false;
            }
        }
        return true;
    }

    // placedBy가 차지한 칸을 등록하고, 그 칸에 이미 다른 블록이 있으면 둘 다 겹침 상태로 표시한다.
    public void PlaceBlock(BlockRow[] shapeGrid, Vector2Int anchor, int anchorRow, int anchorCol, BlockDrag placedBy)
    {
        for (int r = 0; r < shapeGrid.Length; r++)
        {
            bool[] rowCols = shapeGrid[r].cols;
            for (int c = 0; c < rowCols.Length; c++)
            {
                if (!rowCols[c]) continue;
                int targetRow = anchorRow + (r - anchor.y);
                int targetCol = anchorCol + (c - anchor.x);

                var list = cellOwners[targetRow, targetCol];
                if (!list.Contains(placedBy)) list.Add(placedBy);
            }
        }

        RefreshOverlapStates();
    }

    // 재배치/회전/반전 시, 특정 블록이 차지하고 있던 칸 등록을 전부 지운다.
    public void ClearOwnedCells(BlockDrag block)
    {
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                cellOwners[r, c].Remove(block);
            }
        }

        RefreshOverlapStates();
    }

    // 모든 칸을 훑어서, 2개 이상의 블록이 차지한 칸에 관련된 블록들에게 겹침 여부를 통지한다.
    private void RefreshOverlapStates()
    {
        HashSet<BlockDrag> overlapping = new HashSet<BlockDrag>();

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var list = cellOwners[r, c];
                if (list.Count >= 2)
                {
                    foreach (var b in list) overlapping.Add(b);
                }
            }
        }

        HashSet<BlockDrag> allBlocks = new HashSet<BlockDrag>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                foreach (var b in cellOwners[r, c]) allBlocks.Add(b);

        foreach (var b in allBlocks)
        {
            if (b == null) continue;
            b.SetOverlapVisual(overlapping.Contains(b));
        }
    }

    public Color OverlapColor => overlapColor;

    public Vector2 GetCellAnchoredPosition(int row, int col)
    {
        Rect rect = rectTransform.rect;
        float x = rect.xMin + (col + 0.5f) * cellWidth;
        float y = rect.yMax - (row + 0.5f) * cellHeight;
        return new Vector2(x, y);
    }

    // 어느 칸이든 2개 이상의 블록이 겹쳐 있으면 true (클리어/승리 판정 등에서 활용 가능)
    public bool HasAnyOverlap()
    {
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (cellOwners[r, c].Count >= 2) return true;
        return false;
    }

    public bool IsFull()
    {
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (cellOwners[r, c].Count == 0) return false;
        return true;
    }
}
