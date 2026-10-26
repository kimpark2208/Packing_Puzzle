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

    [SerializeField, Min(0.1f), Tooltip("하루 결산(또는 결산 없는 밤)이 끝나 다음 날 낮으로 갈 때 밤 → 낮으로 섞이는 시간(초).")] private float morningBlendSeconds = 1f;

    private LookSprite[] looks = System.Array.Empty<LookSprite>(); // images에 설치된 가구의 그림을 입힌 것(Apply가 정한다)

    /// <summary>
    /// 설치된 가구가 이 이미지 자리(sceneImageName)를 차지하면 그 가구의 낮·노을·밤 그림으로 바꾼다. 같은 자리에 여럿이면 나중에 설치된 것이 쓰인다.
    /// 주문한 가구는 다음 날 설치되므로(CurrencyManager.InstallPendingFurniture) 주문한 날에는 바뀌지 않는다.
    /// </summary>
    private LookSprite[] WithInstalledFurniture()
    {
        var result = (LookSprite[])images.Clone();
        CurrencyManager currency = CurrencyManager.Instance;
        DataRegistry registry = DataRegistry.Instance;
        if (currency == null || registry == null) return result;

        foreach (int id in currency.InstalledFurniture)
        {
            FurnitureData furniture = registry.GetFurniture(id);
            if (furniture == null || string.IsNullOrEmpty(furniture.sceneImageName)) continue;

            for (int i = 0; i < result.Length; i++)
            {
                if (result[i].imageName != furniture.sceneImageName) continue;

                if (furniture.DaySprite != null) result[i].day = furniture.DaySprite;
                if (furniture.SunsetSprite != null) result[i].sunset = furniture.SunsetSprite;
                if (furniture.NightSprite != null) result[i].night = furniture.NightSprite;
            }
        }
        return result;
    }

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
        looks = WithInstalledFurniture();
        foreach (LookSprite entry in looks)
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

        SetSprites(entry => entry.night); // 섞지 않는 이미지(전화기, 계산대, 말풍선 …)는 섞기가 끝날 때 밤 모습으로 바뀐다
    }

    /// <summary>
    /// 밤이 지나 새 날 낮이 열렸다(Apply(DayCustomer) 뒤에 부른다): 밤 모습으로 시작해, 씬 전환의 페이드가 걷히면 낮 → 노을 섞기와 같은 방식으로 밤 → 낮으로 섞는다.
    /// </summary>
    public void PlayMorningBlend()
    {
        if (blends.Count == 0) return;

        nightBlending = true; // 섞는 동안 Update가 낮 모습으로 덮어쓰지 않게
        IsMorningBlending = true;
        foreach ((PhaseBlend phase, LookSprite look) in blends) phase.Show(NightOf(look), look.day, 0f);
        SetSprites(entry => entry.night); // 섞지 않는 이미지는 밤 모습으로 시작해 섞기가 끝날 때 낮이 된다
        if (GameClock.Instance != null)
        {
            GameClock.Instance.MorningFill = 0f; // 시계는 비워진 밤 시계로 시작한다
            GameClock.Instance.SetPaused(true);  // 연출이 끝나야 하루 시간이 흐른다
        }
        StartCoroutine(MorningBlend());
    }

    /// <summary>밤 → 낮 연출 중인가(이 동안 낮 손님은 나타나지 않는다).</summary>
    public bool IsMorningBlending { get; private set; }

    private IEnumerator MorningBlend()
    {
        while (SceneTransition.Busy) yield return null; // 페이드가 걷힌 뒤에 섞기 시작한다

        GameClock clock = GameClock.Instance;
        for (float t = 0f; t < morningBlendSeconds; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / morningBlendSeconds); // 배경 섞기와 시계 채우기가 같이 간다
            foreach ((PhaseBlend phase, LookSprite look) in blends) phase.Show(NightOf(look), look.day, p);
            if (clock != null) clock.MorningFill = p;
            yield return null;
        }
        foreach ((PhaseBlend phase, LookSprite look) in blends) phase.Show(NightOf(look), look.day, 1f);
        SetSprites(entry => entry.day);
        if (clock != null)
        {
            clock.MorningFill = -1f;
            clock.SetPaused(false);
        }
        nightBlending = false;
        IsMorningBlending = false;
    }

    private static Sprite NightOf(LookSprite look) => look.night != null ? look.night : look.sunset;

    /// <summary>섞지 않는 이미지(전화기, 계산대, 말풍선 …)의 그림을 고른 모습으로 바꾼다(비워 둔 모습은 그대로 둔다).</summary>
    private void SetSprites(Func<LookSprite, Sprite> pick)
    {
        foreach (LookSprite entry in looks)
        {
            Sprite sprite = pick(entry);
            if (sprite == null) continue;
            foreach (Image image in SceneFind.All<Image>(gameObject.scene, entry.imageName))
            {
                if (!image.TryGetComponent(out PhaseBlend _)) image.sprite = sprite;
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
