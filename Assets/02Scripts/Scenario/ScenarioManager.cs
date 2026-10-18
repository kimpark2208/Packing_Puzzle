using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 베타 시나리오: 일차별 정해진 손님·보상·해금(DayScenarioData)과 대사 CSV를 들고 있다가, 게임의 다른 곳이 "오늘 무슨 일이 있나"를 물으면 알려 준다.
/// 부트스트랩 씬(00Start)에 두고 씬 전환 간 유지한다. 해금은 따로 저장하지 않고 일수에서 계산한다(저장 기능이 생기면 그때 바꾼다).
/// 정해진 것이 없으면 아무것도 돌려주지 않아, 호출한 쪽이 평소처럼 무작위로 하게 한다.
/// </summary>
public class ScenarioManager : Singleton<ScenarioManager>
{
    [SerializeField, Tooltip("대사 CSV(Assets/05SO/Dialogue/Dialogue.csv).")] private TextAsset dialogueCsv;
    [SerializeField, Tooltip("일차별 시나리오(1~10일차).")] private List<DayScenarioData> days = new();

    private DialogueDatabase dialogue;

    public IReadOnlyList<DayScenarioData> Days => days;

    /// <summary>대사 데이터베이스(처음 쓸 때 CSV를 읽는다). CSV를 고쳤으면 에디터에서 Play를 다시 시작해야 반영된다.</summary>
    public DialogueDatabase Dialogue
    {
        get
        {
            if (dialogue != null) return dialogue;

            var errors = new List<string>();
            dialogue = dialogueCsv != null ? DialogueDatabase.Parse(dialogueCsv.text, errors) : new DialogueDatabase();
            foreach (string error in errors) Debug.LogWarning($"[ScenarioManager] 대사 CSV: {error}");
            return dialogue;
        }
    }

    /// <summary>그 일차의 시나리오. 없으면 null(평소처럼 무작위로 진행).</summary>
    public DayScenarioData ForDay(int day) => days.FirstOrDefault(d => d != null && d.day == day);

    /// <summary>그날 낮의 index번째 손님(1부터)이 정해진 NPC면 그 방문, 아니면 null(무작위 손님).</summary>
    public DayVisit? DayVisitAt(int day, int index)
    {
        DayScenarioData scenario = ForDay(day);
        if (scenario == null) return null;

        foreach (DayVisit visit in scenario.dayVisits)
        {
            if (visit.orderIndex == index) return visit;
        }
        return null;
    }

    /// <summary>전화기를 쓸 수 있는가(전화기가 해금된 일차부터).</summary>
    public bool IsPhoneUnlocked(int day) => days.Any(d => d != null && d.day <= day && d.unlocksPhone);

    /// <summary>달력을 쓸 수 있는가(달력이 해금된 일차부터).</summary>
    public bool IsCalendarUnlocked(int day) => days.Any(d => d != null && d.day <= day && d.unlocksCalendar);
}
