using System.Collections;
using UnityEngine;

/// <summary>
/// 씬을 옮기려고 화면이 덮이기 시작하면(SceneTransition) 이 소리의 볼륨을 같은 시간 동안 0까지 줄인다.
/// 씬이 바뀌면 소리가 뚝 끊기는 것을 막는다. 소리가 나는 오브젝트(AudioSource)에 붙인다.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioFadeOnTransition : MonoBehaviour
{
    private AudioSource source;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        SceneTransition.FadingOut += FadeOut;
    }

    private void OnDisable()
    {
        SceneTransition.FadingOut -= FadeOut;
    }

    private void FadeOut(float seconds)
    {
        StopAllCoroutines();
        StartCoroutine(Fade(seconds));
    }

    private IEnumerator Fade(float seconds)
    {
        float from = source.volume;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            source.volume = Mathf.Lerp(from, 0f, t / seconds);
            yield return null;
        }
        source.volume = 0f;
    }
}
