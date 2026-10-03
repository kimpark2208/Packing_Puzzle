using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "WrapperData", menuName = "Puzzle/WrapperData", order = 2)]
public class WrapperData : ScriptableObject
{
    [Header("상점/보유 목록의 포장지 번호 (ShopManager의 포장지 itemId와 같음)")]
    public int wrapperId;

    [Header("포장지 색 (손님이 \"○○색 포장지\"라고 말할 때의 이름 + 표시 색)")]
    public string colorName;
    public Color color = Color.white;

    [Header("포장지 크기 (동심원 개수)")]
    [Min(1)] public int ringCount = 3;

    [Header("이 포장지의 프리셋 (라운드 시작 시 무작위로 하나 선택)")]
    public List<WrapperPresetData> presets = new();

    private void OnValidate()
    {
        var valid = presets.Where(p => p != null).ToList();
        foreach (var p in valid.Where(p => p.MaxRing > ringCount))
            Debug.LogWarning($"[{name}] 프리셋 {p.name}이(가) 링 {p.MaxRing}을 쓰지만 ringCount는 {ringCount}", this);
    }
}
