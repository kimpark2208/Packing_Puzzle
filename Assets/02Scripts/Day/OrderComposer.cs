using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static CustomerRequirementGenerator;

/// <summary>
/// 손님의 주문을 만든다: 요구사항(퍼즐 조건)과 그것을 말하는 대사, 되묻기 대사.
/// 주문은 꽃 부분과 포장지 부분으로 나뉘고, 각각 직설형(템플릿에 이름을 넣어 말한다)이나 은유형(비유로 말한다)으로 정해진다(OrderTemplateData.metaphorChance).
/// - 꽃 직설형: 꽃 한 종이나 두 종을 말하는 템플릿, 또는 색만 말하는 템플릿.
/// - 꽃 은유형: 꽃의 특수주문(말장난, 확률은 flowerSpecialOrderChance)이나 색 은유("동백 같은 꽃").
/// - 포장지 직설형: 템플릿에 포장지 색 이름. 포장지 은유형: 그 색의 은유 문장(없으면 직설형).
/// 되묻기 대사는 특수주문이 있는 꽃의 1차·2차 되묻기만 있다.
/// </summary>
public static class OrderComposer
{
    public struct Result
    {
        public CustomerRequirement requirement;
        public string line;          // 손님이 말하는 주문 전체(꽃 부분 + 포장지 부분)
        public string[] reaskLines;  // 되묻기에 대한 대사들(1차, 2차). 없으면 빈 배열
    }

    private static readonly string[] NoLines = System.Array.Empty<string>();

    /// <param name="preset">null이면 제한 없이 만든다(씬 단독 실행 등).</param>
    /// <param name="wrapperColor">손님이 말할 포장지의 색 종류(WrapperData.wrapperColor). 모르면 null(은유 없이 fallbackColorName으로 직설형만).</param>
    /// <param name="fallbackColorName">wrapperColor가 없을 때 쓰는 포장지 색 이름.</param>
    /// <param name="templates">null이면 기본 템플릿을 쓴다.</param>
    public static Result Compose(int day, WrapperPresetData preset, WrapperColor? wrapperColor, string fallbackColorName, OrderTemplateData templates)
    {
        if (templates == null)
        {
            templates = ScriptableObject.CreateInstance<OrderTemplateData>();
            templates.FillDefaults();
        }

        var requirement = new CustomerRequirement { requirementId = day, baseAmount = BaseAmount, targetId2 = -1 };
        string[] reask = NoLines;

        string flowerPart = null;
        if (Random.value < templates.metaphorChance) flowerPart = TryFlowerMetaphor(ref requirement, preset, templates, out reask);
        if (flowerPart == null) flowerPart = TryFlowerDirect(ref requirement, preset, templates);
        if (flowerPart == null) flowerPart = FlowerFallback(ref requirement);

        string wrapperName = wrapperColor.HasValue ? ColorPalette.ToKoreanName(wrapperColor.Value) : fallbackColorName;
        string wrapperPart = wrapperColor.HasValue && Random.value < templates.metaphorChance ? TryWrapperMetaphor(wrapperColor.Value, templates) : null;
        if (wrapperPart == null) wrapperPart = WrapperDirect(wrapperName, templates);

        Debug.Log($"[OrderComposer] Day {day} 주문: {flowerPart} {wrapperPart}");
        return new Result { requirement = requirement, line = $"{flowerPart} {wrapperPart}".Trim(), reaskLines = reask };
    }

    // ── 꽃 부분 ──────────────────────────────────────────────────────────────

    private static string TryFlowerMetaphor(ref CustomerRequirement requirement, WrapperPresetData preset, OrderTemplateData t, out string[] reask)
    {
        reask = NoLines;

        if (Random.value < t.flowerSpecialOrderChance)
        {
            var specials = ObtainedFlowers().Where(f => f.HasSpecialOrder && FlowerCapacity(preset, f) >= 1).ToList();
            if (specials.Count > 0)
            {
                FlowerData flower = specials[Random.Range(0, specials.Count)];
                SetFlower(ref requirement, flower, null);
                reask = new[] { flower.specialReask1, flower.specialReask2 }.Where(IsLine).Select(s => s.Trim()).ToArray();
                return flower.specialOrder.Trim();
            }
        }
        return TryColor(ref requirement, preset, t, metaphor: true);
    }

    private static string TryFlowerDirect(ref CustomerRequirement requirement, WrapperPresetData preset, OrderTemplateData t)
    {
        bool colorFirst = Random.value < 0.5f;
        string line = colorFirst ? TryColor(ref requirement, preset, t, metaphor: false) : TryFlowers(ref requirement, preset, t);
        return line ?? (colorFirst ? TryFlowers(ref requirement, preset, t) : TryColor(ref requirement, preset, t, metaphor: false));
    }

    /// <summary>색 주문(직설형은 "빨강색", 은유형은 "동백 같은"으로 말한다). 놓을 수 있는 색이 없으면 null.</summary>
    private static string TryColor(ref CustomerRequirement requirement, WrapperPresetData preset, OrderTemplateData t, bool metaphor)
    {
        if (t.colorDirect == null || t.colorDirect.Count == 0) return null;

        var colors = new List<int>();
        for (int c = 0; c < System.Enum.GetValues(typeof(FlowerData.Color)).Length; c++)
        {
            if (ColorCapacity(preset, (FlowerData.Color)c) < 2) continue;
            if (metaphor && !t.PhrasesOf((FlowerData.Color)c).Any(p => !string.IsNullOrWhiteSpace(p))) continue;
            colors.Add(c);
        }
        if (colors.Count == 0) return null;

        int color = colors[Random.Range(0, colors.Count)];
        int capacity = ColorCapacity(preset, (FlowerData.Color)color);
        int count = Random.Range(2, Mathf.Min(4, capacity) + 1); // 2~4개

        string name = metaphor
            ? PickPhrase(t, (FlowerData.Color)color)
            : ColorPalette.ToKoreanName((FlowerData.Color)color) + "색";

        requirement.type = RequirementType.Color;
        requirement.targetId = color;
        requirement.targetId2 = -1;
        requirement.minCount = count;
        requirement.description = $"<b>{name}</b> 꽃을 <b>{count}개</b> 이상 사용하세요";

        string line = t.colorDirect[Random.Range(0, t.colorDirect.Count)];
        line = KoreanParticle.Replace(line, "색", name, $"<b>{name}</b>");
        return KoreanParticle.Replace(line, "개수", $"{count}개", $"<b>{count}개</b>");
    }

    /// <summary>꽃 이름을 말하는 주문(템플릿의 [꽃이름] 개수만큼 서로 다른 꽃). 놓을 수 있는 꽃이 모자라면 null.</summary>
    private static string TryFlowers(ref CustomerRequirement requirement, WrapperPresetData preset, OrderTemplateData t)
    {
        var flowers = ObtainedFlowers().Where(f => FlowerCapacity(preset, f) >= 1).ToList();
        var templates = (t.flowerDirect ?? new List<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Where(s => { int n = KoreanParticle.CountToken(s, "꽃이름"); return n >= 1 && n <= 2 && flowers.Count >= n; })
            .ToList();
        if (templates.Count == 0) return null;

        string template = templates[Random.Range(0, templates.Count)];
        int need = KoreanParticle.CountToken(template, "꽃이름");

        FlowerData first = flowers[Random.Range(0, flowers.Count)];
        FlowerData second = null;
        if (need == 2)
        {
            var seconds = flowers.Where(f => f != first && CanPlacePair(preset, first, f)).ToList();
            if (seconds.Count == 0) return TryFlowersSingleFallback(ref requirement, preset, t, flowers);
            second = seconds[Random.Range(0, seconds.Count)];
        }

        SetFlower(ref requirement, first, second);
        string line = KoreanParticle.Replace(template, "꽃이름", first.DisplayName, $"<b>{first.DisplayName}</b>");
        if (second != null) line = KoreanParticle.Replace(line, "꽃이름", second.DisplayName, $"<b>{second.DisplayName}</b>");
        return line;
    }

    /// <summary>두 종을 함께 놓을 수 없을 때는 꽃 한 종 템플릿으로 말한다.</summary>
    private static string TryFlowersSingleFallback(ref CustomerRequirement requirement, WrapperPresetData preset, OrderTemplateData t, List<FlowerData> flowers)
    {
        var singles = t.flowerDirect.Where(s => !string.IsNullOrWhiteSpace(s) && KoreanParticle.CountToken(s, "꽃이름") == 1).ToList();
        if (singles.Count == 0) return null;

        FlowerData flower = flowers[Random.Range(0, flowers.Count)];
        SetFlower(ref requirement, flower, null);
        return KoreanParticle.Replace(singles[Random.Range(0, singles.Count)], "꽃이름", flower.DisplayName, $"<b>{flower.DisplayName}</b>");
    }

    /// <summary>템플릿과 꽃이 아무것도 쓸 수 없을 때(데이터가 비었을 때)의 마지막 수단: 제한 없이 만든 색 주문.</summary>
    private static string FlowerFallback(ref CustomerRequirement requirement)
    {
        requirement.type = RequirementType.Color;
        requirement.targetId = Random.Range(0, 6);
        requirement.targetId2 = -1;
        requirement.minCount = 2;
        string name = ColorPalette.ToKoreanName((FlowerData.Color)requirement.targetId) + "색";
        requirement.description = $"<b>{name}</b> 꽃을 <b>2개</b> 이상 사용하세요";
        return $"<b>{name}</b> 꽃 <b>2개</b>는 꼭 넣어주세요.";
    }

    private static void SetFlower(ref CustomerRequirement requirement, FlowerData first, FlowerData second)
    {
        requirement.type = RequirementType.Flower;
        requirement.targetId = first.flowerID;
        requirement.targetId2 = second != null ? second.flowerID : -1;
        requirement.minCount = second != null ? 2 : 1; // 말한 꽃마다 한 송이 이상
        requirement.description = second != null
            ? $"<b>{first.DisplayName}</b>, <b>{second.DisplayName}</b>을(를) 한 송이 이상씩 사용하세요"
            : $"<b>{first.DisplayName}</b>을(를) 한 송이 이상 사용하세요";
    }

    private static string PickPhrase(OrderTemplateData t, FlowerData.Color color)
    {
        var phrases = t.PhrasesOf(color).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        return phrases[Random.Range(0, phrases.Count)].Trim();
    }

    private static bool IsLine(string s) => !string.IsNullOrWhiteSpace(s) && s.Trim() != "-";

    // ── 포장지 부분 ──────────────────────────────────────────────────────────

    private static string TryWrapperMetaphor(WrapperColor color, OrderTemplateData t)
    {
        var sentences = t.SentencesOf(color).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
        return sentences.Count > 0 ? sentences[Random.Range(0, sentences.Count)] : null;
    }

    private static string WrapperDirect(string wrapperColorName, OrderTemplateData t)
    {
        string name = wrapperColorName + "색"; // 빨간색, 남색 …
        var templates = (t.wrapperDirect ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        string template = templates.Count > 0 ? templates[Random.Range(0, templates.Count)] : "포장지는 [포장지]로 할게요.";
        return KoreanParticle.Replace(template, "포장지", name, $"<b>{name}</b>");
    }
}
