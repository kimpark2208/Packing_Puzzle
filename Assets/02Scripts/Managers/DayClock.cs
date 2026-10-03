using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 낮 시간 시계. 하루 남은 시간과 현재 손님의 기분(인내심) 남은 시간을 실제 시간으로 깎는다.
/// 낮 씬(낮 메인/꽃 선택/낮 퍼즐)에 있을 때만 흐르고, 밤 씬에서는 멈춘다.
/// 하루 시간은 날이 바뀔 때(EventBus.OnDayAdvanced), 기분 시간은 새 손님이 올 때(ResetMood) 가득 찬다.
/// 하루 시간이 0이 되면 EventBus.OnDayTimeUp을 한 번 발행한다(손님 기분 시간이 0일 때의 동작은 아직 없다).
/// </summary>
public class DayClock : Singleton<DayClock>
{
    [SerializeField] private float dayDurationSeconds = 300f;
    [SerializeField] private float moodDurationSeconds = 60f;

    public float DayRemaining { get; private set; }
    public float MoodRemaining { get; private set; }

    public float DayRatio => dayDurationSeconds > 0f ? Mathf.Clamp01(DayRemaining / dayDurationSeconds) : 0f;
    public float MoodRatio => moodDurationSeconds > 0f ? Mathf.Clamp01(MoodRemaining / moodDurationSeconds) : 0f;

    private bool running;

    protected override void OnAwake()
    {
        ResetDay();
        ResetMood();
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
        running = scene.name == GameFlowController.SceneDayMain
               || scene.name == GameFlowController.SceneFlowerSelect
               || scene.name == GameFlowController.SceneDayPuzzle;
    }

    private void OnDayAdvanced(int newDay)
    {
        ResetDay();
    }

    private void Update()
    {
        if (!running) return;
        Tick(Time.deltaTime);
    }

    private void Tick(float seconds)
    {
        DayRemaining = Mathf.Max(0f, DayRemaining - seconds);
        MoodRemaining = Mathf.Max(0f, MoodRemaining - seconds);
        if (DayRemaining <= 0f) TimeUp();
    }

    private void TimeUp()
    {
        running = false; // 한 번만 발행
        EventBus.RaiseDayTimeUp();
    }

    public void ResetDay()
    {
        DayRemaining = dayDurationSeconds;
    }

    public void ResetMood()
    {
        MoodRemaining = moodDurationSeconds;
    }

    /// <summary>디버그용: 하루 남은 시간만 앞당긴다(손님 기분은 그대로).</summary>
    public void SkipDayTime(float seconds)
    {
        DayRemaining = Mathf.Max(0f, DayRemaining - seconds);
    }

    /// <summary>디버그용: 손님 기분을 최대치의 ratio만큼 깎는다(하루 시간은 그대로).</summary>
    public void DrainMood(float ratio)
    {
        MoodRemaining = Mathf.Max(0f, MoodRemaining - moodDurationSeconds * ratio);
    }
}
