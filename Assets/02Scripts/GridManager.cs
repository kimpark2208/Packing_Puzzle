using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("그리드 크기 (행, 열)")]
    public int rows = 5;
    public int cols = 5;

    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private float cellWidth;
    private float cellHeight;

    public float CellWidth => cellWidth;
    public float CellHeight => cellHeight;


    private bool[,] occupied;

    void Awake()
    {
        Instance = this;
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();
        occupied = new bool[rows, cols];
        RecalculateCellSize();

        // ▼ 디버깅: Grid와 그 부모들의 위치/스케일을 전부 출력 ▼
        Transform t = transform;
        while (t != null)
        {
            Debug.Log($"[계층] {t.name} - localPos: {t.localPosition}, localScale: {t.localScale}, worldPos: {t.position}");
            t = t.parent;
        }
        // ▲ 디버깅 ▲
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
        if (!converted)
        {
            Debug.LogWarning($"변환 실패. cam: {cam}");
            return false;
        }

        Rect rect = rectTransform.rect;

        float xFromLeft = localPoint.x - rect.xMin;
        float yFromTop = rect.yMax - localPoint.y;

        col = Mathf.FloorToInt(xFromLeft / cellWidth);
        row = Mathf.FloorToInt(yFromTop / cellHeight);

        if (row < 0 || row >= rows || col < 0 || col >= cols)
            return false;

        return true;
    }

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

                if (targetRow < 0 || targetRow >= rows || targetCol < 0 || targetCol >= cols)
                {
                    Debug.Log($"CanPlace 실패: 범위 밖 -> r:{r} c:{c} => targetRow:{targetRow} targetCol:{targetCol} (anchorRow:{anchorRow} anchorCol:{anchorCol})");
                    return false;
                }
                if (occupied[targetRow, targetCol])
                {
                    Debug.Log($"CanPlace 실패: 이미 점유됨 -> targetRow:{targetRow} targetCol:{targetCol}");
                    return false;
                }
            }
        }
        return true;
    }

    public void PlaceBlock(BlockRow[] shapeGrid, Vector2Int anchor, int anchorRow, int anchorCol)
    {
        for (int r = 0; r < shapeGrid.Length; r++)
        {
            bool[] rowCols = shapeGrid[r].cols;
            for (int c = 0; c < rowCols.Length; c++)
            {
                if (!rowCols[c]) continue;
                int targetRow = anchorRow + (r - anchor.y);
                int targetCol = anchorCol + (c - anchor.x);
                occupied[targetRow, targetCol] = true;
            }
        }
    }

    public Vector2 GetCellAnchoredPosition(int row, int col)
    {
        Rect rect = rectTransform.rect;
        float x = rect.xMin + (col + 0.5f) * cellWidth;
        float y = rect.yMax - (row + 0.5f) * cellHeight;
        return new Vector2(x, y);
    }

    public bool IsFull()
    {
        foreach (bool b in occupied)
            if (!b) return false;
        return true;
    }
}
