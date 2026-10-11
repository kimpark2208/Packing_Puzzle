using TMPro;
using UnityEngine;

/// <summary>보유 부적 수량을 "{보유}/{한도}"로 보여주는 텍스트(상단 재화 표시 옆). 수량이 바뀌면 같이 갱신된다.</summary>
[RequireComponent(typeof(TMP_Text))]
public class CharmCountDisplay : MonoBehaviour
{
    private TMP_Text text;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        EventBus.OnCharmsChanged += Show;
        if (CurrencyManager.Instance != null) Show(CurrencyManager.Instance.Charms);
    }

    private void OnDisable()
    {
        EventBus.OnCharmsChanged -= Show;
    }

    private void Show(int charms)
    {
        int max = CurrencyManager.Instance != null ? CurrencyManager.Instance.MaxCharms : charms;
        text.text = $"{charms}/{max}";
    }
}
