using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬을 옮길 때 화면을 검게 덮었다가(페이드 아웃) 씬을 바꾼 뒤 걷는다(페이드 인). 씬 이동은 LoadScene 대신 Go를 쓴다.
/// 덮개(CanvasGroup)는 이 오브젝트와 함께 씬을 넘어 유지된다. 시작 씬에 두며, 없으면(씬 단독 실행) 효과 없이 바로 옮긴다.
/// 나중에 이미지/와이프 전환으로 바꿀 때는 이 컴포넌트의 덮고 걷는 부분(Fade)만 바꾸면 된다.
/// </summary>
public class SceneTransition : Singleton<SceneTransition>
{
    [SerializeField] private CanvasGroup cover;
    [SerializeField, Min(0.01f)] private float fadeOutSeconds = 0.25f;
    [SerializeField, Min(0.01f)] private float fadeInSeconds = 0.25f;

    private bool busy;

    /// <summary>화면을 덮기 시작했다(덮는 시간 전달). 소리를 같이 줄일 때 쓴다(AudioFadeOnTransition).</summary>
    public static event Action<float> FadingOut;

    /// <summary>화면을 덮고 씬을 바꾼 뒤 걷는다. 이미 전환 중이면 무시한다.</summary>
    public static void Go(string scene)
    {
        SceneTransition transition = Instance;
        if (transition == null) SceneManager.LoadScene(scene);
        else if (!transition.busy) transition.StartCoroutine(transition.Run(scene));
    }

    /// <summary>씬 불러오기를 직접 다루는 쪽(로딩 씬)용: 화면을 덮는다. 덮은 뒤 씬을 열고 Reveal로 걷는다.</summary>
    public static IEnumerator Cover()
    {
        SceneTransition transition = Instance;
        return transition != null ? transition.CoverRoutine() : null;
    }

    /// <summary>덮어 둔 화면을 걷는다(새 씬이 한 번 그려진 뒤에).</summary>
    public static void Reveal()
    {
        SceneTransition transition = Instance;
        if (transition != null) transition.StartCoroutine(transition.RevealRoutine());
    }

    private IEnumerator Run(string scene)
    {
        yield return CoverRoutine();
        SceneManager.LoadScene(scene);
        yield return RevealRoutine();
    }

    private IEnumerator CoverRoutine()
    {
        busy = true;
        cover.blocksRaycasts = true; // 전환 중에는 아래 화면을 누르지 못하게 한다
        FadingOut?.Invoke(fadeOutSeconds);
        yield return Fade(0f, 1f, fadeOutSeconds);
    }

    private IEnumerator RevealRoutine()
    {
        yield return null;
        yield return null; // 새 씬이 한 번 그려진 뒤에 걷는다
        yield return Fade(1f, 0f, fadeInSeconds);
        cover.blocksRaycasts = false;
        busy = false;
    }

    private IEnumerator Fade(float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            cover.alpha = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }
        cover.alpha = to;
    }
}
