using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>캐릭터 머리카락 한 색(에셋 이름이 가리키는 색)의 스프라이트. 목록 순서가 번호다(첫 칸이 1번). Tie만 번호가 파일명과 무관하다.</summary>
[CreateAssetMenu(fileName = "CharacterHairData", menuName = "Character/CharacterHairData")]
public class CharacterHairData : ScriptableObject
{
    public enum HairColor
    {
        Black,
        Brown,
        Gold,
        Red,
        Silver
    }

    [Serializable]
    public class TieSprite
    {
        public Sprite sprite; // 기본(쌍이면 R)
        public Sprite spriteL; // 쌍일 때만 추가
    }

    public int bodyType = 1; // Character 폴더 번호(1, 2, ...)
    public HairColor color;
    public List<Sprite> back = new();
    public List<Sprite> front = new();
    public List<Sprite> side = new();
    public List<TieSprite> tie = new();
}
