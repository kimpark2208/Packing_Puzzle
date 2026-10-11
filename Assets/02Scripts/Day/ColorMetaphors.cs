using UnityEngine;

/// <summary>색 주문에서 색 이름 대신 손님이 쓰는 비유 문구. 색마다 둘이고, 뒤에 "꽃"을 붙여 말한다("동백 같은 꽃", "귤빛 꽃").</summary>
public static class ColorMetaphors
{
    // FlowerData.Color 순서: Red, Orange, Yellow, Blue, Purple, Pink, White
    private static readonly string[][] Phrases =
    {
        new[] { "동백 같은", "딸기 같은" },
        new[] { "노을 같은", "귤빛" },
        new[] { "병아리 같은", "햇살 같은" },
        new[] { "바다 같은", "하늘빛" },
        new[] { "라벤더 같은", "포도빛" },
        new[] { "벚꽃 같은", "볼터치 같은" },
        new[] { "눈 같은", "구름 같은" },
    };

    public static string Pick(FlowerData.Color color)
    {
        string[] options = Phrases[(int)color];
        return options[Random.Range(0, options.Length)];
    }
}
