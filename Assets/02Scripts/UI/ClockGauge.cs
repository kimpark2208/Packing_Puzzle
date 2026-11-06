using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GameClock의 남은 시간을 원형 Image(fillAmount)로 보여준다. 영업 시간(하루 시간)을 보여 주고(밤이 되면 채우기 그림이 밤 모습으로 바뀐다),
/// 낮 메인, 꽃 선택, 낮 퍼즐 씬이 같은 프리팹을 쓴다. GameClock이 없으면(씬 단독 실행) 가득 찬 상태로 둔다.
/// 시계 옆 전등 영역은 밤의 메인 화면(카운터)에서만 보인다.
/// </summary>
public class ClockGauge : MonoBehaviour
{
    [SerializeField] private Image fillImage;

    [Header("채우기 그림: 낮/밤에 보여 줄 모습")]
    [SerializeField] private Sprite dayFillSprite;
    [SerializeField] private Sprite nightFillSprite;

    [Header("전등 영역: 밤의 메인 화면(카운터)에서만 보인다. 낮 영업 중이거나, 포장지·꽃 선택 페이지·퍼즐 씬이면 숨긴다")]
    [SerializeField] private GameObject lanternArea;

    private SwipeParallax swipe; // 메인 씬의 카운터 ↔ 옆 페이지 이동(전등은 카운터 페이지에서만 보인다)

    private void Update()
    {
        GameClock clock = GameClock.Instance;
        bool night = clock != null && clock.IsNightMode;
        if (lanternArea != null) lanternArea.SetActive(night && OnCounter());

        bool morning = clock != null && clock.MorningFill >= 0f; // 밤 → 낮 연출: 비워진 밤 시계에서 낮 시계가 채워진다
        Sprite sprite = morning ? (clock.MorningFill > 0f ? dayFillSprite : nightFillSprite) : night ? nightFillSprite : dayFillSprite;
        if (fillImage != null && sprite != null && fillImage.sprite != sprite) fillImage.sprite = sprite;
        if (fillImage != null) fillImage.fillAmount = clock == null ? 1f : morning ? clock.MorningFill : clock.DayRatio;
    }

    /// <summary>지금 메인 씬의 카운터 페이지가 화면에 있는가(포장지·꽃 선택 같은 옆 페이지로 넘어가는 중이거나 가 있으면 false).</summary>
    private bool OnCounter()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != GameManager.SceneDayMain) return false;

        if (swipe == null) swipe = FindFirstObjectByType<SwipeParallax>();
        return swipe == null || swipe.IsAtHome;
    }
}
