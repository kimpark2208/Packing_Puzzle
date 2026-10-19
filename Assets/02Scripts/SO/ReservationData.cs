using System;
using UnityEngine;

/// <summary>예약 주문 하나(예약 쇼카드 팝업에 보이는 내용과 전화 속 예약자의 말).</summary>
[CreateAssetMenu(fileName = "Reservation", menuName = "Puzzle/ReservationData", order = 8)]
public class ReservationData : ScriptableObject
{
    public enum ItemKind
    {
        [InspectorName("꽃")] Flower,
        [InspectorName("포장지")] Wrapper,
        [InspectorName("꾸미기 용품")] Supply,
        [InspectorName("꽃다발")] Bouquet,
    }

    [Serializable]
    public struct Item
    {
        public string name;
        [Tooltip("꽃, 포장지, 꾸미기 용품은 보유한 재고에서 보관한다. 꽃다발은 예약 당일에 만들 개수라 보관하지 않는다.")] public ItemKind kind;
        [Tooltip("꽃이면 꽃 번호(flowerID), 포장지면 포장지 번호(wrapperId), 꾸미기 용품이면 DecorationData의 id. 0이면 연결하지 않아 보관할 수 없다.")] public int id;
        [Min(1)] public int count;
        [Tooltip("항목 그림(선택). 비우면 꽃과 꾸미기 용품은 그 데이터의 그림을 쓴다.")] public Sprite icon;

        /// <summary>미리 모아 두는(보관하는) 항목인가: 꽃, 포장지, 꾸미기 용품.</summary>
        public bool Storable => id > 0 && (kind == ItemKind.Flower || kind == ItemKind.Wrapper || kind == ItemKind.Supply);

        /// <summary>보여 줄 그림: 직접 정한 그림, 없으면 꽃의 아이콘이나 꾸미기 용품의 그림. 없으면 null.</summary>
        public Sprite ResolveIcon()
        {
            if (icon != null) return icon;
            if (id <= 0 || DataRegistry.Instance == null) return null;

            if (kind == ItemKind.Flower)
            {
                FlowerData flower = DataRegistry.Instance.GetFlower(id);
                return flower != null ? flower.iconSprite : null;
            }
            if (kind == ItemKind.Supply)
            {
                DecorationData decoration = DataRegistry.Instance.GetDecoration(id);
                return decoration != null ? decoration.sprite : null;
            }
            return null;
        }
    }

    [Tooltip("팝업 맨 위 제목. 예: 소국을 왕창 넣은 결혼식 꽃다발")] public string title;
    [Tooltip("달력의 예약일 칸에 뜨는 짧은 이름. 예: 결혼식")] public string calendarLabel = "예약";
    [Tooltip("전화를 받았을 때 전화기 말풍선에 뜨는 예약자의 말. TMP 리치 텍스트(<b>굵게</b>)를 쓸 수 있다.")] [TextArea] public string callLine;
    [Min(1), Tooltip("기한(일).")] public int deadlineDays = 4;
    [Tooltip("필요한 꽃·꽃다발 용품·포장지·꽃다발(순서대로 보인다).")] public Item[] items = Array.Empty<Item>();
    [Min(0)] public int reward = 5000;
    [Min(0), Tooltip("예약금. 팝업에는 보상금과 예약금을 더한 총 수령금이 보인다.")] public int deposit = 1000;

    public int Total => reward + deposit;
}
