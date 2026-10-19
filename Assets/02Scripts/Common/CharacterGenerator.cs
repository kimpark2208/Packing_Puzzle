using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>랜덤으로 뽑은 캐릭터 한 명의 구성. 없는 파츠(Tie 등)는 null.</summary>
public struct CharacterAppearance
{
    public int bodyType;
    public Sprite brow, cloth, eyes, mouth, skin;
    public CharacterHairData.HairColor hairColor;
    public Sprite hairBack, hairFront, hairSide;
    public Sprite tieR; // 기본(쌍의 R)
    public Sprite tieL; // 쌍의 L(추가)
}

/// <summary>
/// 캐릭터를 무작위로 만들고 캐릭터 이미지들에 적용한다. 체형 하나를 고르고, 그 체형의 파츠마다 스프라이트를 하나씩,
/// 머리카락은 색 하나를 고른 뒤 그 색의 Back/Front를 하나씩, Side와 Tie는 있을지 없을지를 먼저 고르고(Tie는 L을 더할지도) 있으면 하나를 고른다. 모든 선택은 균등 랜덤이다.
/// </summary>
public class CharacterGenerator : MonoBehaviour
{
    [Header("SO 에셋 (파츠 5종 + 머리카락 색 5개)")]
    [SerializeField] private CharacterPartData[] partAssets;
    [SerializeField] private CharacterHairData[] hairAssets;

    [Header("피부")]
    [SerializeField, Tooltip("밤 손님(귀신)의 피부. 밤 손님은 이 피부 중에서만, 낮 손님은 이 피부를 뺀 나머지에서 고른다.")] private Sprite[] nightSkins;

    [Header("확률")]
    [SerializeField, Range(0f, 1f)] private float sideChance = 0.5f; // 사이드 헤어가 있을 확률
    [SerializeField, Range(0f, 1f)] private float tieChance = 0.5f; // Tie가 있을 확률
    [SerializeField, Range(0f, 1f)] private float leftTieChance = 0.5f; // Tie가 있을 때 L을 더할 확률(L 스프라이트가 있는 항목만)

    [Header("적용할 이미지")]
    [SerializeField] private Image brow;
    [SerializeField] private Image cloth;
    [SerializeField] private Image eyes;
    [SerializeField] private Image mouth;
    [SerializeField] private Image skin;
    [SerializeField] private Image hairBack;
    [SerializeField] private Image hairFront;
    [SerializeField] private Image hairSide;
    [SerializeField] private Image tieR;
    [SerializeField] private Image tieL;

    public CharacterAppearance Current { get; private set; }

    [ContextMenu("Randomize")]
    public void Randomize() => Apply(Generate());

    /// <summary>night가 true면 밤 손님(피부가 nightSkins 중 하나), false면 낮 손님(피부가 nightSkins 밖)을 만든다.</summary>
    public void Randomize(bool night) => Apply(Generate(night));

    public CharacterAppearance Generate(bool night = false)
    {
        var result = new CharacterAppearance();

        var bodies = new List<int>();
        foreach (CharacterPartData asset in partAssets)
        {
            if (!bodies.Contains(asset.bodyType)) bodies.Add(asset.bodyType);
        }
        if (bodies.Count == 0) return result;
        result.bodyType = bodies[Random.Range(0, bodies.Count)];

        result.brow = PickPart(result.bodyType, CharacterPartData.PartType.Brow);
        result.cloth = PickPart(result.bodyType, CharacterPartData.PartType.Cloth);
        result.eyes = PickPart(result.bodyType, CharacterPartData.PartType.Eyes);
        result.mouth = PickPart(result.bodyType, CharacterPartData.PartType.Mouth);
        result.skin = PickSkin(result.bodyType, night);

        var hairs = new List<CharacterHairData>();
        foreach (CharacterHairData asset in hairAssets)
        {
            if (asset.bodyType == result.bodyType) hairs.Add(asset);
        }
        if (hairs.Count == 0) return result;

        CharacterHairData hair = hairs[Random.Range(0, hairs.Count)];
        result.hairColor = hair.color;
        result.hairBack = Pick(hair.back);
        result.hairFront = Pick(hair.front);
        if (Random.value < sideChance) result.hairSide = Pick(hair.side);

        if (hair.tie.Count > 0 && Random.value < tieChance)
        {
            CharacterHairData.TieSprite tie = hair.tie[Random.Range(0, hair.tie.Count)];
            result.tieR = tie.sprite;
            if (tie.spriteL != null && Random.value < leftTieChance) result.tieL = tie.spriteL;
        }

        return result;
    }

    public void Apply(CharacterAppearance appearance)
    {
        Current = appearance;
        Show(brow, appearance.brow);
        Show(cloth, appearance.cloth);
        Show(eyes, appearance.eyes);
        Show(mouth, appearance.mouth);
        Show(skin, appearance.skin);
        Show(hairBack, appearance.hairBack);
        Show(hairFront, appearance.hairFront);
        Show(hairSide, appearance.hairSide);
        Show(tieR, appearance.tieR);
        Show(tieL, appearance.tieL);
    }

    private Sprite PickPart(int bodyType, CharacterPartData.PartType part)
    {
        foreach (CharacterPartData asset in partAssets)
        {
            if (asset.bodyType == bodyType && asset.part == part) return Pick(asset.sprites);
        }
        return null;
    }

    /// <summary>피부를 고른다. 밤 손님은 nightSkins 중에서만, 낮 손님은 nightSkins를 뺀 것 중에서만(해당하는 피부가 없으면 전체에서).</summary>
    private Sprite PickSkin(int bodyType, bool night)
    {
        foreach (CharacterPartData asset in partAssets)
        {
            if (asset.bodyType != bodyType || asset.part != CharacterPartData.PartType.Skin) continue;

            var allowed = asset.sprites.FindAll(s => nightSkins != null && System.Array.IndexOf(nightSkins, s) >= 0 == night);
            return Pick(allowed.Count > 0 ? allowed : asset.sprites);
        }
        return null;
    }

    private static Sprite Pick(List<Sprite> sprites) => sprites.Count == 0 ? null : sprites[Random.Range(0, sprites.Count)];

    private static void Show(Image image, Sprite sprite)
    {
        if (image == null) return;
        image.sprite = sprite;
        image.enabled = sprite != null; // 없는 파츠는 이미지를 꺼서 숨긴다
    }
}
