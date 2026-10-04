using UnityEngine;

/// <summary>금액 규칙에 쓰는 수치 모음. 밸런스는 이 에셋에서 조정한다.</summary>
[CreateAssetMenu(fileName = "MoneyRuleSettings", menuName = "Puzzle/MoneyRuleSettings", order = 3)]
public class MoneyRuleSettings : ScriptableObject
{
    [Header("주문 요구조건")]
    [Tooltip("요구 개수보다 더 쓴 꽃 1송이당 가산 금액")]
    public int overAchievePerExtra = 30;
    [Tooltip("요구 미충족 시, 주문에 정해진 보너스의 몇 %를 감산할까(0~1)")]
    [Range(0f, 1f)] public float missedPenaltyRatio = 0.5f;

    [Header("손님 기분 (퍼즐 종료 시 남은 비율)")]
    [Range(0f, 1f)] public float goodMoodRatio = 0.7f;
    public int goodMoodBonus = 50;
    [Range(0f, 1f)] public float badMoodRatio = 0.3f;
    public int badMoodPenalty = 50;

    [Header("붕괴")]
    public int collapsePenalty = 100;
}
