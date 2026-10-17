using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 손님 주문 대사의 템플릿과 확률. 인스펙터에서 템플릿을 더하고 고치고 확률을 조절한다.
/// 주문은 꽃 부분과 포장지 부분으로 나뉘고, 각각 직설형(이름을 그대로 말한다)이나 은유형(비유로 말한다)으로 나온다.
/// 템플릿 안의 [꽃이름] [색] [개수] [포장지]는 주문 값으로 바뀌고, 바로 뒤에 붙인 조사(이/가, 을/를, 은/는, 이랑/랑, 이요/요, 으로/로, 과/와)는 받침에 맞게 저절로 고쳐진다.
/// 은유는 색 종류(열거형)별로 묶여 있어서, 그 색 칸 안에 문구를 더하면 된다(칸 이름은 저절로 맞춰진다).
/// </summary>
[CreateAssetMenu(fileName = "OrderTemplateData", menuName = "Puzzle/OrderTemplateData", order = 3)]
public class OrderTemplateData : ScriptableObject
{
    /// <summary>꽃 색 하나의 은유 문구들. 칸 이름(label)은 색 종류에 맞춰 저절로 정해진다.</summary>
    [Serializable]
    public class ColorPhrases
    {
        public string label;
        [Tooltip("색 이름 대신 말하는 비유. 뒤에 \"꽃\"이 붙는다(예: 동백 같은).")] public List<string> phrases = new();
    }

    /// <summary>포장지 색 하나의 은유 문장들. 칸 이름(label)은 색 종류에 맞춰 저절로 정해진다.</summary>
    [Serializable]
    public class WrapperSentences
    {
        public string label;
        [TextArea, Tooltip("포장지 부분을 비유로 말하는 문장 전체.")] public List<string> sentences = new();
    }

    [Header("확률")]
    [Range(0f, 1f), Tooltip("꽃 부분, 포장지 부분이 각각 은유형으로 나올 확률(반반이면 0.5). 꽃은 직설인데 포장지는 은유인 식으로 섞일 수 있다.")] public float metaphorChance = 0.5f;
    [Range(0f, 1f), Tooltip("꽃 은유형 중에서 꽃의 특수주문(말장난 주문)으로 말할 확률. 나머지는 색 은유로 말한다. 특수주문이 있는 꽃이 없으면 색 은유로 간다.")] public float flowerSpecialOrderChance = 0.1f;

    [Header("꽃 직설형 템플릿: [꽃이름]이 하나면 꽃 한 종, 둘이면 꽃 두 종을 말한다")]
    [TextArea] public List<string> flowerDirect = new();

    [Header("색 직설형 템플릿(꽃 주문을 색으로만 말한다): [색], [개수]")]
    [TextArea] public List<string> colorDirect = new();

    [Header("포장지 직설형 템플릿: [포장지]")]
    [TextArea] public List<string> wrapperDirect = new();

    [Header("꽃 색 은유: 색별 문구(색 직설형 템플릿의 [색] 자리에 들어간다)")]
    public ColorPhrases[] colorMetaphors = NewColorPhrases();

    [Header("포장지 은유: 색별 문장. 그 색 문장이 없으면 포장지는 직설형으로 말한다")]
    public WrapperSentences[] wrapperMetaphors = NewWrapperSentences();

    /// <summary>그 꽃 색의 은유 문구들(없으면 빈 목록).</summary>
    public IReadOnlyList<string> PhrasesOf(FlowerData.Color color)
    {
        int i = (int)color;
        return colorMetaphors != null && i < colorMetaphors.Length && colorMetaphors[i] != null ? colorMetaphors[i].phrases : new List<string>();
    }

    /// <summary>그 포장지 색의 은유 문장들(없으면 빈 목록).</summary>
    public IReadOnlyList<string> SentencesOf(WrapperColor color)
    {
        int i = (int)color;
        return wrapperMetaphors != null && i < wrapperMetaphors.Length && wrapperMetaphors[i] != null ? wrapperMetaphors[i].sentences : new List<string>();
    }

    private static ColorPhrases[] NewColorPhrases()
    {
        var groups = new ColorPhrases[Enum.GetValues(typeof(FlowerData.Color)).Length];
        for (int i = 0; i < groups.Length; i++) groups[i] = new ColorPhrases();
        return groups;
    }

    private static WrapperSentences[] NewWrapperSentences()
    {
        var groups = new WrapperSentences[Enum.GetValues(typeof(WrapperColor)).Length];
        for (int i = 0; i < groups.Length; i++) groups[i] = new WrapperSentences();
        return groups;
    }

    /// <summary>색 종류가 늘거나 줄어도 칸 수와 칸 이름이 맞도록 고친다(문구는 그대로 둔다).</summary>
    private void OnValidate()
    {
        colorMetaphors = Resize(colorMetaphors, Enum.GetValues(typeof(FlowerData.Color)).Length, () => new ColorPhrases());
        for (int i = 0; i < colorMetaphors.Length; i++) colorMetaphors[i].label = $"{(FlowerData.Color)i} ({ColorPalette.ToKoreanName((FlowerData.Color)i)})";

        wrapperMetaphors = Resize(wrapperMetaphors, Enum.GetValues(typeof(WrapperColor)).Length, () => new WrapperSentences());
        for (int i = 0; i < wrapperMetaphors.Length; i++) wrapperMetaphors[i].label = $"{(WrapperColor)i} ({ColorPalette.ToKoreanName((WrapperColor)i)})";
    }

    private static T[] Resize<T>(T[] array, int length, Func<T> make) where T : class
    {
        if (array == null) array = new T[0];
        int old = array.Length;
        if (old != length) Array.Resize(ref array, length);
        for (int i = 0; i < length; i++) if (array[i] == null) array[i] = make();
        return array;
    }

    /// <summary>에셋을 새로 만들 때 기본 내용으로 채운다.</summary>
    private void Reset() => FillDefaults();

    /// <summary>기본 템플릿과 문구(처음 정한 직설형 템플릿, 색 은유, 포장지 은유 예시)로 채운다.</summary>
    public void FillDefaults()
    {
        metaphorChance = 0.5f;
        flowerSpecialOrderChance = 0.1f;

        flowerDirect = new List<string>
        {
            "[꽃이름] 한 송이는 꼭 들어가야 해요.",
            "[꽃이름]이랑 [꽃이름]이요.",
            "[꽃이름]이 들어간다면 멋질 것 같아요!",
        };
        colorDirect = new List<string>
        {
            "[색] 꽃 [개수]는 꼭 넣어주세요.",
            "[색] 꽃이 [개수] 정도는 들어갔으면 좋겠어요.",
        };
        wrapperDirect = new List<string>
        {
            "포장지는… [포장지]로 할게요.",
            "포장지는 [포장지]가 좋겠네요.",
            "그리고… 포장지는 가장 좋아하는 색으로… [포장지]가 좋겠어요.",
        };

        colorMetaphors = NewColorPhrases();
        colorMetaphors[(int)FlowerData.Color.Red].phrases.AddRange(new[] { "동백 같은", "딸기 같은" });
        colorMetaphors[(int)FlowerData.Color.Orange].phrases.AddRange(new[] { "노을 같은", "귤빛" });
        colorMetaphors[(int)FlowerData.Color.Yellow].phrases.AddRange(new[] { "병아리 같은", "햇살 같은" });
        colorMetaphors[(int)FlowerData.Color.Blue].phrases.AddRange(new[] { "바다 같은", "하늘빛" });
        colorMetaphors[(int)FlowerData.Color.Purple].phrases.AddRange(new[] { "라벤더 같은", "포도빛" });
        colorMetaphors[(int)FlowerData.Color.Pink].phrases.AddRange(new[] { "벚꽃 같은", "볼터치 같은" });
        colorMetaphors[(int)FlowerData.Color.White].phrases.AddRange(new[] { "눈 같은", "구름 같은" });

        wrapperMetaphors = NewWrapperSentences();
        wrapperMetaphors[(int)WrapperColor.Blue].sentences.Add("포장지는 바다를 닮은 것으로요.");
        wrapperMetaphors[(int)WrapperColor.Red].sentences.Add("포장지는… 저 작렬하는 노을을 닮은 색이 좋겠네요!");
        wrapperMetaphors[(int)WrapperColor.White].sentences.Add("포장지는… 아무 색도 없다면 좋을 것 같아요.");
        wrapperMetaphors[(int)WrapperColor.Pink].sentences.Add("포장지는 봄날을 담은 색이었으면 좋겠어요.");

        OnValidate();
    }
}
