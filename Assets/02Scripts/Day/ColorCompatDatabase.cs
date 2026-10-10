/// <summary>
/// 이웃한 두 꽃의 색 관계 점수(FlowerLight가 쓴다). FlowerData.Color의 선언 순서가 색상환 순서(빨강-주황-노랑-파랑-보라-분홍)이고 흰색이 맨 뒤다.
/// 조화(색상환에서 바로 옆 색) +2, 대비(맞은편 색) +2, 중재(한쪽이 흰색) +1, 그 외(같은 색 포함) 0. 감점은 없다.
/// </summary>
public static class ColorCompatDatabase
{
    private const int Harmony = 2, Contrast = 2, Mediation = 1;

    /// <summary>두 색상의 관계 점수.</summary>
    public static int GetBonus(FlowerData.Color a, FlowerData.Color b)
    {
        if (a == FlowerData.Color.White || b == FlowerData.Color.White) return Mediation;

        int gap = System.Math.Abs((int)a - (int)b); // 색상환 위 거리: 1·5 = 바로 옆, 3 = 맞은편
        return gap == 1 || gap == 5 ? Harmony : gap == 3 ? Contrast : 0;
    }
}
