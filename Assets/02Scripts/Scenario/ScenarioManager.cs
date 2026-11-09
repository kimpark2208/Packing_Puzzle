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
    [SerializeField, Tooltip("반응 대사 CSV(Assets/05SO/Dialogue/Reaction.csv). 열은 대사 CSV와 같고, 대화ID는 `접두어_perfect/success/mistake/fail` 꼴이다.")] private TextAsset reactionCsv;
    [SerializeField, Tooltip("일차별 시나리오(1~10일차).")] private List<DayScenarioData> days = new();

    [SerializeField, Tooltip("꺼 두면 시나리오(정해진 손님, 예약 전화, 전화기·달력 해금)가 모두 없는 것처럼 동작해, 1일차부터 무작위 손님과 퍼즐만 나온다.")] private bool scenarioEnabled;

    private DialogueDatabase dialogue;

    /// <summary>대사 데이터베이스(처음 쓸 때 CSV를 읽는다). CSV를 고쳤으면 에디터에서 Play를 다시 시작해야 반영된다.</summary>
    public DialogueDatabase Dialogue
    {
        get
        {
            if (dialogue != null) return dialogue;

            var errors = new List<string>();
            dialogue = dialogueCsv != null ? DialogueDatabase.Parse(dialogueCsv.text, errors) : new DialogueDatabase();
            if (reactionCsv != null) dialogue.AddFrom(DialogueDatabase.Parse(reactionCsv.text, errors), errors);
            foreach (string error in errors) Debug.LogWarning($"[ScenarioManager] 대사 CSV: {error}");
            return dialogue;
        }
    }

    /// <summary>그 일차의 시나리오. 없으면 null(평소처럼 무작위로 진행).</summary>
    public DayScenarioData ForDay(int day) => scenarioEnabled ? days.FirstOrDefault(d => d != null && d.day == day) : null;

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

    /// <summary>일차 데이터에 나오는 NPC 중 이름(NpcData.displayName)이 같은 것. 없으면 null.</summary>
    public NpcData NpcByName(string displayName) => days.Where(d => d != null)
        .SelectMany(d => d.dayVisits.Select(v => v.npc).Append(d.nightNpc))
        .FirstOrDefault(n => n != null && n.displayName == displayName);

    /// <summary>전화기를 쓸 수 있는가(전화기가 해금된 일차부터).</summary>
    public bool IsPhoneUnlocked(int day) => scenarioEnabled && days.Any(d => d != null && d.day <= day && d.unlocksPhone);

    /// <summary>그 카탈로그("가구"/"용품")를 받았는가: day일차까지의 정해진 방문 대사에 `카탈로그:대상` 효과가 있으면 받은 것으로 계산한다(일수를 건너뛰어도 맞게).</summary>
    public bool IsCatalogUnlockedByDay(string target, int day)
    {
        if (!scenarioEnabled) return false;

        foreach (DayScenarioData d in days.Where(d => d != null && d.day < day)) // 오늘 받는 카탈로그는 대사 효과(UnlockCatalog)가 처리한다. 여기는 지난 날만 본다
        {
            foreach (DayVisit visit in d.dayVisits)
            {
                foreach (string id in new[] { visit.introDialogueId, visit.dialogueId })
                {
                    Dialogue dlg = Dialogue.Get(id);
                    if (dlg != null && dlg.lines.Any(l => l.effects.Any(e => e.kind == DialogueEffect.Catalog && e.target == target))) return true;
                }
            }
        }
        return false;
    }

    /// <summary>달력을 쓸 수 있는가(달력이 해금된 일차부터).</summary>
    public bool IsCalendarUnlocked(int day) => scenarioEnabled && days.Any(d => d != null && d.day <= day && d.unlocksCalendar);
}
