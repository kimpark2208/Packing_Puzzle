using System.Collections.Generic;
using UnityEngine;

/// <summary>머리카락을 뺀 캐릭터 파츠 하나(눈썹/옷/눈/입/피부 중 에셋 이름이 가리키는 것)의 스프라이트 목록. 목록 순서가 번호다(첫 칸이 1번).</summary>
[CreateAssetMenu(fileName = "CharacterPartData", menuName = "Character/CharacterPartData")]
public class CharacterPartData : ScriptableObject
{
    public enum PartType
    {
        Brow,
        Cloth,
        Eyes,
        Mouth,
        Skin
    }

    public int bodyType = 1; // Character 폴더 번호(1, 2, ...)
    public PartType part;
    public List<Sprite> sprites = new();
}
