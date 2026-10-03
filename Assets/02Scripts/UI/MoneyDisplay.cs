using TMPro;
using UnityEngine;

/// <summary>현재 금액을 "{금액}원"으로 보여주는 텍스트. 금액이 바뀌면 같이 갱신된다.</summary>
[RequireComponent(typeof(TMP_Text))]
public class MoneyDisplay : MonoBehaviour
{
    private TMP_Text text;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        EventBus.OnMoneyChanged += Show;
        if (CurrencyManager.Instance != null) Show(CurrencyManager.Instance.CurrentMoney);
    }

    private void OnDisable()
    {
        EventBus.OnMoneyChanged -= Show;
    }

    private void Show(int money)
    {
        text.text = $"{money}원";
    }
}
