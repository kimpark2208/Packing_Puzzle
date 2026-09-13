using System.Collections;
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
    [SerializeField] private NightCellView cellPrefab;
    [SerializeField] private Sprite curFlowerCellSprite;
    [SerializeField] private Color curFlowerColor = Color.white;

    [Header("Test Only")]
    [SerializeField] private int gridSize = 5; //TODO: 테스트용. 실제 게임에서는 스테이지 데이터에서 받아와야 함.
    [Tooltip("테스트용 목표 도형 좌표 목록. 실제 게임에서는 스테이지 데이터에서 받아와야 함.")]
    [SerializeField]
    private List<Vector2Int> testTargetShape = new()
    {
        new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0),
        new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(3, 1), new Vector2Int(4, 1),
        new Vector2Int(1, 2), new Vector2Int(2, 2), new Vector2Int(3, 2),
        new Vector2Int(2, 3),
        new Vector2Int(2, 4),
    };

    private bool[,] filled;
    private NightCellView[,] cells;
    private GridLayoutGroup gridLayout;
    private GraphicRaycaster graphicRaycaster; // Raycast로 셀을 찾아 드래그 시작 가능하게 함

    // 드래그 중 지나간 경로. 순서를 유지해야 "되돌아가기" 판정이 가능하다.
    private readonly List<Vector2Int> currentPath = new();
    private readonly HashSet<Vector2Int> currentPathSet = new();

    // 이번 드래그가 "지우기 모드"인지 여부. 시작 칸이 이미 확정된 상태였을 때만 true가 될 수 있다.
    // 실제로 지우기가 확정되는 건 EndDrawing에서 경로 길이가 1(움직임 없음)일 때뿐이다.
    private bool startedOnConfirmedCell;

    private bool isDrawing;

    public INightPuzzleValidator Validator { get; set; }

    private void Start()
    {
        if (!TryGetComponent(out gridLayout))
        {
            Debug.LogWarning("GridLayoutGroup 컴포넌트가 없어서 NightBoardController가 정상 동작하지 않을 수 있습니다.");
            return;
        }

        // GraphicRaycaster 캐시 (Canvas 레벨에 있어야 함)
        graphicRaycaster = GetComponentInParent<GraphicRaycaster>();
        if (graphicRaycaster == null)
        {
            Debug.LogWarning("GraphicRaycaster가 부모 Canvas에 없습니다. UI 이벤트 레이캐스트가 동작하지 않을 수 있습니다.");
        }

        // TODO: 실제 게임에서는 스테이지 데이터(일반/잉여 스테이지의 정답 도형)를 받아 Validator를 주입해야 한다.
        // 지금은 테스트용으로 일반 스테이지 방식(목표 도형을 전부 채우면 완료)의 Validator를 직접 생성한다.
        if (Validator == null)
        {
            Validator = new NightShapePuzzleValidator(testTargetShape);
        }

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
                    cell.SetAsWall(curFlowerCellSprite); // TODO: 벽 전용 스프라이트로 교체
                }

                AttachInputHandlers(cell);

                cells[row, col] = cell;
            }
        }

        // GridLayoutGroup이 실제로 자식들을 배치할 시간을 준 뒤에 끈다.
        // 생성 직후 같은 프레임에 바로 끄면 레이아웃 재계산이 한 번도 일어나지 않아
        // 모든 셀이 기본 위치(좌측 상단)에 겹쳐서 남는 문제가 생긴다.
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
        // 핵심: 이벤트 수신 대상은 Raycast가 실제 히트하는 Graphic(보통 Image) 게임오브젝트입니다.
        // cell 프리팹의 루트에 Graphic이 없고 자식 Image(CellFilled)에만 Graphic이 있다면
        // 이벤트는 자식에서 발생하므로 루트에 EventTrigger를 붙여도 동작하지 않습니다.
        // 그래서 가능한 경우 "히트되는 Graphic 게임오브젝트"에 EventTrigger를 붙입니다.

        // 1) 부모(root)에 Graphic이 있으면 그걸 우선 사용
        Graphic targetGraphic = cell.GetComponent<Graphic>();

        // 2) 없으면 이름이 CellFilled인 자식 우선 검색
        if (targetGraphic == null)
        {
            Transform filledTransform = cell.transform.Find("CellFilled");
            if (filledTransform != null) targetGraphic = filledTransform.GetComponent<Graphic>();
        }

        // 3) 그래도 없으면 아무 자식 Graphic이라도 사용
        if (targetGraphic == null)
        {
            targetGraphic = cell.GetComponentInChildren<Graphic>(true);
        }

        if (targetGraphic == null)
        {
            // 이벤트 대상이 전혀 없으면 경고하고 루트에 트리거를 붙여 시도
            Debug.LogWarning($"[{cell.name}] 이벤트 수신 가능한 Graphic(Image 등)을 찾지 못했습니다. 부모(또는 자식)에 Image를 추가하거나 자식의 Raycast Target을 켜세요.");
            AddEventTriggerToGameObject(cell.gameObject, cell);
            return;
        }

        // Graphic이 발견된 게임오브젝트가 이벤트 수신 대상
        GameObject targetGO = targetGraphic.gameObject;

        // RaycastTarget 강제 보장 (대체로 Inspector에서 켜져 있어야 함)
        targetGraphic.raycastTarget = true;

        AddEventTriggerToGameObject(targetGO, cell);
    }

    private void AddEventTriggerToGameObject(GameObject go, NightCellView cell)
    {
        // 기존 트리거가 있으면 제거하여 중복을 방지
        EventTrigger existing = go.GetComponent<EventTrigger>();
        if (existing != null)
        {
            existing.triggers.Clear();
        }
        else
        {
            existing = go.AddComponent<EventTrigger>();
        }

        AddTrigger(existing, EventTriggerType.PointerDown, _ => OnCellPointerDown(cell));
        AddTrigger(existing, EventTriggerType.PointerEnter, _ => OnCellPointerEnter(cell));
    }

    private void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action<BaseEventData> action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(data => action(data));
        trigger.triggers.Add(entry);
    }

    private void Update()
    {
        // 보드 밖에서 마우스 다운 후 내부로 드래그 시작한 경우를 처리:
        // (graphicRaycaster와 EventSystem이 있어야 동작)
        if (!isDrawing && Input.GetMouseButtonDown(0) && graphicRaycaster != null && EventSystem.current != null)
        {
            TryStartDrawingFromPointer();
        }

        // 드래그 중 손을 뗀 순간을 감지 (셀 밖에서 뗄 수도 있으므로 보드 레벨에서 체크)
        if (isDrawing && Input.GetMouseButtonUp(0))
        {
            EndDrawing();
        }
    }

    private void TryStartDrawingFromPointer()
    {
        PointerEventData ped = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        graphicRaycaster.Raycast(ped, results);

        foreach (var res in results)
        {
            var cell = res.gameObject.GetComponentInParent<NightCellView>();
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
        currentPath.Clear();
        currentPathSet.Clear();

        // 이 드래그가 "이미 확정된 칸"에서 시작했는지 기록해둔다.
        // 실제로 지우기로 처리할지는 EndDrawing에서 경로 길이를 보고 결정한다.
        startedOnConfirmedCell = cell.State == NightCellView.CellVisualState.Confirmed;

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

        // 시작 칸이 이미 확정 상태였고 아직 경로가 그 칸 하나뿐이라면,
        // "지우기 후보" 상태이므로 굳이 미리보기(빨강/기본색)를 새로 씌우지 않는다.
        // 두 번째 칸으로 이동하는 순간 지우기 후보는 취소되고 일반 그리기로 전환된다.
        if (startedOnConfirmedCell && currentPath.Count == 1)
        {
            return;
        }

        // 두 번째 칸부터는 더 이상 지우기 후보가 아니다 (드래그로 전환됨).
        startedOnConfirmedCell = false;

        bool isValid = Validator == null || Validator.IsCellInTarget(cell.Coord);
        cell.ShowPreview(isValid, curFlowerCellSprite, curFlowerColor);
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

        // 지우기 케이스: 이미 확정된 칸을 눌렀다가 움직이지 않고 그대로 뗀 경우.
        if (startedOnConfirmedCell && currentPath.Count == 1)
        {
            Vector2Int coord = currentPath[0];
            cells[coord.y, coord.x].ResetToEmpty();
            filled[coord.y, coord.x] = false;

            currentPath.Clear();
            currentPathSet.Clear();
            startedOnConfirmedCell = false;

            CheckCompletion();
            return;
        }

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
                filled[coord.y, coord.x] = false;
            }
        }

        currentPath.Clear();
        currentPathSet.Clear();
        startedOnConfirmedCell = false;

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
