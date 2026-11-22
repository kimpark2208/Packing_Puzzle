using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>대사 한 줄이 끝날 때 일어나는 일. CSV의 효과 칸에서 읽는다: `효과:대상` 또는 `효과:대상x수량`(수량을 안 쓰면 1), 대상이 없는 효과는 낱말만(`퇴장`).</summary>
public readonly struct DialogueEffect
{
    public const string Gift = "획득";           // 획득:포장지1x30 / 획득:금어초x40 / 획득:웨딩 리본x1 / 획득:가구 카탈로그 / 획득:에어팟x1 (포장지, 꽃, 꾸미기 용품, 카탈로그, 그 밖의 소지품)
    public const string Order = "주문";          // 주문:금어초x2 / 주문:분홍색꽃x2 / 주문:포장지1
    public const string Unlock = "해금";         // 해금:전화기 / 해금:달력
    public const string Tutorial = "튜토리얼";   // 튜토리얼:night-intro
    public const string Leave = "퇴장";
    public const string TimeSkip = "시간경과";
    public const string Remember = "기억";       // 기억:핑호출 (선택지가 있는 줄에 쓴다: 그 줄에서 누른 응답 번호를 이름으로 기억한다) / 기억:에어팟=0 (값을 직접 적는다: 소지품을 건넸다 같은 사실). 뒤의 줄이 조건 칸에 `핑호출=1`, `에어팟=1&명찰=1`처럼 쓴다

    public static readonly string[] AllKinds = { Gift, Order, Unlock, Tutorial, Leave, TimeSkip, Remember };

    private const string WrapperPrefix = "포장지";

    public readonly string kind;
    public readonly string target;
    public readonly int amount;

    public DialogueEffect(string kind, string target, int amount)
    {
        this.kind = kind;
        this.target = target;
        this.amount = amount;
    }

    /// <summary>대상이 "포장지N"이면 그 번호, 아니면 0.</summary>
    public int WrapperNumber => target.StartsWith(WrapperPrefix) && int.TryParse(target.Substring(WrapperPrefix.Length), out int n) ? n : 0;

    public static DialogueEffect Parse(string text)
    {
        text = text.Trim();
        int colon = text.IndexOf(':');
        string kind = colon < 0 ? text : text.Substring(0, colon).Trim();
        string rest = colon < 0 ? "" : text.Substring(colon + 1).Trim();

        Match match = Regex.Match(rest, @"^(.*?)\s*x(\d+)$");
        return match.Success ? new DialogueEffect(kind, match.Groups[1].Value.Trim(), int.Parse(match.Groups[2].Value)) : new DialogueEffect(kind, rest, 1);
    }

    public override string ToString() => string.IsNullOrEmpty(target) ? kind : $"{kind}:{target}x{amount}";
}

/// <summary>기억 조건 하나: 이름으로 기억해 둔 값이 value일 때 맞다(기억한 적 없으면 0).</summary>
public readonly struct MemoryCondition
{
    public readonly string key;
    public readonly int value;

    public MemoryCondition(string key, int value)
    {
        this.key = key;
        this.value = value;
    }

    public bool Holds => ScenarioMemory.Get(key) == value;
}

/// <summary>말풍선 한 번에 나오는 대사. answers가 비어 있으면 말풍선의 아무 곳이나 눌러 넘기고, 있으면 그 버튼 문구(1~2개)로 답한다.</summary>
public class DialogueLine
{
    public string speaker; // CSV의 화자 열(NpcData.displayName과 같으면 그 NPC의 그림을 보인다). 비어 있을 수 있다
    public string text;
    public int condition; // 0이면 언제나 나오고, 1·2면 직전 선택에서 그 번호의 응답을 눌렀을 때만 나온다(CSV의 조건 열: 응답1/응답2)
    /// <summary>조건 열이 `이름=값`(여러 개면 `&`로 잇는다: `에어팟=1&명찰=1`)이면 그 목록. 기억해 둔 값(효과 `기억:이름`, `기억:이름=값`)이 모두 맞을 때만 나온다. 0은 "기억한 적 없음"이다.
    /// 선택이 겹치거나 다른 대화·다른 날에서 이어질 때, 물건을 얻었는지 같은 사실에 쓴다.</summary>
    public MemoryCondition[] memory = Array.Empty<MemoryCondition>();
    public bool HasCondition => condition != 0 || memory.Length > 0;
    public string[] answers = Array.Empty<string>();
    /// <summary>응답마다 드는 다이아(answers와 같은 순서). 응답 칸 끝에 `[다이아10]`을 쓰면 그 응답을 누를 때 10다이아가 빠진다. 안 쓰면 0.</summary>
    public int[] answerCosts = Array.Empty<int>();
    public DialogueEffect[] effects = Array.Empty<DialogueEffect>();
}

/// <summary>같은 대화ID의 줄들(순번 순서).</summary>
public class Dialogue
{
    public string id;
    public List<DialogueLine> lines = new();

    /// <summary>이 대화 줄들의 효과 중 kind인 것(대화 전체, 줄 순서대로).</summary>
    public IEnumerable<DialogueEffect> EffectsOf(string kind) => lines.SelectMany(l => l.effects).Where(e => e.kind == kind);
}

/// <summary>대사 CSV(Assets/05SO/Dialogue/Dialogue.csv)를 읽어 대화ID로 찾을 수 있게 한다. 열: 대화ID, 순번, 화자, 조건, 대사, 응답1, 응답2, 효과, 메모(메모는 읽지 않는다). 열은 머리줄의 이름으로 찾아서 순서가 바뀌어도 된다.</summary>
public class DialogueDatabase
{
    private readonly Dictionary<string, Dialogue> byId = new();

    public IEnumerable<Dialogue> All => byId.Values;

    public bool Has(string id) => !string.IsNullOrEmpty(id) && byId.ContainsKey(id);

    /// <summary>대화ID의 대화. 없으면 null.</summary>
    public Dialogue Get(string id) => !string.IsNullOrEmpty(id) && byId.TryGetValue(id, out Dialogue dialogue) ? dialogue : null;

    /// <summary>다른 CSV(반응 대사 등)에서 읽은 대화를 이 데이터베이스에 더한다. 대화ID가 겹치면 errors에 적고 앞의 것을 둔다.</summary>
    public void AddFrom(DialogueDatabase other, List<string> errors = null)
    {
        foreach (Dialogue dialogue in other.All)
        {
            if (byId.ContainsKey(dialogue.id)) errors?.Add($"대화ID '{dialogue.id}'가 두 CSV에 모두 있습니다(앞의 것을 씁니다).");
            else byId[dialogue.id] = dialogue;
        }
    }

    /// <summary>CSV 글을 읽는다. 문제가 있는 줄은 errors에 적고 건너뛴다.</summary>
    public static DialogueDatabase Parse(string csv, List<string> errors = null)
    {
        var database = new DialogueDatabase();
        List<string[]> rows = ParseCsv(csv.TrimStart('﻿'));
        if (rows.Count == 0) return database;

        string[] header = rows[0].Select(h => h.Trim()).ToArray();
        int Column(string name) => Array.IndexOf(header, name);
        int idCol = Column("대화ID"), orderCol = Column("순번"), textCol = Column("대사");
        int answer1Col = Column("응답1"), answer2Col = Column("응답2"), effectCol = Column("효과"), conditionCol = Column("조건"), speakerCol = Column("화자");
        if (idCol < 0 || orderCol < 0 || textCol < 0)
        {
            errors?.Add("머리줄에 대화ID, 순번, 대사 열이 없습니다.");
            return database;
        }

        string Cell(string[] row, int col) => col >= 0 && col < row.Length ? row[col].Trim() : "";

        var ordered = new Dictionary<string, List<(int order, DialogueLine line)>>();
        for (int r = 1; r < rows.Count; r++)
        {
            string[] row = rows[r];
            string id = Cell(row, idCol);
            if (id.Length == 0) continue; // 빈 줄

            if (!int.TryParse(Cell(row, orderCol), out int order))
            {
                errors?.Add($"{r + 1}행({id}): 순번이 숫자가 아닙니다.");
                continue;
            }

            string condition = Cell(row, conditionCol);
            var memory = new List<MemoryCondition>();
            bool memoryOk = condition.Length > 0 && condition != "응답1" && condition != "응답2";
            if (memoryOk)
            {
                foreach (string part in condition.Split('&'))
                {
                    Match m = Regex.Match(part.Trim(), @"^(.+?)\s*=\s*(\d+)$");
                    if (!m.Success) { memoryOk = false; break; }
                    memory.Add(new MemoryCondition(m.Groups[1].Value, int.Parse(m.Groups[2].Value)));
                }
            }
            if (condition.Length > 0 && condition != "응답1" && condition != "응답2" && !memoryOk) errors?.Add($"{r + 1}행({id}): 조건은 비우거나 응답1/응답2/이름=값(여러 개는 &로 잇기)여야 합니다('{condition}').");

            // 응답 칸 끝의 `[다이아10]`은 그 응답을 누를 때 드는 다이아다
            var answerTexts = new[] { Cell(row, answer1Col), Cell(row, answer2Col) }.Where(a => a.Length > 0).Select(a =>
            {
                Match cost = Regex.Match(a, @"\s*\[다이아\s*(\d+)\]\s*$");
                return cost.Success ? (text: a.Substring(0, cost.Index), cost: int.Parse(cost.Groups[1].Value)) : (text: a, cost: 0);
            }).ToArray();

            var line = new DialogueLine
            {
                speaker = Cell(row, speakerCol),
                text = Cell(row, textCol),
                condition = condition == "응답1" ? 1 : condition == "응답2" ? 2 : 0,
                memory = memoryOk ? memory.ToArray() : Array.Empty<MemoryCondition>(),
                answers = answerTexts.Select(a => a.text).ToArray(),
                answerCosts = answerTexts.Select(a => a.cost).ToArray(),
                effects = Cell(row, effectCol).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(DialogueEffect.Parse).ToArray(),
            };
            if (!ordered.TryGetValue(id, out var list)) ordered[id] = list = new List<(int, DialogueLine)>();
            list.Add((order, line));
        }

        foreach (var pair in ordered)
        {
            var dialogue = new Dialogue { id = pair.Key };
            foreach (var (order, line) in pair.Value.OrderBy(p => p.order)) dialogue.lines.Add(line);
            database.byId[pair.Key] = dialogue;
        }
        return database;
    }

    /// <summary>따옴표("…", 안의 쉼표·줄바꿈·"")를 지키는 CSV 읽기.</summary>
    private static List<string[]> ParseCsv(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c != '"') cell.Append(c);
                else if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else inQuotes = false;
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { row.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString());
                cell.Clear();
                rows.Add(row.ToArray());
                row.Clear();
            }
            else cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row.ToArray());
        }
        return rows;
    }
}
