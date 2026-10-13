using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 시간 시계. 하루 남은 시간, 현재 손님의 기분(인내심) 남은 시간, 밤 남은 시간을 실제 시간으로 깎는다.
/// 하루/기분 시간은 낮 씬(메인 화면의 낮 손님 상황/꽃 선택/낮 퍼즐)에서만 흐른다. 밤 시간은 밤이 시작된 뒤(BeginNight) 메인 화면의 밤 상황/밤 퍼즐 씬에서만 흐른다.
/// 밤 시간이 0이 되면 EventBus.OnNightTimeUp을 한 번 발행한다.
/// 하루 시간은 날이 바뀔 때(EventBus.OnDayAdvanced) 가득 찬다.
/// 기분 시간은 새 손님이 오면 가득 찬 채 멈춰 있다가(ResetMood), 플레이어가 주문을 수락할 때(StartMood) 가득 차서 흐르기 시작하고,
/// 퍼즐이 끝나면(StopMood) 멈춘다.
/// 설정 창이 열려 있거나 정산 결과 화면이 떠 있는 동안(SetPaused)은 하루/기분 시간이 모두 멈춘다.
/// 하루 시간이 0이 되면 EventBus.OnDayTimeUp을 한 번 발행한다(손님 기분 시간이 0일 때의 동작은 아직 없다).
/// </summary>
public class GameClock : Singleton<GameClock>
{
    [SerializeField] private float dayDurationSeconds = 300f;
    [SerializeField] private float moodDurationSeconds = 60f;
    [SerializeField] private float nightDurationSeconds = 180f; // 밤 길이(초)

    public float DayRemaining { get; private set; }
    public float MoodRemaining { get; private set; }
    public float NightRemaining { get; private set; }

    /// <summary>시계 UI가 밤 모습으로 보여야 하는 화면인가: 밤 시간이 흐르는 화면(메인 화면의 밤 손님/밤 종료, 밤 퍼즐)과, 밤이 시작되기 직전의 낮 종료 화면(카운터와 부적 선반. 밤 시간은 아직 흐르지 않고 가득 차 있다).</summary>
    public bool IsNightMode => nightLook;

    public float DayRatio => dayDurationSeconds > 0f ? Mathf.Clamp01(DayRemaining / dayDurationSeconds) : 0f;
    public float MoodRatio => moodDurationSeconds > 0f ? Mathf.Clamp01(MoodRemaining / moodDurationSeconds) : 0f;
    public float NightRatio => nightDurationSeconds > 0f ? Mathf.Clamp01(NightRemaining / nightDurationSeconds) : 0f;

    // 기분 단계의 경계(남은 기분 시간의 비율)
    private const float HappyAbove = 0.65f;
    private const float NormalAbove = 0.32f;

    /// <summary>지금 손님 기분 단계: 남은 비율이 65%를 넘으면 좋음, 32%를 넘으면 보통, 그 아래는 나쁨.</summary>
    public MoodLevel Mood => MoodRatio > HappyAbove ? MoodLevel.Happy : MoodRatio > NormalAbove ? MoodLevel.Normal : MoodLevel.Bad;

    /// <summary>기분 시간이 지금 실제로 줄고 있는가(주문을 수락했고, 낮 화면이고, 설정/결과로 멈춰 있지 않다).</summary>
    public bool MoodFlowing => moodActive && running && !paused && !tutorialHold;

    private bool running;
    private bool tutorialHold; // 튜토리얼 안내가 떠 있는 동안 모든 시간이 멈춘다(설정 창의 멈춤과 따로 센다)
    private bool moodActive; // 현재 주문이 진행 중이라 기분 시간이 흐르는가
    private bool paused;     // 설정 창이 열려 있는가
    private bool nightScene;  // 지금 메인 화면의 밤 상황(밤 손님/밤 종료)이거나 밤 퍼즐 씬인가
    private bool nightLook;   // 시계를 밤 모습으로 보여 주는 화면인가(nightScene이거나 부적 화면)
    private bool nightActive; // 밤이 시작됐고 아직 끝나지 않았는가
    private bool dayTimeUpRaised; // 오늘 하루 시간이 끝났다는 이벤트를 이미 발행했는가

    protected override void OnAwake()
    {
        ResetDay();
        ResetMood();
        NightRemaining = nightDurationSeconds;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EventBus.OnDayAdvanced += OnDayAdvanced;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        EventBus.OnDayAdvanced -= OnDayAdvanced;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        paused = false; // 씬이 바뀌면 설정 창은 닫힌 상태로 시작한다
        // 메인 화면은 하나(낮 메인)라서, 낮 시간이 흐르는지 밤 시간이 흐르는지는 메인 화면의 상황(MainState)으로 정한다.
        MainState main = GameManager.Instance != null ? GameManager.Instance.MainState : MainState.DayCustomer;
        bool mainScene = scene.name == GameManager.SceneDayMain;
        running = (mainScene && main == MainState.DayCustomer) // 낮 종료 상황에서는 하루 시간이 이미 끝났으므로 흐르지 않는다(꽃 선택은 이 메인 씬의 옆 페이지다)
               || scene.name == GameManager.SceneDayPuzzle;
        nightScene = (mainScene && (main == MainState.NightCustomer || main == MainState.NightEnding))
                  || scene.name == GameManager.SceneNightPuzzle;
        nightLook = nightScene || (mainScene && main == MainState.DayEnding);
    }

    private void OnDayAdvanced(int newDay)
    {
        ResetDay();
        nightActive = false; // 날이 바뀌면 지난 밤은 끝난 것
    }

    private void Update()
    {
        if (paused || tutorialHold) return;
        if (running) Tick(Time.deltaTime);
        if (nightScene && nightActive) TickNight(Time.deltaTime);
    }

    private void Tick(float seconds)
    {
        DayRemaining = Mathf.Max(0f, DayRemaining - seconds);
        if (moodActive) MoodRemaining = Mathf.Max(0f, MoodRemaining - seconds);
        if (DayRemaining <= 0f) TimeUp();
    }

    private void TickNight(float seconds)
    {
        NightRemaining = Mathf.Max(0f, NightRemaining - seconds);
        if (NightRemaining > 0f) return;

        nightActive = false; // 한 번만 발행
        EventBus.RaiseNightTimeUp();
    }

    private void TimeUp()
    {
        if (dayTimeUpRaised) return; // 한 번만 발행(주문을 마무리하는 동안에는 시간이 0인 채로 계속 흐른다)
        dayTimeUpRaised = true;
        EventBus.RaiseDayTimeUp();
    }

    /// <summary>밤이 시작될 때(낮을 마치고 밤으로 넘어갈 때): 밤 시간을 가득 채우고 흐르게 한다.</summary>
    public void BeginNight()
    {
        NightRemaining = nightDurationSeconds;
        nightActive = true;
    }

    public void ResetDay()
    {
        DayRemaining = dayDurationSeconds;
        dayTimeUpRaised = false;
    }

    public void ResetMood()
    {
        MoodRemaining = moodDurationSeconds;
    }

    /// <summary>플레이어가 주문을 수락했을 때: 기분 시간을 가득 채우고 흐르게 한다.</summary>
    public void StartMood()
    {
        ResetMood();
        moodActive = true;
    }

    /// <summary>주문이 끝났을 때(퍼즐 종료): 기분 시간을 지금 값에서 멈춘다. 다음 StartMood까지 채우지 않는다.</summary>
    public void StopMood()
    {
        moodActive = false;
    }

    /// <summary>설정 창을 열고 닫을 때, 정산 결과 화면이 뜰 때: 멈춰 있는 동안 하루/기분 시간이 모두 멈춘다.</summary>
    public void SetPaused(bool value)
    {
        paused = value;
    }

    /// <summary>튜토리얼 안내가 뜨고 사라질 때: 떠 있는 동안 하루/기분/밤 시간이 모두 멈춘다.</summary>
    public void SetTutorialHold(bool value)
    {
        tutorialHold = value;
    }

    /// <summary>디버그용: 하루 남은 시간만 앞당긴다(손님 기분은 그대로).</summary>
    public void SkipDayTime(float seconds)
    {
        DayRemaining = Mathf.Max(0f, DayRemaining - seconds);
    }

    /// <summary>디버그용: 밤 남은 시간을 앞당긴다.</summary>
    public void SkipNightTime(float seconds)
    {
        NightRemaining = Mathf.Max(0f, NightRemaining - seconds);
    }

    /// <summary>디버그용: 손님 기분을 최대치의 ratio만큼 깎는다(하루 시간은 그대로).</summary>
    public void DrainMood(float ratio)
    {
        MoodRemaining = Mathf.Max(0f, MoodRemaining - moodDurationSeconds * ratio);
    }
}
