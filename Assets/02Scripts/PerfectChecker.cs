using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 그리드에 배치된 블록들을 모아서, 모양별(blockID별) 개수가 레시피와 정확히 일치하는지 판정한다.
// 칸의 위치나 배치 형태는 보지 않고, "어떤 모양이 몇 개 쓰였는가"만 비교한다.
public class PerfectChecker : MonoBehaviour
{
    [Header("완성 판정 대상 그리드")]
    public GridManager grid;

    [Header("정답 레시피")]
    public PuzzleRecipe recipe;

    // 결과: 성공 여부와, 어떤 blockID가 몇 개 남거나 모자라는지를 함께 반환해서 디버깅/UI에 활용 가능하게 한다.
    public struct CheckResult
    {
        public bool isPerfect;
        public Dictionary<int, int> requiredCounts;   // blockID -> 필요 개수
        public Dictionary<int, int> actualCounts;     // blockID -> 실제 개수
    }

    public CheckResult CheckPerfect()
    {
        var result = new CheckResult
        {
            requiredCounts = recipe != null ? recipe.BuildRequiredCountMap() : new Dictionary<int, int>(),
            actualCounts = CountPlacedBlocksByID()
        };

        result.isPerfect = AreCountsEqual(result.requiredCounts, result.actualCounts);
        return result;
    }

    // 현재 그리드에 배치되어 있는 모든 BlockDrag를 찾아 blockID별로 개수를 센다.
    private Dictionary<int, int> CountPlacedBlocksByID()
    {
        var counts = new Dictionary<int, int>();
        if (grid == null) return counts;

        // 그리드 오브젝트의 자식으로 배치된 블록들을 전부 찾는다.
        BlockDrag[] placedBlocks = grid.GetComponentsInChildren<BlockDrag>();

        foreach (var block in placedBlocks)
        {
            if (block == null || block.blockData == null) continue;

            int id = block.blockData.blockID;
            if (!counts.ContainsKey(id)) counts[id] = 0;
            counts[id]++;
        }

        return counts;
    }

    // 두 딕셔너리(필요 개수 vs 실제 개수)가 모든 키에 대해 정확히 같은 값을 가지는지 비교한다.
    // 한쪽에만 있는 키가 있어도(필요 없는 블록을 쓨거나, 필요한데 안 쓐) 실패로 처리한다.
    private bool AreCountsEqual(Dictionary<int, int> required, Dictionary<int, int> actual)
    {
        var allKeys = new HashSet<int>(required.Keys);
        allKeys.UnionWith(actual.Keys);

        foreach (int key in allKeys)
        {
            int req = required.TryGetValue(key, out int r) ? r : 0;
            int act = actual.TryGetValue(key, out int a) ? a : 0;
            if (req != act) return false;
        }

        return true;
    }
}
