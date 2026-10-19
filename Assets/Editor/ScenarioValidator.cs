using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 메뉴 Tools/Scenario/Validate: 대사 CSV와 일차별 시나리오(DayScenarioData)의 오타·빠진 연결을 한꺼번에 찾아 콘솔에 적는다.
/// CSV를 고칠 때마다 한 번 돌려 보면 좋다.
/// </summary>
public static class ScenarioValidator
{
    private const string CsvPath = "Assets/05SO/Dialogue/Dialogue.csv";
    private static readonly string[] CatalogTargets = { "가구", "용품" };
    private static readonly string[] UnlockTargets = { "전화기", "달력" };
    private static readonly string[] ReactionSuffixes = { "perfect", "success", "mistake", "fail" };

    [MenuItem("Tools/Scenario/Validate")]
    private static void Validate()
    {
        var problems = new List<string>();

        var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(CsvPath);
        if (csv == null)
        {
            Debug.LogError($"[Scenario] 대사 CSV가 없습니다: {CsvPath}");
            return;
        }

        var parseErrors = new List<string>();
        DialogueDatabase database = DialogueDatabase.Parse(csv.text, parseErrors);
        problems.AddRange(parseErrors);

        HashSet<string> flowerNames = AssetDatabase.FindAssets("t:FlowerData")
            .Select(g => AssetDatabase.LoadAssetAtPath<FlowerData>(AssetDatabase.GUIDToAssetPath(g))).Where(f => f != null).Select(f => f.DisplayName).ToHashSet();
        HashSet<int> wrapperIds = AssetDatabase.FindAssets("t:WrapperData")
            .Select(g => AssetDatabase.LoadAssetAtPath<WrapperData>(AssetDatabase.GUIDToAssetPath(g))).Where(w => w != null).Select(w => w.wrapperId).ToHashSet();

        foreach (Dialogue dialogue in database.All)
        {
            for (int i = 0; i < dialogue.lines.Count; i++)
            {
                DialogueLine line = dialogue.lines[i];
                string where = $"{dialogue.id} {i + 1}번째 줄";
                if (string.IsNullOrWhiteSpace(line.text)) problems.Add($"{where}: 대사가 비어 있습니다.");
                if (line.answers.Length == 1) problems.Add($"{where}: 응답이 하나뿐입니다(둘 다 쓰거나 둘 다 비워 주세요).");

                foreach (DialogueEffect effect in line.effects) CheckEffect(effect, where, flowerNames, wrapperIds, problems);
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:DayScenarioData"))
        {
            var scenario = AssetDatabase.LoadAssetAtPath<DayScenarioData>(AssetDatabase.GUIDToAssetPath(guid));
            string where = $"{scenario.name}(day {scenario.day})";
            foreach (DayVisit visit in scenario.dayVisits)
            {
                CheckDialogueId(database, visit.dialogueId, where, problems, required: true);
                CheckDialogueId(database, visit.bondDialogueId, where, problems);
                CheckReaction(database, visit.reactionPrefix, where, problems);
            }
            foreach (NightVisit visit in scenario.nightVisits)
            {
                CheckDialogueId(database, visit.dialogueId, where, problems);
                CheckReaction(database, visit.reactionPrefix, where, problems);
            }
        }

        if (problems.Count == 0) Debug.Log($"[Scenario] 문제 없음 — 대화 {database.All.Count()}개, 줄 {database.All.Sum(d => d.lines.Count)}개");
        else Debug.LogWarning($"[Scenario] 문제 {problems.Count}개\n- " + string.Join("\n- ", problems));
    }

    private static void CheckEffect(DialogueEffect effect, string where, HashSet<string> flowerNames, HashSet<int> wrapperIds, List<string> problems)
    {
        if (!DialogueEffect.AllKinds.Contains(effect.kind))
        {
            problems.Add($"{where}: 알 수 없는 효과 '{effect}' (쓸 수 있는 효과: {string.Join(", ", DialogueEffect.AllKinds)})");
            return;
        }

        switch (effect.kind)
        {
            case DialogueEffect.Gift:
                if (!wrapperIds.Contains(effect.WrapperNumber)) problems.Add($"{where}: 선물 대상 '{effect.target}'은 포장지N 형식이거나 없는 번호입니다.");
                break;
            case DialogueEffect.Order:
                if (effect.target.StartsWith("포장지")) { if (!wrapperIds.Contains(effect.WrapperNumber)) problems.Add($"{where}: 주문 포장지 '{effect.target}'이 없습니다."); }
                else if (!flowerNames.Contains(effect.target)) problems.Add($"{where}: 주문 꽃 '{effect.target}'이 꽃 목록에 없습니다.");
                break;
            case DialogueEffect.GrantFlower:
                if (!flowerNames.Contains(effect.target)) problems.Add($"{where}: 꽃지급 꽃 '{effect.target}'이 꽃 목록에 없습니다.");
                break;
            case DialogueEffect.Catalog:
                if (!CatalogTargets.Contains(effect.target)) problems.Add($"{where}: 카탈로그 대상 '{effect.target}'은 가구 또는 용품이어야 합니다.");
                break;
            case DialogueEffect.Unlock:
                if (!UnlockTargets.Contains(effect.target)) problems.Add($"{where}: 해금 대상 '{effect.target}'은 {string.Join(" 또는 ", UnlockTargets)}이어야 합니다.");
                break;
            case DialogueEffect.Tutorial:
                if (string.IsNullOrEmpty(effect.target)) problems.Add($"{where}: 튜토리얼 ID가 비어 있습니다.");
                break;
        }
    }

    private static void CheckDialogueId(DialogueDatabase database, string id, string where, List<string> problems, bool required = false)
    {
        if (string.IsNullOrEmpty(id)) { if (required) problems.Add($"{where}: 대화ID가 비어 있습니다."); return; }
        if (!database.Has(id)) problems.Add($"{where}: 대화ID '{id}'가 CSV에 없습니다.");
    }

    private static void CheckReaction(DialogueDatabase database, string prefix, string where, List<string> problems)
    {
        if (string.IsNullOrEmpty(prefix)) return;
        if (!ReactionSuffixes.Any(s => database.Has($"{prefix}_{s}"))) problems.Add($"{where}: 반응 대사 '{prefix}_perfect/success/mistake/fail'이 CSV에 하나도 없습니다.");
    }
}
