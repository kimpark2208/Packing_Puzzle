using UnityEngine;

/// <summary>가구의 하위 탭(가구_품목표 기준).</summary>
public enum FurnitureCategory
{
    [InspectorName("카운터")] Counter,
    [InspectorName("야외")] Outdoor,
    [InspectorName("꽃 선반")] FlowerShelf,
    [InspectorName("벽지&바닥재")] WallFloor,
}

/// <summary>가구 한 종류의 데이터.</summary>
[CreateAssetMenu(fileName = "FurnitureData", menuName = "Puzzle/FurnitureData", order = 5)]
public class FurnitureData : ScriptableObject
{
    public int id;
    public FurnitureCategory category;
    public string displayName;
    [TextArea] public string description;
    [Min(0)] public int price;
    [Tooltip("같은 세트의 가구끼리 묶는 이름(예: 별자리). 세트가 없으면 비워 둔다.")] public string set;
    [Header("카탈로그 표시")]
    [Min(0), Tooltip("카탈로그에 나오기 시작하는 일차(그 일차 포함). 0이면 처음부터.")] public int availableFromDay;
    [Tooltip("끄면 카탈로그에 나오지 않는다(그림이 아직 없는 가구를 숨겨 둘 때).")] public bool inCatalog = true;
    [Tooltip("시간대 구분이 없는 가구(선반·벽지·바닥재 등)의 그림.")] public Sprite sprite;
    [Header("카운터·야외 가구: 시간대별 그림(그림이 하나뿐이면 셋 다 같은 그림)")]
    public Sprite morningSprite;
    public Sprite sunsetSprite;
    public Sprite eveningSprite;
    [Tooltip("가구에 딸린 그림들(예: 성단 무리 천막에 뜨는 별자리 10종, 선반의 위·아래 그림).")] public Sprite[] extraSprites;

    /// <summary>카탈로그에 보여 줄 그림: 시간대별 그림이 있으면 낮(아침) 그림, 없으면 sprite.</summary>
    public Sprite CatalogSprite => morningSprite != null ? morningSprite : sprite;

    public static string CategoryName(FurnitureCategory category) => category switch
    {
        FurnitureCategory.Counter => "카운터",
        FurnitureCategory.Outdoor => "야외",
        FurnitureCategory.FlowerShelf => "꽃 선반",
        _ => "벽지&바닥재",
    };
}
