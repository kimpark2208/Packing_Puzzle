using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 정산 결과창의 -100 ~ +100 게이지. 슬라이더(핸들 = 흰 막대)가 값을 들고 있고,
/// 0을 기준으로 왼쪽(빨강)/오른쪽(초록) 채우기 폭과 핸들 아래 숫자를 그 값에 맞춘다.
/// 에디터에서도 슬라이더 값을 바꾸면 바로 보인다.
/// </summary>
[ExecuteAlways]
public class ResultGauge : MonoBehaviour
{
    [SerializeField] private Slider slider; // 최소/최대는 슬라이더에서 정한다(-100 ~ +100)
    [SerializeField] private RectTransform fillNegative; // 0에서 왼쪽으로 차는 채우기(오른쪽 끝이 기준)
    [SerializeField] private RectTransform fillPositive; // 0에서 오른쪽으로 차는 채우기(왼쪽 끝이 기준)
    [SerializeField] private TMP_Text valueText;
    [Header("채우기 안의 줄무늬: 크기가 고정이고 채우기의 RectMask2D가 잘라 보여 준다. 채우기 이미지의 색(빨강/초록)을 그대로 입는다")]
    [SerializeField] private Image patternNegative;
    [SerializeField] private Image patternPositive;

    [Header("구간 라벨 (점수가 속한 구간을 강조. 기본 보상 라벨은 늘 같은 모습이다)")]
    [SerializeField] private Band[] bands;
    [SerializeField] private Color idleColor = new(0.24f, 0.24f, 0.24f, 0.95f);
    [SerializeField] private Color idleTextColor = new(0.82f, 0.82f, 0.80f);
    [SerializeField] private Color highlightTextColor = Color.white;

    [System.Serializable]
    private class Band
    {
        public int tick; // -100, -50, -25, 0, 25, 50, 100 (게이지 눈금)
        public Image background;
        public TMP_Text label;
    }

    public float Value
    {
        get => slider.value;
        set => slider.value = value;
    }

    private void Update()
    {
        if (slider == null) return;

        float value = slider.value;
        float perUnit = ((RectTransform)slider.transform).rect.width / (slider.maxValue - slider.minValue);

        SetWidth(fillNegative, Mathf.Max(0f, -value) * perUnit);
        SetWidth(fillPositive, Mathf.Max(0f, value) * perUnit);

        Color negativeColor = fillNegative.GetComponent<Image>().color;
        Color positiveColor = fillPositive.GetComponent<Image>().color;
        if (patternNegative != null) patternNegative.color = negativeColor;
        if (patternPositive != null) patternPositive.color = positiveColor;

        if (valueText != null)
        {
            valueText.text = value > 0f ? $"+{value:0}" : $"{value:0}";
            valueText.color = value < 0f ? negativeColor : value > 0f ? positiveColor : Color.white;
        }

        if (bands == null) return;

        int activeTick = ScoreTier.Tick(value);
        foreach (Band band in bands)
        {
            if (band.tick == 0) continue; // 기본 보상 라벨은 늘 같은 모습
            bool on = band.tick == activeTick;
            band.background.color = on ? (band.tick < 0 ? negativeColor : positiveColor) : idleColor;
            band.label.color = on ? highlightTextColor : idleTextColor;
        }
    }

    private static void SetWidth(RectTransform rect, float width)
    {
        if (rect != null && !Mathf.Approximately(rect.sizeDelta.x, width)) rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
    }
}
