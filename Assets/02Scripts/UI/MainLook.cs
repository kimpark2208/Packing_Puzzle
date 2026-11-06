using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인 화면의 모습을 하루 진행도(시계 게이지의 반대)에 맞춰 낮 → 노을 → 밤으로 바꾼다.
/// 배경, 걸이, 책상, 전화기, 계산대, 말풍선처럼 모양이 같은 이미지는 오브젝트 하나의 스프라이트만 바꾼다(스프라이트는 인스펙터에 넣어 둔다). 오브젝트는 이름으로 찾는다(SceneFind).
/// PhaseBlend가 붙은 이미지(배경, 책상, 걸이)는 그림을 바로 바꾸지 않고 피벗에서 퍼지며 섞는다:
/// 하루가 GameClock.SunsetStartElapsed(전체 시간의 2/4)만큼 지나면 낮 → 노을로 서서히 섞이기 시작하고, 밤이 시작되면(GameClock.NightStartElapsed, 남은 시간 1/4) 노을 → 밤으로 섞이기 시작한다.
/// 섞지 않는 이미지(전화기, 계산대, 말풍선 …)는 섞기가 반쯤 지났을 때 그림이 바뀐다.
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

    private enum Phase { None, Day, Sunset, Night }

    [SerializeField] private string[] dayOnlyNames = { "DayCharacterArea" };      // 손님 캐릭터(항상 보인다)
    [SerializeField] private string[] nightOnlyNames = { "NightCharacterArea" };  // 옛 밤 손님 캐릭터(밤 퍼즐이 없어져 항상 꺼 둔다)
    [SerializeField] private LookSprite[] images;

    [Header("섞기(PhaseBlend가 붙은 이미지)")]
    [SerializeField, Range(0.01f, 0.5f), Tooltip("밤이 시작되면(GameClock.NightStartElapsed) 노을 → 밤으로 섞이기 시작해, 하루 진행도가 이만큼 더 지나면 완전히 밤이 된다. 낮 → 노을은 밤이 시작될 때까지 섞인다.")] private float nightBlendWidth = 0.1f;

    [SerializeField, Min(0.1f), Tooltip("하루 결산이 끝나 다음 날로 갈 때 밤 → 낮으로 섞이는 시간(초).")] private float morningBlendSeconds = 1f;

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
    private Phase shownPhase;
    private bool morningBlending;

    public void Apply()
    {
        SetActive(dayOnlyNames, true);
        SetActive(nightOnlyNames, false);

        blends.Clear();
        looks = WithInstalledFurniture();
        foreach (LookSprite entry in looks)
        {
            foreach (Image image in SceneFind.All<Image>(gameObject.scene, entry.imageName))
            {
                if (image.TryGetComponent(out PhaseBlend phase)) blends.Add((phase, entry)); // 섞는 이미지는 Refresh가 맡는다
            }
        }
        shownPhase = Phase.None;
        Refresh();
    }

    private void Update()
    {
        if (!morningBlending) Refresh(); // 시계 게이지에 맞춰 낮 → 노을 → 밤으로 서서히
    }

    /// <summary>하루 진행도에 맞춰 섞는 이미지의 모습을 정하고, 섞지 않는 이미지는 단계가 바뀔 때 그림을 바꾼다.</summary>
    private void Refresh()
    {
        GameClock clock = GameClock.Instance;
        float elapsed = clock != null ? 1f - clock.DayRatio : 0f; // 시계가 없으면(씬 단독 실행) 낮 그대로다
        float nightStart = clock != null ? clock.NightStartElapsed : 1f;
        float sunsetStart = clock != null ? clock.SunsetStartElapsed : 1f;
        float sunsetEnd = Mathf.Max(nightStart, sunsetStart + 0.01f); // 낮 → 노을은 밤 시작 때 끝나고, 그때부터 노을 → 밤이 시작된다
        float nightEnd = sunsetEnd + nightBlendWidth;
        float sunset = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(sunsetStart, sunsetEnd, elapsed));
        float night = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(sunsetEnd, nightEnd, elapsed));

        foreach ((PhaseBlend phase, LookSprite look) in blends)
        {
            if (night > 0f) phase.Show(SunsetOf(look), NightOf(look), night);
            else phase.Show(look.day, SunsetOf(look), sunset);
        }

        Phase now = night >= 0.5f ? Phase.Night : sunset >= 0.5f ? Phase.Sunset : Phase.Day;
        if (now == shownPhase) return;

        shownPhase = now;
        SetSprites(entry => now == Phase.Night ? entry.night : now == Phase.Sunset ? entry.sunset : entry.day);
    }

    /// <summary>
    /// 하루가 끝나 새 날이 열렸다(Apply 뒤에 부른다): 밤 모습으로 시작해, 씬 전환의 페이드가 걷히면 밤 → 낮으로 섞는다.
    /// </summary>
    public void PlayMorningBlend()
    {
        if (blends.Count == 0) return;

        morningBlending = true; // 섞는 동안 Update가 낮 모습으로 덮어쓰지 않게
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

    /// <summary>밤 → 낮 연출 중인가(이 동안 손님은 나타나지 않는다).</summary>
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
        shownPhase = Phase.Day;
        if (clock != null)
        {
            clock.MorningFill = -1f;
            clock.SetPaused(false);
        }
        morningBlending = false;
        IsMorningBlending = false;
    }

    private static Sprite SunsetOf(LookSprite look) => look.sunset != null ? look.sunset : look.day;

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
