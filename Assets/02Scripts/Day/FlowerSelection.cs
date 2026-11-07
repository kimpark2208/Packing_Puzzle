using System;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

/// <summary>
/// 꽃 선정 화면의 선택 규칙(UI를 모르는 순수 계산). 포장지 프리셋의 칸마다 꽃을 한 송이씩 미리 채운다.
/// - 꽃을 고르면 그 꽃의 역할과 같은 역할의 빈 칸 중 무작위 한 칸에 채워진다. 같은 꽃을 여러 칸에 채워도 된다.
/// - 한 꽃은 보유 수량(재고)만큼만 채울 수 있다.
/// - 채운 칸은 비울 수 있고, 프리셋의 모든 칸이 차면 완성이다. 완성된 꽃 목록(송이 수까지)이 그대로 퍼즐의 꽃 공급이 된다(FlowerPool).
/// </summary>
public class FlowerSelection
{
    private readonly WrapperPresetData preset;
    private readonly Func<FlowerData, int> stockOf;
    private readonly FlowerData[] cells; // 칸 번호(프리셋의 cellRoles 순서) → 채운 꽃(빈 칸은 null)

    public FlowerSelection(WrapperPresetData preset, Func<FlowerData, int> stockOf)
    {
        this.preset = preset;
        this.stockOf = stockOf;
        cells = new FlowerData[preset != null ? preset.cellRoles.Length : 0];
    }

    public int CellCount => cells.Length;

    /// <summary>채운 칸 수.</summary>
    public int FilledCount => cells.Count(c => c != null);

    public FlowerData At(int cell) => cells[cell];
    public FlowerData.FlowerRole RoleAt(int cell) => preset.RoleAt(cell);

    /// <summary>그 역할의 칸 수(프리셋에서)와 그중 채운 칸 수.</summary>
    public int CellsOf(FlowerData.FlowerRole role) => preset != null ? preset.CountByTag(role) : 0;
    public int FilledOf(FlowerData.FlowerRole role) => Enumerable.Range(0, cells.Length).Count(i => cells[i] != null && RoleAt(i) == role);

    /// <summary>그 꽃을 몇 칸에 채웠는가.</summary>
    public int Count(FlowerData flower) => cells.Count(c => c == flower);

    /// <summary>모든 칸이 찼는가(칸이 하나도 없으면 완성이 아니다).</summary>
    public bool IsComplete => cells.Length > 0 && cells.All(c => c != null);

    /// <summary>이 꽃을 한 칸 더 채울 수 있는가: 그 역할에 빈 칸이 있고, 채운 수가 재고보다 적다.</summary>
    public bool CanAdd(FlowerData flower)
    {
        return FilledOf(flower.flowerRole) < CellsOf(flower.flowerRole) && Count(flower) < stockOf(flower);
    }

    /// <summary>꽃을 그 역할의 빈 칸 중 무작위 한 칸에 채운다. 채운 칸 번호를 돌려주고, 채울 수 없으면 -1.</summary>
    public int Add(FlowerData flower)
    {
        if (!CanAdd(flower)) return -1;

        var empty = Enumerable.Range(0, cells.Length).Where(i => cells[i] == null && RoleAt(i) == flower.flowerRole).ToList();
        int cell = empty[Random.Range(0, empty.Count)];
        cells[cell] = flower;
        return cell;
    }

    /// <summary>예약 꽃다발의 필수 꽃: 그 역할의 빈 칸을 모두 그 꽃으로 채운다(재고가 모자라면 되는 만큼만). 채운 칸 수를 돌려준다.</summary>
    public int FillRole(FlowerData flower)
    {
        int filled = 0;
        while (Add(flower) >= 0) filled++;
        return filled;
    }

    /// <summary>채운 칸을 비운다. 채워져 있었으면 true.</summary>
    public bool Clear(int cell)
    {
        if (cells[cell] == null) return false;

        cells[cell] = null;
        return true;
    }

    /// <summary>채운 꽃 전부(송이 수만큼 같은 꽃이 반복된다). 퍼즐에 넘기는 꽃 공급이다.</summary>
    public List<FlowerData> Chosen() => cells.Where(c => c != null).ToList();
}
