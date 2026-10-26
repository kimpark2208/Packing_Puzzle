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
    }

    [Serializable]
    public struct Item
    {
        public string name;
        [Tooltip("꽃은 보유한 재고에서 수량만큼 보관한다. 포장지와 꾸미기 용품은 개수를 따지지 않고 가지고 있는지(보유/미보유)만 본다(임시).")] public ItemKind kind;
        [Tooltip("꽃이면 꽃 번호(flowerID), 포장지면 포장지 번호(wrapperId), 꾸미기 용품이면 DecorationData의 id. 0이면 연결하지 않아 미보유다.")] public int id;
        [Min(1), Tooltip("필요한 수량(꽃만 쓴다).")] public int count;
        [Tooltip("항목 그림(선택). 비우면 꽃과 꾸미기 용품은 그 데이터의 그림을 쓴다.")] public Sprite icon;

        /// <summary>미리 모아 두는(보관하는) 항목인가: 꽃만.</summary>
        public bool Storable => id > 0 && kind == ItemKind.Flower;

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
    [Header("예약일에 찾아온 예약자의 대사")]
    [TextArea, Tooltip("예약자가 찾아와 처음 하는 말(준비가 됐는지 묻는다). 이 말 뒤에 플레이어의 응답 버튼이 뜬다.")] public string pickupLine = "예약했던 꽃다발 찾으러 왔어요! 준비되셨나요?";
    [TextArea, Tooltip("준비가 됐다고 답했을 때 예약자의 말.")] public string pickupReadyReply = "고마워요! 잘 부탁드려요.";
    [TextArea, Tooltip("준비가 안 됐다고 답했을 때 예약자의 말. 예약은 취소되고 예약금의 2배를 돌려준다.")] public string pickupFailReply = "…준비가 안 됐다고요? 그럼 예약은 없던 걸로 할게요.";
    [Header("예약일 꽃다발 제작")]
    [Min(1), Tooltip("예약일에 만들어야 하는 꽃다발 개수. 하루 안에 다 못 만든 꽃다발은 -100점으로 센다.")] public int bouquetCount = 5;
    [TextArea, Tooltip("꽃다발을 다 만든 뒤 평균 점수가 S등급(+75 이상)일 때 예약자의 말.")] public string gradeLineS = "세상에, 이렇게 멋진 꽃다발이라니! 기대 이상이에요. 보너스까지 챙겨 드릴게요!";
    [TextArea, Tooltip("A등급(+25~+74)일 때 예약자의 말.")] public string gradeLineA = "마음에 쏙 들어요! 덕분에 행사가 빛나겠어요. 약속보다 조금 더 드릴게요.";
    [TextArea, Tooltip("B등급(-24~+24)일 때 예약자의 말.")] public string gradeLineB = "네, 부탁드린 대로 잘 만들어 주셨네요. 감사합니다.";
    [TextArea, Tooltip("C등급(-74~-25)일 때 예약자의 말.")] public string gradeLineC = "음… 생각했던 것과는 좀 다르네요. 보상은 절반만 드릴게요.";
    [TextArea, Tooltip("D등급(-100~-75)일 때 예약자의 말.")] public string gradeLineD = "이걸 행사에 쓰라고요? 보상은 못 드리겠어요.";
    [Tooltip("필요한 꽃·꽃다발 용품·포장지·꽃다발(순서대로 보인다).")] public Item[] items = Array.Empty<Item>();
    [Min(0)] public int reward = 5000;
    [Min(0), Tooltip("예약금. 팝업에는 보상금과 예약금을 더한 총 수령금이 보인다.")] public int deposit = 1000;

    public int Total => reward + deposit;

    /// <summary>평균 점수 등급에 맞는 예약자의 말.</summary>
    public string GradeLine(ReservationGrade grade)
    {
        switch (grade)
        {
            case ReservationGrade.S: return gradeLineS;
            case ReservationGrade.A: return gradeLineA;
            case ReservationGrade.B: return gradeLineB;
            case ReservationGrade.C: return gradeLineC;
            default: return gradeLineD;
        }
    }
}
