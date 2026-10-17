using UnityEngine;

[System.Serializable]
public struct BlockRow
{
    public bool[] cols;
}

/// <summary>
/// 퍼즐용 블록(폴리오미노 모양). 꽃(FlowerData)과는 일대일로 이어지지 않는다: 블록이 필요한 퍼즐은 블록 목록(DataRegistry)에서 골라 조합해 만든다.
/// 밤 퍼즐의 블록 그리기에서 모양, 칸 그림, 미리보기 그림, 색을 여기서 가져온다.
/// </summary>
[CreateAssetMenu(fileName = "BlockData", menuName = "Puzzle/BlockData", order = 2)]
public class BlockData : ScriptableObject
{
    [Header("블록 정보")]
    public int blockID;
    [Tooltip("블록 그림을 틴트하는 색.")] public FlowerData.Color color;

    [Header("블록 모양")]
    public BlockRow[] shapeGrid;

    [Header("회전 기준점")]
    public Vector2Int anchorCoord;

    [Header("밤 퍼즐 - 셀 단위로 표시되는 블록 그림 (색상은 런타임에 ColorPalette로 틴트됨)")]
    public Sprite nightCellSprite;

    [Header("밤 퍼즐 - 블록 미리보기용 통짜 이미지 (모양대로 칸을 흰색으로 채운 한 장, 색상은 런타임에 ColorPalette로 틴트됨). BlockImageGenerator가 자동 생성해 채운다.")]
    public Sprite dayPieceSprite;

    /// <summary>이 블록이 차지하는 칸 수. 모노미노=1 ~ 펜토미노=5.</summary>
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
