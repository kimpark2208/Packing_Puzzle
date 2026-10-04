using UnityEngine;

/// <summary>
/// FlowerData.Color enum을 실제 렌더링 색상 및 한글 이름으로 변환하는 공용 팔레트.
/// 색상 관련 매핑이 여러 스크립트(BlockDrag, WrapperSlot, CustomerRequirementGenerator 등)에 흩어지는 것을 방지한다.
/// </summary>
public static class ColorPalette
{
    public static Color ToUnityColor(FlowerData.Color color)
    {
        switch (color)
        {
            case FlowerData.Color.Red: return new Color(0.88f, 0.35f, 0.30f);
            case FlowerData.Color.Orange: return new Color(0.95f, 0.62f, 0.22f);
            case FlowerData.Color.Yellow: return new Color(0.95f, 0.80f, 0.28f);
            case FlowerData.Color.Blue: return new Color(0.35f, 0.58f, 0.88f);
            case FlowerData.Color.Purple: return new Color(0.62f, 0.50f, 0.82f);
            case FlowerData.Color.Pink: return new Color(0.93f, 0.55f, 0.70f);
            case FlowerData.Color.White: return new Color(0.97f, 0.97f, 0.97f);
            default: return Color.gray;
        }
    }

    /// <summary>꽃 역할(속성)의 상징 색(기획서): 라인=주황, 매스=파랑, 폼=핑크, 필러=노랑.</summary>
    public static Color ToRoleColor(FlowerData.FlowerRole role)
    {
        switch (role)
        {
            case FlowerData.FlowerRole.Line: return new Color(0.96f, 0.62f, 0.45f);
            case FlowerData.FlowerRole.Mass: return new Color(0.55f, 0.75f, 0.95f);
            case FlowerData.FlowerRole.Form: return new Color(0.95f, 0.60f, 0.75f);
            default: return new Color(0.98f, 0.78f, 0.35f);
        }
    }

    public static string ToKoreanName(FlowerData.Color color)
    {
        switch (color)
        {
            case FlowerData.Color.Red: return "빨간";
            case FlowerData.Color.Orange: return "주황";
            case FlowerData.Color.Yellow: return "노란";
            case FlowerData.Color.Blue: return "파란";
            case FlowerData.Color.Purple: return "보라";
            case FlowerData.Color.Pink: return "분홍";
            case FlowerData.Color.White: return "흰";
            default: return "알 수 없는";
        }
    }
}
