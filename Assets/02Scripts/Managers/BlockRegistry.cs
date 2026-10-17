using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 퍼즐에 쓰는 모든 BlockData(블록 모양) 카탈로그. 블록이 필요한 퍼즐(밤 퍼즐의 블록 그리기 등)이 여기서 블록을 골라 조합한다.
/// 꽃과는 일대일로 이어지지 않는다(꽃 카탈로그는 FlowerRegistry). 부트스트랩 씬의 매니저 오브젝트에 배치한다.
/// </summary>
public class BlockRegistry : Singleton<BlockRegistry>
{
    [SerializeField] private List<BlockData> allBlocks = new();

    public IReadOnlyList<BlockData> AllBlocks => allBlocks;

    public BlockData GetById(int blockId)
    {
        return allBlocks.FirstOrDefault(b => b.blockID == blockId);
    }

    /// <summary>블록 목록에서 서로 다른 블록을 count개 무작위로 고른다(목록이 모자라면 있는 만큼).</summary>
    public List<BlockData> PickRandom(int count)
    {
        return allBlocks.Where(b => b != null).OrderBy(_ => Random.value).Take(count).ToList();
    }
}
