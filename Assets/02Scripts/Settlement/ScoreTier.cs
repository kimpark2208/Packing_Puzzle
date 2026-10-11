using UnityEngine;

/// <summary>
/// 점수(-100 ~ +100)를 재화 배율 구간으로 바꾼다. 구간은 결과창 게이지의 눈금(-100, -50, -25, 0, 25, 50, 100)과 같다.
/// 점수의 절댓값이 눈금 이상이면 그 눈금의 구간이다(예: -30 → -25 구간, +60 → +50 구간, -25~25 안쪽 → 0 구간).
/// </summary>
public static class ScoreTier
{
    public const int Min = -100;
    public const int Max = 100;

    public static int Clamp(int score) => Mathf.Clamp(score, Min, Max);

    /// <summary>점수가 속한 눈금.</summary>
    public static int Tick(float score)
    {
        int abs = Mathf.Abs(Mathf.RoundToInt(score));
        int tick = abs >= 100 ? 100 : abs >= 50 ? 50 : abs >= 25 ? 25 : 0;
        return score < 0f ? -tick : tick;
    }

    /// <summary>눈금별 재화 배율. -100은 재화 없음, 0은 기본 보상, +100은 2배.</summary>
    public static float Multiplier(int tick)
    {
        switch (tick)
        {
            case -100: return 0f;
            case -50: return 0.5f;
            case -25: return 0.8f;
            case 0: return 1f;
            case 25: return 1.2f;
            case 50: return 1.5f;
            default: return 2f;
        }
    }
}
