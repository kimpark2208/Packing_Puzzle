using System.Collections.Generic;
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

    public static bool IsFaceB(this WrapperRegionPosition p) => p != WrapperRegionPosition.Center && ((int)p - 1) % 4 >= 2;
}

[System.Serializable]
public struct WrapperRegion
{
    public WrapperRegionPosition position;
    public FlowerData.FlowerRole tag;
}

[CreateAssetMenu(fileName = "WrapperPreset", menuName = "Puzzle/WrapperPresetData", order = 3)]
public class WrapperPresetData : ScriptableObject
{
    [Header("이 프리셋이 요구하는 영역 (영역당 꽃 한 송이)")]
    public List<WrapperRegion> regions = new();

    public int CountByTag(FlowerData.FlowerRole tag) => regions.Count(r => r.tag == tag);

    public int FaceACount => regions.Count(r => r.position.IsFaceA());

    public int FaceBCount => regions.Count(r => r.position.IsFaceB());

    public int MaxRing => regions.Count == 0 ? 0 : regions.Max(r => r.position.Ring());

    private void OnValidate()
    {
        foreach (var dup in regions.GroupBy(r => r.position).Where(g => g.Count() > 1))
            Debug.LogWarning($"[{name}] 같은 영역이 중복됨: {dup.Key}", this);

        if (Mathf.Abs(FaceACount - FaceBCount) > 2)
            Debug.LogWarning($"[{name}] A면({FaceACount})과 B면({FaceBCount}) 영역 수 차이가 2를 넘음 (클리어 불가)", this);
    }
}
