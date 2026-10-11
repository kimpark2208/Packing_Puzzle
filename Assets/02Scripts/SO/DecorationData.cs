using UnityEngine;

/// <summary>꾸미기 용품의 항목(꾸미기용품_품목표 기준).</summary>
public enum DecorationCategory
{
    [InspectorName("포장지")] Wrapper,
    [InspectorName("리본")] Ribbon,
    [InspectorName("카드")] Card,
    [InspectorName("장식")] Ornament,
    [InspectorName("잎")] Leaf,
}

/// <summary>꾸미기 용품 한 종류의 데이터.</summary>
[CreateAssetMenu(fileName = "DecorationData", menuName = "Puzzle/DecorationData", order = 4)]
public class DecorationData : ScriptableObject
{
    public int id;
    public DecorationCategory category;
    public string displayName;
    [Min(0)] public int price;
    public Sprite sprite;
}
