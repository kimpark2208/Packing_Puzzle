using UnityEngine;

/// <summary>점수 규칙에 쓰는 수치 모음(단위는 점수). 밸런스는 이 에셋에서 조정한다. 점수가 재화 배율로 바뀌는 구간은 ScoreTier에 있다.</summary>
[CreateAssetMenu(fileName = "ScoreRuleSettings", menuName = "Puzzle/ScoreRuleSettings", order = 3)]
public class ScoreRuleSettings : ScriptableObject
{
    [Header("주문 요구조건")]
    [Tooltip("요구조건을 채웠을 때 가산 점수")]
    public int requirementMetScore = 15;
    [Tooltip("요구 개수보다 더 쓴 꽃 1송이당 가산 점수")]
    public int overAchievePerExtra = 3;
    [Tooltip("요구 미충족 시, 충족 점수의 몇 %를 감산할까(0~1)")]
    [Range(0f, 1f)] public float missedPenaltyRatio = 0.5f;

    [Header("손님 기분 (퍼즐 종료 시 남은 비율)")]
    [Range(0f, 1f)] public float goodMoodRatio = 0.7f;
    public int goodMoodBonus = 8;
    [Range(0f, 1f)] public float badMoodRatio = 0.3f;
    public int badMoodPenalty = 8;

    [Header("붕괴")]
    public int collapsePenalty = 15;
}
