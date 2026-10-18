using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GameClock의 남은 시간을 원형 Image(fillAmount)로 보여준다. 낮에는 하루 시간, 밤에는 밤 시간을 보여 주고(채우기 그림도 낮/밤 모습으로 바뀐다),
/// 낮 메인, 꽃 선택, 낮 퍼즐, 밤 퍼즐 씬이 같은 프리팹을 쓴다. GameClock이 없으면(씬 단독 실행) 가득 찬 상태로 둔다.
/// 시계 옆 전등 영역도 같은 낮/밤 판단으로 보이고 숨긴다.
/// </summary>
public class ClockGauge : MonoBehaviour
{
    [SerializeField] private Image fillImage;

    [Header("채우기 그림: 낮/밤에 보여 줄 모습")]
    [SerializeField] private Sprite dayFillSprite;
    [SerializeField] private Sprite nightFillSprite;

    [Header("전등 영역: 낮 종료 화면과 밤 화면에서만 보인다(낮 영업 중에는 숨긴다)")]
    [SerializeField] private GameObject lanternArea;

    private void Update()
    {
        GameClock clock = GameClock.Instance;
        bool night = clock != null && clock.IsNightMode;
        if (lanternArea != null) lanternArea.SetActive(night);

        bool morning = clock != null && clock.MorningFill >= 0f; // 밤 → 낮 연출: 비워진 밤 시계에서 낮 시계가 채워진다
        Sprite sprite = morning ? (clock.MorningFill > 0f ? dayFillSprite : nightFillSprite) : night ? nightFillSprite : dayFillSprite;
        if (fillImage != null && sprite != null && fillImage.sprite != sprite) fillImage.sprite = sprite;
        if (fillImage != null) fillImage.fillAmount = clock == null ? 1f : morning ? clock.MorningFill : night ? clock.NightRatio : clock.DayRatio;
    }
}
