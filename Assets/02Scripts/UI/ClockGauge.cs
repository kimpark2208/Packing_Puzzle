using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DayClock의 남은 시간을 게이지로 보여준다. Day/Night는 원형 Image(fillAmount), Mood는 Slider(value).
/// 낮 메인과 낮 퍼즐 씬이 같은 프리팹을 쓴다. DayClock이 없으면(씬 단독 실행) 가득 찬 상태로 둔다.
/// </summary>
public class ClockGauge : MonoBehaviour
{
    private enum Kind { Day, Mood, Night }

    [SerializeField] private Kind kind;
    [SerializeField] private Image fillImage;
    [SerializeField] private Slider slider;

    private Image sliderFill;

    private void Awake()
    {
        if (slider == null) return;
        slider.interactable = false;
        if (slider.fillRect != null) sliderFill = slider.fillRect.GetComponent<Image>();
    }

    private void Update()
    {
        DayClock clock = DayClock.Instance;
        float ratio = clock == null ? 1f : kind == Kind.Day ? clock.DayRatio : kind == Kind.Night ? clock.NightRatio : clock.MoodRatio;

        if (fillImage != null) fillImage.fillAmount = ratio;
        if (slider != null) slider.value = ratio;

        // 기분 게이지: 가득 차면 초록, 줄어들수록 노랑을 거쳐 빨강. (RGB 보간은 중간이 탁해져서 색상(Hue)으로 보간)
        if (sliderFill != null) sliderFill.color = Color.HSVToRGB(Mathf.Lerp(0f, 0.33f, ratio), 0.8f, 0.95f);
    }
}
