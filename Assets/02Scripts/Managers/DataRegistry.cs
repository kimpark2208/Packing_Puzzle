using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 게임에 존재하는 모든 데이터(SO)의 카탈로그: 꽃(FlowerData), 퍼즐용 블록(BlockData), 포장지(WrapperData), 가구(FurnitureData).
/// 낮/밤 퍼즐 생성기가 공통으로 참조하는 단일 소스(Single Source of Truth)다. 부트스트랩 씬의 매니저 오브젝트에 배치한다.
/// 꽃과 블록은 일대일로 이어지지 않는다(블록이 필요한 퍼즐은 블록 목록에서 골라 조합한다). 포장지는 상점 시스템과는 별개로 번호만 맞춘다.
/// </summary>
public class DataRegistry : Singleton<DataRegistry>
{
    [SerializeField, FormerlySerializedAs("allFlowers")] private List<FlowerData> flowers = new();
    [SerializeField] private List<BlockData> blocks = new();
    [SerializeField] private List<WrapperData> wrappers = new();
    [SerializeField, Tooltip("카탈로그(가구 탭)에 나오는 가구들.")] private List<FurnitureData> furniture = new();
    [SerializeField, Tooltip("꾸미기 용품 전부(카탈로그의 꾸미기 용품 탭, 보유 재고의 기준).")] private List<DecorationData> decorations = new();

    public IReadOnlyList<FlowerData> Flowers => flowers;
    public IReadOnlyList<FurnitureData> Furniture => furniture;
    public IReadOnlyList<DecorationData> Decorations => decorations;

    public FurnitureData GetFurniture(int furnitureId) => furniture.FirstOrDefault(f => f != null && f.id == furnitureId);

    public DecorationData GetDecoration(int decorationId) => decorations.FirstOrDefault(d => d != null && d.id == decorationId);
    public IReadOnlyList<BlockData> Blocks => blocks;

    public FlowerData GetFlower(int flowerId) => flowers.FirstOrDefault(f => f != null && f.flowerID == flowerId);

    public BlockData GetBlock(int blockId) => blocks.FirstOrDefault(b => b != null && b.blockID == blockId);

    public WrapperData GetWrapper(int wrapperId) => wrappers.FirstOrDefault(w => w != null && w.wrapperId == wrapperId);

    /// <summary>블록 목록에서 서로 다른 블록을 count개 무작위로 고른다(목록이 모자라면 있는 만큼).</summary>
    public List<BlockData> PickRandomBlocks(int count)
    {
        return blocks.Where(b => b != null).OrderBy(_ => Random.value).Take(count).ToList();
    }
}
