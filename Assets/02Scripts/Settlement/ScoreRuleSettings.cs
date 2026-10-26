using UnityEngine;

/// <summary>점수 규칙에 쓰는 수치 모음(단위는 점수). 밸런스는 이 에셋에서 조정한다. 점수가 재화 배율로 바뀌는 구간은 ScoreTier에 있다.</summary>
[CreateAssetMenu(fileName = "ScoreRuleSettings", menuName = "Puzzle/ScoreRuleSettings", order = 3)]
public class ScoreRuleSettings : ScriptableObject
{
    [Header("꽃의 빛 (꽃 상태별 점수. 상태는 이웃 꽃과의 색 관계로 정해진다)")]
    [Tooltip("흐림(관계 합계 0 이하) 꽃 한 송이의 점수")]
    public int flowerLightDimScore = -2;
    [Tooltip("선명(관계 합계 1~2) 꽃 한 송이의 점수")]
    public int flowerLightClearScore = 1;
    [Tooltip("빛남(관계 합계 3 이상) 꽃 한 송이의 점수")]
    public int flowerLightBrightScore = 3;
    [Tooltip("꽃의 빛 점수 = 꽃 한 송이당 평균 상태 점수 × 이 값(기본 10이면 전부 흐림 -20 ~ 전부 빛남 +30, 포장지 크기와 상관없다)")]
    public int flowerLightScale = 10;

    [Header("주문 요구조건")]
    [Tooltip("요구조건을 채웠을 때 가산 점수")]
    public int requirementMetScore = 15;
    [Tooltip("요구 개수보다 더 쓴 꽃 1송이당 가산 점수")]
    public int overAchievePerExtra = 3;
    [Tooltip("요구 미충족 시 감산 점수. 2링 포장지(9칸)에서 다른 항목을 모두 잘해도(꽃의 빛 최대 +30, 기분 +8) 합계가 기본 보상 구간의 끝(+24) 아래에 머문다. 꽃의 빛은 포장지 크기와 상관없이 -20~+30이다.")]
    public int requirementMissedPenalty = 25;

    [Header("손님 기분 (퍼즐 종료 시 남은 비율)")]
    [Range(0f, 1f)] public float goodMoodRatio = 0.7f;
    public int goodMoodBonus = 8;
    [Range(0f, 1f)] public float badMoodRatio = 0.3f;
    public int badMoodPenalty = 8;

    [Header("붕괴")]
    public int collapsePenalty = 15;

    [Header("예약 꽃다발")]
    [Tooltip("예약이 요구한 꾸미기 용품을 꽃다발에 놓지 않았을 때 감산 점수(기획서: -30)")]
    public int supplyMissingPenalty = 30;
}
