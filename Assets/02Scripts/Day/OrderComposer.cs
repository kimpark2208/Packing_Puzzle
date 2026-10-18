using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 손님의 주문을 만든다: 요구사항(퍼즐 조건)과 그것을 말하는 대사, 되묻기 대사.
/// 주문은 꽃 부분과 포장지 부분으로 나뉘고, 각각 직설형(템플릿에 이름을 넣어 말한다)이나 은유형(비유로 말한다)으로 정해진다(OrderTemplateData.metaphorChance).
/// - 꽃 직설형: 꽃 한 종이나 두 종을 말하는 템플릿, 또는 색만 말하는 템플릿.
/// - 꽃 은유형: 꽃의 특수주문(말장난, 확률은 flowerSpecialOrderChance)이나 색 은유("동백 같은 꽃").
/// - 포장지 직설형: 템플릿에 포장지 색 이름. 포장지 은유형: 그 색의 은유 문장(없으면 직설형).
/// 되묻기(1회, 2회) 대사도 여기서 미리 만든다(PlainReasks, SpecialReasks). 되묻기에서는 꽃의 색 은유와 포장지 은유를 은유 없이 바로 말한다.
/// 꽃 부분과 포장지 부분은 주문 전체 템플릿(OrderTemplateData.fullOrder)에 넣어 말한다. 단, 꽃의 특수주문은 전체 템플릿을 쓰지 않고 그대로 말한 뒤 포장지 부분을 잇는다.
/// 요구사항(CustomerRequirement)의 형태와, 그것을 만들 재료(획득한 꽃, 프리셋이 받아 줄 수 있는 개수)도 여기에 있다.
/// 프리셋이 주어지면 그 프리셋으로 풀 수 있는 요구사항만 만든다(칸당 꽃 한 송이이므로 역할별 칸 수가 상한).
/// </summary>
public static class OrderComposer
{
    public enum RequirementType
    {
        Color,   // 색상 기반: "빨간색 꽃을 3개 이상 사용하세요"
        Flower   // 꽃 기반: "금어초(와 소국)를 하나 이상씩 사용하세요"
    }

    public struct CustomerRequirement
    {
        public int requirementId;        // 요구사항 고유 ID (일수)
        public RequirementType type;     // 색상/꽃 기반
        public FlowerData.Color color;   // 색상 기반: 요구한 꽃 색
        public int flowerId;             // 꽃 기반: 요구한 첫 번째 꽃
        public int flowerId2;            // 꽃 기반: 요구한 두 번째 꽃(한 종이면 -1)
        public int flowerCount;          // 꽃 기반: 첫 번째 꽃의 요구 송이 수(0이면 한 송이)
        public int flowerCount2;         // 꽃 기반: 두 번째 꽃의 요구 송이 수(0이면 한 송이)
        public int minCount;             // 색상 기반: 최소 개수. 꽃 기반: 말한 꽃들의 요구 송이 수 합(고정 주문이 아니면 종류마다 한 송이)
        public int baseAmount;           // 이 퍼즐의 기본 금액(정산 점수 구간의 배율이 곱해진다)

        /// <summary>꽃 기반에서 요구한 꽃과 송이 수(한 종이면 하나, 두 종이면 둘).</summary>
        public IEnumerable<(int flowerId, int count)> FlowerTargets()
        {
            yield return (flowerId, Mathf.Max(1, flowerCount));
            if (flowerId2 >= 0) yield return (flowerId2, Mathf.Max(1, flowerCount2));
        }
    }

    /// <summary>퍼즐 기본 금액(임시: 모든 주문이 같다). 100 = 초반에 0~25점 꽃다발 1개를 판매한 정도.</summary>
    public const int BaseAmount = 100;

    private const string FlowerSlot = "[꽃템플릿]";     // 주문 전체 템플릿에서 꽃 부분이 들어갈 자리
    private const string WrapperSlot = "[포장지템플릿]"; // 포장지 부분이 들어갈 자리

    public struct Result
    {
        public CustomerRequirement requirement;
        public string line;          // 손님이 말하는 주문 전체(꽃 부분 + 포장지 부분)
        public string[] reaskLines;  // 되묻기를 누를 때마다 말하는 대사 전체(1회, 2회)
    }

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

        var requirement = new CustomerRequirement { requirementId = day, baseAmount = BaseAmount, flowerId2 = -1 };
        FlowerData special = null; // 꽃의 특수주문(말장난)으로 말했으면 그 꽃
        bool flowerMetaphor = false;

        string flowerPart = null;
        string flowerDirectPart = null; // 색 은유일 때, 같은 색을 은유 없이 바로 말한 꽃 부분(되묻기용)
        if (Random.value < templates.metaphorChance)
        {
            flowerPart = TryFlowerMetaphor(ref requirement, preset, templates, out special, out flowerDirectPart);
            flowerMetaphor = flowerPart != null;
        }
        if (flowerPart == null) flowerPart = TryFlowerDirect(ref requirement, preset, templates);
        if (flowerPart == null) flowerPart = FlowerFallback(ref requirement);

        string wrapperName = wrapperColor.HasValue ? ColorPalette.ToKoreanName(wrapperColor.Value) : fallbackColorName;
        string wrapperPart = wrapperColor.HasValue && Random.value < templates.metaphorChance ? TryWrapperMetaphor(wrapperColor.Value, templates) : null;
        bool wrapperMetaphor = wrapperPart != null;
        if (wrapperPart == null) wrapperPart = WrapperDirect(wrapperName, templates);

        // 꽃의 특수주문과 되묻기는 주문 전체 템플릿 없이 두 부분을 붙여서 말한다. 되묻기에서는 꽃의 색 은유와 포장지 은유를 은유 없이 바로 말한다.
        string joined = $"{flowerPart} {wrapperPart}";
        string wrapperDirectPart = wrapperMetaphor ? WrapperDirect(wrapperName, templates) : wrapperPart;
        string restated = $"{flowerDirectPart ?? flowerPart} {wrapperDirectPart}";
        string[] reask = special != null
            ? SpecialReasks(special, restated, wrapperDirectPart, templates)
            : PlainReasks(restated, !flowerMetaphor && !wrapperMetaphor, templates);

        Debug.Log($"[OrderComposer] Day {day} 주문: {joined}");
        return new Result { requirement = requirement, line = special != null ? joined : Frame(flowerPart, wrapperPart, templates), reaskLines = reask };
    }

    /// <summary>주문 전체 템플릿(꽃·포장지 자리가 모두 있는 것) 중 하나에 두 부분을 끼운다. 쓸 만한 템플릿이 없으면 두 부분을 붙여서 말한다.</summary>
    private static string Frame(string flowerPart, string wrapperPart, OrderTemplateData t)
    {
        var frames = (t.fullOrder ?? new List<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s) && s.Contains(FlowerSlot) && s.Contains(WrapperSlot))
            .ToList();
        if (frames.Count == 0) return $"{flowerPart} {wrapperPart}";

        return frames[Random.Range(0, frames.Count)].Trim().Replace(FlowerSlot, flowerPart).Replace(WrapperSlot, wrapperPart);
    }

    // ── 되묻기 ───────────────────────────────────────────────────────────────

    /// <summary>
    /// 되묻기(1회, 2회) 대사: 주문 전체 템플릿 없이 꽃 부분과 포장지 부분을 붙여 다시 말하고, 횟수에 맞는 말을 덧붙인다.
    /// 둘 다 직설형이면 reaskAfterDirect를, 은유가 들어 있으면 reaskAfterMetaphor를 덧붙인다(빈 칸이면 덧붙이지 않는다).
    /// </summary>
    private static string[] PlainReasks(string joined, bool bothDirect, OrderTemplateData t)
    {
        string[] after = bothDirect ? t.reaskAfterDirect : t.reaskAfterMetaphor;
        var lines = new string[2];
        for (int i = 0; i < 2; i++)
        {
            string extra = after != null && i < after.Length && IsLine(after[i]) ? after[i].Trim() : "";
            lines[i] = $"{joined} {extra}".Trim();
        }
        return lines;
    }

    /// <summary>
    /// 꽃의 특수주문일 때의 되묻기: 1회는 그 꽃의 1차 되묻기, 2회는 2차 되묻기에 포장지 부분(은유였어도 바로 말한 것)을 이어 말한다.
    /// 꽃의 되묻기 문장이 비어 있는 차례는 은유 주문의 되묻기(PlainReasks)로 대신한다.
    /// </summary>
    private static string[] SpecialReasks(FlowerData flower, string restated, string wrapperDirectPart, OrderTemplateData t)
    {
        string[] own = { flower.specialReask1, flower.specialReask2 };
        string[] plain = PlainReasks(restated, false, t);

        var lines = new string[2];
        for (int i = 0; i < 2; i++) lines[i] = IsLine(own[i]) ? $"{own[i].Trim()} {wrapperDirectPart}" : plain[i];
        return lines;
    }

    // ── 꽃 부분 ──────────────────────────────────────────────────────────────

    /// <param name="special">꽃의 특수주문(말장난)으로 말했으면 그 꽃, 아니면 null. 특수주문은 그 자체로 한 편의 대사라서 주문 전체 템플릿에 넣지 않는다.</param>
    private static string TryFlowerMetaphor(ref CustomerRequirement requirement, WrapperPresetData preset, OrderTemplateData t, out FlowerData special, out string direct)
    {
        special = null;
        direct = null;

        if (Random.value < t.flowerSpecialOrderChance)
        {
            var specials = ObtainedFlowers().Where(f => f.HasSpecialOrder && FlowerCapacity(preset, f) >= 1).ToList();
            if (specials.Count > 0)
            {
                FlowerData flower = specials[Random.Range(0, specials.Count)];
                SetFlower(ref requirement, flower, null);
                special = flower;
                return flower.specialOrder.Trim();
            }
        }
        return TryColor(ref requirement, preset, t, metaphor: true, out direct);
    }

    private static string TryFlowerDirect(ref CustomerRequirement requirement, WrapperPresetData preset, OrderTemplateData t)
    {
        bool colorFirst = Random.value < 0.5f;
        string line = colorFirst ? TryColor(ref requirement, preset, t, metaphor: false) : TryFlowers(ref requirement, preset, t);
        return line ?? (colorFirst ? TryFlowers(ref requirement, preset, t) : TryColor(ref requirement, preset, t, metaphor: false));
    }

    /// <summary>색 주문(직설형은 "빨강색", 은유형은 "동백 같은"으로 말한다). 놓을 수 있는 색이 없으면 null.</summary>
    private static string TryColor(ref CustomerRequirement requirement, WrapperPresetData preset, OrderTemplateData t, bool metaphor) => TryColor(ref requirement, preset, t, metaphor, out _);

    /// <param name="direct">은유형일 때, 같은 색과 개수를 은유 없이 바로 말한 문장(되묻기용). 직설형이면 null.</param>
    private static string TryColor(ref CustomerRequirement requirement, WrapperPresetData preset, OrderTemplateData t, bool metaphor, out string direct)
    {
        direct = null;
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
        requirement.color = (FlowerData.Color)color;
        requirement.minCount = count;

        string template = t.colorDirect[Random.Range(0, t.colorDirect.Count)];
        string Fill(string colorName) => KoreanParticle.Replace(KoreanParticle.Replace(template, "색", colorName, $"<b>{colorName}</b>"), "개수", $"{count}개", $"<b>{count}개</b>");
        if (metaphor) direct = Fill(ColorPalette.ToKoreanName((FlowerData.Color)color) + "색");
        return Fill(name);
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
        requirement.color = (FlowerData.Color)Random.Range(0, 6);
        requirement.minCount = 2;
        string name = ColorPalette.ToKoreanName(requirement.color) + "색";
        return $"<b>{name}</b> 꽃 <b>2개</b>는 꼭 넣어주세요.";
    }

    private static void SetFlower(ref CustomerRequirement requirement, FlowerData first, FlowerData second)
    {
        requirement.type = RequirementType.Flower;
        requirement.flowerId = first.flowerID;
        requirement.flowerId2 = second != null ? second.flowerID : -1;
        requirement.minCount = second != null ? 2 : 1; // 말한 꽃마다 한 송이 이상
    }

    // ── 요구사항 재료 ────────────────────────────────────────────────────────

    /// <summary>플레이어가 획득한 꽃(FlowerData) 목록.</summary>
    private static List<FlowerData> ObtainedFlowers()
    {
        if (CurrencyManager.Instance == null || DataRegistry.Instance == null) return new List<FlowerData>();
        return CurrencyManager.Instance.GetObtainedFlowerIds()
            .Select(id => DataRegistry.Instance.GetFlower(id))
            .Where(f => f != null)
            .ToList();
    }

    /// <summary>프리셋에서 이 꽃을 최대 몇 송이 놓을 수 있나 (그 꽃의 역할 칸 수).</summary>
    private static int FlowerCapacity(WrapperPresetData preset, FlowerData flower)
    {
        return preset == null ? int.MaxValue : preset.CountByTag(flower.flowerRole);
    }

    /// <summary>프리셋에서 이 색 꽃을 최대 몇 송이 놓을 수 있나 (그 색 꽃이 있는 역할들의 칸 수 합).</summary>
    private static int ColorCapacity(WrapperPresetData preset, FlowerData.Color color)
    {
        if (preset == null) return int.MaxValue;
        return ObtainedFlowers().Where(f => f.color == color).Select(f => f.flowerRole).Distinct().Sum(role => preset.CountByTag(role));
    }

    /// <summary>꽃 두 종을 한 송이씩 놓을 수 있나: 서로 다른 역할이면 각 역할에 한 칸씩, 같은 역할이면 그 역할에 두 칸이 있어야 한다.</summary>
    private static bool CanPlacePair(WrapperPresetData preset, FlowerData a, FlowerData b)
    {
        if (preset == null) return true;
        if (a.flowerRole == b.flowerRole) return preset.CountByTag(a.flowerRole) >= 2;
        return FlowerCapacity(preset, a) >= 1 && FlowerCapacity(preset, b) >= 1;
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
