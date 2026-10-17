using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인 화면의 모습을 메인 화면의 상황(MainState)에 맞춰 바꾼다: 낮 손님 = 낮, 낮 종료 = 노을, 밤 손님과 밤 종료 = 밤.
/// 배경, 걸이, 책상, 전화기, 계산대, 말풍선처럼 모양이 같은 이미지는 오브젝트 하나의 스프라이트만 바꾼다(스프라이트는 인스펙터에 넣어 둔다).
/// 낮 손님 캐릭터와 밤 손님 캐릭터처럼 구성이 다른 오브젝트는 낮에만/밤에만 보이게 켜고 끈다. 오브젝트는 이름으로 찾는다(SceneFind).
/// PhaseBlend가 붙은 이미지(배경, 책상, 걸이)는 그림을 바로 바꾸지 않고 피벗에서 퍼지며 섞는다:
/// 낮 손님 동안 낮의 후반부터 시계 게이지(하루 진행도)에 맞춰 낮 → 노을로 서서히, 낮 종료에서 밤으로 갈 때는 몇 초 만에 노을 → 밤으로 확 섞인다(PlayNightBlend).
/// </summary>
public class MainLook : MonoBehaviour
{
    [Serializable]
    private struct LookSprite
    {
        public string imageName; // 씬에서 이 이름의 Image를 찾는다
        public Sprite day;
        public Sprite sunset;
        public Sprite night;
    }

    [SerializeField] private string[] dayOnlyNames = { "DayCharacterArea" };      // 낮 손님과 낮 종료(노을)에서만 보인다
    [SerializeField] private string[] nightOnlyNames = { "NightCharacterArea" };  // 밤 손님과 밤 종료에서만 보인다
    [SerializeField] private LookSprite[] images;

    [Header("섞기(PhaseBlend가 붙은 이미지)")]
    [SerializeField, Range(0f, 1f), Tooltip("하루가 이만큼 지나면(0~1) 낮 → 노을로 섞이기 시작해, 하루가 끝날 때 완전히 노을이 된다.")] private float sunsetBlendStart = 0.5f;
    [SerializeField, Min(0.1f), Tooltip("낮 종료에서 전등을 켜 밤으로 갈 때 노을 → 밤으로 섞이는 시간(초).")] private float nightBlendSeconds = 2.5f;

    private readonly List<(PhaseBlend phase, LookSprite look)> blends = new();
    private MainState state;
    private bool nightBlending;

    public void Apply(MainState newState)
    {
        state = newState;
        bool night = state == MainState.NightCustomer || state == MainState.NightEnding;
        SetActive(dayOnlyNames, !night);
        SetActive(nightOnlyNames, night);

        blends.Clear();
        foreach (LookSprite entry in images)
        {
            Sprite sprite = state == MainState.DayCustomer ? entry.day : state == MainState.DayEnding ? entry.sunset : entry.night;
            foreach (Image image in SceneFind.All<Image>(gameObject.scene, entry.imageName))
            {
                if (image.TryGetComponent(out PhaseBlend phase)) blends.Add((phase, entry)); // 섞는 이미지는 아래 UpdateBlends가 맡는다
                else if (sprite != null) image.sprite = sprite; // 비워 둔 모습은 지금 그림을 그대로 둔다
            }
        }
        UpdateBlends();
    }

    private void Update()
    {
        if (state == MainState.DayCustomer && !nightBlending) UpdateBlends(); // 시계 게이지에 맞춰 낮 → 노을로 서서히
    }

    /// <summary>섞는 이미지의 모습을 지금 상황에 맞춘다(낮 손님 = 하루 진행도에 따라 낮 → 노을, 낮 종료 = 노을, 밤 = 밤).</summary>
    private void UpdateBlends()
    {
        foreach ((PhaseBlend phase, LookSprite look) in blends)
        {
            switch (state)
            {
                case MainState.DayCustomer: phase.Show(look.day, look.sunset, SunsetProgress()); break;
                case MainState.DayEnding: phase.Show(look.sunset != null ? look.sunset : look.day, null, 0f); break;
                default: phase.Show(look.night != null ? look.night : look.sunset, null, 0f); break;
            }
        }
    }

    /// <summary>하루가 지난 정도(시계 게이지의 반대)로 낮 → 노을 진행도를 정한다. 시계가 없으면(씬 단독 실행) 낮 그대로다.</summary>
    private float SunsetProgress()
    {
        GameClock clock = GameClock.Instance;
        if (clock == null || clock.IsNightMode) return 0f;

        float elapsed = 1f - clock.DayRatio;
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(sunsetBlendStart, 1f, elapsed));
    }

    /// <summary>낮 종료에서 전등을 켰다: 노을 → 밤으로 몇 초 동안 섞는다. 섞는 시간(초)을 돌려준다(섞는 이미지가 없으면 0).</summary>
    public float PlayNightBlend()
    {
        if (blends.Count == 0) return 0f;

        nightBlending = true;
        StartCoroutine(NightBlend());
        return nightBlendSeconds;
    }

    private IEnumerator NightBlend()
    {
        for (float t = 0f; t < nightBlendSeconds; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / nightBlendSeconds);
            foreach ((PhaseBlend phase, LookSprite look) in blends) phase.Show(look.sunset, look.night, p);
            yield return null;
        }
        foreach ((PhaseBlend phase, LookSprite look) in blends) phase.Show(look.sunset, look.night, 1f);

        // 섞지 않는 이미지(전화기, 계산대, 말풍선 …)는 섞기가 끝날 때 밤 모습으로 바뀐다
        foreach (LookSprite entry in images)
        {
            if (entry.night == null) continue;
            foreach (Image image in SceneFind.All<Image>(gameObject.scene, entry.imageName))
            {
                if (!image.TryGetComponent(out PhaseBlend _)) image.sprite = entry.night;
            }
        }
    }

    private void SetActive(string[] names, bool active)
    {
        foreach (string objectName in names)
        {
            foreach (Transform t in SceneFind.All<Transform>(gameObject.scene, objectName)) t.gameObject.SetActive(active);
        }
    }
}
