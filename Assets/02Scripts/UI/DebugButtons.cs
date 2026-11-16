using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 설정 창의 디버그 버튼: 낮 시간 -1분, 하루 스킵, 낮 퍼즐 스킵, 광고 제거 켜기/끄기. 빌드(APK)에서도 보인다(테스트용이라 출시 전에는 이 오브젝트를 지운다).
/// 퍼즐 스킵과 하루 스킵은 누르면 설정 창을 닫고(시간 멈춤도 풀고) 바로 실행한다. 시간 줄이기는 창을 연 채로 여러 번 누를 수 있다.
/// 광고 제거는 누를 때마다 켜고 끄며(AdManager.AdsRemoved, 기기에 저장됨) 버튼 글자가 지금 상태를 보여 준다.
/// 버튼 UI는 글로벌 UI 프리팹의 설정 창에 미리 배치되어 있고, 이 컴포넌트는 누름만 이어 준다.
/// </summary>
public class DebugButtons : MonoBehaviour
{
    private const float SkipSeconds = 60f;

    [SerializeField] private PreferencesPanel preferences;
    [SerializeField] private Button dayMinusButton;
    [FormerlySerializedAs("nightMinusButton")] [SerializeField] private Button daySkipButton;
    [SerializeField] private Button dayPuzzleSkipButton;
    [SerializeField] private Button adsToggleButton;

    private TMP_Text adsToggleLabel;

    private void Awake()
    {
        dayMinusButton.onClick.AddListener(() => GameClock.Instance?.SkipDayTime(SkipSeconds));
        daySkipButton.onClick.AddListener(() => CloseThen(() => GameManager.Instance?.DebugAdvanceDay()));
        dayPuzzleSkipButton.onClick.AddListener(() => CloseThen(() => GameManager.Instance?.DebugSkipDayPuzzle()));

        if (adsToggleButton != null)
        {
            adsToggleLabel = adsToggleButton.GetComponentInChildren<TMP_Text>(true);
            adsToggleButton.onClick.AddListener(() =>
            {
                AdManager.AdsRemoved = !AdManager.AdsRemoved;
                RefreshAdsLabel();
            });
        }
    }

    private void OnEnable() => RefreshAdsLabel(); // 설정 창을 열 때마다 지금 상태를 보여 준다

    private void RefreshAdsLabel()
    {
        if (adsToggleLabel != null) adsToggleLabel.text = AdManager.AdsRemoved ? "광고 제거: 켜짐" : "광고 제거: 꺼짐";
    }

    private void CloseThen(System.Action action)
    {
        preferences.Close();
        action();
    }
}
