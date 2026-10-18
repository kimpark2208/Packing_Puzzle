using System;
using UnityEngine;

/// <summary>낮 손님 방문 하나: 그날 몇 번째 손님으로 NPC가 오는지와 그 대사. 고정 주문(꽃·포장지)은 대사 CSV의 `주문:` 효과에 적는다.</summary>
[Serializable]
public struct DayVisit
{
    [Min(1), Tooltip("그날 몇 번째 방문자인가(1이 첫 방문자, 상인도 한 번으로 센다).")] public int orderIndex;
    public NpcData npc;
    [Tooltip("등장·주문 대화ID(CSV).")] public string dialogueId;
    [Tooltip("친밀 대사 대화ID(CSV, 선택).")] public string bondDialogueId;
    [Tooltip("꽃다발 점수에 따른 반응 대사의 앞부분(예: jaemin_react → jaemin_react_perfect/success/mistake/fail). 선택.")] public string reactionPrefix;
}

/// <summary>밤 손님 방문 하나. npc가 없으면 모브 손님(대사 없이 밤 주문 템플릿)이다.</summary>
[Serializable]
public struct NightVisit
{
    public NpcData npc;
    [Tooltip("등장 대화ID(CSV, 선택). 모브 손님은 비운다.")] public string dialogueId;
    [Tooltip("수리 없이 대화만 하고 간다(1일차 세이렌의 소개).")] public bool dialogueOnly;
    [Tooltip("이 손님이 들고 오는 꽃. 비우면 보통 규칙(부적을 붙인 꽃)으로 정한다.")] public FlowerData flower;
    [Min(0), Tooltip("수리가 끝나면 확정으로 주는 복제품 수. 0이면 보통 규칙(맞게 체크한 아픔 수 × 3)을 쓴다.")] public int fixedReplicas;
    [Tooltip("수리 결과 반응 대사의 앞부분(예: siren_react → siren_react_success/mistake/fail). 선택.")] public string reactionPrefix;
}

/// <summary>베타 시나리오의 하루(일차) 하나: 그날 오는 정해진 손님, 확정 지급, 해금, 튜토리얼. 정해진 것이 없으면 평소처럼 무작위 손님이 온다.</summary>
[CreateAssetMenu(fileName = "DayScenario", menuName = "Puzzle/DayScenarioData", order = 7)]
public class DayScenarioData : ScriptableObject
{
    [Min(1)] public int day = 1;

    [Header("낮")]
    [Tooltip("낮 장사 없이 낮 손님 대화만 하고 곧바로 밤으로 간다(1일차).")] public bool skipDayBusiness;
    [Tooltip("그날 정해진 낮 손님들. 여기에 없는 순번은 무작위 손님이다.")] public DayVisit[] dayVisits = Array.Empty<DayVisit>();

    [Header("밤")]
    [Tooltip("밤 시간이 흐르지 않고, 정해진 밤 손님을 모두 마쳐야 밤이 끝난다. 끝나면 하루 결산 없이 다음 날 낮으로 간다(1일차).")] public bool freeNight;
    [Tooltip("그날 정해진 밤 손님들(순서대로). 있으면 부적 꽃 대신 이 손님들이 먼저 온다.")] public NightVisit[] nightVisits = Array.Empty<NightVisit>();

    [Header("해금(이 일차부터 켜진다)")]
    public bool unlocksPhone;
    public bool unlocksCalendar;

    [Header("연출")]
    [Tooltip("그날 낮 → 밤 전환 소리를 이 소리로 바꾼다(6일차 세이렌 노랫소리). 비우면 기본 소리.")] public AudioClip dayToNightSound;
}
