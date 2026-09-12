using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class BlockDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("이 블록의 그리드 판정용 데이터 (BlockData SO)")]
    public BlockData blockData;

    [Header("조작 방식")]
    [Tooltip("더블클릭 시 90도 회전")]
    public bool rotateOnDoubleClick = true;
    [Tooltip("우클릭 시 좌우반전")]
    public bool flipOnRightClick = true;

    [Header("롱프레스 설정")]
    [Tooltip("롱프레스(초) 동안 누르면 플립됩니다.")]
    public float longPressThreshold = 0.6f;

    private float lastClickTime = -1f;
    private const float DoubleClickThreshold = 0.3f;

    public event Action<GameObject> OnPlaced;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Canvas rootCanvas;
    private Image blockImage;
    private Color originalColor;
    private bool hasCachedOriginalColor = false;

    private Transform originalParent;
    private Vector2 originalAnchoredPos;

    private BlockRow[] currentShapeGrid;
    private Vector2Int currentAnchor;
    private bool isFlipped = false;

    private bool isPlacedOnGrid = false;
    private bool wasPlacedBeforeDrag = false;

    // 롱프레스 관련 상태
    private bool pointerDown = false;
    private float pointerDownTime = 0f;
    private bool hadLongPress = false;
    private bool isDragging = false;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        blockImage = GetComponent<Image>();

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            Debug.LogWarning($"[{name}] 부모 계층에 Canvas가 없습니다.");
            return;
        }
        rootCanvas = parentCanvas.rootCanvas;
    }

    void Start()
    {
        if (blockData != null)
        {
            InitShapeFromBlockData();
        }
    }

    void Update()
    {
        // 롱프레스 감지: 포인터가 눌려있고 드래그 중이 아닐 때만 카운트
        if (pointerDown && !isDragging && !hadLongPress)
        {
            if (Time.unscaledTime - pointerDownTime >= longPressThreshold)
            {
                hadLongPress = true;
                pointerDown = false; // 롱프레스 한 번만 트리거
                FlipHorizontal();
            }
        }
    }

    public void InitShapeFromBlockData()
    {
        currentShapeGrid = CloneShape(blockData.shapeGrid);
        currentAnchor = blockData.anchorCoord;
        isFlipped = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (rootCanvas == null) return;

        originalParent = transform.parent;
        originalAnchoredPos = rectTransform.anchoredPosition;

        // 드래그 시작 전에 그리드에 있었는지 기억해둔다.
        wasPlacedBeforeDrag = isPlacedOnGrid;

        if (isPlacedOnGrid)
        {
            GridManager.Instance.ClearOwnedCells(this);
            isPlacedOnGrid = false;
        }

        transform.SetParent(rootCanvas.transform, true);
        canvasGroup.blocksRaycasts = false;

        // 드래그 시작 시 롱프레스 취소
        isDragging = true;
        pointerDown = false;
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

        // 드래그 끝났음을 표시
        isDragging = false;

        if (!placed)
        {
            // 드래그 시작 전에 그리드에 있었고, 드래그 후 그리드에 다시 놓지 않았으면 삭제
            if (wasPlacedBeforeDrag)
            {
                Destroy(gameObject);
                return;
            }

            // 그리드에 원래 없던 블록이면 원래 위치로 복귀
            transform.SetParent(originalParent, true);
            rectTransform.anchoredPosition = originalAnchoredPos;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // 포인터 다운 시 롱프레스 타이머 시작
        pointerDown = true;
        hadLongPress = false;
        pointerDownTime = Time.unscaledTime;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // 포인터 업 시 롱프레스 취소(이미 롱프레스가 발생했다면 hadLongPress가 true)
        pointerDown = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 롱프레스가 이미 발생했다면 클릭 이벤트 처리하지 않음
        if (hadLongPress)
        {
            hadLongPress = false;
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (flipOnRightClick) FlipHorizontal();
            return;
        }

        if (!rotateOnDoubleClick) return;

        float now = Time.unscaledTime;
        if (now - lastClickTime <= DoubleClickThreshold)
        {
            RotateClockwise90();
            lastClickTime = -1f;
        }
        else
        {
            lastClickTime = now;
        }
    }

    public void RotateClockwise90()
    {
        if (isPlacedOnGrid)
        {
            GridManager.Instance.ClearOwnedCells(this);
        }

        int oldRows = currentShapeGrid.Length;
        int oldCols = currentShapeGrid[0].cols.Length;

        BlockRow[] rotated = new BlockRow[oldCols];
        for (int r = 0; r < oldCols; r++)
        {
            rotated[r].cols = new bool[oldRows];
        }

        for (int r = 0; r < oldRows; r++)
        {
            for (int c = 0; c < oldCols; c++)
            {
                rotated[c].cols[oldRows - 1 - r] = currentShapeGrid[r].cols[c];
            }
        }

        Vector2Int newAnchor = new Vector2Int(oldRows - 1 - currentAnchor.y, currentAnchor.x);

        currentShapeGrid = rotated;
        currentAnchor = newAnchor;

        // 정확한 90도 단위로 회전 각도를 정규화해서 부동소수점 누적 오차 방지
        float currentZ = Mathf.Round(rectTransform.localEulerAngles.z / 90f) * 90f;
        float newZ = currentZ - 90f;
        rectTransform.localEulerAngles = new Vector3(0f, 0f, newZ);

        // 위치(anchoredPosition)는 TryRePlaceAfterTransform가 재정렬해줍니다.
        TryRePlaceAfterTransform();
    }

    public void FlipHorizontal()
    {
        if (isPlacedOnGrid)
        {
            GridManager.Instance.ClearOwnedCells(this);
        }

        int totalRows = currentShapeGrid.Length;
        int totalCols = currentShapeGrid[0].cols.Length;

        BlockRow[] flipped = new BlockRow[totalRows];
        for (int r = 0; r < totalRows; r++)
        {
            flipped[r].cols = new bool[totalCols];
            for (int c = 0; c < totalCols; c++)
            {
                flipped[r].cols[totalCols - 1 - c] = currentShapeGrid[r].cols[c];
            }
        }

        Vector2Int newAnchor = new Vector2Int(totalCols - 1 - currentAnchor.x, currentAnchor.y);

        currentShapeGrid = flipped;
        currentAnchor = newAnchor;

        // 시각적 반전 상태 토글
        isFlipped = !isFlipped;

        // 수평(좌우) 반전만 수행: X 스케일의 부호만 토글하고 Y 스케일은 절대값으로 보존
        Vector3 scale = rectTransform.localScale;
        float absX = Mathf.Abs(scale.x);
        float absY = Mathf.Abs(scale.y);
        rectTransform.localScale = new Vector3(isFlipped ? -absX : absX, absY, scale.z);

        TryRePlaceAfterTransform();
    }

    // 그리드에 이미 놓여 있던 블록을 회전/반전한 경우, 같은 자리 기준으로 다시 배치한다.
    // 겹치더라도 더 이상 실패로 취급하지 않고, 그리드 범위 안에만 있으면 그대로 배치한다.
    private void TryRePlaceAfterTransform()
    {
        if (GridManager.Instance == null) return;

        int row, col;
        bool insideGrid = GridManager.Instance.ScreenPointToCell(
            RectTransformUtility.WorldToScreenPoint(null, rectTransform.position),
            null, out row, out col);

        if (!insideGrid) return;

        Vector2Int clamped = ClampAnchorToFit(currentShapeGrid, currentAnchor, row, col);

        bool canPlace = GridManager.Instance.CanPlace(currentShapeGrid, currentAnchor, clamped.y, clamped.x);
        if (!canPlace) return;

        GridManager.Instance.PlaceBlock(currentShapeGrid, currentAnchor, clamped.y, clamped.x, this);

        Vector2 offset = GetPivotOffsetFromAnchor(currentShapeGrid, currentAnchor);
        Vector2 anchorCellPos = GridManager.Instance.GetCellAnchoredPosition(clamped.y, clamped.x);
        rectTransform.anchoredPosition = anchorCellPos + offset;

        isPlacedOnGrid = true;
    }

    private bool TryPlaceOnGrid(PointerEventData eventData)
    {
        if (GridManager.Instance == null || blockData == null) return false;

        int pointedRow, pointedCol;
        bool insideGrid = GridManager.Instance.ScreenPointToCell(eventData.position, eventData.pressEventCamera, out pointedRow, out pointedCol);
        if (!insideGrid) return false;

        Vector2Int anchor = currentAnchor;

        Vector2Int anchorRowCol = ResolveAnchorGridPosition(eventData, pointedRow, pointedCol);
        int anchorRow = anchorRowCol.y;
        int anchorCol = anchorRowCol.x;

        Vector2Int clamped = ClampAnchorToFit(currentShapeGrid, anchor, anchorRow, anchorCol);
        anchorRow = clamped.y;
        anchorCol = clamped.x;

        // 그리드 범위 안에 있는지만 확인한다. 다른 블록과 겹치는 것은 배치를 막지 않는다.
        bool canPlace = GridManager.Instance.CanPlace(currentShapeGrid, anchor, anchorRow, anchorCol);
        if (!canPlace) return false;

        GridManager.Instance.PlaceBlock(currentShapeGrid, anchor, anchorRow, anchorCol, this);

        transform.SetParent(GridManager.Instance.transform, false);

        Vector2 offset = GetPivotOffsetFromAnchor(currentShapeGrid, anchor);
        Vector2 anchorCellPos = GridManager.Instance.GetCellAnchoredPosition(anchorRow, anchorCol);
        rectTransform.anchoredPosition = anchorCellPos + offset;

        isPlacedOnGrid = true;
        OnPlaced?.Invoke(gameObject);

        return true;
    }

    // GridManager가 호출하여, 이 블록이 다른 블록과 겹쳐 있는지 여부를 색으로 표시하도록 지시한다.
    public void SetOverlapVisual(bool isOverlapping)
    {
        if (blockImage == null) return;

        if (!hasCachedOriginalColor)
        {
            originalColor = blockImage.color;
            hasCachedOriginalColor = true;
        }

        blockImage.color = isOverlapping ? GridManager.Instance.OverlapColor : originalColor;
    }

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

        int lowRow = -minRowOffset;
        int highRow = (rows - 1) - maxRowOffset;
        if (lowRow <= highRow) anchorRow = Mathf.Clamp(anchorRow, lowRow, highRow);

        int lowCol = -minColOffset;
        int highCol = (cols - 1) - maxColOffset;
        if (lowCol <= highCol) anchorCol = Mathf.Clamp(anchorCol, lowCol, highCol);

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

        BlockRow[] shapeGrid = currentShapeGrid;
        Vector2Int anchor = currentAnchor;

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

    private BlockRow[] CloneShape(BlockRow[] source)
    {
        BlockRow[] clone = new BlockRow[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            clone[i].cols = (bool[])source[i].cols.Clone();
        }
        return clone;
    }
}
