using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 게임에 존재하는 모든 FlowerData(꽃) 카탈로그. 낮/밤 퍼즐 생성기가 공통으로 참조하는
/// 단일 소스(Single Source of Truth) 역할을 한다. 부트스트랩 씬의 매니저 오브젝트에 배치한다.
/// </summary>
public class BlockRegistry : Singleton<BlockRegistry>
{
    [SerializeField] private List<FlowerData> allBlocks = new();

    public IReadOnlyList<FlowerData> AllBlocks => allBlocks;

    public FlowerData GetById(int blockId)
    {
        return allBlocks.FirstOrDefault(b => b.blockID == blockId);
    }
}
