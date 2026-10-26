using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 줄기 꺾임 수리 퍼즐: 블록을 칠해 줄기 길 잇기. 8×8 꽉 찬 격자의 반대쪽 모서리 칸 두 개에 점 1, 점 2가 있고, 가시 칸은 칠할 수 없다.
/// 노트에 그려진 블록 두 종(큰 것 하나, 작은 것 하나)의 모양대로 한 번에 한 블록씩 칸을 드래그로 칠해, 칠한 칸들이 상하좌우로 이어져 두 점을 잇게 한다(두 점 칸도 칠해져 있어야 한다).
/// 블록은 회전·반전해도 되고 위치는 상관없으며, 같은 블록을 여러 번 써도 되지만 두 블록을 모두 한 번 이상 써야 한다. 이미 칠한 블록은 그 칸을 눌러 지울 수 있다(레거시 밤 퍼즐과 같다).
/// 퍼즐은 StemBendPuzzleGenerator가 만들고, 이 패널은 화병과 꽃을 숨긴다. 칸 64개와 점 두 개는 에디터에 미리 놓아 두고 코드가 상태와 점 위치만 바꾼다.
/// </summary>
public class StemBendPuzzlePanel : RepairPuzzlePanel
{
    private const int Size = StemBendPuzzleGenerator.Size;

    [Header("격자")]
    [SerializeField, Tooltip("격자 칸 64개(위쪽 줄부터, 줄마다 왼쪽에서 오른쪽 순서). 칸마다 배경 Image와 자식 CellFilled(Image)가 있고 NightCellView가 붙어 있다.")] private NightCellView[] cells;
    [SerializeField, Tooltip("빈 칸 그림(점선).")] private Sprite emptySprite;
    [SerializeField, Tooltip("가시 칸 그림.")] private Sprite wallSprite;
    [SerializeField, Tooltip("칠해진 칸 그림(흰색, 블록 색으로 틴트된다).")] private Sprite filledSprite;
    [SerializeField, Tooltip("드래그하는 동안 칠하는 칸에 보이는 미리보기 색(알파로 투명도). 모양이 틀리면 칸이 붉게 보인다.")] private Color previewColor = new(0.45f, 0.45f, 0.45f, 0.75f);
    [SerializeField, Min(0f), Tooltip("각 칸의 터치 판정을 칸 둘레로 이만큼(화면 단위) 넓힌다. 손가락이 칸 경계에서 조금 벗어나도 그 칸으로 인식된다.")] private float cellHitInflate = 10f;
    [SerializeField, Tooltip("점 1, 점 2 그림(숫자가 적힌 원). 퍼즐을 만들 때마다 해당 점 칸 위로 옮겨진다. 칸이 아니라 격자(Board)의 자식이며 LayoutElement로 격자 배치에서 빠져 있다.")] private RectTransform[] dots = new RectTransform[2];

    [Header("노트(이번 퍼즐의 블록)")]
    [SerializeField, Tooltip("블록 그림 2개. 먼저 큰 블록, 다음에 작은 블록. 노트 안 원하는 자리에 둔다(위치는 그대로, 그림과 크기는 코드가 정한다).")] private Image[] blockPreviews = new Image[2];
    [SerializeField, Min(1f), Tooltip("노트에 그려지는 블록 한 칸의 크기. 격자 칸 크기와 같게 두면 노트 그림과 격자가 같은 크기로 보인다.")] private float previewCellSize = 96f;

    [Header("표시")]
    [SerializeField] private TMP_Text message;
    [SerializeField] private string startMessage = "블록을 칠해 점 1과 점 2를 이어요";
    [SerializeField] private string solvedMessage = "줄기가 이어졌어요!";
    [SerializeField, Min(0f), Tooltip("이은 뒤 패널이 닫히기까지 잠시 보여 주는 시간(초).")] private float solvedDelay = 0.8f;

    [Header("퍼즐 만들기")]
    [SerializeField, Tooltip("가시 수와 허용 오차, 큰/작은 블록의 기준.")] private StemBendPuzzleGenerator.Settings settings = new();
    [Header("이 씬만 실행했을 때(시작 씬 없이 테스트할 때)")]
    [SerializeField, Tooltip("블록 목록(DataRegistry)이 없을 때 대신 쓰는 블록들.")] private List<BlockData> testBlocks = new();

    private StemBendPuzzleGenerator.Puzzle puzzle;
    private NightShapePuzzleValidator validator;
    private GraphicRaycaster raycaster;
    private bool[,] unavailable;   // 가시 칸
    private bool[,] filled;        // 칠해진 칸(가시 칸도 true로 두어 칠할 수 없는 칸이 된다)
    private int[,] placedIds;      // 칠한 블록 번호(안 칠했으면 -1)
    private readonly Dictionary<int, int> placedBlockOf = new(); // 칠한 블록 번호 -> 그 블록의 blockID
    private int nextPlacedId;

    private readonly HashSet<Vector2Int> path = new();
    private readonly List<Vector2Int> previewCells = new();
    private readonly HashSet<int> erasedIds = new();
    private Vector2Int? lastCoord;
    private bool playing;
    private bool drawing;
    private bool erasing;
    private bool invalid;

    public override bool HidesFlower => true;

    protected override void Begin()
    {
        StopAllCoroutines();
        raycaster = GetComponentInParent<GraphicRaycaster>();

        IReadOnlyList<BlockData> blocks = DataRegistry.Instance != null ? DataRegistry.Instance.Blocks : testBlocks;
        puzzle = StemBendPuzzleGenerator.Generate(blocks, settings);
        if (puzzle == null)
        {
            Debug.LogError("[StemBendPuzzlePanel] 퍼즐을 만들 블록이 없어 이 퍼즐을 건너뜁니다.");
            Solve();
            return;
        }

        unavailable = puzzle.thorns;
        validator = new NightShapePuzzleValidator(puzzle.blocks);
        filled = new bool[Size, Size];
        placedIds = new int[Size, Size];
        placedBlockOf.Clear();
        nextPlacedId = 0;
        path.Clear();
        previewCells.Clear();
        drawing = erasing = invalid = false;
        lastCoord = null;

        for (int row = 0; row < Size; row++)
        {
            for (int col = 0; col < Size; col++)
            {
                NightCellView cell = cells[row * Size + col];
                var background = cell.GetComponent<Image>();
                cell.Initialize(new Vector2Int(col, row));
                cell.SetValidPreviewColor(previewColor);
                placedIds[row, col] = -1;
                filled[row, col] = unavailable[row, col];

                background.enabled = !puzzle.thorns[row, col];
                background.sprite = emptySprite;
                background.raycastPadding = new Vector4(-cellHitInflate, -cellHitInflate, -cellHitInflate, -cellHitInflate);
                if (puzzle.thorns[row, col]) cell.SetAsWall(wallSprite, Color.white);
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)cells[0].transform.parent); // 패널을 켠 바로 이 프레임에는 칸 위치가 아직 배치되지 않았다
        dots[0].position = cells[puzzle.dotA.y * Size + puzzle.dotA.x].transform.position;
        dots[1].position = cells[puzzle.dotB.y * Size + puzzle.dotB.x].transform.position;
        ShowBlocks();
        message.text = startMessage;
        playing = true;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        playing = false;
    }

    /// <summary>노트에 이번 퍼즐의 블록 모양을 그린다(블록 한 칸이 previewCellSize가 되게).</summary>
    private void ShowBlocks()
    {
        for (int i = 0; i < blockPreviews.Length; i++)
        {
            bool has = i < puzzle.blocks.Count;
            blockPreviews[i].gameObject.SetActive(has);
            if (!has) continue;

            BlockData block = puzzle.blocks[i];
            HashSet<Vector2Int> shape = PolyominoUtil.ReadShape(block);
            blockPreviews[i].sprite = block.dayPieceSprite;
            blockPreviews[i].color = ColorPalette.ToUnityColor(block.color); // 블록 그림은 흰색이라 블록 색으로 틴트한다
            blockPreviews[i].preserveAspect = true;
            blockPreviews[i].rectTransform.sizeDelta = new Vector2(shape.Max(v => v.x) + 1, shape.Max(v => v.y) + 1) * previewCellSize;
        }
    }

    private void Update()
    {
        if (!playing) return;

        if (!drawing && Input.GetMouseButtonDown(0) && !TutorialPopup.IsShowing && !TutorialOverlay.IsShowing) // 튜토리얼 팝업 뒤의 칸이 눌려 그리기가 시작되지 않게
        {
            NightCellView cell = CellAt(Input.mousePosition);
            if (cell != null && !unavailable[cell.Coord.y, cell.Coord.x]) StartDrawing(cell);
        }
        else if (drawing)
        {
            ContinueDrawing();
            if (Input.GetMouseButtonUp(0)) EndDrawing();
        }
    }

    private NightCellView CellAt(Vector2 screenPosition)
    {
        if (raycaster == null || EventSystem.current == null) return null;

        var results = new List<RaycastResult>();
        raycaster.Raycast(new PointerEventData(EventSystem.current) { position = screenPosition }, results);
        foreach (RaycastResult result in results)
        {
            NightCellView cell = result.gameObject.GetComponentInParent<NightCellView>();
            if (cell != null) return cell;
        }
        return null;
    }

    /// <summary>누른 칸이 칠해져 있으면 그 블록을 지우는 드래그, 비어 있으면 칠하는 드래그를 시작한다.</summary>
    private void StartDrawing(NightCellView cell)
    {
        drawing = true;
        invalid = false;
        lastCoord = null;
        path.Clear();
        previewCells.Clear();
        erasedIds.Clear();

        Vector2Int coord = cell.Coord;
        erasing = filled[coord.y, coord.x];
        if (erasing) Erase(coord);
        else AddToPath(cell);
        lastCoord = coord;
    }

    private void ContinueDrawing()
    {
        NightCellView cell = CellAt(Input.mousePosition);
        if (cell == null || unavailable[cell.Coord.y, cell.Coord.x] || cell.Coord == lastCoord) return;

        if (erasing)
        {
            Erase(cell.Coord);
        }
        else if (lastCoord.HasValue)
        {
            // 한 프레임 사이 여러 칸을 건너뛰었으면 그 사이 칸들도 순서대로 칠한다
            foreach (Vector2Int step in LineCells(lastCoord.Value, cell.Coord))
            {
                if (path.Contains(step) || unavailable[step.y, step.x]) continue;
                AddToPath(cells[step.y * Size + step.x]);
            }
        }
        lastCoord = cell.Coord;
    }

    /// <summary>두 칸 사이를 잇는 칸들(시작 칸 제외, 끝 칸 포함)을 순서대로 돌려준다(Bresenham).</summary>
    private static IEnumerable<Vector2Int> LineCells(Vector2Int from, Vector2Int to)
    {
        int x = from.x, y = from.y;
        int dx = Mathf.Abs(to.x - x), sx = x < to.x ? 1 : -1;
        int dy = -Mathf.Abs(to.y - y), sy = y < to.y ? 1 : -1;
        int error = dx + dy;
        while (x != to.x || y != to.y)
        {
            int twice = 2 * error;
            if (twice >= dy) { error += dy; x += sx; }
            if (twice <= dx) { error += dx; y += sy; }
            yield return new Vector2Int(x, y);
        }
    }

    private void AddToPath(NightCellView cell)
    {
        Vector2Int coord = cell.Coord;
        if (path.Contains(coord)) return;

        previewCells.Add(coord);
        if (filled[coord.y, coord.x])
        {
            invalid = true; // 이미 칠한 칸을 지나가면 이 드래그는 실패다
            ShowPreviewsInvalid();
            return;
        }

        path.Add(coord);
        if (!validator.CanStillMatch(path))
        {
            invalid = true; // 어떤 블록 모양으로도 이어질 수 없다
            ShowPreviewsInvalid();
            return;
        }

        cell.ShowPreview(true, filledSprite, Color.white);
    }

    private void ShowPreviewsInvalid()
    {
        foreach (Vector2Int coord in previewCells)
            if (!filled[coord.y, coord.x]) cells[coord.y * Size + coord.x].ShowPreview(false, filledSprite, Color.white);
    }

    /// <summary>coord 칸이 속한 칠한 블록을 통째로 지운다.</summary>
    private void Erase(Vector2Int coord)
    {
        int id = placedIds[coord.y, coord.x];
        if (id < 0 || !erasedIds.Add(id)) return;

        placedBlockOf.Remove(id);
        for (int row = 0; row < Size; row++)
        {
            for (int col = 0; col < Size; col++)
            {
                if (placedIds[row, col] != id) continue;

                cells[row * Size + col].ResetToEmpty();
                filled[row, col] = false;
                placedIds[row, col] = -1;
            }
        }
    }

    /// <summary>손을 뗐다: 칠한 칸이 어떤 블록 모양과 정확히 같으면 그 블록 색으로 확정하고, 아니면 지운다.</summary>
    private void EndDrawing()
    {
        drawing = false;
        lastCoord = null;

        if (erasing)
        {
            erasing = false;
            erasedIds.Clear();
            return;
        }

        BlockData matched = invalid ? null : validator.GetExactMatchBlock(path);
        if (matched != null)
        {
            Color color = ColorPalette.ToUnityColor(matched.color);
            foreach (Vector2Int coord in path)
            {
                cells[coord.y * Size + coord.x].Confirm(filledSprite, color);
                filled[coord.y, coord.x] = true;
                placedIds[coord.y, coord.x] = nextPlacedId;
            }
            placedBlockOf[nextPlacedId++] = matched.blockID;

            // 칠한 칸이 이어져 두 점을 잇고, 주어진 블록을 모두 한 번 이상 써야 완료다
            if (IsConnected() && validator.AreAllBlocksUsed(placedBlockOf.Values)) StartCoroutine(SolvedRoutine());
        }
        else
        {
            foreach (Vector2Int coord in previewCells)
                if (!filled[coord.y, coord.x]) cells[coord.y * Size + coord.x].ResetToEmpty();
        }

        path.Clear();
        previewCells.Clear();
        invalid = false;
    }

    /// <summary>칠한 칸들이 상하좌우로 이어져 점 1 칸에서 점 2 칸까지 닿는가(두 점 칸도 칠해져 있어야 한다).</summary>
    private bool IsConnected()
    {
        Vector2Int start = puzzle.dotA, goal = puzzle.dotB;
        if (placedIds[start.y, start.x] < 0 || placedIds[goal.y, goal.x] < 0) return false;

        var seen = new HashSet<Vector2Int> { start };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (current == goal) return true;

            foreach (Vector2Int step in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
            {
                Vector2Int next = current + step;
                if (next.x < 0 || next.x >= Size || next.y < 0 || next.y >= Size || placedIds[next.y, next.x] < 0) continue;
                if (seen.Add(next)) queue.Enqueue(next);
            }
        }
        return false;
    }

    private IEnumerator SolvedRoutine()
    {
        playing = false;
        message.text = solvedMessage;
        yield return GameTime.Wait(solvedDelay);
        Solve();
    }
}
