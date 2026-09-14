using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 일반/잉여 밤 퍼즐 보드.
/// 한 번의 드래그로 하나의 블럭을 배치한다. 드래그 도중 같은 칸을 재통과해도 취소하지 않으며,
/// 처음 방문한 고유 셀의 집합만 BlockData의 회전/반전 변형과 비교한다.
/// </summary>
public class NightBoardController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NightCellView cellPrefab;
    [SerializeField] private Sprite curFlowerCellSprite;
    [SerializeField] private Color curFlowerColor = Color.white;

    [Header("Test Only")]
    [SerializeField] private int gridSize = 5; // TODO: 실제 게임에서는 스테이지 데이터에서 받아와야 함.
    [SerializeField] private BlockData testTargetBlock;

    private bool[,] filled;
    private int[,] placedBlockIds;
    private int nextPlacedBlockId;

    private NightCellView[,] cells;
    private GridLayoutGroup gridLayout;
    private GraphicRaycaster graphicRaycaster;
    private NightShapePuzzleValidator shapeValidator;

    // 이번 드래그에서 처음 방문한 고유 셀들.
    // 순서는 필요 없으며, 같은 셀을 다시 지나가도 이 집합은 유지한다.
    private readonly HashSet<Vector2Int> currentPathSet = new();

    // 프리뷰가 표시된 셀들. 드래그 실패 시 한 번에 원상복구하기 위해 별도로 보관한다.
    private readonly List<Vector2Int> previewCells = new();

    // 점유된 칸에서 시작한 입력은 블럭 삭제 모드다.
    private bool isErasing;

    // 한 번의 삭제 드래그에서 이미 삭제한 블럭 ID들.
    // 같은 블럭의 다른 칸을 다시 지나가도 중복 삭제하지 않는다.
    private readonly HashSet<int> erasedBlockIds = new();

    private bool isDrawing;
    private bool currentDrawIsInvalid;

    public INightPuzzleValidator Validator { get; set; }

    private void Start()
    {
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

        if (testTargetBlock == null)
        {
            Debug.LogWarning("Test Target Block이 비어 있습니다. BlockData SO를 NightBoardController Inspector에 할당하세요.");
            return;
        }

        // TODO: 실제 게임에서는 스테이지 데이터가 BlockData를 전달하고, 여기서 Validator를 생성/주입한다.
        shapeValidator = new NightShapePuzzleValidator(testTargetBlock);
        Validator = shapeValidator;

        if (gridSize > 0)
        {
            CreateBoard(gridSize);
        }
    }

    public void CreateBoard(int size)
    {
        ClearBoardObjects();
        gridLayout.enabled = true;

        gridSize = size;
        cells = new NightCellView[size, size];
        filled = new bool[size, size];
        placedBlockIds = new int[size, size];
        nextPlacedBlockId = 0;

        // 빈 칸은 -1. 0 이상은 확정된 블럭 배치 ID다.
        for (int row = 0; row < size; row++)
        {
            for (int col = 0; col < size; col++)
            {
                placedBlockIds[row, col] = -1;
            }
        }

        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = size;

        for (int row = 0; row < size; row++)
        {
            for (int col = 0; col < size; col++)
            {
                NightCellView cell = Instantiate(cellPrefab, gridLayout.transform);
                Vector2Int coord = new Vector2Int(col, row);

                cell.Initialize(coord);
                AttachInputHandlers(cell);

                cells[row, col] = cell;
            }
        }

        StartCoroutine(DisableLayoutAfterOneFrame());
    }

    private IEnumerator DisableLayoutAfterOneFrame()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        if (gridLayout != null)
        {
            gridLayout.enabled = false;
        }
    }

    private void AttachInputHandlers(NightCellView cell)
    {
        // CellBox 루트의 투명 Image가 반드시 입력을 받는다.
        Image inputImage = cell.GetComponent<Image>();

        if (inputImage == null)
        {
            Debug.LogError(
                $"{cell.name}: CellBox 루트에 Image가 없습니다. " +
                "투명 Image를 추가하고 Raycast Target을 켜세요.");

            return;
        }

        inputImage.raycastTarget = true;

        EventTrigger trigger = cell.GetComponent<EventTrigger>();

        if (trigger == null)
        {
            trigger = cell.gameObject.AddComponent<EventTrigger>();
        }
        else
        {
            trigger.triggers.Clear();
        }

        AddTrigger(
            trigger,
            EventTriggerType.PointerDown,
            _ => OnCellPointerDown(cell));

        AddTrigger(
            trigger,
            EventTriggerType.PointerEnter,
            _ => OnCellPointerEnter(cell));
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
        PointerEventData pointerEvent = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

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

        // 이미 점유된 셀에서 시작하면 이 입력 전체는 블럭 삭제 모드다.
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
            // 삭제 모드에서는 지나간 점유 칸이 속한 블럭 전체를 지운다.
            ErasePlacedBlockAt(cell.Coord);
            return;
        }

        // 같은 칸 재통과는 한 붓으로 못 그리는 도형을 허용하기 위해 무시한다.
        if (currentPathSet.Contains(cell.Coord)) return;

        TryAddToCurrentBlock(cell);
    }

    private void TryAddToCurrentBlock(NightCellView cell)
    {
        Vector2Int coord = cell.Coord;

        if (currentPathSet.Contains(coord)) return;

        // 이미 확정된 블럭에 겹치면 현재 스트로크는 즉시 무효다.
        if (filled[coord.y, coord.x])
        {
            currentDrawIsInvalid = true;
            cell.ShowPreview(false, curFlowerCellSprite, curFlowerColor);
            previewCells.Add(coord);
            ShowCurrentPreviewAsInvalid();
            return;
        }

        currentPathSet.Add(coord);
        previewCells.Add(coord);

        // 새로 추가된 칸까지 포함했을 때 어떤 회전/반전 블럭으로도 완성될 수 없으면 무효.
        if (!shapeValidator.CanStillMatch(currentPathSet))
        {
            currentDrawIsInvalid = true;
            ShowCurrentPreviewAsInvalid();
            return;
        }

        cell.ShowPreview(true, curFlowerCellSprite, curFlowerColor);
    }

    private void ShowCurrentPreviewAsInvalid()
    {
        foreach (Vector2Int coord in previewCells)
        {
            if (!filled[coord.y, coord.x])
            {
                cells[coord.y, coord.x].ShowPreview(false, curFlowerCellSprite, curFlowerColor);
            }
        }
    }

    /// <summary>
    /// 클릭/드래그가 닿은 점유 칸이 속한 블럭 하나를 통째로 삭제한다.
    /// 같은 blockId를 가진 모든 셀을 찾아 Empty로 되돌린다.
    /// </summary>
    private void ErasePlacedBlockAt(Vector2Int clickedCoord)
    {
        int blockId = placedBlockIds[clickedCoord.y, clickedCoord.x];

        // 빈 칸이거나 이미 이번 드래그에서 삭제한 블럭이면 무시한다.
        if (blockId < 0 || erasedBlockIds.Contains(blockId)) return;

        erasedBlockIds.Add(blockId);

        int rowCount = placedBlockIds.GetLength(0);
        int colCount = placedBlockIds.GetLength(1);

        for (int row = 0; row < rowCount; row++)
        {
            for (int col = 0; col < colCount; col++)
            {
                if (placedBlockIds[row, col] != blockId) continue;

                cells[row, col].ResetToEmpty();
                filled[row, col] = false;
                placedBlockIds[row, col] = -1;
            }
        }
    }

    private void EndDrawing()
    {
        isDrawing = false;

        // 점유 칸에서 시작한 클릭/드래그는 블럭 삭제만 하고 종료한다.
        // 클릭만 해도 PointerDown에서 해당 블럭 전체가 즉시 지워지고,
        // 손을 떼는 시점에는 상태만 정리한다.
        if (isErasing)
        {
            isErasing = false;
            erasedBlockIds.Clear();
            CheckCompletion();
            return;
        }

        bool isExactBlock = !currentDrawIsInvalid && shapeValidator.IsExactMatch(currentPathSet);

        if (isExactBlock)
        {
            // 이번 드래그로 확정되는 모든 칸에 동일한 배치 ID를 기록한다.
            int placedBlockId = nextPlacedBlockId++;

            foreach (Vector2Int coord in currentPathSet)
            {
                NightCellView cell = cells[coord.y, coord.x];
                cell.Confirm();
                filled[coord.y, coord.x] = true;
                placedBlockIds[coord.y, coord.x] = placedBlockId;
            }

            CheckCompletion();
        }
        else
        {
            // 틀린 모양은 드래그 중 빨갛게 보이고, 손을 떼면 이번 프리뷰만 전부 취소한다.
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
        // TODO: 완성 연출 및 보상 지급 연결
        Debug.Log("Night puzzle complete");
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
