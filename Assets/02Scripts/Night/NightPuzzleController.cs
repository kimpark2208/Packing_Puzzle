using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 일반/잉여 밤 퍼즐 보드.
/// 한 번의 드래그로 하나의 블럭을 배치한다. 드래그 도중 같은 칸을 재통과해도 취소하지 않으며,
/// 처음 방문한 고유 셀의 집합만 후보 FlowerData들의 회전/반전 변형과 비교한다.
/// GameManager가 만든 퍼즐 큐의 현재 퍼즐을 EventBus.OnNightStageReady로 받아 로드하고, 풀면 OnNightStageCompleted를 발행한다.
/// </summary>
public class NightPuzzleController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NightCellView cellPrefab;
    [SerializeField] private RectTransform flowerBlockContainer;
    [SerializeField] private float flowerBlockPreviewSize = 220f;  // 블록 한 칸의 최대 크기(블록이 많으면 NoteBack 안에 맞게 줄어든다)
    [SerializeField] private float flowerBlockPadding = 90f;       // NoteBack 가장자리에서 블록을 띄우는 여백(윗부분의 스프링 제본을 피한다)

    [Header("Test Only (인스펙터에서 직접 테스트할 때)")]
    [SerializeField] private int testGridSize = 5;
    [SerializeField] private List<FlowerData> testAllowedBlocks = new();

    [Header("모바일 터치 오차 허용")]
    [Tooltip("각 칸의 터치 판정 영역을 시각적 경계보다 이만큼(px) 더 넓힌다. 특히 그리드 가장자리 칸은 바깥쪽으로 여유가 없어 손가락이 살짝 벗어나면 아예 아무 칸도 감지되지 않는데, 이를 완화한다.")]
    [SerializeField] private float cellHitAreaInflate = 10f;

    private bool[,] filled;
    private int[,] placedBlockIds;
    private Dictionary<int, int> placedBlockFlowerId = new(); // placedBlockId -> flowerId(blockID)
    private int nextPlacedBlockId;
    private int gridSize;

    private NightCellView[,] cells;
    private GridLayoutGroup gridLayout;
    private GraphicRaycaster graphicRaycaster;
    private NightShapePuzzleValidator shapeValidator;

    private readonly HashSet<Vector2Int> currentPathSet = new();
    private readonly List<Vector2Int> previewCells = new();
    private bool isErasing;
    private readonly HashSet<int> erasedBlockIds = new();
    private bool isDrawing;
    private bool currentDrawIsInvalid;
    private bool puzzleCompleted; // 완료 뒤엔 입력을 받지 않는다(결과 팝업의 버튼을 누를 때 뒤의 블록이 지워지지 않도록)

    public NightShapePuzzleValidator Validator { get; set; }

    private void Awake()
    {
        // 퍼즐 정보(EventBus.OnNightStageReady)는 GameManager.OnSceneLoaded에서 오는데, 이는 이 씬 오브젝트들의
        // Start()보다 먼저이므로, LoadPuzzle에 필요한 참조들은 반드시 Awake에서 준비해 둔다(구독은 OnEnable).
        if (!TryGetComponent(out gridLayout))
        {
            Debug.LogWarning("GridLayoutGroup 컴포넌트가 없어서 NightPuzzleController가 정상 동작하지 않을 수 있습니다.");
            return;
        }

        graphicRaycaster = GetComponentInParent<GraphicRaycaster>();
        if (graphicRaycaster == null)
        {
            Debug.LogWarning("GraphicRaycaster가 부모 Canvas에 없습니다. UI 이벤트 레이캐스트가 동작하지 않을 수 있습니다.");
        }
    }

    private void OnEnable()
    {
        EventBus.OnNightStageReady += LoadPuzzle;
    }

    private void OnDisable()
    {
        EventBus.OnNightStageReady -= LoadPuzzle;
    }

    // 퍼즐 정보 이벤트는 이 씬 오브젝트들의 Start()보다 먼저 온다(GameManager.OnSceneLoaded).
    // 그래서 실제 퍼즐로 LoadPuzzle이 이미 불린 뒤에 Start()가 실행되는데,
    // 이 플래그가 없으면 인스펙터에 남아있는 테스트용 블록 설정이 실제 퍼즐 보드를 덮어써버린다.
    private bool loadedExternally;

    private void Start()
    {
        // 인스펙터에 테스트용 블록이 세팅되어 있어도, 이미 실제 퍼즐이 로드된 상태라면 건드리지 않는다.
        if (!loadedExternally && testAllowedBlocks != null && testAllowedBlocks.Count > 0)
        {
            LoadPuzzle(testGridSize, testAllowedBlocks, new bool[testGridSize, testGridSize]);
        }
    }

    /// <summary>절차적으로 생성된 퍼즐 하나를 이 보드에 로드한다.</summary>
    private void LoadPuzzle(int size, List<FlowerData> allowedBlocks, bool[,] wallGrid)
    {
        loadedExternally = true;
        shapeValidator = new NightShapePuzzleValidator(allowedBlocks, wallGrid);
        Validator = shapeValidator;
        CreateBoard(size, wallGrid);
        UpdateFlowerBlockPreview(allowedBlocks);
    }

    private void LoadPuzzle(NightPuzzleData data, List<FlowerData> allowedBlocks)
    {
        LoadPuzzle(data.gridSize, allowedBlocks, data.wallGrid ?? new bool[data.gridSize, data.gridSize]);
    }

    /// <summary>
    /// 이번 스테이지에서 그려야 할 꽃 블록 모양(들)을 NoteBack(flowerBlockContainer) 안에 미리보기로 채운다.
    /// LayoutGroup + AspectRatioFitter 조합은 FitInParent가 셀 크기가 아니라 컨테이너 전체 크기를
    /// 기준으로 맞춰버려서 컨테이너가 커지면 미리보기도 같이 커지는 문제가 있었다. 그래서 크기/위치를
    /// 직접 계산해서 배치한다. 블록이 많으면 여러 줄로 나누고 크기를 줄여, 가장자리 여백(flowerBlockPadding)을
    /// 뺀 컨테이너 안에 항상 들어오게 한다(한 칸의 최대 크기는 flowerBlockPreviewSize).
    /// </summary>
    private void UpdateFlowerBlockPreview(List<FlowerData> allowedBlocks)
    {
        if (flowerBlockContainer == null) return;

        for (int i = flowerBlockContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(flowerBlockContainer.GetChild(i).gameObject);
        }

        if (allowedBlocks == null) return;

        var validBlocks = allowedBlocks.Where(b => b != null && b.dayPieceSprite != null).ToList();
        if (validBlocks.Count == 0) return;

        const float Spacing = 12f;
        Rect area = flowerBlockContainer.rect;
        float availableWidth = Mathf.Max(1f, area.width - flowerBlockPadding * 2f);
        float availableHeight = Mathf.Max(1f, area.height - flowerBlockPadding * 2f);

        // 한 줄에 몇 개를 놓을 때 정사각 칸이 가장 커지는지 고른다(칸 안에 블록을 비율대로 넣는다).
        int count = validBlocks.Count;
        int columns = 1;
        float bestCell = 0f;
        for (int c = 1; c <= count; c++)
        {
            int rowCount = Mathf.CeilToInt(count / (float)c);
            float cell = Mathf.Min((availableWidth - Spacing * (c - 1)) / c, (availableHeight - Spacing * (rowCount - 1)) / rowCount);
            if (cell > bestCell)
            {
                bestCell = cell;
                columns = c;
            }
        }

        float cellSize = Mathf.Min(bestCell, flowerBlockPreviewSize);
        int rows = Mathf.CeilToInt(count / (float)columns);
        float gridHeight = rows * cellSize + Spacing * (rows - 1);

        for (int i = 0; i < count; i++)
        {
            FlowerData block = validBlocks[i];
            int row = i / columns;
            int indexInRow = i % columns;
            int itemsInRow = row == rows - 1 ? count - row * columns : columns; // 마지막 줄은 남는 만큼만 가운데로 모은다
            float rowWidth = itemsInRow * cellSize + Spacing * (itemsInRow - 1);

            float aspect = block.dayPieceSprite.rect.width / block.dayPieceSprite.rect.height;
            float w = cellSize;
            float h = cellSize;
            if (aspect >= 1f) h = w / aspect; else w = h * aspect;

            GameObject previewGO = new GameObject($"BlockPreview_{block.blockID}", typeof(RectTransform), typeof(Image));
            previewGO.transform.SetParent(flowerBlockContainer, false);

            var rt = (RectTransform)previewGO.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            float x = -rowWidth / 2f + indexInRow * (cellSize + Spacing) + cellSize / 2f;
            float y = gridHeight / 2f - row * (cellSize + Spacing) - cellSize / 2f;
            rt.anchoredPosition = area.center + new Vector2(x, y);

            var image = previewGO.GetComponent<Image>();
            image.sprite = block.dayPieceSprite;
            image.color = ColorPalette.ToUnityColor(block.color); // 블록 이미지는 흰색이라 꽃 색으로 틴트한다
            image.preserveAspect = true;
        }
    }

    private void CreateBoard(int size, bool[,] wallGrid)
    {
        ClearBoardObjects();
        puzzleCompleted = false;
        gridLayout.enabled = true;

        gridSize = size;
        cells = new NightCellView[size, size];
        filled = new bool[size, size];
        placedBlockIds = new int[size, size];
        placedBlockFlowerId.Clear();
        nextPlacedBlockId = 0;

        for (int row = 0; row < size; row++)
            for (int col = 0; col < size; col++)
                placedBlockIds[row, col] = -1;

        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = size;

        for (int row = 0; row < size; row++)
        {
            for (int col = 0; col < size; col++)
            {
                NightCellView cell = Instantiate(cellPrefab, gridLayout.transform);
                Vector2Int coord = new Vector2Int(col, row);

                cell.Initialize(coord);

                bool isWall = wallGrid != null &&
                    row < wallGrid.GetLength(0) && col < wallGrid.GetLength(1) && wallGrid[row, col];
                if (isWall)
                {
                    cell.SetAsWall();
                    filled[row, col] = true; // 벽은 채워진 것으로 취급해 완료 판정에서 자연히 제외되게 한다
                }
                else
                {
                    AttachInputHandlers(cell);
                }

                cells[row, col] = cell;
            }
        }

        StartCoroutine(DisableLayoutAfterOneFrame());
    }

    private IEnumerator DisableLayoutAfterOneFrame()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        if (gridLayout != null) gridLayout.enabled = false;

        InflateCellHitAreas();
    }

    // GridLayoutGroup이 칸을 딱 맞닿게 배치하고 나면, 각 칸의 터치 판정 영역을 살짝 더 넓혀서
    // 손가락이 경계에서 몇 픽셀 벗어나도 인접 칸(가장자리는 그 칸 자체)이 계속 인식되게 한다.
    private void InflateCellHitAreas()
    {
        if (cells == null || cellHitAreaInflate <= 0f) return;

        foreach (NightCellView cell in cells)
        {
            if (cell == null) continue;
            var rt = (RectTransform)cell.transform;
            rt.offsetMin -= new Vector2(cellHitAreaInflate, cellHitAreaInflate);
            rt.offsetMax += new Vector2(cellHitAreaInflate, cellHitAreaInflate);
        }
    }

    // 이전엔 Unity EventSystem의 EventTrigger(PointerEnter)로 드래그 연속 진행을 감지했는데,
    // 이는 Unity 자체의 호버 갱신 주기에 의존한다. 빠른 스와이프에서 EventSystem의 호버 갱신이
    // 한 프레임을 건너뛰면 그 사이 지나간 칸(특히 그리드 가장자리 - 넘어가면 더 이상 잡아줄 칸이 없는 마지막 줄/칸)이
    // 통째로 인식되지 않는 문제가 있었다. 대신 매 프레임 직접 레이캐스트하고, 이전 칸과 이어지지 않으면
    // 그 사이 칸들까지 보간해서 채워, 프레임을 건너뛰어도 놓치지 않게 한다.
    private Vector2Int? lastProcessedCoord;

    private void AttachInputHandlers(NightCellView cell)
    {
        Image inputImage = cell.GetComponent<Image>();
        if (inputImage == null)
        {
            Debug.LogError($"{cell.name}: CellBox 루트에 Image가 없습니다. 투명 Image를 추가하고 Raycast Target을 켜세요.");
            return;
        }
        inputImage.raycastTarget = true;
    }

    private void Update()
    {
        if (puzzleCompleted) return;

        if (!isDrawing && Input.GetMouseButtonDown(0) && graphicRaycaster != null && EventSystem.current != null)
        {
            TryStartDrawingFromPointer();
        }
        else if (isDrawing)
        {
            UpdateDrawingFromPointer();

            if (Input.GetMouseButtonUp(0))
            {
                EndDrawing();
            }
        }
    }

    private NightCellView RaycastCellAt(Vector2 screenPosition)
    {
        if (graphicRaycaster == null || EventSystem.current == null) return null;

        PointerEventData pointerEvent = new PointerEventData(EventSystem.current) { position = screenPosition };
        List<RaycastResult> results = new List<RaycastResult>();
        graphicRaycaster.Raycast(pointerEvent, results);

        foreach (RaycastResult result in results)
        {
            NightCellView cell = result.gameObject.GetComponentInParent<NightCellView>();
            if (cell != null) return cell;
        }

        return null;
    }

    private void TryStartDrawingFromPointer()
    {
        NightCellView cell = RaycastCellAt(Input.mousePosition);
        if (cell == null || cell.IsWall) return;

        lastProcessedCoord = null;
        OnCellPointerDown(cell);
    }

    private void UpdateDrawingFromPointer()
    {
        NightCellView cell = RaycastCellAt(Input.mousePosition);
        if (cell == null || cell.IsWall) return;
        if (cell.Coord == lastProcessedCoord) return;

        if (isErasing)
        {
            ErasePlacedBlockAt(cell.Coord);
            lastProcessedCoord = cell.Coord;
            return;
        }

        if (lastProcessedCoord.HasValue)
        {
            // 한 프레임 사이 여러 칸을 건너뛰었으면 그 경로상의 칸들도 순서대로 채워 넣는다.
            foreach (Vector2Int step in GetLineCoords(lastProcessedCoord.Value, cell.Coord))
            {
                if (currentPathSet.Contains(step)) continue;
                if (step.y < 0 || step.y >= gridSize || step.x < 0 || step.x >= gridSize) continue;

                NightCellView stepCell = cells[step.y, step.x];
                if (stepCell == null || stepCell.IsWall) continue;

                TryAddToCurrentBlock(stepCell);
            }
        }
        else if (!currentPathSet.Contains(cell.Coord))
        {
            TryAddToCurrentBlock(cell);
        }

        lastProcessedCoord = cell.Coord;
    }

    // 두 좌표 사이를 잇는 격자 칸들을 Bresenham 알고리즘으로 순서대로 반환한다 (시작점 제외, 끝점 포함).
    private static IEnumerable<Vector2Int> GetLineCoords(Vector2Int from, Vector2Int to)
    {
        int x0 = from.x, y0 = from.y, x1 = to.x, y1 = to.y;
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        bool first = true;
        while (true)
        {
            if (!first) yield return new Vector2Int(x0, y0);
            first = false;

            if (x0 == x1 && y0 == y1) yield break;

            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    private void OnCellPointerDown(NightCellView cell)
    {
        if (cell.IsWall) return;

        isDrawing = true;
        currentDrawIsInvalid = false;
        currentPathSet.Clear();
        previewCells.Clear();
        erasedBlockIds.Clear();

        Vector2Int coord = cell.Coord;
        isErasing = filled[coord.y, coord.x];

        if (isErasing)
        {
            ErasePlacedBlockAt(coord);
            return;
        }

        TryAddToCurrentBlock(cell);
    }

    private void TryAddToCurrentBlock(NightCellView cell)
    {
        Vector2Int coord = cell.Coord;
        if (currentPathSet.Contains(coord)) return;

        if (filled[coord.y, coord.x])
        {
            currentDrawIsInvalid = true;
            previewCells.Add(coord);
            ShowCurrentPreviewAsInvalid();
            return;
        }

        currentPathSet.Add(coord);
        previewCells.Add(coord);

        if (!shapeValidator.CanStillMatch(currentPathSet))
        {
            currentDrawIsInvalid = true;
            ShowCurrentPreviewAsInvalid();
            return;
        }

        cell.ShowPreview(true, null, Color.white);
    }

    private void ShowCurrentPreviewAsInvalid()
    {
        foreach (Vector2Int coord in previewCells)
        {
            if (!filled[coord.y, coord.x])
            {
                cells[coord.y, coord.x].ShowPreview(false, null, Color.white);
            }
        }
    }

    private void ErasePlacedBlockAt(Vector2Int clickedCoord)
    {
        int blockId = placedBlockIds[clickedCoord.y, clickedCoord.x];
        if (blockId < 0 || erasedBlockIds.Contains(blockId)) return;

        erasedBlockIds.Add(blockId);
        placedBlockFlowerId.Remove(blockId);

        int rowCount = placedBlockIds.GetLength(0);
        int colCount = placedBlockIds.GetLength(1);

        for (int row = 0; row < rowCount; row++)
        {
            for (int col = 0; col < colCount; col++)
            {
                if (placedBlockIds[row, col] != blockId) continue;
                if (cells[row, col].IsWall) continue;

                cells[row, col].ResetToEmpty();
                filled[row, col] = false;
                placedBlockIds[row, col] = -1;
            }
        }
    }

    private void EndDrawing()
    {
        isDrawing = false;
        lastProcessedCoord = null;

        if (isErasing)
        {
            isErasing = false;
            erasedBlockIds.Clear();
            CheckCompletion();
            return;
        }

        FlowerData matched = currentDrawIsInvalid ? null : shapeValidator.GetExactMatchBlock(currentPathSet);

        if (matched != null)
        {
            int placedBlockId = nextPlacedBlockId++;
            Sprite finalSprite = matched.nightCellSprite;
            Color finalColor = ColorPalette.ToUnityColor(matched.color);

            foreach (Vector2Int coord in currentPathSet)
            {
                NightCellView cell = cells[coord.y, coord.x];
                cell.Confirm(finalSprite, finalColor);
                filled[coord.y, coord.x] = true;
                placedBlockIds[coord.y, coord.x] = placedBlockId;
            }

            placedBlockFlowerId[placedBlockId] = matched.blockID;
            CheckCompletion();
        }
        else
        {
            foreach (Vector2Int coord in previewCells)
            {
                if (!filled[coord.y, coord.x])
                {
                    cells[coord.y, coord.x].ResetToEmpty();
                }
            }
        }

        currentPathSet.Clear();
        previewCells.Clear();
        currentDrawIsInvalid = false;
    }

    private void CheckCompletion()
    {
        // 칸을 모두 채우고, 주어진 꽃을 모두 한 번 이상 써야 완료다(하나만 쓰고 채운 채로는 끝나지 않는다).
        if (Validator != null && Validator.IsPuzzleComplete(filled) && Validator.AreAllBlocksUsed(placedBlockFlowerId.Values))
        {
            OnPuzzleCompleted();
        }
    }

    private void OnPuzzleCompleted()
    {
        puzzleCompleted = true;
        List<int> obtainedFlowerIds = placedBlockFlowerId.Values.Distinct().ToList();

        foreach (int flowerId in obtainedFlowerIds)
        {
            CurrencyManager.Instance.ObtainFlower(flowerId);
        }

        Debug.Log($"[NightPuzzleController] 퍼즐 완료. 획득한 꽃: {string.Join(",", obtainedFlowerIds)}");
        EventBus.RaiseNightStageCompleted(obtainedFlowerIds);
    }

    private void ClearBoardObjects()
    {
        if (gridLayout == null) return;
        for (int i = gridLayout.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(gridLayout.transform.GetChild(i).gameObject);
        }
    }
}
