using System.Text.RegularExpressions;

/// <summary>받침 유무에 따라 고르는 한국어 조사.</summary>
public static class KoreanParticle
{
    // 조사 짝(받침 있음, 받침 없음). 긴 것을 먼저 둔다(이랑/이요가 이보다 먼저 잡혀야 한다).
    private static readonly string[][] Pairs =
    {
        new[] { "이랑", "랑" }, new[] { "이고요", "고요" }, new[] { "이요", "요" }, new[] { "으로", "로" },
        new[] { "이", "가" }, new[] { "을", "를" }, new[] { "은", "는" }, new[] { "과", "와" },
    };

    private static readonly Regex ParticleRegex = new("^(이랑|랑|이고요|고요|이요|요|으로|로|이|가|을|를|은|는|과|와)", RegexOptions.Compiled);

    /// <summary>받침이 있으면 "이", 없으면 "가". 한글이 아닌 글자로 끝나면 "이".</summary>
    public static string IGa(string word) => EndsWithoutBatchim(word) ? "가" : "이";

    /// <summary>
    /// 템플릿에서 첫 번째 [token]을 display로 바꾼다. 바로 뒤에 조사가 붙어 있으면 plain(태그 없는 글자)의 받침에 맞게 고친다.
    /// 예: Replace("[꽃이름]이 좋아요", "꽃이름", "금어초", "&lt;b&gt;금어초&lt;/b&gt;") → "&lt;b&gt;금어초&lt;/b&gt;가 좋아요". token이 없으면 그대로 돌려준다.
    /// </summary>
    public static string Replace(string template, string token, string plain, string display)
    {
        string marker = $"[{token}]";
        int at = template.IndexOf(marker, System.StringComparison.Ordinal);
        if (at < 0) return template;

        int after = at + marker.Length;
        string rest = template.Substring(after);
        Match match = ParticleRegex.Match(rest);
        string particle = "";
        if (match.Success)
        {
            particle = Resolve(match.Value, plain);
            rest = rest.Substring(match.Length);
        }
        return template.Substring(0, at) + display + particle + rest;
    }

    /// <summary>템플릿에 [token]이 몇 번 나오는가.</summary>
    public static int CountToken(string template, string token)
    {
        string marker = $"[{token}]";
        int count = 0;
        for (int i = template.IndexOf(marker, System.StringComparison.Ordinal); i >= 0; i = template.IndexOf(marker, i + marker.Length, System.StringComparison.Ordinal)) count++;
        return count;
    }

    private static string Resolve(string written, string word)
    {
        foreach (string[] pair in Pairs)
        {
            if (written != pair[0] && written != pair[1]) continue;

            bool batchim = !EndsWithoutBatchim(word);
            if (pair[0] == "으로") return batchim && !EndsWithRieul(word) ? "으로" : "로";
            return batchim ? pair[0] : pair[1];
        }
        return written;
    }

    private static bool EndsWithoutBatchim(string word)
    {
        if (string.IsNullOrEmpty(word)) return false;

        char last = word[word.Length - 1];
        bool isHangul = last >= 0xAC00 && last <= 0xD7A3;
        return isHangul && (last - 0xAC00) % 28 == 0;
    }

    private static bool EndsWithRieul(string word)
    {
        if (string.IsNullOrEmpty(word)) return false;

        char last = word[word.Length - 1];
        return last >= 0xAC00 && last <= 0xD7A3 && (last - 0xAC00) % 28 == 8; // ㄹ 받침
    }
}
