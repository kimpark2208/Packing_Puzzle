using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 게임에 존재하는 모든 BlockData(꽃) 카탈로그. 낮/밤 퍼즐 생성기가 공통으로 참조하는
/// 단일 소스(Single Source of Truth) 역할을 한다. 부트스트랩 씬의 매니저 오브젝트에 배치한다.
/// </summary>
public class BlockDatabase : Singleton<BlockDatabase>
{
    [SerializeField] private List<BlockData> allBlocks = new();

    public IReadOnlyList<BlockData> AllBlocks => allBlocks;

    public BlockData GetById(int blockId)
    {
        return allBlocks.FirstOrDefault(b => b.blockID == blockId);
    }

    /// <summary>특정 포장지 단계(tier)에서 사용 가능한 모든 꽃(누적 해금).</summary>
    public List<BlockData> GetBlocksForWrapperTier(int wrapperTier)
    {
        return allBlocks.Where(b => b.unlockWrapperTier <= wrapperTier).ToList();
    }

    /// <summary>플레이어가 실제로 획득한 꽃 중, 해당 포장지 단계에서 쓸 수 있는 것들.</summary>
    public List<BlockData> GetObtainedBlocksForWrapperTier(IEnumerable<int> obtainedFlowerIds, int wrapperTier)
    {
        var obtainedSet = new HashSet<int>(obtainedFlowerIds);
        return allBlocks
            .Where(b => obtainedSet.Contains(b.blockID) && b.unlockWrapperTier <= wrapperTier)
            .ToList();
    }
}
