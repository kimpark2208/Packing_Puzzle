using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>기분이 나쁜 손님의 얼굴. 체형 하나에 에셋 하나: 기분 나쁜 입 목록과, 피부마다 함께 띄울 홍조.</summary>
[CreateAssetMenu(fileName = "CharacterMoodData", menuName = "Character/CharacterMoodData")]
public class CharacterMoodData : ScriptableObject
{
    [Serializable]
    public class SkinBlush
    {
        public Sprite skin;
        public Sprite blush;
    }

    public int bodyType = 1; // Character 폴더 번호(1, 2, ...)
    public List<Sprite> badMouths = new();
    public List<SkinBlush> blushes = new();

    /// <summary>이 피부에 띄울 홍조. 없으면 null.</summary>
    public Sprite BlushOf(Sprite skin) => blushes.Find(entry => entry.skin == skin)?.blush;
}
