using System.Collections.Generic;
using UnityEngine;

/// <summary>CurrencyManager의 꾸미기 용품 부분: 보유 재고. 카탈로그에서 주문하거나 선물로 받는 순간 재고에 들어간다(포장지와 같다). 저장 시스템이 생기기 전까지는 게임을 켠 동안만 유지된다.</summary>
public partial class CurrencyManager
{
    private readonly Dictionary<int, int> decorationStock = new(); // 꾸미기 용품 번호(DecorationData.id) → 재고

    public int GetDecorationStock(int decorationId) => decorationStock.TryGetValue(decorationId, out int count) ? count : 0;

    /// <summary>이 꾸미기 용품을 한 번이라도 얻었는가(기본 지급, 카탈로그 주문, 선물). 재고가 다 떨어져도 얻은 것으로 친다. 손님은 얻은 용품만 주문한다.</summary>
    public bool HasObtainedDecoration(int decorationId) => decorationStock.ContainsKey(decorationId);

    /// <summary>꾸미기 용품 재고를 늘린다(줄일 때는 음수). 재고가 0 아래로 내려가지 않는다.</summary>
    public void AddDecorationStock(int decorationId, int amount)
    {
        decorationStock[decorationId] = Mathf.Max(0, GetDecorationStock(decorationId) + amount);
    }
}
