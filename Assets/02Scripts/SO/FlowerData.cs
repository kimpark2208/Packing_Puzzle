using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 꽃 한 종류의 정보(이름, 색, 역할, 그림). 퍼즐용 블록 모양은 BlockData가 따로 가지며, 꽃과 블록은 일대일로 이어지지 않는다.
/// </summary>
[CreateAssetMenu(fileName = "FlowerData", menuName = "Puzzle/FlowerData", order = 1)]
public class FlowerData : ScriptableObject
{
    /// <summary>꽃 색: 색상환 6색(빨강/주황/노랑/파랑/보라/분홍) + 흰색.</summary>
    public enum Color
    {
        Red,
        Orange,
        Yellow,
        Blue,
        Purple,
        Pink,
        White
    }

    /// <summary>꽃꽂이 디자인 역할(라인/매스/폼/필러).</summary>
    public enum FlowerRole
    {
        Line,
        Mass,
        Form,
        Filler
    }

    [Header("꽃 정보")]
    [FormerlySerializedAs("blockID")] public int flowerID;
    public string flowerName;

    /// <summary>화면에 보여 줄 이름. 이름이 비어 있으면 "꽃 {번호}".</summary>
    public string DisplayName => string.IsNullOrEmpty(flowerName) ? $"꽃 {flowerID}" : flowerName;
    public Color color;
    public FlowerRole flowerRole;

    [Tooltip("이 꽃이 밤 상점에 나올 수 있게 되는 첫 일차(꽃 시트의 '확률 증가 시작 일차'). 0이면 처음부터 가진다.")] public int acquireDay;

    /// <summary>역할 이름(라인/매스/폼/필러).</summary>
    public string RoleName => flowerRole switch
    {
        FlowerRole.Line => "라인",
        FlowerRole.Mass => "매스",
        FlowerRole.Form => "폼",
        _ => "필러",
    };

    [Header("밤 상점: 파는 값(밸런싱 전 임시값)")]
    [Min(0), Tooltip("한 번 살 때 내는 돈.")] public int shopPrice = 200;
    [Min(1), Tooltip("한 번 살 때 받는 꽃 개수.")] public int shopBundle = 15;

    [Header("특수주문: 손님이 꽃 이름 대신 말장난으로 이 꽃을 주문한다(꽃 시트의 특수주문 / 1차·2차 되묻기). 비워 두면 이 꽃은 특수주문이 없다")]
    [TextArea] public string specialOrder;
    [TextArea] public string specialReask1;
    [TextArea] public string specialReask2;

    /// <summary>이 꽃의 특수주문(말장난 주문)이 있는가.</summary>
    public bool HasSpecialOrder => !string.IsNullOrWhiteSpace(specialOrder) && specialOrder.Trim() != "-";

    [Header("일반 아이콘 (요청 UI, 드래그 조각 등. 색상은 런타임에 ColorPalette로 틴트됨)")]
    public Sprite iconSprite;

    [Header("꽃 선택 선반 - 화병에 꽂히는 줄기까지 있는 꽃 (색상은 런타임에 ColorPalette로 틴트됨)")]
    public Sprite stemSprite;

    /// <summary>낮 퍼즐 칸 그림의 개수: 중앙 + 1~4링.</summary>
    public const int RingSpriteCount = 5;

    [Header("낮 퍼즐 - 칸이 채워졌을 때 칸에 들어가는 꽃 이미지 (꽃마다 따로 만들 예정). 꽃이 놓이는 링에 따라 다른 그림을 쓴다")]
    [Tooltip("0 = 중앙, 1 = 1링, 2 = 2링, 3 = 3링, 4 = 4링. 비어 있는 링은 안쪽 링의 그림을 대신 쓴다. 색은 런타임에 꽃 색으로 칠해진다.")] public Sprite[] ringSprites = new Sprite[RingSpriteCount];

    /// <summary>ring(0 = 중앙, 1~4 = 링)에 놓일 때의 꽃 그림. 그 링의 그림이 비어 있으면 안쪽 링의 그림을, 아무것도 없으면 null.</summary>
    public Sprite RingSprite(int ring)
    {
        if (ringSprites == null) return null;

        for (int i = Mathf.Min(ring, ringSprites.Length - 1); i >= 0; i--)
        {
            if (ringSprites[i] != null) return ringSprites[i];
        }
        return null;
    }
}
