using UnityEngine;

[System.Serializable]
public struct BlockRow
{
    public bool[] cols;
}

[CreateAssetMenu(fileName = "BlockData", menuName = "Puzzle/BlockData", order = 1)]
public class BlockData : ScriptableObject
{
    public enum Color
    {
        Red,
        Blue,
        Yellow,
        Black,
        Green,
        white,
        Purple
    }

    /// <summary>낮 퍼즐(포장) 태그. 부케 디자인에서의 역할을 나타낸다.</summary>
    public enum FlowerTag
    {
        Line,   // 라인: 골격, 긴 막대 형태
        Mass,   // 매스: 부피감, 덩어리 형태
        Form,   // 품: 포인트, 독특한 형태
        Filler  // 필러: 빈 공간, 소형 형태
    }

    [Header("블록 정보")]
    public int blockID;
    public Color color;

    [Header("낮 퍼즐 - 부케 디자인 태그 (라인/매스/품/필러)")]
    public FlowerTag flowerTag;

    [Header("이 꽃을 사용하려면 필요한 최소 포장지 단계 (1부터 시작, 누적 해금)")]
    public int unlockWrapperTier = 1;

    [Header("블록 모양")]
    public BlockRow[] shapeGrid;

    [Header("회전 기준점")]
    public Vector2Int anchorCoord;

    [Header("시각 (완성 후 표시되는 꽃 아이콘, 색상은 런타임에 ColorPalette로 틴트됨. 밤 퍼즐 셀 단위 표시에 사용)")]
    public Sprite flowerIcon;

    [Header("낮 퍼즐 - 배치 중(진행중) 블록 통짜 이미지. BlockImageGenerator가 자동 생성해 채운다.")]
    public Sprite blockImage;

    [Header("낮 퍼즐 - 완성 후 표시할 블록 크기와 동일한 꽃다발 통짜 이미지 (추후 직접 제작해 채워넣을 슬롯, 비어있으면 임시로 자동 생성된 콜라주 사용)")]
    public Sprite flowerWholeImage;

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
