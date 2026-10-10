using System.Collections.Generic;

/// <summary>
/// 꽃 색상 간 조합(궁합) 점수표. 실제 색 이론(보색/유사색/클래식 조합)을 참고해 만든
/// 예시 수치이며, 인접한 슬롯에 이 조합으로 꽃을 놓으면 추가 점수를 준다(패널티 없음).
/// 점수는 결과창 게이지(-100~+100) 기준이고, 보색 &gt; 유사색 &gt; 흰색 순서(5 : 3 : 2)를 유지한다.
/// </summary>
public static class ColorCompatDatabase
{
    private static readonly Dictionary<(FlowerData.Color, FlowerData.Color), int> Table = new();

    private static readonly List<((FlowerData.Color, FlowerData.Color) pair, int bonus)> Entries = new()
    {
        // 보색 조합 (색상환에서 마주 보는 색: 빨강-파랑, 주황-보라, 노랑-분홍)
        ((FlowerData.Color.Red, FlowerData.Color.Blue), 5),
        ((FlowerData.Color.Orange, FlowerData.Color.Purple), 5),
        ((FlowerData.Color.Yellow, FlowerData.Color.Pink), 5),

        // 유사색 조합 (색상환에서 이웃한 색)
        ((FlowerData.Color.Red, FlowerData.Color.Orange), 3),
        ((FlowerData.Color.Orange, FlowerData.Color.Yellow), 3),
        ((FlowerData.Color.Yellow, FlowerData.Color.Blue), 3),
        ((FlowerData.Color.Blue, FlowerData.Color.Purple), 3),
        ((FlowerData.Color.Purple, FlowerData.Color.Pink), 3),
        ((FlowerData.Color.Pink, FlowerData.Color.Red), 3),

        // 흰색은 어느 색과도 잘 어울린다
        ((FlowerData.Color.Red, FlowerData.Color.White), 2),
        ((FlowerData.Color.Orange, FlowerData.Color.White), 2),
        ((FlowerData.Color.Yellow, FlowerData.Color.White), 2),
        ((FlowerData.Color.Blue, FlowerData.Color.White), 2),
        ((FlowerData.Color.Purple, FlowerData.Color.White), 2),
        ((FlowerData.Color.Pink, FlowerData.Color.White), 2),
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
