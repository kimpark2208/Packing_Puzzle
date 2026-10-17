using UnityEngine;

/// <summary>
/// 효과음 한 번 재생(2D). 소리를 내는 오브젝트가 곧 꺼지거나 사라져도(예: 팝업이 닫히는 소리) 끝까지 들리도록,
/// 씬을 넘어 남는 전용 AudioSource 하나로 재생한다. 처음 쓸 때 만들어진다.
/// </summary>
public static class Sfx
{
    private static AudioSource source;
    private static AudioClip lastRandom;

    public static void Play(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;

        if (source == null)
        {
            var go = new GameObject("SfxPlayer");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }
        source.PlayOneShot(clip, volume);
    }

    /// <summary>여러 소리 중 하나를 무작위로 재생한다. 바로 앞에 낸 소리는 되도록 다시 고르지 않는다.</summary>
    public static void PlayRandom(AudioClip[] clips, float volume = 1f)
    {
        if (clips == null || clips.Length == 0) return;

        AudioClip pick = clips[Random.Range(0, clips.Length)];
        if (clips.Length > 1 && pick == lastRandom) pick = clips[(System.Array.IndexOf(clips, pick) + 1 + Random.Range(0, clips.Length - 1)) % clips.Length];
        lastRandom = pick;
        Play(pick, volume);
    }
}
