using System;
using GoogleMobileAds.Api;
using UnityEngine;

/// <summary>
/// 전면 광고(Google AdMob). 하루 결산 화면에서 "다음"을 누를 때 한 번 띄운다.
/// 에디터와 안드로이드가 아닌 곳에서는 광고 없이 바로 통과한다. 광고가 아직 안 불러와졌거나 실패해도 게임을 막지 않는다.
/// 광고 제거 결제를 넣을 때는 AdsRemoved = true 한 줄이면 광고가 꺼진다(PlayerPrefs에 저장).
/// </summary>
public class AdManager : Singleton<AdManager>
{
    /// <summary>이 날(일차) 결산부터 광고를 띄운다.</summary>
    public const int FirstAdDay = 2;

    // 베타 동안은 테스트 광고. 정식 출시 때 false로 바꾸면 실제 광고 단위(아래 InterstitialId)를 쓴다.
    private static readonly bool UseTestAds = true;
    private const string TestInterstitialId = "ca-app-pub-3940256099942544/1033173712"; // 구글 공식 테스트 전면 광고
    private const string InterstitialId = "ca-app-pub-4313741128186946/5567184618";    // 실제 전면 광고 단위(AdMob)
    private const string RemovedKey = "ads_removed";

    /// <summary>광고 끄기 스위치. 켜 두면 광고를 아예 불러오지도 띄우지도 않는다.</summary>
    public static bool AdsRemoved
    {
        get => PlayerPrefs.GetInt(RemovedKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(RemovedKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (Instance == null) new GameObject("AdManager").AddComponent<AdManager>();
    }

    /// <summary>광고를 띄우고(조건이 맞고 준비돼 있으면) 닫힌 뒤에, 아니면 바로 then을 부른다.</summary>
    public static void ShowInterstitialThen(int day, Action then)
    {
        if (Instance != null) Instance.ShowThen(day, then);
        else then();
    }

#if UNITY_ANDROID // 에디터(타깃이 안드로이드일 때)에서도 컴파일은 되게 두고, 실행만 아래에서 막는다
    private InterstitialAd interstitial;

    protected override void OnAwake()
    {
        if (AdsRemoved) return;
        MobileAds.RaiseAdEventsOnUnityMainThread = true; // 광고 이벤트를 유니티 메인 스레드에서 받는다
        MobileAds.Initialize(_ => Load());
    }

    private void Load()
    {
        if (AdsRemoved) return;
        if (interstitial != null)
        {
            interstitial.Destroy();
            interstitial = null;
        }

        InterstitialAd.Load(UseTestAds ? TestInterstitialId : InterstitialId, new AdRequest(), (ad, error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogWarning($"[Ad] 전면 광고 불러오기 실패: {error}");
                return;
            }
            interstitial = ad;
        });
    }

    private void ShowThen(int day, Action then)
    {
        if (AdsRemoved || day < FirstAdDay) { then(); return; }

        if (interstitial == null || !interstitial.CanShowAd())
        {
            Load(); // 다음을 위해 다시 불러 두고, 이번에는 그냥 넘어간다
            then();
            return;
        }

        InterstitialAd ad = interstitial;
        interstitial = null;
        bool finished = false;
        Action finish = () =>
        {
            if (finished) return;
            finished = true;
            ad.Destroy();
            Load();
            then();
        };
        ad.OnAdFullScreenContentClosed += finish;
        ad.OnAdFullScreenContentFailed += _ => finish();
        ad.Show();
    }
#else
    private void ShowThen(int day, Action then) => then();
#endif
}
