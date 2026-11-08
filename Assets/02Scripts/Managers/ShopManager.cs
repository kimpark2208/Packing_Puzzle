using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 카탈로그 탭 종류(ShopCategory)와 예술품 목록(싱글톤). 포장지·가구·꾸미기 용품은 각자의 데이터(DataRegistry)에서 읽는다.
/// </summary>
public class ShopManager : Singleton<ShopManager>
{
    public enum ShopCategory
    {
        Wrapper,    // 포장지 (그리드 사이즈)
        Furniture,  // 가구
        Deco,       // 장식
        Artifact    // 예술품
    }

    public struct ShopItem
    {
        public int itemId;              // 아이템 고유 ID
        public string itemName;         // 아이템 이름
        public int price;               // 가격
        public ShopCategory category;   // 카테고리
        public string description;      // 설명
    }

    // 모든 상점 아이템
    private List<ShopItem> allItems = new();

    protected override void OnAwake()
    {
        InitializeItems();
    }

    // TODO: 아이템 정의를 코드에서 빼서 ScriptableObject로 옮긴다(지금은 InitializeItems에 박혀 있다).
    private void InitializeItems()
    {
        // 가구(Furniture)는 FurnitureData(DataRegistry)에서 읽는다 — 카탈로그가 직접 보여 준다

        // 꾸미기 용품(Deco)은 DecorationData(DataRegistry)에서 읽는다 — 카탈로그가 직접 보여 준다

        // ========== 예술품 (Artifact) ==========
        allItems.Add(new ShopItem
        {
            itemId = 301,
            itemName = "조각상",
            price = 500,
            category = ShopCategory.Artifact,
            description = "대리석 조각상"
        });

        allItems.Add(new ShopItem
        {
            itemId = 302,
            itemName = "거울",
            price = 350,
            category = ShopCategory.Artifact,
            description = "장식용 거울"
        });

        allItems.Add(new ShopItem
        {
            itemId = 303,
            itemName = "꽃병",
            price = 450,
            category = ShopCategory.Artifact,
            description = "도자기 꽃병"
        });

        Debug.Log($"[ShopManager] {allItems.Count}개 아이템 로드 완료");
    }

    /// <summary>
    /// 특정 카테고리의 아이템 반환
    /// </summary>
    public List<ShopItem> GetItemsByCategory(ShopCategory category) => allItems.Where(i => i.category == category).ToList();
}
