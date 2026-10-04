using UnityEngine;

[System.Serializable]
public struct BlockRow
{
    public bool[] cols;
}

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

    [Header("블록 정보")]
    public int blockID;
    public string flowerName;
    public Color color;
    public FlowerRole flowerRole;

    [Header("블록 모양")]
    public BlockRow[] shapeGrid;

    [Header("회전 기준점")]
    public Vector2Int anchorCoord;

    [Header("일반 아이콘 (요청 UI, 드래그 조각 등. 색상은 런타임에 ColorPalette로 틴트됨)")]
    public Sprite iconSprite;

    [Header("꽃 선택 선반 - 화병에 꽂히는 줄기까지 있는 꽃 (색상은 런타임에 ColorPalette로 틴트됨)")]
    public Sprite stemSprite;

    [Header("밤 퍼즐 - 셀 단위로 표시되는 꽃 아이콘 (색상은 런타임에 ColorPalette로 틴트됨)")]
    [UnityEngine.Serialization.FormerlySerializedAs("flowerIcon")]
    public Sprite nightCellSprite;

    [Header("밤 퍼즐 - 블록 미리보기용 통짜 이미지 (모양대로 칸을 색으로 채운 한 장). BlockImageGenerator가 자동 생성해 채운다.")]
    [UnityEngine.Serialization.FormerlySerializedAs("blockImage")]
    public Sprite dayPieceSprite;

    [Header("낮 퍼즐 - 칸이 채워졌을 때 칸에 들어가는 꽃 이미지 (꽃마다 따로 만들 예정, 지금은 같은 역할이면 Temp_역할 이미지를 임시로 사용)")]
    [UnityEngine.Serialization.FormerlySerializedAs("flowerWholeImage")]
    [UnityEngine.Serialization.FormerlySerializedAs("dayBouquetSprite")]
    public Sprite dayFillSprite;

    /// <summary>이 블록이 차지하는 칸 수 (= 점수/크기). 모노미노=1 ~ 펜토미노=5.</summary>
    public int CellCount
    {
        get
        {
            int count = 0;
            if (shapeGrid == null) return 0;
            foreach (var row in shapeGrid)
            {
                if (row.cols == null) continue;
                foreach (var cell in row.cols)
                    if (cell) count++;
            }
            return count;
        }
    }
}
