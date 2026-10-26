using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 예약 주문 전화. 전화기(CounterTelephone)에 붙인다.
/// Begin()을 부르면 전화기 그림이 흔들리고(진동. 자식 오브젝트는 흔들리지 않는다), 아래의 Ringing 오브젝트 둘이 차례로 페이드로 점멸하며, 벨소리가 난다. ringSeconds 안에 전화기를 터치하지 않으면 전화가 끊긴다(벨소리도 같이 끊긴다).
/// 받으면 벨소리와 진동이 멈추고 하루 시간과 손님 기분 시간이 멈춘다. 전화기 아래 말풍선에 예약자의 말이 페이드로 뜨고, popupDelaySeconds 뒤에 예약 쇼카드 팝업이 뜨며 말풍선은 사라진다.
/// 수락하면 예약이 접수되고(예약금을 받는다: CurrencyManager.AcceptReservation) 거절하면 아무 일도 없다. 어느 쪽이든 팝업이 닫히고 시간이 다시 흐른다.
/// 전화가 오는 시점(예: 6일차에 손님 사이)을 정하는 일은 이 컴포넌트 밖에서 Begin()을 부르는 쪽이 맡는다(MainUI). Busy가 true인 동안은 다른 손님이 나타나면 안 된다.
/// </summary>
public class ReservationCall : MonoBehaviour
{
    private enum State { Idle, Ringing, Talking }

    public static ReservationCall Instance { get; private set; }

    /// <summary>전화가 울리는 중이거나 통화(팝업) 중인가. 전화기의 평소 동작(메뉴 바 열기)과 손님 등장은 이 동안 막는다.</summary>
    public static bool Busy => Instance != null && Instance.state != State.Idle;

    [SerializeField] private ReservationData reservation;
    [SerializeField] private Button phoneButton;
    [SerializeField, Tooltip("흔들릴 전화기 그림(전화기 오브젝트의 ImageTilt). 그림만 흔들리고 자식(말풍선, Ringing)은 그대로다.")] private ImageTilt phoneTilt;
    [SerializeField, Tooltip("차례로 점멸하는 Ringing 오브젝트 둘. 각각의 UIFader에서 페이드인·머묾·페이드아웃 시간을 정한다.")] private UIFader ringing1;
    [SerializeField] private UIFader ringing2;
    [SerializeField, Tooltip("받으면 예약자의 말이 뜨는 전화기 아래 말풍선(TelephoneBubble의 UIFader)과 그 글씨. 팝업이 뜰 때까지 보였다가 사라진다.")] private UIFader bubble;
    [SerializeField] private TMP_Text bubbleText;
    [SerializeField] private ReservationShowCard showCard;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip ringClip;
    [SerializeField, Range(0f, 1f)] private float ringVolume = 0.8f;
    [SerializeField, Tooltip("전화를 받는 순간 한 번 나는 소리.")] private AudioClip answerClip;
    [SerializeField, Range(0f, 1f)] private float answerVolume = 0.8f;

    [Header("시간(초)")]
    [SerializeField, Min(1f), Tooltip("전화가 울리는 시간. 이 안에 받지 않으면 끊긴다(소리도 같이 끊긴다).")] private float ringSeconds = 10f;
    [SerializeField, Min(0f), Tooltip("받은 뒤 말풍선이 뜨고 나서 팝업이 뜨기까지.")] private float popupDelaySeconds = 0.8f;

    [Header("진동")]
    [SerializeField, Tooltip("전화기가 흔들리는 각도(±도).")] private float shakeDegrees = 8f;
    [SerializeField, Min(0.02f), Tooltip("한 번 왕복하는 데 걸리는 시간(초).")] private float shakePeriodSeconds = 0.12f;

    private State state;
    private ReservationData current; // 지금 걸려 온 예약
    private ReservationCalendar calendar; // 수락한 뒤 예약일을 보여 주는 달력(꺼져 있다가 연출 때 켜진다)
    private Coroutine ringRoutine;
    private Coroutine blinkRoutine;

    private void Awake()
    {
        Instance = this;
        calendar = FindFirstObjectByType<ReservationCalendar>(FindObjectsInactive.Include); // 꺼져 있는 달력도 찾는다(수락 연출에 쓴다)
        bubble.gameObject.SetActive(false);
        ringing1.gameObject.SetActive(false);
        ringing2.gameObject.SetActive(false);
        phoneButton.onClick.AddListener(Answer);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>전화를 건다(이미 통화 중이면 무시). data가 없으면 인스펙터의 예약을 쓴다.</summary>
    public void Begin(ReservationData data = null)
    {
        if (state != State.Idle) return;

        current = data != null ? data : reservation;
        state = State.Ringing;
        audioSource.clip = ringClip;
        audioSource.loop = true;
        audioSource.volume = ringVolume;
        audioSource.Play();
        ringRoutine = StartCoroutine(Ring());
        blinkRoutine = StartCoroutine(Blink());
    }

    private IEnumerator Ring()
    {
        float elapsed = 0f; // 튜토리얼이 떠 있는 동안은 세지 않는다(제한 시간, 진동, 벨소리가 모두 멈춘다)
        bool paused = false;
        while (elapsed < ringSeconds)
        {
            bool held = GameTime.Held;
            if (held != paused)
            {
                paused = held;
                if (paused) audioSource.Pause(); else audioSource.UnPause();
            }
            elapsed += GameTime.Delta;
            phoneTilt.Angle = Mathf.Sin(elapsed * Mathf.PI * 2f / shakePeriodSeconds) * shakeDegrees;
            yield return null;
        }

        StopRinging(); // 받지 않았다: 전화가 끊긴다
        state = State.Idle;
    }

    /// <summary>Ringing 둘이 차례로(1 → 2 → 1 …) 서서히 나타났다 사라진다.</summary>
    private IEnumerator Blink()
    {
        foreach (UIFader fader in new[] { ringing1, ringing2 })
        {
            fader.Alpha = 0f;
            fader.gameObject.SetActive(true);
        }
        while (true)
        {
            foreach (UIFader fader in new[] { ringing1, ringing2 })
            {
                yield return fader.Pulse();
            }
        }
    }

    private void StopRinging()
    {
        if (ringRoutine != null) StopCoroutine(ringRoutine);
        if (blinkRoutine != null) StopCoroutine(blinkRoutine);
        ringRoutine = blinkRoutine = null;
        audioSource.Stop();
        phoneTilt.Angle = 0f;
        ringing1.gameObject.SetActive(false);
        ringing2.gameObject.SetActive(false);
    }

    /// <summary>울리는 동안 전화기를 터치했다.</summary>
    private void Answer()
    {
        if (state != State.Ringing) return;

        StopRinging();
        if (answerClip != null) audioSource.PlayOneShot(answerClip, answerVolume);
        state = State.Talking;
        GameClock.Instance?.SetPaused(true); // 받는 순간부터 수락·거절까지 하루 시간과 손님 기분 시간이 멈춘다
        StartCoroutine(Talk());
    }

    private IEnumerator Talk()
    {
        bubbleText.text = current.callLine;
        bubble.Alpha = 0f;
        bubble.gameObject.SetActive(true);
        yield return bubble.FadeIn();
        yield return GameTime.Wait(popupDelaySeconds);
        showCard.Show(current, accepted => Hangup(accepted));
        yield return bubble.FadeOut(); // 팝업이 뜨면 말풍선은 사라진다
        bubble.gameObject.SetActive(false);
    }

    private void Hangup(bool accepted)
    {
        ReservationRecord record = accepted && CurrencyManager.Instance != null ? CurrencyManager.Instance.AcceptReservation(current) : null; // 예약금을 받고 예약이 접수된다
        StartCoroutine(Finish(record));
    }

    /// <summary>수락했으면 달력이 잠깐 열려 예약일을 보여 준 뒤(ReservationCalendar.PlayAcceptedPreview) 통화가 끝난다: 그때까지 시간이 멈춘 채 손님이 오지 않는다.</summary>
    private IEnumerator Finish(ReservationRecord record)
    {
        if (record != null && calendar != null) yield return calendar.PlayAcceptedPreview(record);

        GameClock.Instance?.SetPaused(false);
        state = State.Idle;
    }
}
