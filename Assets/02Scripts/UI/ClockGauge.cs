using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DayClock의 남은 시간을 게이지로 보여준다. Day/Night는 원형 Image(fillAmount), Mood는 세로 띠 Image(fillAmount)에
/// Slider 핸들(얼굴)이 따라 움직인다. 얼굴은 띠의 초록/노랑/빨강 구간에 맞춰 바뀐다.
/// 낮 메인과 낮 퍼즐 씬이 같은 프리팹을 쓴다. DayClock이 없으면(씬 단독 실행) 가득 찬 상태로 둔다.
/// </summary>
public class ClockGauge : MonoBehaviour
{
    private enum Kind { Day, Mood, Night }

    // 띠 이미지(UI_MoodBar_Fill)의 초록/노랑 경계에 맞춘 구간
    private const float HappyAbove = 0.65f;
    private const float NormalAbove = 0.32f;

    [SerializeField] private Kind kind;
    [SerializeField] private Image fillImage;
    [SerializeField] private Slider slider;

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
        DayClock clock = DayClock.Instance;
        float ratio = clock == null ? 1f : kind == Kind.Day ? clock.DayRatio : kind == Kind.Night ? clock.NightRatio : clock.MoodRatio;

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
}
