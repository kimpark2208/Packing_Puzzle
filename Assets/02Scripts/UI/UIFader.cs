using System.Collections;
using UnityEngine;

/// <summary>
/// 독립적인 UI 페이더. 페이드할 오브젝트에 이 컴포넌트만 붙이면(CanvasGroup은 같이 붙는다) 페이드인 시간, 머무는 시간, 페이드아웃 시간을 인스펙터에서 정한다.
/// 다른 코드는 이 컴포넌트를 참조해서 FadeIn()/FadeOut()/Pulse()를 부르고, 필요하면 FadeInSeconds 같은 값을 읽어 간다(시간을 부른 쪽이 따로 들고 있지 않는다).
/// 페이드는 코루틴으로 돌려준다: <c>yield return fader.FadeIn();</c> 처럼 기다리거나 그냥 StartCoroutine으로 돌린다. 시간은 timeScale과 상관없이 흐른다(unscaled).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class UIFader : MonoBehaviour
{
    [SerializeField, Min(0f), Tooltip("서서히 나타나는 시간(초). 0이면 바로.")] private float fadeInSeconds = 0.3f;
    [SerializeField, Min(0f), Tooltip("다 보인 채로 머무는 시간(초). Pulse()와, 이 값을 읽어 가는 쪽(예: DayLabel)이 쓴다.")] private float holdSeconds;
    [SerializeField, Min(0f), Tooltip("서서히 사라지는 시간(초). 0이면 바로.")] private float fadeOutSeconds = 0.3f;

    private CanvasGroup group;

    private CanvasGroup Group => group != null ? group : group = GetComponent<CanvasGroup>();

    public float FadeInSeconds => fadeInSeconds;
    public float HoldSeconds => holdSeconds;
    public float FadeOutSeconds => fadeOutSeconds;

    /// <summary>지금 투명도(0 = 안 보임, 1 = 다 보임).</summary>
    public float Alpha
    {
        get => Group.alpha;
        set => Group.alpha = value;
    }

    public IEnumerator FadeIn() => FadeTo(1f, fadeInSeconds);

    public IEnumerator FadeOut() => FadeTo(0f, fadeOutSeconds);

    /// <summary>나타났다 → 머물렀다 → 사라진다(점멸 한 번).</summary>
    public IEnumerator Pulse()
    {
        yield return FadeIn();
        if (holdSeconds > 0f) yield return new WaitForSecondsRealtime(holdSeconds);
        yield return FadeOut();
    }

    /// <summary>지금 투명도에서 alpha까지 seconds 동안 서서히 바꾼다(0이면 바로).</summary>
    public IEnumerator FadeTo(float alpha, float seconds)
    {
        float from = Alpha;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            Alpha = Mathf.Lerp(from, alpha, t / seconds);
            yield return null;
        }
        Alpha = alpha;
    }
}
