/// <summary>절차적으로 생성된 밤 퍼즐 데이터</summary>
public struct NightPuzzleData
{
    public int puzzleId;                // 퍼즐 고유 ID
    public int gridSize;                // 그리드 크기 (5x5, 8x8 등)
    public int[] requiredBlockIds;      // 이 스테이지에서 그려야 하는 블록 ID 목록(블록 목록에서 고른 것, 꽃과 무관)
    public int[] requiredFlowerIds;     // 이 퍼즐을 풀면 보상으로 받는 꽃 ID 목록
    public bool isBonusStage;           // 요청 스테이지가 아닌 잉여시간 스테이지인지
    public bool[,] wallGrid;            // true = 벽(그릴 수 없는 칸, 임의 필러가 채운 자리)
}
