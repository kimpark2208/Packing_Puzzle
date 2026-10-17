using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 밤 손님 한 명의 진단 문제. 손님이 말하는 꽃(flower), 진단 그림을 내는 꽃(art), 그 꽃이 앓는 아픔 1~2개(ailments)를 정한다.
/// 정답은 여기서 정하고, 플레이어가 체크한 아픔과 비교한다(Compare). 손님이 말하는 꽃에 진단용 부위 그림이 없으면 그림이 있는 다른 꽃의 그림을 대신 쓴다.
/// </summary>
public class NightDiagnosisCase
{
    public const int MaxAilments = 2;

    public FlowerData flower;   // 손님이 말하는 꽃(이름)
    public FlowerData art;      // 꽃 그림(부위별)을 내는 꽃. 그림이 없는 꽃이면 그림이 있는 다른 꽃
    public List<FlowerData.Ailment> ailments = new();

    /// <summary>아픔 5종 중 1~2개를 무작위로 골라 문제를 만든다. 꽃잎 결손은 art에 결손 그림이 있을 때만 나온다.</summary>
    public static NightDiagnosisCase Generate(FlowerData flower, FlowerData art)
    {
        var result = new NightDiagnosisCase { flower = flower, art = art };

        bool canLoss = art.lossPetals != null && art.lossPetals.Length > 0;
        var candidates = ((FlowerData.Ailment[])System.Enum.GetValues(typeof(FlowerData.Ailment)))
            .Where(a => a != FlowerData.Ailment.PetalLoss || canLoss)
            .OrderBy(_ => Random.value);
        result.ailments = candidates.Take(Random.value < 0.5f ? 1 : MaxAilments).ToList();

        return result;
    }

    /// <summary>플레이어가 체크한 아픔을 정답과 비교한다: 놓친 아픔 수(missed)와 잘못 체크한 아픔 수(wrong).</summary>
    public void Compare(IEnumerable<FlowerData.Ailment> picked, out int missed, out int wrong)
    {
        var chosen = new HashSet<FlowerData.Ailment>(picked);
        missed = ailments.Count(a => !chosen.Contains(a));
        wrong = chosen.Count(a => !ailments.Contains(a));
    }
}
