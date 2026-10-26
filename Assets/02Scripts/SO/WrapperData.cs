using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "WrapperData", menuName = "Puzzle/WrapperData", order = 2)]
public class WrapperData : ScriptableObject
{
    [Header("상점/보유 목록의 포장지 번호 (ShopManager의 포장지 itemId와 같음)")]
    public int wrapperId;

    [Header("포장지 색: 색 종류(손님이 \"○○색 포장지\"라고 말할 때의 이름과 주문 은유의 기준) + 화면에 보이는 색(RGB)")]
    public WrapperColor wrapperColor;
    public Color color = Color.white;

    /// <summary>손님이 말하는 포장지 색 이름("빨간", "남" …). 뒤에 "색"을 붙여 말한다.</summary>
    public string ColorName => ColorPalette.ToKoreanName(wrapperColor);

    [Header("포장지 크기 (동심원 개수)")]
    [Range(1, 4)] public int ringCount = 3;

    [Tooltip("포장지 선택 화면에서 이 포장지를 눌렀을 때 뜨는 미리보기 그림. 비우면 화면에 놓인 기본 그림을 쓴다.")] public Sprite previewSprite;

    [Header("꽃 선택에서 고를 수 있는 꽃 종류의 전체 개수 (2링 6 / 3링 8 / 4링 10). 역할별로는 그 프리셋에서 그 역할의 칸 수까지다")]
    [Min(4)] public int maxFlowerTypes = 6;

    [Header("가챠(버킷) 한 번에 나오는 꽃 개수 (버킷의 슬롯 수까지만 보인다)")]
    [Min(1)] public int flowersPerDraw = 3;

    [Header("이 포장지의 프리셋 (라운드 시작 시 무작위로 하나 선택)")]
    public List<WrapperPresetData> presets = new();

    private void OnValidate()
    {
        // 빈 칸이 없어야 하므로, 프리셋의 칸 수가 이 포장지의 모든 칸(1 + 4 × 링 수)과 같아야 한다.
        foreach (var p in presets.Where(p => p != null && p.cellRoles.Length != 1 + 4 * ringCount))
            Debug.LogWarning($"[{name}] 프리셋 {p.name}의 칸이 {p.cellRoles.Length}개인데 ringCount {ringCount}는 {1 + 4 * ringCount}칸입니다", this);
    }
}
