using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 게임에 존재하는 모든 FlowerData(꽃) 카탈로그. 낮/밤 퍼즐 생성기가 공통으로 참조하는
/// 단일 소스(Single Source of Truth) 역할을 한다. 부트스트랩 씬의 매니저 오브젝트에 배치한다.
/// 퍼즐용 블록 모양의 카탈로그는 BlockRegistry가 따로 맡는다.
/// </summary>
public class FlowerRegistry : Singleton<FlowerRegistry>
{
    [SerializeField, FormerlySerializedAs("allBlocks")] private List<FlowerData> allFlowers = new();

    public IReadOnlyList<FlowerData> AllFlowers => allFlowers;

    public FlowerData GetById(int flowerId)
    {
        return allFlowers.FirstOrDefault(f => f.flowerID == flowerId);
    }
}
