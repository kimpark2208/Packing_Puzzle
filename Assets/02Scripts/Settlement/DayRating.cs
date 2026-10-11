/// <summary>
/// 하루 평점(별 5개)과 그에 따른 보상(낮 종료 → 하루 종료 기획서 4-2). 별은 그날 퍼즐 점수(-100~+100)의 평균으로 정하고,
/// 평점 보상은 부적 개수와 순수입에 곱하는 재화 배수다. 수치는 기획서에서 바뀔 수 있으니 이 파일 한 곳에서만 고친다.
/// </summary>
public static class DayRating
{
    public const int MaxStars = 5;

    /// <summary>평균 점수 → 별 개수: 0 미만 1개, 0~29 2개, 30~59 3개, 60~89 4개, 90 이상 5개.</summary>
    public static int Stars(float averageScore)
    {
        if (averageScore < 0f) return 1;
        if (averageScore < 30f) return 2;
        if (averageScore < 60f) return 3;
        if (averageScore < 90f) return 4;
        return MaxStars;
    }

    /// <summary>별 개수에 따라 받는 부적: 3개 1, 4개 2, 5개 2, 그 아래는 없음.</summary>
    public static int CharmReward(int stars)
    {
        if (stars >= 4) return 2;
        return stars == 3 ? 1 : 0;
    }

    /// <summary>별 개수에 따라 순수입에 곱하는 재화 배수: 5개만 1.5배, 나머지는 그대로.</summary>
    public static float MoneyMultiplier(int stars)
    {
        return stars >= 5 ? 1.5f : 1f;
    }
}
