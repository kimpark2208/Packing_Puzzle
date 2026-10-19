using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 시계 가운데에 뜨는 글씨(그림 없이 글씨만). 하루가 시작되면(씬 전환이 끝나고 밤 → 낮 연출로 낮 시계가 다 채워진 뒤) "N년차 / 계절"이 잠깐 뜨고, 그 뒤로는 "N일"이 계속 보인다.
/// 그 밖의 화면(밤, 퍼즐, 결산)과 하루 중 손님이 바뀌어 메인 화면이 다시 열릴 때는 처음부터 "N일"이다.
/// 글로벌 UI의 시계 아래 DayLabel에 붙이고, 세 글씨(년차, 계절, 일)의 자리와 크기는 에디터에서 정한다. 베타는 1년차 봄뿐이라 년차와 계절은 인스펙터 값이다.
/// </summary>
public class DayLabel : MonoBehaviour
{
    [SerializeField] private TMP_Text yearText;
    [SerializeField] private TMP_Text seasonText;
    [SerializeField] private TMP_Text dayText;

    [SerializeField, Min(1), Tooltip("몇 년차인가(베타는 1년차뿐).")] private int year = 1;
    [SerializeField, Tooltip("계절(베타는 봄뿐).")] private string season = "봄";
    [SerializeField, Min(0f), Tooltip("하루가 시작될 때 \"N년차 계절\"이 다 보인 채로 떠 있는 시간(초). 그 뒤 \"N일\"로 바뀐다.")] private float introSeconds = 1f;
    [SerializeField, Min(0f), Tooltip("글씨가 나타나고 사라지는 시간(초). \"N년차 계절\"이 서서히 나타나고, 서서히 사라지고, \"N일\"이 서서히 나타난다. 0이면 바로 바뀐다.")] private float fadeSeconds = 0.3f;
    [SerializeField, Tooltip("세 글씨를 한꺼번에 서서히 보이고 숨기는 CanvasGroup(DayLabel 오브젝트에 둔다).")] private CanvasGroup group;

    [Header("글씨 색")]
    [SerializeField, Tooltip("시계가 낮 모습일 때.")] private Color dayColor = new(0.353f, 0.231f, 0.141f, 1f);
    [SerializeField, Tooltip("시계가 밤 모습일 때(밤 시계 바탕이 어두워서 밝은 색을 쓴다).")] private Color nightColor = new(1f, 0.965f, 0.89f, 1f);

    private int shownDay = -1;
    private bool shownNight;

    private void Start()
    {
        yearText.text = $"{year}년차";
        seasonText.text = season;
        SetIntro(false, false);
        group.alpha = 1f; // 에디터에서 저장된 투명도와 상관없이 보이게 시작한다(년차·계절은 Intro가 0에서 올린다)

        // 하루가 막 시작됐으면 시계가 다 채워질 때까지 비워 두었다가 "N년차 계절"을 보인다
        if (GameManager.Instance != null && GameManager.Instance.ConsumeDayIntro()) StartCoroutine(Intro());
        else SetIntro(false, true);
    }

    private void Update()
    {
        int day = CurrencyManager.Instance != null ? CurrencyManager.Instance.CurrentDay : 1;
        if (day != shownDay)
        {
            shownDay = day;
            dayText.text = $"{day}일차";
        }

        bool night = GameClock.Instance != null && GameClock.Instance.IsNightMode;
        if (night != shownNight || dayText.color != (night ? nightColor : dayColor))
        {
            shownNight = night;
            foreach (TMP_Text text in new[] { yearText, seasonText, dayText }) text.color = night ? nightColor : dayColor;
        }
    }

    private IEnumerator Intro()
    {
        yield return null; // 같은 프레임의 다른 Start(메인 화면의 밤 → 낮 연출)가 먼저 끝나게 한다
        while (SceneTransition.Busy || (GameClock.Instance != null && GameClock.Instance.MorningFill >= 0f)) yield return null;

        group.alpha = 0f;
        SetIntro(true, false);
        yield return Fade(0f, 1f);
        yield return new WaitForSecondsRealtime(introSeconds);
        yield return Fade(1f, 0f);
        SetIntro(false, true);
        yield return Fade(0f, 1f);
    }

    private IEnumerator Fade(float from, float to)
    {
        for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, t / fadeSeconds);
            yield return null;
        }
        group.alpha = to;
    }

    /// <summary>년차·계절을 보일지, 일을 보일지 정한다(둘 다 false면 비워 둔다).</summary>
    private void SetIntro(bool intro, bool showDay)
    {
        yearText.gameObject.SetActive(intro);
        seasonText.gameObject.SetActive(intro);
        dayText.gameObject.SetActive(showDay);
    }
}
