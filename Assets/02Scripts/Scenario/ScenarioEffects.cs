using System.Linq;
using UnityEngine;

/// <summary>
/// 대사 한 줄이 끝날 때 CSV의 효과를 실제로 일으킨다. 퇴장과 시간경과는 대화가 끝난 뒤의 흐름(MainUI)이 정하므로 여기서는 다루지 않는다.
/// 받는 것은 모두 획득 하나로 쓰고(포장지·꽃·꾸미기 용품·카탈로그·소지품), 받는 순간 NoticeToast로 획득 알림을 띄운다.
/// 아직 연결하지 않은 효과(튜토리얼)는 콘솔에만 남긴다(주문 효과는 GameManager가 낮 주문을 만들 때 읽는다).
/// </summary>
public static class ScenarioEffects
{
    private const string CatalogSuffix = " 카탈로그";

    public static void Apply(DialogueEffect effect)
    {
        switch (effect.kind)
        {
            case DialogueEffect.Gift:
                GiveItem(effect);
                break;
            case DialogueEffect.Leave:
            case DialogueEffect.Remember: // 기억은 대사를 재생하는 DialoguePlayer가 선택 번호와 함께 처리한다
            case DialogueEffect.TimeSkip:
            case DialogueEffect.Unlock: // 해금은 일수에서 계산한다(ScenarioManager)
                break;
            default:
                Debug.Log($"[Scenario] 아직 연결하지 않은 효과: {effect}");
                break;
        }
    }

    /// <summary>획득 대상이 카탈로그(`획득:가구 카탈로그`, `획득:용품 카탈로그`)면 그 종류(가구/용품). 아니면 null.</summary>
    public static string CatalogOf(string target) => target != null && target.EndsWith(CatalogSuffix) ? target.Substring(0, target.Length - CatalogSuffix.Length).Trim() : null;

    /// <summary>
    /// 획득: 받는 순간 들어온다. 대상은 아래 순서로 찾는다.
    /// - 포장지(`획득:포장지2x10`): 재고에 들어가 바로 쓸 수 있다.
    /// - 꽃(`획득:금어초x40`, 꽃 이름): 재고에 들어가고 얻은 꽃이 되어 주문과 상점 진열에 나온다.
    /// - 꾸미기 용품(`획득:웨딩 리본x1`, 용품 이름): 재고에 들어간다.
    /// - 카탈로그(`획득:가구 카탈로그`, `획득:용품 카탈로그`): 그 카탈로그가 열린다.
    /// - 그 밖의 이름(`획득:에어팟x1`): 소지품. 이름과 개수를 기억해 두고(ScenarioMemory), 대사 조건(`에어팟=1`)이 읽는다. 건네면 `기억:에어팟=0`으로 지운다.
    /// </summary>
    private static void GiveItem(DialogueEffect effect)
    {
        string catalog = CatalogOf(effect.target);
        if (catalog != null)
        {
            if (CurrencyManager.Instance != null) CurrencyManager.Instance.UnlockCatalog(catalog == "용품" ? CurrencyManager.CatalogKind.Supplies : CurrencyManager.CatalogKind.Furniture);
            NoticeToast.Show($"{(catalog == "용품" ? "꽃다발 용품" : "가구")} 카탈로그 {NoticeToast.Highlight("NEW")}");
            return;
        }

        int wrapper = effect.WrapperNumber;
        if (wrapper > 0)
        {
            if (CurrencyManager.Instance == null) return;

            CurrencyManager.Instance.AddWrapperStock(wrapper, effect.amount); // 받은 포장지는 받는 순간 재고에 들어가 바로 쓸 수 있다
            WrapperData data = DataRegistry.Instance != null ? DataRegistry.Instance.GetWrapper(wrapper) : null;
            NoticeToast.Show($"{(data != null ? data.ColorName + "색 " : "")}포장지 {NoticeToast.Highlight($"X{effect.amount}")}");
            return;
        }

        if (CurrencyManager.Instance != null && DataRegistry.Instance != null)
        {
            FlowerData flower = DataRegistry.Instance.Flowers.FirstOrDefault(f => f != null && f.DisplayName == effect.target);
            if (flower != null)
            {
                CurrencyManager.Instance.ObtainFlower(flower.flowerID);
                CurrencyManager.Instance.AddFlowerStock(flower.flowerID, effect.amount);
                NoticeToast.Show($"새 꽃 {flower.DisplayName} {NoticeToast.Highlight($"X{effect.amount}")}");
                return;
            }

            DecorationData decoration = DataRegistry.Instance.Decorations.FirstOrDefault(d => d != null && d.displayName == effect.target);
            if (decoration != null)
            {
                CurrencyManager.Instance.AddDecorationStock(decoration.id, effect.amount);
                NoticeToast.Show($"{decoration.displayName} {NoticeToast.Highlight($"X{effect.amount}")}");
                return;
            }
        }

        ScenarioMemory.Set(effect.target, effect.amount); // 소지품: 같은 획득을 다시 받아도(하루 스킵 등) 값이 그대로여서 조건(`에어팟=1`)이 깨지지 않는다. 알림은 개수가 아니라 카탈로그처럼 NEW
        NoticeToast.Show($"{effect.target} {NoticeToast.Highlight("NEW")}");
    }
}
