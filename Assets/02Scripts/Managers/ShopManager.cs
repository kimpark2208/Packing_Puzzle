using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상점 데이터 관리 (싱글톤)
/// 포장지, 가구, 장식, 예술품 등 모든 아이템 정의
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
        public string gridSize;         // 포장지만: "5x5", "8x8" 등 (표시용)
        public int gridSizeInt;         // 포장지만: 실제 그리드 한 변 길이
        public int tier;                // 포장지만: 누적 해금 단계 (1부터)
        public string colorHintName;    // 포장지만: 명확한 힌트용 색깔 이름 (예: "핑크")
        public string sizeHintWord;     // 포장지만: 모호한 힌트용 크기 표현 (예: "가장 작은")
    }

    // 모든 상점 아이템
    private List<ShopItem> allItems = new();

    protected override void OnAwake()
    {
        InitializeItems();
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
            gridSize = "5x5",
            gridSizeInt = 5,
            tier = 1,
            colorHintName = "핑크",
            sizeHintWord = "가장 작은"
        });

        allItems.Add(new ShopItem
        {
            itemId = 2,
            itemName = "포장지 (8x8)",
            price = 1000,
            category = ShopCategory.Wrapper,
            description = "더 큰 포장지",
            gridSize = "8x8",
            gridSizeInt = 8,
            tier = 2,
            colorHintName = "노란",
            sizeHintWord = "중간 크기의"
        });

        allItems.Add(new ShopItem
        {
            itemId = 3,
            itemName = "포장지 (10x10)",
            price = 2500,
            category = ShopCategory.Wrapper,
            description = "매우 큰 포장지",
            gridSize = "10x10",
            gridSizeInt = 10,
            tier = 3,
            colorHintName = "보라",
            sizeHintWord = "가장 큰"
        });

        allItems.Add(new ShopItem
        {
            itemId = 4,
            itemName = "포장지 (품 등장)",
            price = 4000,
            category = ShopCategory.Wrapper,
            description = "라인/매스/필러 요구 개수가 늘어난 포장지",
            gridSize = "12x12",
            gridSizeInt = 12,
            tier = 4,
            colorHintName = "초록",
            sizeHintWord = "더 화려한"
        });

        allItems.Add(new ShopItem
        {
            itemId = 5,
            itemName = "포장지 (5단계)",
            price = 6000,
            category = ShopCategory.Wrapper,
            description = "라인/매스/품/필러를 모두 요구하는 포장지",
            gridSize = "14x14",
            gridSizeInt = 14,
            tier = 5,
            colorHintName = "주황",
            sizeHintWord = "한층 더 화려한"
        });

        allItems.Add(new ShopItem
        {
            itemId = 6,
            itemName = "포장지 (6단계)",
            price = 8500,
            category = ShopCategory.Wrapper,
            description = "요구 개수가 더 늘어난 고난도 포장지",
            gridSize = "16x16",
            gridSizeInt = 16,
            tier = 6,
            colorHintName = "청록",
            sizeHintWord = "매우 화려한"
        });

        allItems.Add(new ShopItem
        {
            itemId = 7,
            itemName = "포장지 (최종)",
            price = 12000,
            category = ShopCategory.Wrapper,
            description = "가장 많은 꽃을 요구하는 최종 단계 포장지",
            gridSize = "18x18",
            gridSizeInt = 18,
            tier = 7,
            colorHintName = "금빛",
            sizeHintWord = "가장 화려한"
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

    /// <summary>포장지 ID로 그리드 한 변 길이를 얻는다. 못 찾으면 기본값 5.</summary>
    public int GetGridSize(int wrapperId)
    {
        var item = GetItemById(wrapperId);
        return (item.HasValue && item.Value.gridSizeInt > 0) ? item.Value.gridSizeInt : 5;
    }

    /// <summary>포장지 ID로 누적 해금 단계(tier)를 얻는다. 못 찾으면 기본값 1.</summary>
    public int GetTier(int wrapperId)
    {
        var item = GetItemById(wrapperId);
        return (item.HasValue && item.Value.tier > 0) ? item.Value.tier : 1;
    }

    /// <summary>손님이 "명확한" 힌트를 줄 때 사용하는 포장지 색깔 이름.</summary>
    public string GetColorHintName(int wrapperId)
    {
        var item = GetItemById(wrapperId);
        return item.HasValue && !string.IsNullOrEmpty(item.Value.colorHintName) ? item.Value.colorHintName : "특별한";
    }

    /// <summary>손님이 "모호한" 힌트를 줄 때 사용하는 크기 표현.</summary>
    public string GetSizeHintWord(int wrapperId)
    {
        var item = GetItemById(wrapperId);
        return item.HasValue && !string.IsNullOrEmpty(item.Value.sizeHintWord) ? item.Value.sizeHintWord : "적당한";
    }
}
