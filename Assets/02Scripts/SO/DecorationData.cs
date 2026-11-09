using System.Collections.Generic;
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
    [Tooltip("카탈로그(꾸미기 용품 탭) 카드에 보이는 설명.")] public string description;
    [Tooltip("카탈로그에 나오는가(꾸미기 용품 탭).")] public bool inCatalog = true;
    [Min(1), Tooltip("카탈로그에 나오기 시작하는 일차(꽃다발 용품 카탈로그가 열리는 5일차부터).")] public int availableFromDay = 5;
    [Min(0), Tooltip("게임을 시작할 때 기본으로 주는 개수(0이면 주지 않는다. 기획서: 고사리 잎, 마른 밀 이삭). 나머지는 카탈로그나 선물로 얻는다.")] public int startingStock;
    [TextArea, Tooltip("손님이 이 용품을 은유로 주문하는 문장들(예: 아, 그리고 작은 나비핀도 꼭 꽂아주세요!). 비워 두면 이 용품은 직설형 템플릿(OrderTemplateData)으로만 주문된다.")] public List<string> orderMetaphors = new();
    public Sprite sprite;
    [Min(0.1f)] public float sizeScale = 1f; // 꽃다발에 놓였을 때의 크기 배율(항목별 상자에 맞춘 크기에 곱한다). 용품마다 따로 키우고 줄인다
}
