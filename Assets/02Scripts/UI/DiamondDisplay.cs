using TMPro;
using UnityEngine;

/// <summary>현재 다이아 개수를 보여주는 텍스트(상단 자원 바의 다이아 버튼). 개수가 바뀌면 같이 갱신된다.</summary>
[RequireComponent(typeof(TMP_Text))]
public class DiamondDisplay : MonoBehaviour
{
    private TMP_Text text;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        EventBus.OnDiamondsChanged += Show;
        Show(CurrencyManager.Instance != null ? CurrencyManager.Instance.CurrentDiamonds : 0);
    }

    private void OnDisable()
    {
        EventBus.OnDiamondsChanged -= Show;
    }

    private void Show(int diamonds)
    {
        text.text = diamonds.ToString();
    }
}
