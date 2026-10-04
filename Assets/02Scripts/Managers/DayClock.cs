using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 시간 시계. 하루 남은 시간, 현재 손님의 기분(인내심) 남은 시간, 밤 남은 시간을 실제 시간으로 깎는다.
/// 하루/기분 시간은 낮 씬(낮 메인/꽃 선택/낮 퍼즐)에서만 흐른다. 밤 시간은 밤이 시작된 뒤(BeginNight) 밤 메인/밤 퍼즐 씬에서만 흐른다.
/// 밤 시간이 0이 되면 EventBus.OnNightTimeUp을 한 번 발행한다.
/// 하루 시간은 날이 바뀔 때(EventBus.OnDayAdvanced) 가득 찬다.
/// 기분 시간은 새 주문이 시작될 때(StartMood)에만 가득 차서 흐르기 시작하고, 퍼즐이 끝나면(StopMood) 멈춘다.
/// 설정 창이 열려 있는 동안(SetPaused)은 하루/기분 시간이 모두 멈춘다.
/// 하루 시간이 0이 되면 EventBus.OnDayTimeUp을 한 번 발행한다(손님 기분 시간이 0일 때의 동작은 아직 없다).
/// </summary>
public class DayClock : Singleton<DayClock>
{
    [SerializeField] private float dayDurationSeconds = 300f;
    [SerializeField] private float moodDurationSeconds = 60f;
    [SerializeField] private float nightDurationSeconds = 180f; // 밤 길이(초)

    public float DayRemaining { get; private set; }
    public float MoodRemaining { get; private set; }
    public float NightRemaining { get; private set; }

    public float DayRatio => dayDurationSeconds > 0f ? Mathf.Clamp01(DayRemaining / dayDurationSeconds) : 0f;
    public float MoodRatio => moodDurationSeconds > 0f ? Mathf.Clamp01(MoodRemaining / moodDurationSeconds) : 0f;
    public float NightRatio => nightDurationSeconds > 0f ? Mathf.Clamp01(NightRemaining / nightDurationSeconds) : 0f;

    private bool running;
    private bool moodActive; // 현재 주문이 진행 중이라 기분 시간이 흐르는가
    private bool paused;     // 설정 창이 열려 있는가
    private bool nightScene;  // 지금 밤 메인/밤 퍼즐 씬인가
    private bool nightActive; // 밤이 시작됐고 아직 끝나지 않았는가

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
        running = scene.name == GameFlowController.SceneDayMain
               || scene.name == GameFlowController.SceneFlowerSelect
               || scene.name == GameFlowController.SceneDayPuzzle;
        nightScene = scene.name == GameFlowController.SceneNightMain
                  || scene.name == GameFlowController.SceneNightPuzzle;
    }

    private void OnDayAdvanced(int newDay)
    {
        ResetDay();
        nightActive = false; // 날이 바뀌면 지난 밤은 끝난 것
    }

    private void Update()
    {
        if (paused) return;
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
        running = false; // 한 번만 발행
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
    }

    public void ResetMood()
    {
        MoodRemaining = moodDurationSeconds;
    }

    /// <summary>새 주문이 시작됐을 때: 기분 시간을 가득 채우고 흐르게 한다.</summary>
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

    /// <summary>설정 창을 열고 닫을 때: 열려 있는 동안 하루/기분 시간이 모두 멈춘다.</summary>
    public void SetPaused(bool value)
    {
        paused = value;
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
