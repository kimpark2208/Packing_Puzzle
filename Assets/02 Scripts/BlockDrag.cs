using System;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class BlockDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("이 블록의 그리드 판정용 데이터 (BlockData SO)")]
    public BlockData blockData;

    public event Action<GameObject> OnPlaced;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Canvas rootCanvas;

    private Transform originalParent;
    private Vector2 originalAnchoredPos;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            Debug.LogWarning($"[{name}] 부모 계층에 Canvas가 없습니다.");
            return;
        }
        rootCanvas = parentCanvas.rootCanvas;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (rootCanvas == null) return;

        originalParent = transform.parent;
        originalAnchoredPos = rectTransform.anchoredPosition;

        transform.SetParent(rootCanvas.transform, true);
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (rootCanvas == null) return;
        rectTransform.anchoredPosition += eventData.delta / rootCanvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        bool placed = TryPlaceOnGrid(eventData);
        if (!placed)
        {
            transform.SetParent(originalParent, true);
            rectTransform.anchoredPosition = originalAnchoredPos;
        }
    }

    private bool TryPlaceOnGrid(PointerEventData eventData)
    {
        if (GridManager.Instance == null || blockData == null) return false;

        int pointedRow, pointedCol;
        bool insideGrid = GridManager.Instance.ScreenPointToCell(eventData.position, eventData.pressEventCamera, out pointedRow, out pointedCol);
        if (!insideGrid) return false;

        Vector2Int anchor = blockData.anchorCoord;

        Vector2Int anchorRowCol = ResolveAnchorGridPosition(eventData, pointedRow, pointedCol);
        int anchorRow = anchorRowCol.y;
        int anchorCol = anchorRowCol.x;

        // ▼ 추가: 도형이 그리드 경계를 살짝 벗어나면, 안쪽으로 밀어 넣을 수 있는지 먼저 시도한다 ▼
        Vector2Int clamped = ClampAnchorToFit(blockData.shapeGrid, anchor, anchorRow, anchorCol);
        anchorRow = clamped.y;
        anchorCol = clamped.x;
        // ▲ 추가 ▲

        bool canPlace = GridManager.Instance.CanPlace(blockData.shapeGrid, anchor, anchorRow, anchorCol);
        if (!canPlace) return false;

        GridManager.Instance.PlaceBlock(blockData.shapeGrid, anchor, anchorRow, anchorCol);

        transform.SetParent(GridManager.Instance.transform, false);

        Vector2 offset = GetPivotOffsetFromAnchor(blockData.shapeGrid, anchor);
        Vector2 anchorCellPos = GridManager.Instance.GetCellAnchoredPosition(anchorRow, anchorCol);
        rectTransform.anchoredPosition = anchorCellPos + offset;

        this.enabled = false;
        OnPlaced?.Invoke(gameObject);

        return true;
    }

    // shapeGrid의 각 칸이 실제로 차지하는 targetRow/targetCol 중 그리드를 벗어나는 만큼
    // anchorRow/anchorCol을 안쪽으로 밀어서, 도형 전체가 그리드 안에 들어갈 수 있으면 보정된 좌표를 반환한다.
    // (점유된 칸과의 충돌까지 봐주지는 않음 - 그건 이후 CanPlace가 최종 판정)
    private Vector2Int ClampAnchorToFit(BlockRow[] shapeGrid, Vector2Int anchor, int anchorRow, int anchorCol)
    {
        int minRowOffset = int.MaxValue, maxRowOffset = int.MinValue;
        int minColOffset = int.MaxValue, maxColOffset = int.MinValue;

        for (int r = 0; r < shapeGrid.Length; r++)
        {
            bool[] rowCols = shapeGrid[r].cols;
            for (int c = 0; c < rowCols.Length; c++)
            {
                if (!rowCols[c]) continue;
                int rOffset = r - anchor.y;
                int cOffset = c - anchor.x;
                if (rOffset < minRowOffset) minRowOffset = rOffset;
                if (rOffset > maxRowOffset) maxRowOffset = rOffset;
                if (cOffset < minColOffset) minColOffset = cOffset;
                if (cOffset > maxColOffset) maxColOffset = cOffset;
            }
        }

        int rows = GridManager.Instance.rows;
        int cols = GridManager.Instance.cols;

        // anchorRow + minRowOffset >= 0, anchorRow + maxRowOffset <= rows-1 를 동시에 만족하도록 클램프
        int lowRow = -minRowOffset;
        int highRow = (rows - 1) - maxRowOffset;
        if (lowRow <= highRow)
        {
            anchorRow = Mathf.Clamp(anchorRow, lowRow, highRow);
        }

        int lowCol = -minColOffset;
        int highCol = (cols - 1) - maxColOffset;
        if (lowCol <= highCol)
        {
            anchorCol = Mathf.Clamp(anchorCol, lowCol, highCol);
        }

        return new Vector2Int(anchorCol, anchorRow);
    }

    private Vector2Int ResolveAnchorGridPosition(PointerEventData eventData, int pointedRow, int pointedCol)
    {
        Vector3 blockWorldCenter = rectTransform.TransformPoint(rectTransform.rect.center);
        Vector2 blockScreenCenter = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, blockWorldCenter);

        int centerRow, centerCol;
        bool centerInsideGrid = GridManager.Instance.ScreenPointToCell(blockScreenCenter, eventData.pressEventCamera, out centerRow, out centerCol);

        int baseRow = centerInsideGrid ? centerRow : pointedRow;
        int baseCol = centerInsideGrid ? centerCol : pointedCol;

        BlockRow[] shapeGrid = blockData.shapeGrid;
        Vector2Int anchor = blockData.anchorCoord;

        int totalRows = shapeGrid.Length;
        int totalCols = shapeGrid[0].cols.Length;

        float centerColF = (totalCols - 1) / 2f;
        float centerRowF = (totalRows - 1) / 2f;

        int anchorRow = baseRow + Mathf.RoundToInt(anchor.y - centerRowF);
        int anchorCol = baseCol + Mathf.RoundToInt(anchor.x - centerColF);

        return new Vector2Int(anchorCol, anchorRow);
    }

    private Vector2 GetPivotOffsetFromAnchor(BlockRow[] shapeGrid, Vector2Int anchor)
    {
        int totalRows = shapeGrid.Length;
        int totalCols = shapeGrid[0].cols.Length;

        float centerCol = (totalCols - 1) / 2f;
        float centerRow = (totalRows - 1) / 2f;

        float offsetCol = anchor.x - centerCol;
        float offsetRow = anchor.y - centerRow;

        float cellW = GridManager.Instance.CellWidth;
        float cellH = GridManager.Instance.CellHeight;

        return new Vector2(-offsetCol * cellW, offsetRow * cellH);
    }
}
