using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GameClock의 남은 시간을 원형 Image(fillAmount)로 보여준다. 낮에는 하루 시간, 밤에는 밤 시간을 보여 주고(채우기 그림도 낮/밤 모습으로 바뀐다),
/// 낮 메인, 꽃 선택, 낮 퍼즐, 밤 퍼즐 씬이 같은 프리팹을 쓴다. GameClock이 없으면(씬 단독 실행) 가득 찬 상태로 둔다.
/// </summary>
public class ClockGauge : MonoBehaviour
{
    [SerializeField] private Image fillImage;

    [Header("채우기 그림: 낮/밤에 보여 줄 모습")]
    [SerializeField] private Sprite dayFillSprite;
    [SerializeField] private Sprite nightFillSprite;

    private void Update()
    {
        GameClock clock = GameClock.Instance;
        bool night = clock != null && clock.IsNightMode;

        Sprite sprite = night ? nightFillSprite : dayFillSprite;
        if (fillImage != null && sprite != null && fillImage.sprite != sprite) fillImage.sprite = sprite;
        if (fillImage != null) fillImage.fillAmount = clock == null ? 1f : night ? clock.NightRatio : clock.DayRatio;
    }
}
