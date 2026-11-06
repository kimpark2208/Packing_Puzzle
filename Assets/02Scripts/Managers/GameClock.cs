using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 시간 시계. 영업 시간(하루 남은 시간)과 현재 손님의 기분(인내심) 남은 시간을 실제 시간으로 깎는다.
/// 하루는 낮 → 노을 → 밤으로 이어지는 하나의 영업 시간이다: 하루 시간이 지난 정도(0~1)가 nightStartElapsed를 넘으면 밤이고, 밤에는 파란 피부 손님이 온다.
/// 하루/기분 시간은 낮 씬(메인 화면의 손님 상황/꽃 선택/낮 퍼즐)에서만 흐른다. 하루 시간은 날이 바뀔 때(EventBus.OnDayAdvanced) 가득 찬다.
/// 기분 시간은 새 손님이 오면 가득 찬 채 멈춰 있다가(ResetMood), 플레이어가 주문을 수락할 때(StartMood) 가득 차서 흐르기 시작하고,
/// 퍼즐이 끝나면(StopMood) 멈춘다.
/// 설정 창이 열려 있거나 정산 결과 화면이 떠 있는 동안(SetPaused)은 하루/기분 시간이 모두 멈춘다.
/// 하루 시간이 0이 되면 EventBus.OnDayTimeUp을 한 번 발행한다(손님 기분 시간이 0일 때의 동작은 아직 없다).
/// </summary>
public class GameClock : Singleton<GameClock>
{
    [SerializeField] private float dayDurationSeconds = 300f;
    [SerializeField] private float moodDurationSeconds = 60f;
    [SerializeField, Range(0f, 1f), Tooltip("하루가 이만큼 지나면(0~1) 노을이 시작된다. 화면이 노을로 섞이기 시작하고, 이때부터 파란 피부 손님이 나올 수 있다(밤 시작까지 확률이 0에서 100%로 늘어난다).")] private float sunsetStartElapsed = 0.5f;
    [SerializeField, Range(0f, 1f), Tooltip("하루가 이만큼 지나면(0~1) 밤이다. 밤에는 파란 피부 손님만 오고 시계와 화면이 밤 모습이 된다.")] private float nightStartElapsed = 0.75f;

    public float DayRemaining { get; private set; }
    public float MoodRemaining { get; private set; }

    /// <summary>하루가 이만큼 지나면(0~1) 노을이 시작된다.</summary>
    public float SunsetStartElapsed => sunsetStartElapsed;

    /// <summary>하루가 이만큼 지나면(0~1) 밤이다.</summary>
    public float NightStartElapsed => nightStartElapsed;

    /// <summary>지금이 밤인가(하루 시간이 nightStartElapsed만큼 지났다). 시계 UI와 화면 모습, 파란 피부 손님이 이것을 따른다.</summary>
    public bool IsNightMode => 1f - DayRatio >= nightStartElapsed;

    /// <summary>다음 손님이 파란 피부(귀신)일 확률: 노을이 시작될 때 0, 밤이 시작될 때 100%로 늘어난다(밤에는 파란 피부만 온다).</summary>
    public float GhostCustomerChance => Mathf.Clamp01(Mathf.InverseLerp(sunsetStartElapsed, nightStartElapsed, 1f - DayRatio));

    public float DayRatio => dayDurationSeconds > 0f ? Mathf.Clamp01(DayRemaining / dayDurationSeconds) : 0f;
    public float MoodRatio => moodDurationSeconds > 0f ? Mathf.Clamp01(MoodRemaining / moodDurationSeconds) : 0f;

    // 기분 단계의 경계(남은 기분 시간의 비율)
    private const float HappyAbove = 0.65f;
    private const float NormalAbove = 0.32f;

    /// <summary>지금 손님 기분 단계: 남은 비율이 65%를 넘으면 좋음, 32%를 넘으면 보통, 그 아래는 나쁨.</summary>
    public MoodLevel Mood => MoodRatio > HappyAbove ? MoodLevel.Happy : MoodRatio > NormalAbove ? MoodLevel.Normal : MoodLevel.Bad;

    /// <summary>기분 시간이 지금 실제로 줄고 있는가(주문을 수락했고, 낮 화면이고, 설정/결과로 멈춰 있지 않다).</summary>
    public bool MoodFlowing => moodActive && running && !Paused && !TutorialHold;

    /// <summary>튜토리얼 안내가 떠 있는 동안 true: 하루/기분 시간이 모두 멈춘다(설정 창의 멈춤과 따로 센다).</summary>
    public bool TutorialHold { get; set; }

    private bool running;
    private bool moodActive; // 현재 주문이 진행 중이라 기분 시간이 흐르는가
    private int pauseCount; // 멈춤을 건 곳의 수(설정 창, NPC 대화, 정산 결과, 예약 전화 …): 모두 풀어야 시간이 흐른다
    private bool dayTimeUpRaised; // 오늘 하루 시간이 끝났다는 이벤트를 이미 발행했는가

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
        pauseCount = 0; // 씬이 바뀌면 멈춤은 모두 풀린 채로 시작한다
        MorningFill = -1f; // 밤 → 낮 연출은 메인 화면이 열릴 때 다시 시작한다
        running = scene.name == GameManager.SceneDayMain || scene.name == GameManager.SceneDayPuzzle; // 꽃 선택은 메인 씬의 옆 페이지다
    }

    private void OnDayAdvanced(int newDay)
    {
        ResetDay();
    }

    private void Update()
    {
        if (Paused || TutorialHold) return;
        if (running) Tick(Time.deltaTime);
    }

    private void Tick(float seconds)
    {
        DayRemaining = Mathf.Max(0f, DayRemaining - seconds);
        if (moodActive) MoodRemaining = Mathf.Max(0f, MoodRemaining - seconds);
        if (DayRemaining <= 0f) TimeUp();
    }

    /// <summary>새 날 밤 → 낮 연출 중이면 낮 시계가 채워진 정도(0~1), 아니면 -1. 시계 게이지는 이 동안 비워진 밤 시계에서 낮 시계를 채워 보인다.</summary>
    public float MorningFill { get; set; } = -1f;

    private void TimeUp()
    {
        if (dayTimeUpRaised) return; // 한 번만 발행(주문을 마무리하는 동안에는 시간이 0인 채로 계속 흐른다)
        dayTimeUpRaised = true;
        EventBus.RaiseDayTimeUp();
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

    /// <summary>시간을 멈추거나(true) 푼다(false). 설정 창, NPC 대화, 정산 결과, 예약 전화처럼 여럿이 겹쳐 멈출 수 있어서 횟수로 센다(true마다 false가 짝이어야 하고, 모두 풀려야 시간이 흐른다). 멈춰 있는 동안 하루/기분 시간이 모두 멈춘다.</summary>
    public void SetPaused(bool value)
    {
        pauseCount = Mathf.Max(0, pauseCount + (value ? 1 : -1));
    }

    private bool Paused => pauseCount > 0;

    /// <summary>예약 반응이 끝난 뒤: 하루 남은 시간을 seconds 동안 시계가 빠르게 감기듯이 0까지 줄인다(시계 게이지와 낮 → 노을 → 밤 모습이 따라 움직인다).
    /// 하루가 끝났다는 이벤트(OnDayTimeUp)는 내지 않는다. 이 뒤의 흐름은 부른 쪽이 직접 이어 간다.</summary>
    public System.Collections.IEnumerator FastForwardDay(float seconds)
    {
        dayTimeUpRaised = true;
        float start = DayRemaining;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            DayRemaining = Mathf.Lerp(start, 0f, Mathf.SmoothStep(0f, 1f, t / seconds));
            yield return null;
        }
        DayRemaining = 0f;
    }

    /// <summary>디버그용: 하루 남은 시간만 앞당긴다(손님 기분은 그대로).</summary>
    public void SkipDayTime(float seconds)
    {
        DayRemaining = Mathf.Max(0f, DayRemaining - seconds);
    }

    /// <summary>디버그용: 밤이 막 시작되는 때로 건너뛴다(이미 밤이면 그대로).</summary>
    public void SkipToNight()
    {
        float nightRemaining = dayDurationSeconds * (1f - nightStartElapsed);
        if (DayRemaining > nightRemaining) DayRemaining = nightRemaining;
    }

    /// <summary>디버그용: 손님 기분을 최대치의 ratio만큼 깎는다(하루 시간은 그대로).</summary>
    public void DrainMood(float ratio)
    {
        MoodRemaining = Mathf.Max(0f, MoodRemaining - moodDurationSeconds * ratio);
    }
}
