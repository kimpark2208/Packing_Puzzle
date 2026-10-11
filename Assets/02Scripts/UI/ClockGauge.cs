using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GameClock의 남은 시간을 게이지로 보여준다. Clock은 원형 Image(fillAmount)로 낮에는 하루 시간, 밤에는 밤 시간을 보여 주고(채우기 그림도 낮/밤 모습으로 바뀐다),
/// Mood는 세로 띠 Image(fillAmount)에 Slider 핸들(얼굴)이 따라 움직인다. 얼굴은 띠의 초록/노랑/빨강 구간에 맞춰 바뀐다.
/// 낮 메인, 꽃 선택, 낮 퍼즐, 밤 퍼즐 씬이 같은 프리팹을 쓴다. GameClock이 없으면(씬 단독 실행) 가득 찬 상태로 둔다.
/// </summary>
public class ClockGauge : MonoBehaviour
{
    private enum Kind { Clock, Mood }

    // 띠 이미지(UI_MoodBar_Fill)의 초록/노랑 경계에 맞춘 구간
    private const float HappyAbove = 0.65f;
    private const float NormalAbove = 0.32f;

    [SerializeField] private Kind kind;
    [SerializeField] private Image fillImage;
    [SerializeField] private Slider slider;

    [Header("Clock fill: 낮/밤에 보여 줄 채우기 그림")]
    [SerializeField] private Sprite dayFillSprite;
    [SerializeField] private Sprite nightFillSprite;

    [Header("Mood face")]
    [SerializeField] private Image faceImage;
    [SerializeField] private Sprite happyFace;
    [SerializeField] private Sprite normalFace;
    [SerializeField] private Sprite badFace;

    private void Awake()
    {
        if (slider != null) slider.interactable = false;
    }

    private void Update()
    {
        GameClock clock = GameClock.Instance;
        float ratio = 1f;
        if (clock != null)
        {
            bool night = kind == Kind.Clock && clock.IsNightMode;
            ratio = kind == Kind.Mood ? clock.MoodRatio : night ? clock.NightRatio : clock.DayRatio;
        }

        if (kind == Kind.Clock) ShowClockFill(clock != null && clock.IsNightMode);
        if (fillImage != null) fillImage.fillAmount = ratio;
        if (slider != null) slider.value = ratio;

        if (faceImage != null)
        {
            Sprite face = ratio > HappyAbove ? happyFace : ratio > NormalAbove ? normalFace : badFace;
            if (face != null && faceImage.sprite != face)
            {
                faceImage.sprite = face;
                faceImage.SetNativeSize();
            }
        }
    }

    private void ShowClockFill(bool night)
    {
        Sprite sprite = night ? nightFillSprite : dayFillSprite;
        if (fillImage != null && sprite != null && fillImage.sprite != sprite) fillImage.sprite = sprite;
    }
}
