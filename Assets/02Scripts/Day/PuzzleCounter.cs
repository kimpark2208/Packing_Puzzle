using System.Collections.Generic;

/// <summary>보드에 놓인 꽃의 색별/꽃별 개수.</summary>
public struct PuzzleCounts
{
    public Dictionary<int, int> colorCounts;   // 색상ID -> 사용 개수
    public Dictionary<int, int> flowerCounts;  // 꽃(blockID) -> 사용 개수
}

/// <summary>낮 퍼즐 보드에 놓인 꽃의 색/꽃별 개수를 센다(금액 계산은 Money 폴더의 규칙들이 한다).</summary>
public static class PuzzleCounter
{
    public static PuzzleCounts CollectCounts()
    {
        var result = new PuzzleCounts
        {
            colorCounts = new Dictionary<int, int>(),
            flowerCounts = new Dictionary<int, int>()
        };

        var board = WrapperBoardController.Instance;
        if (board == null) return result;

        foreach (var slot in board.AllSlots)
        {
            foreach (var flower in slot.PlacedFlowers)
            {
                int colorId = (int)flower.color;
                result.colorCounts.TryGetValue(colorId, out int c);
                result.colorCounts[colorId] = c + 1;

                int flowerId = flower.blockID;
                result.flowerCounts.TryGetValue(flowerId, out int f);
                result.flowerCounts[flowerId] = f + 1;
            }
        }

        return result;
    }
}
