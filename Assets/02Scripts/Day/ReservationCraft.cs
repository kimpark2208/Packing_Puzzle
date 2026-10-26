using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>예약 꽃다발 제작의 평균 점수 등급(기획서 04 예약 꽃다발 제작).</summary>
public enum ReservationGrade { S, A, B, C, D }

public static class ReservationGrading
{
    /// <summary>평균 점수 → 등급: S +75~, A +25~+74, B -24~+24, C -74~-25, D -75 이하. 수치는 기획서에서 바뀔 수 있으니 이 파일 한 곳에서만 고친다.</summary>
    public static ReservationGrade Of(float averageScore)
    {
        int average = Mathf.RoundToInt(averageScore);
        if (average >= 75) return ReservationGrade.S;
        if (average >= 25) return ReservationGrade.A;
        if (average >= -24) return ReservationGrade.B;
        return average >= -74 ? ReservationGrade.C : ReservationGrade.D;
    }

    /// <summary>보상금에 곱하는 배율: S 2.0, A 1.2, B 1.0, C 0.5, D 0.</summary>
    public static float Multiplier(ReservationGrade grade)
    {
        switch (grade)
        {
            case ReservationGrade.S: return 2f;
            case ReservationGrade.A: return 1.2f;
            case ReservationGrade.B: return 1f;
            case ReservationGrade.C: return 0.5f;
            default: return 0f;
        }
    }
}

/// <summary>
/// 예약일에 하는 예약 꽃다발 제작 하나(UI를 모르는 상태와 규칙): 어떤 예약이고, 꽃다발을 몇 개 만들었고 점수가 얼마인지,
/// 보관해 둔 꽃이 얼마나 남았는지(제작 중에는 이 꽃만 쓴다), 포장지와 필수 용품은 무엇인지.
/// 점수는 꽃다발마다 퍼즐 정산 점수(-100~+100)를 그대로 쌓고, 하루가 끝날 때까지 못 만든 꽃다발은 -100으로 센다.
/// </summary>
public class ReservationCraft
{
    /// <summary>못 만든 꽃다발의 점수.</summary>
    public const int UnmadeScore = ScoreTier.Min;

    public readonly ReservationRecord record;
    private readonly List<int> scores = new();

    public ReservationCraft(ReservationRecord record)
    {
        this.record = record;
    }

    public ReservationData Data => record.data;
    public int BouquetCount => Data.bouquetCount;
    public int Made => scores.Count;
    public int Unmade => Mathf.Max(0, BouquetCount - Made);
    public bool Finished => Made >= BouquetCount;

    /// <summary>만든 꽃다발 점수의 합(주문서에 보이는 누적 점수).</summary>
    public int TotalScore => scores.Sum();

    /// <summary>주문서에 보이는 최대 점수(꽃다발 수 × 100).</summary>
    public int MaxScore => BouquetCount * ScoreTier.Max;

    /// <summary>못 만든 꽃다발을 -100으로 센 총점.</summary>
    public int FinalTotal => TotalScore + Unmade * UnmadeScore;

    public float Average => FinalTotal / (float)BouquetCount;
    public ReservationGrade Grade => ReservationGrading.Of(Average);

    public void AddScore(int score) => scores.Add(score);

    // ---------- 필수 꽃 (보관해 둔 꽃) ----------

    private IEnumerable<int> StoredFlowerIndices => Enumerable.Range(0, Data.items.Length).Where(i => Data.items[i].Storable);

    public bool IsRequired(int flowerId) => StoredFlowerIndices.Any(i => Data.items[i].id == flowerId);

    /// <summary>그 필수 꽃의 남은 보관 수(제작 중에 쓸 수 있는 수).</summary>
    public int Remaining(int flowerId) => StoredFlowerIndices.Where(i => Data.items[i].id == flowerId).Sum(i => record.stored[i]);

    /// <summary>꽃다발에 놓은 필수 꽃만큼 보관 수에서 뺀다.</summary>
    public void Use(int flowerId, int amount)
    {
        foreach (int i in StoredFlowerIndices.Where(i => Data.items[i].id == flowerId))
        {
            int take = Mathf.Min(amount, record.stored[i]);
            record.stored[i] -= take;
            amount -= take;
        }
    }

    public IEnumerable<FlowerData> RequiredFlowers()
    {
        if (DataRegistry.Instance == null) yield break;

        foreach (int id in StoredFlowerIndices.Select(i => Data.items[i].id).Distinct())
        {
            FlowerData flower = DataRegistry.Instance.GetFlower(id);
            if (flower != null) yield return flower;
        }
    }

    /// <summary>필수 꽃의 역할은 직접 고를 수 없다(제작을 시작하면 보관한 필수 꽃이 저절로 담긴다).</summary>
    public bool BlocksRole(FlowerData.FlowerRole role) => RequiredFlowers().Any(f => f.flowerRole == role);

    /// <summary>남은 꽃다발 수만큼 이 프리셋을 계속 만들 수 있는가: 필수 꽃의 역할마다 칸이 있고, 칸 수 × 남은 꽃다발 수가 그 역할의 남은 보관 수 안이다.</summary>
    public bool Fits(WrapperPresetData preset)
    {
        foreach (var group in RequiredFlowers().GroupBy(f => f.flowerRole))
        {
            int cells = preset.CountByTag(group.Key);
            if (cells < 1 || cells * Unmade > group.Sum(f => Remaining(f.flowerID))) return false;
        }
        return true;
    }

    // ---------- 필수 포장지와 용품 ----------

    /// <summary>예약이 요구한 포장지 번호(없으면 0 = 보유한 아무 포장지).</summary>
    public int WrapperId => Data.items.FirstOrDefault(i => i.kind == ReservationData.ItemKind.Wrapper && i.id > 0).id;

    /// <summary>예약이 요구한 꽃다발 꾸미기 용품(꽃다발에 직접 놓아야 한다).</summary>
    public IEnumerable<DecorationData> Supplies()
    {
        if (DataRegistry.Instance == null) yield break;

        foreach (ReservationData.Item item in Data.items.Where(i => i.kind == ReservationData.ItemKind.Supply && i.id > 0))
        {
            DecorationData decoration = DataRegistry.Instance.GetDecoration(item.id);
            if (decoration != null) yield return decoration;
        }
    }

    /// <summary>주문서 글: 제목, 만든 꽃다발 수, 누적 점수.</summary>
    public string OrderSheetText()
    {
        return $"<b>{Data.title} 예약 주문</b>\n\n현재 제작한 꽃다발 개수: ({Made}/{BouquetCount})개\n현재 누적 꽃다발 점수: ({TotalScore}/{MaxScore})점";
    }
}
