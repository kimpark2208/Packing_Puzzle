using System;
using System.Collections.Generic;
using UnityEngine;

// 퍼즐(그리드)을 완성하는 정답 레시피: 어떤 BlockData(모양)가 몇 개 필요한지 정의한다.
[CreateAssetMenu(fileName = "PuzzleRecipe", menuName = "Puzzle/PuzzleRecipe", order = 2)]
public class PuzzleRecipe : ScriptableObject
{
    [Serializable]
    public struct RequiredBlock
    {
        [Tooltip("필요한 블록의 원본 데이터 (회전/반전 여부는 무시하고 모양 종류만 본다)")]
        public BlockData blockData;
        [Tooltip("이 모양이 정확히 몇 개 필요한지")]
        public int requiredCount;
    }

    [Header("이 퍼즐을 완성하는 데 필요한 블록 구성")]
    public List<RequiredBlock> requiredBlocks;

    // blockID -> 필요 개수로 변환해서 비교하기 쉽게 만든다.
    public Dictionary<int, int> BuildRequiredCountMap()
    {
        var map = new Dictionary<int, int>();
        foreach (var entry in requiredBlocks)
        {
            if (entry.blockData == null) continue;
            int id = entry.blockData.blockID;
            if (!map.ContainsKey(id)) map[id] = 0;
            map[id] += entry.requiredCount;
        }
        return map;
    }
}
