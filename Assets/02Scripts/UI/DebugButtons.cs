using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 창의 디버그 버튼: 낮 시간 -1분, 밤 시간 -1분, 낮 퍼즐 스킵, 밤 퍼즐 스킵, 배속. 빌드(APK)에서도 보인다(테스트용이라 출시 전에는 이 오브젝트를 지운다).
/// 퍼즐 스킵은 누르면 설정 창을 닫고(시간 멈춤도 풀고) 바로 실행한다. 시간 줄이기는 창을 연 채로 여러 번 누를 수 있다.
/// 버튼 UI는 글로벌 UI 프리팹의 설정 창에 미리 배치되어 있고, 이 컴포넌트는 누름만 이어 준다.
/// </summary>
public class DebugButtons : MonoBehaviour
{
    private const float SkipSeconds = 60f;
    private static readonly float[] Speeds = { 1f, 2f, 4f, 8f };

    [SerializeField] private PreferencesPanel preferences;
    [SerializeField] private Button dayMinusButton;
    [SerializeField] private Button nightMinusButton;
    [SerializeField] private Button dayPuzzleSkipButton;
    [SerializeField] private Button nightPuzzleSkipButton;
    [SerializeField, Tooltip("누를 때마다 게임 배속이 x1 → x2 → x4 → x8 → x1로 바뀐다(Time.timeScale). 씬을 옮겨도 유지된다.")] private Button speedButton;

    private void Awake()
    {
        speedButton.onClick.AddListener(NextSpeed);
        ShowSpeed();
        dayMinusButton.onClick.AddListener(() => GameClock.Instance?.SkipDayTime(SkipSeconds));
        nightMinusButton.onClick.AddListener(() => GameClock.Instance?.SkipNightTime(SkipSeconds));
        dayPuzzleSkipButton.onClick.AddListener(() => CloseThen(() => FindFirstObjectByType<DayPuzzleUI>()?.DebugForceSubmit()));
        nightPuzzleSkipButton.onClick.AddListener(() => CloseThen(() => GameManager.Instance?.DebugSkipNightPuzzle()));
    }

    private void NextSpeed()
    {
        int index = System.Array.FindIndex(Speeds, s => Mathf.Approximately(s, Time.timeScale));
        Time.timeScale = Speeds[(index + 1) % Speeds.Length];
        ShowSpeed();
    }

    private void ShowSpeed()
    {
        speedButton.GetComponentInChildren<TMPro.TMP_Text>().text = $"배속 x{Time.timeScale:0.#}";
    }

    private void CloseThen(System.Action action)
    {
        preferences.Close();
        action();
    }
}
