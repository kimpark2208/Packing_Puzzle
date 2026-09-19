using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 일반/잉여 밤 퍼즐 보드.
/// 한 번의 드래그로 하나의 블럭을 배치한다. 드래그 도중 같은 칸을 재통과해도 취소하지 않으며,
/// 처음 방문한 고유 셀의 집합만 후보 BlockData들의 회전/반전 변형과 비교한다.
/// GameFlowController가 밤 시간대에 생성된 퍼즐 큐를 순서대로 이 컨트롤러에 넘겨준다.
/// </summary>
public class NightBoardController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NightCellView cellPrefab;

    [Header("Test Only (인스펙터에서 직접 테스트할 때)")]
    [SerializeField] private int testGridSize = 5;
    [SerializeField] private List<BlockData> testAllowedBlocks = new();

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

    public INightPuzzleValidator Validator { get; set; }

    /// <summary>이번 퍼즐이 완료되어 획득한 꽃 ID 목록과 함께 발생.</summary>
    public event System.Action<List<int>> OnStagePuzzleCompleted;

    private void Awake()
    {
        // GameFlowController.OnSceneLoaded(SceneManager.sceneLoaded)는 이 씬 오브젝트들의 Start()보다
        // 먼저 발생하므로, LoadPuzzle 호출에 필요한 참조들은 반드시 Awake에서 준비해 둔다.
        if (!TryGetComponent(out gridLayout))
        {
            Debug.LogWarning("GridLayoutGroup 컴포넌트가 없어서 NightBoardController가 정상 동작하지 않을 수 있습니다.");
            return;
        }

        graphicRaycaster = GetComponentInParent<GraphicRaycaster>();
        if (graphicRaycaster == null)
        {
            Debug.LogWarning("GraphicRaycaster가 부모 Canvas에 없습니다. UI 이벤트 레이캐스트가 동작하지 않을 수 있습니다.");
        }
    }

    private void Start()
    {
        // 인스펙터에 테스트용 블록이 세팅되어 있으면 바로 시작 (에디터 단독 테스트용).
        if (testAllowedBlocks != null && testAllowedBlocks.Count > 0)
        {
            LoadPuzzle(testGridSize, testAllowedBlocks, new bool[testGridSize, testGridSize]);
        }
    }

    /// <summary>GameFlowController가 절차적으로 생성한 퍼즐 하나를 이 보드에 로드한다.</summary>
    public void LoadPuzzle(int size, List<BlockData> allowedBlocks, bool[,] wallGrid)
    {
        shapeValidator = new NightShapePuzzleValidator(allowedBlocks, wallGrid);
        Validator = shapeValidator;
        CreateBoard(size, wallGrid);
    }

    public void LoadPuzzle(NightPuzzleData data, List<BlockData> allowedBlocks)
    {
        LoadPuzzle(data.gridSize, allowedBlocks, data.wallGrid ?? new bool[data.gridSize, data.gridSize]);
    }

    private void CreateBoard(int size, bool[,] wallGrid)
    {
        ClearBoardObjects();
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
    }

    private void AttachInputHandlers(NightCellView cell)
    {
        Image inputImage = cell.GetComponent<Image>();
        if (inputImage == null)
        {
            Debug.LogError($"{cell.name}: CellBox 루트에 Image가 없습니다. 투명 Image를 추가하고 Raycast Target을 켜세요.");
            return;
        }
        inputImage.raycastTarget = true;

        EventTrigger trigger = cell.GetComponent<EventTrigger>();
        if (trigger == null) trigger = cell.gameObject.AddComponent<EventTrigger>();
        else trigger.triggers.Clear();

        AddTrigger(trigger, EventTriggerType.PointerDown, _ => OnCellPointerDown(cell));
        AddTrigger(trigger, EventTriggerType.PointerEnter, _ => OnCellPointerEnter(cell));
    }

    private void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action<BaseEventData> action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(data => action(data));
        trigger.triggers.Add(entry);
    }

    private void Update()
    {
        if (!isDrawing && Input.GetMouseButtonDown(0) && graphicRaycaster != null && EventSystem.current != null)
        {
            TryStartDrawingFromPointer();
        }

        if (isDrawing && Input.GetMouseButtonUp(0))
        {
            EndDrawing();
        }
    }

    private void TryStartDrawingFromPointer()
    {
        PointerEventData pointerEvent = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        List<RaycastResult> results = new List<RaycastResult>();
        graphicRaycaster.Raycast(pointerEvent, results);

        foreach (RaycastResult result in results)
        {
            NightCellView cell = result.gameObject.GetComponentInParent<NightCellView>();
            if (cell != null && !cell.IsWall)
            {
                OnCellPointerDown(cell);
                break;
            }
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

    private void OnCellPointerEnter(NightCellView cell)
    {
        if (!isDrawing) return;
        if (cell.IsWall) return;

        if (isErasing)
        {
            ErasePlacedBlockAt(cell.Coord);
            return;
        }

        if (currentPathSet.Contains(cell.Coord)) return;
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

        if (isErasing)
        {
            isErasing = false;
            erasedBlockIds.Clear();
            CheckCompletion();
            return;
        }

        BlockData matched = currentDrawIsInvalid ? null : shapeValidator.GetExactMatchBlock(currentPathSet);

        if (matched != null)
        {
            int placedBlockId = nextPlacedBlockId++;
            Sprite finalSprite = matched.flowerIcon;
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
        if (Validator != null && Validator.IsPuzzleComplete(filled))
        {
            OnPuzzleCompleted();
        }
    }

    private void OnPuzzleCompleted()
    {
        List<int> obtainedFlowerIds = placedBlockFlowerId.Values.Distinct().ToList();

        foreach (int flowerId in obtainedFlowerIds)
        {
            CurrencyManager.Instance.ObtainFlower(flowerId);
            EventBus.RaiseNightPuzzleComplete(flowerId);
        }

        Debug.Log($"[NightBoardController] 퍼즐 완료. 획득한 꽃: {string.Join(",", obtainedFlowerIds)}");
        OnStagePuzzleCompleted?.Invoke(obtainedFlowerIds);
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
