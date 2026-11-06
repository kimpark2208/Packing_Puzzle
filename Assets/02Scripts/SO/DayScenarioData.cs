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

/// <summary>베타 시나리오의 하루(일차) 하나: 그날 오는 정해진 손님, 예약 전화, 해금. 정해진 것이 없으면 평소처럼 무작위 손님이 온다.</summary>
[CreateAssetMenu(fileName = "DayScenario", menuName = "Puzzle/DayScenarioData", order = 7)]
public class DayScenarioData : ScriptableObject
{
    [Min(1)] public int day = 1;

    [Header("낮")]
    [Tooltip("그날 정해진 낮 손님들. 여기에 없는 순번은 무작위 손님이다.")] public DayVisit[] dayVisits = Array.Empty<DayVisit>();
    [Header("예약 전화")]
    [Tooltip("이 날 낮에 손님과 손님 사이에 걸려 오는 예약 전화. 비우면 오지 않는다.")] public ReservationData reservationCall;
    [Min(1), Tooltip("몇 번째 손님을 마친 뒤(그 손님이 떠나고 다음 손님이 오기 전)에 전화가 오는가. 1이면 첫 손님 뒤.")] public int reservationCallAfterCustomers = 1;

    [Header("해금(이 일차부터 켜진다)")]
    public bool unlocksPhone;
    public bool unlocksCalendar;
}
