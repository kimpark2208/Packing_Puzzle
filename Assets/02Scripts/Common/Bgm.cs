using UnityEngine;

/// <summary>
/// 배경음악 재생(2D, 반복). 씬을 넘어 남는 전용 AudioSource 하나로 재생하므로, 같은 음악을 요청하면 끊기지 않고 이어서 나온다
/// (낮 메인 ↔ 낮 퍼즐을 오가도 음악이 처음부터 다시 시작하지 않는다). 다른 음악을 요청하면 지금 음악이 줄어든 뒤 새 음악이 커진다.
/// 처음 쓸 때 만들어진다.
/// </summary>
public static class Bgm
{
    private static Runner runner;

    /// <summary>음악을 튼다(null이면 끈다). 이미 나오는 음악이면 볼륨도 그대로 둔다(처음 튼 씬의 볼륨이 이어진다). fadeSeconds = 볼륨 0 ↔ 1을 오가는 시간.</summary>
    public static void Play(AudioClip clip, float volume = 0.5f, float fadeSeconds = 1f)
    {
        if (runner == null)
        {
            if (clip == null) return;

            var go = new GameObject("Bgm");
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
        }
        if (clip != null && runner.Wanted == clip) return; // 이미 나오는(또는 켜지는 중인) 음악은 볼륨도 그대로 이어 둔다: 씬마다 볼륨 값이 달라도 소리가 튀지 않는다

        runner.Wanted = clip;
        runner.WantedVolume = volume;
        runner.FadeSeconds = Mathf.Max(fadeSeconds, 0.01f);
    }

    public static void Stop(float fadeSeconds = 1f) => Play(null, 0f, fadeSeconds);

    private class Runner : MonoBehaviour
    {
        public AudioClip Wanted;
        public float WantedVolume;
        public float FadeSeconds = 1f;

        private AudioSource source;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
        }

        private void Update()
        {
            // 나오는 음악이 원하는 음악과 다르면 먼저 0까지 줄이고, 바꾼 뒤 커진다
            bool same = source.clip == Wanted;
            float target = same ? WantedVolume : 0f;
            source.volume = Mathf.MoveTowards(source.volume, target, Time.unscaledDeltaTime / FadeSeconds);

            if (!same && source.volume <= 0f)
            {
                source.clip = Wanted;
                if (Wanted != null) source.Play();
                else source.Stop();
            }
        }
    }
}
