using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상점 데이터 관리 (싱글톤)
/// 포장지, 가구, 장식, 예술품 등 모든 아이템 정의
/// </summary>
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

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
        public string gridSize;         // 포장지만: "5x5", "8x8" 등
    }

    // 모든 상점 아이템
    private List<ShopItem> allItems = new();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeItems();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeItems()
    {
        // ========== 포장지 (Wrapper) ==========
        allItems.Add(new ShopItem
        {
            itemId = 1,
            itemName = "포장지 (5x5)",
            price = 0,
            category = ShopCategory.Wrapper,
            description = "기본 포장지",
            gridSize = "5x5"
        });

        allItems.Add(new ShopItem
        {
            itemId = 2,
            itemName = "포장지 (8x8)",
            price = 1000,
            category = ShopCategory.Wrapper,
            description = "더 큰 포장지",
            gridSize = "8x8"
        });

        allItems.Add(new ShopItem
        {
            itemId = 3,
            itemName = "포장지 (10x10)",
            price = 2500,
            category = ShopCategory.Wrapper,
            description = "매우 큰 포장지",
            gridSize = "10x10"
        });

        // ========== 가구 (Furniture) ==========
        allItems.Add(new ShopItem
        {
            itemId = 101,
            itemName = "테이블",
            price = 200,
            category = ShopCategory.Furniture,
            description = "목재 테이블"
        });

        allItems.Add(new ShopItem
        {
            itemId = 102,
            itemName = "의자",
            price = 150,
            category = ShopCategory.Furniture,
            description = "편안한 의자"
        });

        allItems.Add(new ShopItem
        {
            itemId = 103,
            itemName = "선반",
            price = 300,
            category = ShopCategory.Furniture,
            description = "물건을 정리할 선반"
        });

        // ========== 장식 (Deco) ==========
        allItems.Add(new ShopItem
        {
            itemId = 201,
            itemName = "화분",
            price = 100,
            category = ShopCategory.Deco,
            description = "작은 화분"
        });

        allItems.Add(new ShopItem
        {
            itemId = 202,
            itemName = "액자",
            price = 250,
            category = ShopCategory.Deco,
            description = "벽에 걸 액자"
        });

        allItems.Add(new ShopItem
        {
            itemId = 203,
            itemName = "조명",
            price = 400,
            category = ShopCategory.Deco,
            description = "밝은 조명"
        });

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
    public List<ShopItem> GetItemsByCategory(ShopCategory category)
    {
        var result = new List<ShopItem>();
        foreach (var item in allItems)
        {
            if (item.category == category)
                result.Add(item);
        }
        return result;
    }

    /// <summary>
    /// 모든 아이템 반환
    /// </summary>
    public List<ShopItem> GetAllItems() => new List<ShopItem>(allItems);

    /// <summary>
    /// 아이템 ID로 찾기
    /// </summary>
    public ShopItem? GetItemById(int itemId)
    {
        foreach (var item in allItems)
        {
            if (item.itemId == itemId)
                return item;
        }
        return null;
    }
}
