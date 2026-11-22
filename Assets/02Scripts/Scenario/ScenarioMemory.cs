using System.Collections.Generic;

/// <summary>
/// 플레이어가 한 선택 중 나중 대사가 알아야 하는 것의 기억(대사 효과 `기억:이름`이 적는다). 이름마다 누른 응답 번호(1 또는 2)를 하나 들고 있다.
/// 선택이 겹친 대화, 다른 대화, 다른 날의 장면이 조건 칸에 `이름=1`처럼 써서 읽는다. 새 게임을 시작하면 비운다(저장 기능이 생기면 같이 저장한다).
/// </summary>
public static class ScenarioMemory
{
    private static readonly Dictionary<string, int> values = new();

    public static void Set(string name, int choice)
    {
        if (!string.IsNullOrEmpty(name)) values[name] = choice;
    }

    /// <summary>효과 `기억:` 뒤의 글을 처리한다: `이름=값`이면 그 값을, `이름`만 있으면 chosen(방금 누른 응답 번호)을 기억한다.</summary>
    public static void Remember(string target, int chosen)
    {
        int equals = target.IndexOf('=');
        if (equals > 0 && int.TryParse(target.Substring(equals + 1).Trim(), out int value)) Set(target.Substring(0, equals).Trim(), value);
        else Set(target.Trim(), chosen);
    }

    /// <summary>기억해 둔 응답 번호. 아직 기억한 적이 없으면 0(그러면 `이름=1`도 `이름=2`도 맞지 않아 그 줄은 나오지 않는다).</summary>
    public static int Get(string name) => !string.IsNullOrEmpty(name) && values.TryGetValue(name, out int value) ? value : 0;

    public static void Clear() => values.Clear();
}
