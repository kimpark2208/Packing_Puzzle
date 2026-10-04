/// <summary>받침 유무에 따라 고르는 한국어 조사.</summary>
public static class KoreanParticle
{
    /// <summary>받침이 있으면 "이", 없으면 "가". 한글이 아닌 글자로 끝나면 "이".</summary>
    public static string IGa(string word)
    {
        if (string.IsNullOrEmpty(word)) return "이";

        char last = word[word.Length - 1];
        bool isHangul = last >= 0xAC00 && last <= 0xD7A3;
        return isHangul && (last - 0xAC00) % 28 == 0 ? "가" : "이";
    }
}
