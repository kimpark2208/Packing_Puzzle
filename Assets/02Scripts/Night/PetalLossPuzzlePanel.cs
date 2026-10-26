using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 꽃잎 결손 수리 퍼즐: 빈 꽃잎 채우기. 꽃마다 정해진 8×8 격자 모양(FlowerData.petalGrid) 안에 막힌 칸(가시)이 뭉쳐서 생기고,
/// 노트에 그려진 블록 두 종(큰 것 하나, 작은 것 하나)의 모양대로 한 번에 한 블록씩 칸을 드래그로 칠해 남은 칸을 모두 채운다.
/// 블록은 회전·반전해도 되고 위치는 상관없으며, 두 블록을 모두 한 번 이상 써야 한다. 이미 칠한 블록은 그 칸을 눌러 지울 수 있다(레거시 밤 퍼즐과 같다).
/// 퍼즐은 PetalLossPuzzleGenerator가 만들고, 이 패널은 화병과 꽃을 숨긴다. 칸 64개는 에디터에 미리 놓아 두고 코드가 상태만 바꾼다.
/// </summary>
public class PetalLossPuzzlePanel : RepairPuzzlePanel
{
    private const int Size = FlowerData.PetalGridSize;

    [Header("격자")]
    [SerializeField, Tooltip("격자 칸 64개(위쪽 줄부터, 줄마다 왼쪽에서 오른쪽 순서). 칸마다 배경 Image와 자식 CellFilled(Image)가 있고 NightCellView가 붙어 있다.")] private NightCellView[] cells;
    [SerializeField, Tooltip("빈 칸 그림(점선).")] private Sprite emptySprite;
    [SerializeField, Tooltip("막힌 칸 그림(가시).")] private Sprite wallSprite;
    [SerializeField, Tooltip("칠해진 칸 그림(흰색, 블록 색으로 틴트된다).")] private Sprite filledSprite;
    [SerializeField, Tooltip("드래그하는 동안 칠하는 칸에 보이는 미리보기 색(알파로 투명도). 모양이 틀리면 칸이 붉게 보인다.")] private Color previewColor = new(0.45f, 0.45f, 0.45f, 0.75f);
    [SerializeField, Min(0f), Tooltip("각 칸의 터치 판정을 칸 둘레로 이만큼(화면 단위) 넓힌다. 손가락이 칸 경계에서 조금 벗어나도 그 칸으로 인식된다.")] private float cellHitInflate = 10f;

    [Header("노트(이번 퍼즐의 블록)")]
    [SerializeField, Tooltip("블록 그림 2개. 먼저 큰 블록, 다음에 작은 블록. 노트 안 원하는 자리에 둔다(위치는 그대로, 그림과 크기는 코드가 정한다).")] private Image[] blockPreviews = new Image[2];
    [SerializeField, Min(1f), Tooltip("노트에 그려지는 블록 한 칸의 크기. 격자 칸 크기와 같게 두면 노트 그림과 격자가 같은 크기로 보인다.")] private float previewCellSize = 108f;

    [Header("표시")]
    [SerializeField] private TMP_Text message;
    [SerializeField] private string startMessage = "블록 모양대로 칸을 칠해 빈 꽃잎을 채워요";
    [SerializeField] private string solvedMessage = "빈 곳을 모두 채웠어요!";
    [SerializeField, Min(0f), Tooltip("다 채운 뒤 패널이 닫히기까지 잠시 보여 주는 시간(초).")] private float solvedDelay = 0.8f;

    [Header("퍼즐 만들기")]
    [SerializeField, Tooltip("막힌 칸 수, 뭉치는 정도, 큰/작은 블록의 기준.")] private PetalLossPuzzleGenerator.Settings settings = new();
    [Header("이 씬만 실행했을 때(시작 씬 없이 테스트할 때)")]
    [SerializeField, Tooltip("격자 모양을 가져올 꽃. 비워 두면 모든 칸이 있는 8×8 격자.")] private FlowerData testFlower;
    [SerializeField, Tooltip("블록 목록(DataRegistry)이 없을 때 대신 쓰는 블록들.")] private List<BlockData> testBlocks = new();

    private PetalLossPuzzleGenerator.Puzzle puzzle;
    private NightShapePuzzleValidator validator;
    private GraphicRaycaster raycaster;
    private bool[,] unavailable;   // 칸이 없거나 막힌 칸
    private bool[,] filled;        // 칠해진 칸(그릴 수 없는 칸도 true로 두어 완료 판정에서 빠진다)
    private int[,] placedIds;
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

        FlowerData flower = Case != null ? Case.flower : testFlower;
        bool[,] region = flower != null ? flower.GetPetalGrid() : null;
        if (region == null)
        {
            region = new bool[Size, Size];
            for (int row = 0; row < Size; row++)
                for (int col = 0; col < Size; col++)
                    region[row, col] = true;
        }

        IReadOnlyList<BlockData> blocks = DataRegistry.Instance != null ? DataRegistry.Instance.Blocks : testBlocks;
        puzzle = PetalLossPuzzleGenerator.Generate(region, blocks, settings);
        if (puzzle == null)
        {
            Debug.LogError("[PetalLossPuzzlePanel] 퍼즐을 만들 블록이 없어 이 퍼즐을 건너뜁니다.");
            Solve();
            return;
        }

        unavailable = puzzle.Unavailable();
        validator = new NightShapePuzzleValidator(puzzle.blocks, unavailable);
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

                background.enabled = puzzle.region[row, col] && !puzzle.walls[row, col];
                background.sprite = emptySprite;
                background.raycastPadding = new Vector4(-cellHitInflate, -cellHitInflate, -cellHitInflate, -cellHitInflate);
                if (puzzle.walls[row, col]) cell.SetAsWall(wallSprite, Color.white);
            }
        }

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

        if (!drawing && Input.GetMouseButtonDown(0))
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

            // 막힌 칸을 뺀 칸을 모두 채우고, 주어진 블록을 모두 한 번 이상 써야 완료다
            if (validator.IsPuzzleComplete(filled) && validator.AreAllBlocksUsed(placedBlockOf.Values)) StartCoroutine(SolvedRoutine());
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

    private IEnumerator SolvedRoutine()
    {
        playing = false;
        message.text = solvedMessage;
        yield return GameTime.Wait(solvedDelay);
        Solve();
    }
}
