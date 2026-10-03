using System.Collections.Generic;

/// <summary>
/// 꽃 색상 간 조합(궁합) 점수표. 실제 색 이론(보색/유사색/클래식 조합)을 참고해 만든
/// 예시 수치이며, 인접한 슬롯에 이 조합으로 꽃을 놓으면 추가 점수를 준다(패널티 없음).
/// </summary>
public static class ColorCompatDatabase
{
    private static readonly Dictionary<(FlowerData.Color, FlowerData.Color), int> Table = new();

    private static readonly List<((FlowerData.Color, FlowerData.Color) pair, int bonus)> Entries = new()
    {
        // 동색(모노톤) 조합
        ((FlowerData.Color.Red, FlowerData.Color.Red), 10),
        ((FlowerData.Color.Blue, FlowerData.Color.Blue), 10),
        ((FlowerData.Color.Yellow, FlowerData.Color.Yellow), 10),
        ((FlowerData.Color.Black, FlowerData.Color.Black), 8),
        ((FlowerData.Color.Green, FlowerData.Color.Green), 10),
        ((FlowerData.Color.white, FlowerData.Color.white), 8),
        ((FlowerData.Color.Purple, FlowerData.Color.Purple), 10),

        // 보색 조합 (RYB 색상환 기준)
        ((FlowerData.Color.Red, FlowerData.Color.Green), 25),
        ((FlowerData.Color.Yellow, FlowerData.Color.Purple), 25),

        // 유사색 조합
        ((FlowerData.Color.Red, FlowerData.Color.Yellow), 15),
        ((FlowerData.Color.Blue, FlowerData.Color.Green), 15),
        ((FlowerData.Color.Blue, FlowerData.Color.Purple), 18),

        // 클래식 조합
        ((FlowerData.Color.Red, FlowerData.Color.white), 18),
        ((FlowerData.Color.Black, FlowerData.Color.white), 20),
        ((FlowerData.Color.Yellow, FlowerData.Color.white), 15),
        ((FlowerData.Color.Blue, FlowerData.Color.white), 15),
        ((FlowerData.Color.Green, FlowerData.Color.white), 15),
        ((FlowerData.Color.Purple, FlowerData.Color.white), 15),
        ((FlowerData.Color.Red, FlowerData.Color.Black), 12),
        ((FlowerData.Color.Black, FlowerData.Color.Purple), 12),
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
    public static int GetBonus(FlowerData.Color a, FlowerData.Color b)
    {
        return Table.TryGetValue((a, b), out int bonus) ? bonus : 0;
    }
}
