using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>대사 한 줄이 끝날 때 일어나는 일. CSV의 효과 칸에서 읽는다: `효과:대상` 또는 `효과:대상x수량`(수량을 안 쓰면 1), 대상이 없는 효과는 낱말만(`퇴장`).</summary>
public readonly struct DialogueEffect
{
    public const string Gift = "선물";           // 선물:포장지1x30
    public const string Order = "주문";          // 주문:금어초x2 / 주문:포장지1
    public const string GrantFlower = "꽃지급";  // 꽃지급:금어초x10
    public const string Catalog = "카탈로그";    // 카탈로그:가구 / 카탈로그:용품
    public const string Unlock = "해금";         // 해금:전화기 / 해금:달력
    public const string Tutorial = "튜토리얼";   // 튜토리얼:night-intro
    public const string Leave = "퇴장";
    public const string TimeSkip = "시간경과";

    public static readonly string[] AllKinds = { Gift, Order, GrantFlower, Catalog, Unlock, Tutorial, Leave, TimeSkip };

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

/// <summary>말풍선 한 번에 나오는 대사. answers가 비어 있으면 말풍선의 아무 곳이나 눌러 넘기고, 있으면 그 버튼 문구(1~2개)로 답한다.</summary>
public class DialogueLine
{
    public string text;
    public string[] answers = Array.Empty<string>();
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

/// <summary>대사 CSV(Assets/05SO/Dialogue/Dialogue.csv)를 읽어 대화ID로 찾을 수 있게 한다. 열: 대화ID, 순번, 화자, 대사, 응답1, 응답2, 효과, 메모(메모는 읽지 않는다).</summary>
public class DialogueDatabase
{
    private readonly Dictionary<string, Dialogue> byId = new();

    public IEnumerable<Dialogue> All => byId.Values;

    public bool Has(string id) => !string.IsNullOrEmpty(id) && byId.ContainsKey(id);

    /// <summary>대화ID의 대화. 없으면 null.</summary>
    public Dialogue Get(string id) => !string.IsNullOrEmpty(id) && byId.TryGetValue(id, out Dialogue dialogue) ? dialogue : null;

    /// <summary>CSV 글을 읽는다. 문제가 있는 줄은 errors에 적고 건너뛴다.</summary>
    public static DialogueDatabase Parse(string csv, List<string> errors = null)
    {
        var database = new DialogueDatabase();
        List<string[]> rows = ParseCsv(csv.TrimStart('﻿'));
        if (rows.Count == 0) return database;

        string[] header = rows[0].Select(h => h.Trim()).ToArray();
        int Column(string name) => Array.IndexOf(header, name);
        int idCol = Column("대화ID"), orderCol = Column("순번"), textCol = Column("대사");
        int answer1Col = Column("응답1"), answer2Col = Column("응답2"), effectCol = Column("효과");
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

            var line = new DialogueLine
            {
                text = Cell(row, textCol),
                answers = new[] { Cell(row, answer1Col), Cell(row, answer2Col) }.Where(a => a.Length > 0).ToArray(),
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
