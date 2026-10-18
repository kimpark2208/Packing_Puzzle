using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 꽃 한 종류의 정보(이름, 색, 역할, 그림). 퍼즐용 블록 모양은 BlockData가 따로 가지며, 꽃과 블록은 일대일로 이어지지 않는다.
/// </summary>
[CreateAssetMenu(fileName = "FlowerData", menuName = "Puzzle/FlowerData", order = 1)]
public class FlowerData : ScriptableObject
{
    /// <summary>꽃 색: 색상환 6색(빨강/주황/노랑/파랑/보라/분홍) + 흰색.</summary>
    public enum Color
    {
        Red,
        Orange,
        Yellow,
        Blue,
        Purple,
        Pink,
        White
    }

    /// <summary>꽃꽂이 디자인 역할(라인/매스/폼/필러).</summary>
    public enum FlowerRole
    {
        Line,
        Mass,
        Form,
        Filler
    }

    /// <summary>밤 퍼즐에서 귀신 손님의 꽃이 앓는 아픔(상태이상). 아픔마다 수리 퍼즐이 하나씩 있다.</summary>
    public enum Ailment
    {
        PetalLoss,      // 꽃잎 결손 → 실루엣 채우기
        StemBend,       // 줄기 꺾임 → 끊긴 두 점 잇기
        PetalScatter,   // 꽃잎 흩어짐 → 순서대로 붙이기
        Wilt,           // 메마름 → 물 주기
        FadedMemory     // 기억 흐려짐 → 기억 되짚기
    }

    [Header("꽃 정보")]
    [FormerlySerializedAs("blockID")] public int flowerID;
    public string flowerName;

    /// <summary>화면에 보여 줄 이름. 이름이 비어 있으면 "꽃 {번호}".</summary>
    public string DisplayName => string.IsNullOrEmpty(flowerName) ? $"꽃 {flowerID}" : flowerName;
    public Color color;
    public FlowerRole flowerRole;

    [Tooltip("이 꽃을 얻는 일차(꽃 시트의 '획득 일차'). 0이면 처음부터 가진다. 지금은 시트와 맞추려고 담아 둔 값이다.")] public int acquireDay;

    [Header("특수주문: 손님이 꽃 이름 대신 말장난으로 이 꽃을 주문한다(꽃 시트의 특수주문 / 1차·2차 되묻기). 비워 두면 이 꽃은 특수주문이 없다")]
    [TextArea] public string specialOrder;
    [TextArea] public string specialReask1;
    [TextArea] public string specialReask2;

    /// <summary>이 꽃의 특수주문(말장난 주문)이 있는가.</summary>
    public bool HasSpecialOrder => !string.IsNullOrWhiteSpace(specialOrder) && specialOrder.Trim() != "-";

    [Header("일반 아이콘 (요청 UI, 드래그 조각 등. 색상은 런타임에 ColorPalette로 틴트됨)")]
    public Sprite iconSprite;

    [Header("꽃 선택 선반 - 화병에 꽂히는 줄기까지 있는 꽃 (색상은 런타임에 ColorPalette로 틴트됨)")]
    public Sprite stemSprite;

    [Header("낮 퍼즐 - 칸이 채워졌을 때 칸에 들어가는 꽃 이미지 (꽃마다 따로 만들 예정, 지금은 같은 역할이면 Temp_역할 이미지를 임시로 사용)")]
    public Sprite dayFillSprite;

    /// <summary>밤 퍼즐의 진단용 꽃 그림은 꽃잎 6장으로 나뉜다.</summary>
    public const int PetalPartCount = 6;

    /// <summary>진단 그림 부위의 배치 종류.</summary>
    public enum PartLayout
    {
        Round,  // 둥근형: 중심과 꽃잎 6장이 500x500 꽃송이 상자 안에 둘러 있다
        Tall    // 세로형: 중심과 꽃잎 6장이 줄기와 같은 700x1200 캔버스에 겹쳐 제자리에 놓인다(금어초처럼 줄기를 따라 꽃송이가 달린 꽃)
    }

    [Header("밤 퍼즐 - 진단용 꽃 그림(부위별). 같은 캔버스 크기·같은 위치로 뽑은 투명 PNG를 겹치면 온전한 꽃이 되게 만든다. 아픔(결손/흩어짐/꺾임 …)은 이 부위들을 숨기고 옮기고 꺾어서 보여 준다. 색상은 런타임에 ColorPalette로 틴트됨")]
    [Tooltip("줄기와 이파리(한 장).")] public Sprite stemLeafPartSprite;
    [Tooltip("꽃 중심. 비워 두면 중심을 그리지 않는다(꽃잎 그림에 중심이 들어 있는 꽃).")] public Sprite centerPartSprite;
    [Tooltip("꽃잎 6장. 맨 위 꽃잎이 0번이고 시계 방향으로 놓는다.")] public Sprite[] petalPartSprites = new Sprite[PetalPartCount];

    [Tooltip("부위 그림의 배치. 둥근형 = 중심·꽃잎이 500x500 꽃송이 상자 안에 둘러 있다. 세로형 = 중심·꽃잎이 줄기와 같은 700x1200 캔버스에 겹쳐 있다(위에서 아래로 번호를 매긴다).")] public PartLayout partLayout;

    /// <summary>밤 진단에 쓸 부위 그림(줄기, 꽃잎 6장)이 채워져 있는가. 중심 그림은 없어도 된다(비어 있으면 중심을 그리지 않는다).</summary>
    public bool HasDiagnosisArt => stemLeafPartSprite != null && petalPartSprites != null && petalPartSprites.Length == PetalPartCount && System.Array.TrueForAll(petalPartSprites, s => s != null);

    [Tooltip("꽃잎 결손일 때 바뀔 수 있는 꽃잎들. 칸마다 (위 꽃잎 6장 중 몇 번째인지, 그 자리의 결손 그림)을 적는다. 결손이 나면 이 중 하나가 위 꽃잎 그림 대신 쓰인다. 꽃마다 정해져 있다.")] public LossPetal[] lossPetals;

    /// <summary>꽃잎 결손 하나: 어느 꽃잎이 결손되는지와 그 결손된 모습.</summary>
    [System.Serializable]
    public struct LossPetal
    {
        [Tooltip("결손되는 꽃잎 번호(위 꽃잎 6장 중, 맨 위가 0번이고 시계 방향). 0~5.")] [Range(0, PetalPartCount - 1)] public int petalIndex;
        [Tooltip("결손된 모습의 그 꽃잎 그림.")] public Sprite sprite;
    }

    /// <summary>꽃잎 결손 퍼즐 격자의 한 변 칸 수(모든 꽃이 같다).</summary>
    public const int PetalGridSize = 8;

    [Header("밤 퍼즐 - 꽃잎 결손 퍼즐의 격자 모양(꽃마다 다르다)")]
    [Tooltip("8줄, 줄마다 8글자. 칸이 있는 자리는 #, 칸이 없는 자리는 . 로 적는다. 이 모양 안에서 막힌 칸이 생기고, 나머지 칸을 블록으로 채운다.")] [TextArea(PetalGridSize, PetalGridSize)] public string petalGrid;

    /// <summary>petalGrid를 읽어 [줄, 칸] 배열로 돌려준다(칸이 있으면 true). 적혀 있지 않거나 모양이 틀리면 null.</summary>
    public bool[,] GetPetalGrid()
    {
        if (string.IsNullOrWhiteSpace(petalGrid)) return null;

        string[] lines = petalGrid.Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length != PetalGridSize) return null;

        var grid = new bool[PetalGridSize, PetalGridSize];
        for (int row = 0; row < PetalGridSize; row++)
        {
            string line = lines[row].Trim();
            if (line.Length != PetalGridSize) return null;

            for (int col = 0; col < PetalGridSize; col++) grid[row, col] = line[col] == '#';
        }
        return grid;
    }

    private void OnValidate()
    {
        if (petalPartSprites == null || petalPartSprites.Length != PetalPartCount) System.Array.Resize(ref petalPartSprites, PetalPartCount);
    }
}
