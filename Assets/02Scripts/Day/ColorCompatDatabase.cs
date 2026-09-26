using System.Collections.Generic;

/// <summary>
/// 꽃 색상 간 조합(궁합) 점수표. 실제 색 이론(보색/유사색/클래식 조합)을 참고해 만든
/// 예시 수치이며, 인접한 슬롯에 이 조합으로 꽃을 놓으면 추가 점수를 준다(패널티 없음).
/// </summary>
public static class ColorCompatDatabase
{
    private static readonly Dictionary<(BlockData.Color, BlockData.Color), int> Table = new();

    private static readonly List<((BlockData.Color, BlockData.Color) pair, int bonus)> Entries = new()
    {
        // 동색(모노톤) 조합
        ((BlockData.Color.Red, BlockData.Color.Red), 10),
        ((BlockData.Color.Blue, BlockData.Color.Blue), 10),
        ((BlockData.Color.Yellow, BlockData.Color.Yellow), 10),
        ((BlockData.Color.Black, BlockData.Color.Black), 8),
        ((BlockData.Color.Green, BlockData.Color.Green), 10),
        ((BlockData.Color.white, BlockData.Color.white), 8),
        ((BlockData.Color.Purple, BlockData.Color.Purple), 10),

        // 보색 조합 (RYB 색상환 기준)
        ((BlockData.Color.Red, BlockData.Color.Green), 25),
        ((BlockData.Color.Yellow, BlockData.Color.Purple), 25),

        // 유사색 조합
        ((BlockData.Color.Red, BlockData.Color.Yellow), 15),
        ((BlockData.Color.Blue, BlockData.Color.Green), 15),
        ((BlockData.Color.Blue, BlockData.Color.Purple), 18),

        // 클래식 조합
        ((BlockData.Color.Red, BlockData.Color.white), 18),
        ((BlockData.Color.Black, BlockData.Color.white), 20),
        ((BlockData.Color.Yellow, BlockData.Color.white), 15),
        ((BlockData.Color.Blue, BlockData.Color.white), 15),
        ((BlockData.Color.Green, BlockData.Color.white), 15),
        ((BlockData.Color.Purple, BlockData.Color.white), 15),
        ((BlockData.Color.Red, BlockData.Color.Black), 12),
        ((BlockData.Color.Black, BlockData.Color.Purple), 12),
    };

    static ColorCompatDatabase()
    {
        foreach (var entry in Entries)
        {
            Table[entry.pair] = entry.bonus;
            Table[(entry.pair.Item2, entry.pair.Item1)] = entry.bonus;
        }
    }

    /// <summary>두 색상의 조합 보너스 점수. 표에 없으면 0(패널티 없음).</summary>
    public static int GetBonus(BlockData.Color a, BlockData.Color b)
    {
        return Table.TryGetValue((a, b), out int bonus) ? bonus : 0;
    }
}
