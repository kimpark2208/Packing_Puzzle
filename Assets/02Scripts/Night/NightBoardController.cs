using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 밤 퍼즐 보드. 드래그로 셀을 그리는 입력, 되돌아가기 취소, 확정 처리를 담당한다.
/// 목표 도형 판정은 INightPuzzleValidator에 위임되어 스테이지 타입별로 교체 가능하다.
/// </summary>
public class NightBoardController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridLayoutGroup gridLayout;
    [SerializeField] private NightCellView cellPrefab;

    [Header("Current Flower")]
    [SerializeField] private Sprite currentFlowerSprite;
    [SerializeField] private Color currentFlowerColor = Color.white;

    private int gridSize;
    private NightCellView[,] cells;
    private bool[,] filled;

    // 드래그 중 지나간 경로. 순서를 유지해야 "되돌아가기" 판정이 가능하다.
    private readonly List<Vector2Int> currentPath = new();
    private readonly HashSet<Vector2Int> currentPathSet = new();

    private bool isDrawing;

    public INightPuzzleValidator Validator { get; set; }

    public void CreateBoard(int size)
    {
        ClearBoardObjects();

        gridSize = size;
        cells = new NightCellView[size, size];
        filled = new bool[size, size];

        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = size;

        for (int row = 0; row < size; row++)
        {
            for (int col = 0; col < size; col++)
            {
                NightCellView cell = Instantiate(cellPrefab, gridLayout.transform);
                Vector2Int coord = new Vector2Int(col, row);

                cell.Initialize(coord);

                if (Validator != null && Validator.IsWallCell(coord))
                {
                    cell.SetAsWall(currentFlowerSprite); // TODO: 벽 전용 스프라이트로 교체
                }

                AttachInputHandlers(cell);

                cells[row, col] = cell;
            }
        }
    }

    private void AttachInputHandlers(NightCellView cell)
    {
        EventTrigger trigger = cell.gameObject.AddComponent<EventTrigger>();

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
        // 드래그 중 손을 뗀 순간을 감지 (셀 밖에서 뗄 수도 있으므로 보드 레벨에서 체크)
        if (isDrawing && Input.GetMouseButtonUp(0))
        {
            EndDrawing();
        }
    }

    private void OnCellPointerDown(NightCellView cell)
    {
        if (cell.IsWall) return;

        isDrawing = true;
        currentPath.Clear();
        currentPathSet.Clear();

        TryAddToPath(cell);
    }

    private void OnCellPointerEnter(NightCellView cell)
    {
        if (!isDrawing) return;
        if (cell.IsWall) return;

        // 되돌아가기: 이미 지나온 경로 중간의 칸으로 돌아오면 그 이후 경로를 취소
        int existingIndex = currentPath.IndexOf(cell.Coord);

        if (existingIndex >= 0)
        {
            TrimPathAfter(existingIndex);
            return;
        }

        TryAddToPath(cell);
    }

    private void TryAddToPath(NightCellView cell)
    {
        if (currentPathSet.Contains(cell.Coord)) return;

        currentPath.Add(cell.Coord);
        currentPathSet.Add(cell.Coord);

        bool isValid = Validator == null || Validator.IsCellInTarget(cell.Coord);
        cell.ShowPreview(isValid, currentFlowerSprite, currentFlowerColor);
    }

    /// <summary>
    /// keepIndex 이후에 그려졌던 프리뷰 칸들을 전부 취소한다.
    /// 예: 경로가 [A, B, C, D]인데 B로 되돌아가면 C, D를 취소하고 경로를 [A, B]로 되돌린다.
    /// </summary>
    private void TrimPathAfter(int keepIndex)
    {
        for (int i = currentPath.Count - 1; i > keepIndex; i--)
        {
            Vector2Int coord = currentPath[i];
            cells[coord.y, coord.x].CancelPreview();

            currentPath.RemoveAt(i);
            currentPathSet.Remove(coord);
        }
    }

    private void EndDrawing()
    {
        isDrawing = false;

        foreach (Vector2Int coord in currentPath)
        {
            NightCellView cell = cells[coord.y, coord.x];
            bool isValid = Validator == null || Validator.IsCellInTarget(coord);

            if (isValid)
            {
                cell.Confirm();
                filled[coord.y, coord.x] = true;
            }
            else
            {
                cell.ResetToEmpty();
            }
        }

        currentPath.Clear();
        currentPathSet.Clear();

        CheckCompletion();
    }

    private void CheckCompletion()
    {
        if (Validator == null) return;

        if (Validator.IsPuzzleComplete(filled))
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
