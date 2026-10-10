/// <summary>점수 사유와 점수를 결과 화면에 보여 줄 문구로 바꾼다(표시 전용).</summary>
public static class SettlementFormatter
{
    public static string Label(ScoreReason reason)
    {
        switch (reason)
        {
            case ScoreReason.ColorCombo: return "색 조합 보너스";
            case ScoreReason.RequirementMet: return "요구사항 달성";
            case ScoreReason.RequirementOver: return "요구사항 초과 달성";
            case ScoreReason.MoodGood: return "손님 기분 좋음";
            case ScoreReason.Collapse: return "꽃다발 붕괴";
            case ScoreReason.MoodBad: return "손님 기분 나쁨";
            case ScoreReason.RequirementMissed: return "요구사항 미달성";
            default: return reason.ToString();
        }
    }

    public static string Signed(int value)
    {
        return value > 0 ? $"+{value}" : value.ToString();
    }
}
