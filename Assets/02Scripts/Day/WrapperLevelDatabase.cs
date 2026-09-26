using System.Collections.Generic;

/// <summary>
/// 포장지 레벨(1~7)별 요구 태그 구성. 예시 수치이며 추후 밸런싱이 필요하다
/// (기획서 "07 포장지 레벨" 표 기준).
/// </summary>
public static class WrapperLevelDatabase
{
    /// <summary>영역 하나. leftCount/rightCount는 좌/우 면에 배치되는 슬롯 수, centerCount는 소속 면이 없는(정중앙) 슬롯 수.</summary>
    public struct RegionDef
    {
        public BlockData.FlowerTag tag;
        public int centerCount;
        public int leftCount;
        public int rightCount;

        public int Total => centerCount + leftCount + rightCount;
    }

    public class LevelDef
    {
        public int tier;
        public List<RegionDef> regions = new();
        public int maxRerolls;

        public int TotalSlots
        {
            get
            {
                int total = 0;
                foreach (var r in regions) total += r.Total;
                return total;
            }
        }
    }

    private static readonly List<LevelDef> Levels = new()
    {
        new LevelDef
        {
            tier = 1,
            maxRerolls = 3,
            regions = new List<RegionDef>
            {
                new() { tag = BlockData.FlowerTag.Mass, leftCount = 1, rightCount = 1 },
                new() { tag = BlockData.FlowerTag.Filler, leftCount = 1, rightCount = 0 },
            }
        },
        new LevelDef
        {
            tier = 2,
            maxRerolls = 4,
            regions = new List<RegionDef>
            {
                new() { tag = BlockData.FlowerTag.Mass, leftCount = 2, rightCount = 1 },
                new() { tag = BlockData.FlowerTag.Filler, leftCount = 0, rightCount = 2 },
            }
        },
        new LevelDef
        {
            tier = 3,
            maxRerolls = 4,
            regions = new List<RegionDef>
            {
                new() { tag = BlockData.FlowerTag.Line, centerCount = 1 },
                new() { tag = BlockData.FlowerTag.Mass, leftCount = 1, rightCount = 1 },
                new() { tag = BlockData.FlowerTag.Filler, leftCount = 1, rightCount = 0 },
            }
        },
        new LevelDef
        {
            tier = 4,
            maxRerolls = 5,
            regions = new List<RegionDef>
            {
                new() { tag = BlockData.FlowerTag.Line, leftCount = 1, rightCount = 1 },
                new() { tag = BlockData.FlowerTag.Mass, leftCount = 2, rightCount = 1 },
                new() { tag = BlockData.FlowerTag.Filler, leftCount = 1, rightCount = 1 },
            }
        },
        new LevelDef
        {
            tier = 5,
            maxRerolls = 5,
            regions = new List<RegionDef>
            {
                new() { tag = BlockData.FlowerTag.Line, centerCount = 1 },
                new() { tag = BlockData.FlowerTag.Mass, leftCount = 1, rightCount = 1 },
                new() { tag = BlockData.FlowerTag.Form, leftCount = 1, rightCount = 0 },
                new() { tag = BlockData.FlowerTag.Filler, leftCount = 1, rightCount = 1 },
            }
        },
        new LevelDef
        {
            tier = 6,
            maxRerolls = 6,
            regions = new List<RegionDef>
            {
                new() { tag = BlockData.FlowerTag.Line, leftCount = 1, rightCount = 1 },
                new() { tag = BlockData.FlowerTag.Mass, leftCount = 2, rightCount = 1 },
                new() { tag = BlockData.FlowerTag.Form, leftCount = 1, rightCount = 1 },
                new() { tag = BlockData.FlowerTag.Filler, leftCount = 1, rightCount = 1 },
            }
        },
        new LevelDef
        {
            tier = 7,
            maxRerolls = 6,
            regions = new List<RegionDef>
            {
                new() { tag = BlockData.FlowerTag.Line, centerCount = 1, leftCount = 1, rightCount = 1 },
                new() { tag = BlockData.FlowerTag.Mass, leftCount = 2, rightCount = 2 },
                new() { tag = BlockData.FlowerTag.Form, leftCount = 1, rightCount = 1 },
                new() { tag = BlockData.FlowerTag.Filler, leftCount = 1, rightCount = 1 },
            }
        },
    };

    /// <summary>tier(1부터)에 해당하는 레벨 정의. 범위를 벗어나면 가장 가까운 끝 레벨로 클램프.</summary>
    public static LevelDef GetLevel(int tier)
    {
        int index = UnityEngine.Mathf.Clamp(tier - 1, 0, Levels.Count - 1);
        return Levels[index];
    }
}
