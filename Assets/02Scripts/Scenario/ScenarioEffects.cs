using UnityEngine;

/// <summary>
/// 대사 한 줄이 끝날 때 CSV의 효과를 실제로 일으킨다. 퇴장과 시간경과는 대화가 끝난 뒤의 흐름(MainUI)이 정하므로 여기서는 다루지 않는다.
/// 획득 알림(포장지 선물, 카탈로그)은 NoticeToast로 띄운다. 아직 연결하지 않은 효과(꽃지급, 튜토리얼)는 콘솔에만 남긴다(주문 효과는 GameManager가 낮 주문을 만들 때 읽는다).
/// </summary>
public static class ScenarioEffects
{
    public static void Apply(DialogueEffect effect)
    {
        switch (effect.kind)
        {
            case DialogueEffect.Gift:
                GiveWrapper(effect);
                break;
            case DialogueEffect.Catalog:
                NoticeToast.Show($"{(effect.target == "용품" ? "꽃다발 용품" : "가구")} 카탈로그 {Count("NEW")}");
                break;
            case DialogueEffect.Leave:
            case DialogueEffect.TimeSkip:
            case DialogueEffect.Unlock: // 해금은 일수에서 계산한다(ScenarioManager)
                break;
            default:
                Debug.Log($"[Scenario] 아직 연결하지 않은 효과: {effect}");
                break;
        }
    }

    private static void GiveWrapper(DialogueEffect effect)
    {
        int wrapper = effect.WrapperNumber;
        if (wrapper <= 0 || CurrencyManager.Instance == null)
        {
            Debug.LogWarning($"[Scenario] 선물 대상을 알 수 없습니다: {effect}");
            return;
        }

        CurrencyManager.Instance.AddWrapperStockNextDay(wrapper, effect.amount); // 받은 포장지는 다음 날부터 쓸 수 있다
        WrapperData data = DataRegistry.Instance != null ? DataRegistry.Instance.GetWrapper(wrapper) : null;
        NoticeToast.Show($"{(data != null ? data.ColorName + "색 " : "")}포장지 {Count($"X{effect.amount}")}");
    }

    private static string Count(string text) => NoticeToast.Highlight(text);
}
