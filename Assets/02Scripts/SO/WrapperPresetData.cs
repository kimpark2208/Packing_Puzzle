using System.Linq;
using UnityEngine;

/// <summary>
/// 기획서의 영역 표기("1링A상")를 옮긴 이름: {면}_{링}Ring_{상하}. A=왼쪽 면, B=오른쪽 면.
/// 링마다 A상, A하, B상, B하 순으로 4개씩 이어지므로 순서를 바꾸지 말고 뒤에만 추가할 것.
/// </summary>
public enum WrapperRegionPosition
{
    Center,
    A_1Ring_Upper, A_1Ring_Lower, B_1Ring_Upper, B_1Ring_Lower,
    A_2Ring_Upper, A_2Ring_Lower, B_2Ring_Upper, B_2Ring_Lower,
    A_3Ring_Upper, A_3Ring_Lower, B_3Ring_Upper, B_3Ring_Lower,
    A_4Ring_Upper, A_4Ring_Lower, B_4Ring_Upper, B_4Ring_Lower,
}

public static class WrapperRegionPositionExtensions
{
    /// <summary>0 = 중앙, 1 이상 = 링 번호.</summary>
    public static int Ring(this WrapperRegionPosition p) => p == WrapperRegionPosition.Center ? 0 : ((int)p - 1) / 4 + 1;

    public static bool IsFaceA(this WrapperRegionPosition p) => p != WrapperRegionPosition.Center && ((int)p - 1) % 4 < 2;
}

/// <summary>
/// 포장지 한 판의 칸별 역할. 빈 칸은 없다: 모든 칸이 역할을 하나씩 가지고, 퍼즐은 모든 칸을 꽃으로 채워야 끝난다.
/// 역할은 WrapperRegionPosition 순서대로(중앙, 1링 A상·A하·B상·B하, 2링 …) 나열하므로 칸 수는 1 + 4 × 링 수(2링 = 9칸)다.
/// </summary>
[CreateAssetMenu(fileName = "WrapperPreset", menuName = "Puzzle/WrapperPresetData", order = 3)]
public class WrapperPresetData : ScriptableObject
{
    [Header("칸별 역할 (순서: 중앙, 1링 A상·A하·B상·B하, 2링 A상·A하·B상·B하 …)")]
    public FlowerData.FlowerRole[] cellRoles = new FlowerData.FlowerRole[1];

    /// <summary>칸 수가 1 + 4 × 링 수(링 1 이상) 모양인가.</summary>
    public bool IsWellFormed => cellRoles.Length >= 5 && (cellRoles.Length - 1) % 4 == 0;

    /// <summary>이 프리셋의 링 수(칸 수에서 계산한다).</summary>
    public int MaxRing => (cellRoles.Length - 1) / 4;

    public int CountByTag(FlowerData.FlowerRole tag) => cellRoles.Count(r => r == tag);

    private void OnValidate()
    {
        if (!IsWellFormed)
            Debug.LogWarning($"[{name}] 칸 수가 {cellRoles.Length}개입니다. 1 + 4 × 링 수(2링 = 9칸)여야 합니다.", this);
    }
}
