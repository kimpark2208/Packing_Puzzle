using UnityEngine;

/// <summary>
/// BlockData.Color enum을 실제 렌더링 색상 및 한글 이름으로 변환하는 공용 팔레트.
/// 색상 관련 매핑이 여러 스크립트(BlockView, CustomerRequirementGenerator 등)에 흩어지는 것을 방지한다.
/// </summary>
public static class ColorPalette
{
    public static Color ToUnityColor(BlockData.Color color)
    {
        switch (color)
        {
            case BlockData.Color.Red: return new Color(0.90f, 0.25f, 0.25f);
            case BlockData.Color.Blue: return new Color(0.25f, 0.45f, 0.95f);
            case BlockData.Color.Yellow: return new Color(0.98f, 0.82f, 0.20f);
            case BlockData.Color.Black: return new Color(0.20f, 0.20f, 0.22f);
            case BlockData.Color.Green: return new Color(0.30f, 0.75f, 0.35f);
            case BlockData.Color.white: return new Color(0.95f, 0.95f, 0.95f);
            case BlockData.Color.Purple: return new Color(0.65f, 0.35f, 0.85f);
            default: return Color.gray;
        }
    }

    public static string ToKoreanName(BlockData.Color color)
    {
        switch (color)
        {
            case BlockData.Color.Red: return "빨간";
            case BlockData.Color.Blue: return "파란";
            case BlockData.Color.Yellow: return "노란";
            case BlockData.Color.Black: return "검은";
            case BlockData.Color.Green: return "초록";
            case BlockData.Color.white: return "흰";
            case BlockData.Color.Purple: return "보라";
            default: return "알 수 없는";
        }
    }
}
