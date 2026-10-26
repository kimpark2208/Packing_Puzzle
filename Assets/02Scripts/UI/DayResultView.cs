using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 낮 퍼즐이 끝나면 반투명 오버레이 위에 뜨는 정산 결과 화면.
/// 점수 사유를 감점 → 가점 순으로 하나씩 보여 주며 게이지 막대가 누적 점수로 움직이고, 마지막에 획득 재화를 보여 준다.
/// 진행 중에 한 번 누르면 빨리감기, 마지막 결과에서 한 번 더 누르면 닫히고 다음으로 넘어간다.
/// UI는 씬에 미리 배치되어 있고 내용만 채운다.
/// </summary>
public class DayResultView : MonoBehaviour
{
    [Header("게이지와 사유 알약")]
    [SerializeField] private ResultGauge gauge;
    [SerializeField] private Image mainPillBackground;
    [SerializeField] private TMP_Text mainPillText;
    [SerializeField] private Image prevPillBackground; // 직전 사유(흐리게)
    [SerializeField] private TMP_Text prevPillText;

    [Header("입력과 안내")]
    [SerializeField] private Button tapArea; // 화면 전체(오버레이)
    [SerializeField] private TMP_Text hintText; // 오른쪽 아래

    [Header("속도(초)")]
    [SerializeField] private float stepSeconds = 0.9f; // 사유 하나가 머무는 시간
    [SerializeField] private float moveSeconds = 0.6f; // 막대가 움직이고 사유 알약이 올라오는 시간(처음엔 빠르다가 천천히 도착한다)
    [SerializeField] private float riseDistance = 50f; // 새 사유 알약이 아래에서 올라오는 거리
    [SerializeField] private float fastScale = 0.2f; // 빨리감기 때 위 시간들에 곱한다

    [Header("색")]
    [SerializeField] private Color negativeColor = new(0.80f, 0.38f, 0.30f);
    [SerializeField] private Color positiveColor = new(0.45f, 0.66f, 0.38f);
    [SerializeField] private Color payoutColor = new(0.38f, 0.34f, 0.30f);
    [SerializeField] private float prevPillAlpha = 0.35f;
    [SerializeField] private float prevTextAlpha = 0.55f;

    private const string PlayingHint = "한 번 누르면 빨리감기";
    private const string FinishedHint = "한 번 누르면 계속";

    private Action onClosed;
    private bool showPayout = true;
    private bool fast;
    private bool finished;
    private Vector2 mainBase, prevBase; // 에디터에 놓아 둔 알약 자리
    private Color mainTextColor;        // 가운데 알약 글자의 원래 색(애니메이션 도중에 멈춰도 이 색으로 돌아온다)
    private Coroutine pillRoutine;

    private void Awake()
    {
        tapArea.onClick.AddListener(OnTap);
        mainBase = mainPillBackground.rectTransform.anchoredPosition;
        prevBase = prevPillBackground.rectTransform.anchoredPosition;
        mainTextColor = mainPillText.color;
    }

    /// <param name="showPayout">false면 마지막의 "획득 재화" 알약을 보여 주지 않는다(예약 꽃다발은 점수만 판정한다).</param>
    public void Show(SettlementResult result, Action closed, bool showPayout = true)
    {
        onClosed = closed;
        this.showPayout = showPayout;
        GetComponentInParent<Canvas>(true).gameObject.SetActive(true); // 에디터에서 작업하느라 캔버스를 꺼 두어도 결과 화면은 뜨게 한다
        gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(Play(result));
    }

    private void OnTap()
    {
        if (!finished) fast = true;
        else
        {
            gameObject.SetActive(false);
            onClosed?.Invoke();
        }
    }

    private IEnumerator Play(SettlementResult result)
    {
        fast = false;
        finished = false;
        gauge.Value = 0f;
        gauge.SetBandsVisible(showPayout);
        mainPillBackground.gameObject.SetActive(false);
        prevPillBackground.gameObject.SetActive(false);
        hintText.text = PlayingHint;

        // 감점 사유 → 가점 사유 순서
        var entries = result.Ledger.Entries.Where(e => e.Value < 0).Concat(result.Ledger.Entries.Where(e => e.Value > 0));
        int running = 0;
        bool shownAny = false;

        foreach (var entry in entries)
        {
            bool hadPrev = shownAny;
            if (shownAny) SetPrevPill(mainPillText.text, mainPillBackground.color);
            shownAny = true;

            running += entry.Value;
            SetMainPill($"{Label(entry.Key)}  {Signed(entry.Value)}", entry.Value < 0 ? negativeColor : positiveColor);
            RisePills(hadPrev);

            yield return MoveGauge(ScoreTier.Clamp(running));
            yield return Wait(stepSeconds);
        }

        if (showPayout)
        {
            if (shownAny) SetPrevPill(mainPillText.text, mainPillBackground.color);
            SetMainPill($"획득 재화  {result.Payout}", payoutColor);
            RisePills(shownAny);
        }
        yield return MoveGauge(result.Score);

        finished = true;
        hintText.text = FinishedHint;
    }

    private void SetMainPill(string text, Color color)
    {
        mainPillBackground.gameObject.SetActive(true);
        mainPillBackground.color = color;
        mainPillText.text = text;
    }

    private void SetPrevPill(string text, Color color)
    {
        prevPillBackground.gameObject.SetActive(true);
        prevPillBackground.color = new Color(color.r, color.g, color.b, prevPillAlpha);
        prevPillText.text = text;
        prevPillText.color = new Color(1f, 1f, 1f, prevTextAlpha);
    }

    private float MoveDuration => moveSeconds * (fast ? fastScale : 1f);

    private IEnumerator MoveGauge(float target)
    {
        float from = gauge.Value;
        float duration = MoveDuration;
        for (float t = 0f; t < duration; t += GameTime.Delta) // 튜토리얼 안내가 떠 있는 동안은 연출도 멈춘다
        {
            gauge.Value = Mathf.Lerp(from, target, EaseOut(t / duration));
            yield return null;
        }
        gauge.Value = target;
    }

    /// <summary>새 사유 알약은 아래에서 올라오며 나타나고, 이전 사유 알약은 그 자리에서 위로 밀려 올라가며 흐려진다. 게이지가 움직이는 시간 동안 같이 움직인다.</summary>
    private void RisePills(bool withPrev)
    {
        if (pillRoutine != null) StopCoroutine(pillRoutine);
        pillRoutine = StartCoroutine(RisePillsRoutine(withPrev));
    }

    private IEnumerator RisePillsRoutine(bool withPrev)
    {
        Color mainBg = mainPillBackground.color, mainText = mainTextColor; // SetMainPill/SetPrevPill이 정해 둔 최종 모습
        Color prevBg = prevPillBackground.color, prevText = prevPillText.color;
        RectTransform mainRect = mainPillBackground.rectTransform, prevRect = prevPillBackground.rectTransform;
        float duration = MoveDuration;

        for (float t = 0f; ; t += GameTime.Delta)
        {
            float u = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
            float e = EaseOut(u);

            mainRect.anchoredPosition = Vector2.Lerp(mainBase + Vector2.down * riseDistance, mainBase, e);
            mainPillBackground.color = WithAlpha(mainBg, mainBg.a * e);
            mainPillText.color = WithAlpha(mainText, mainText.a * e);

            if (withPrev)
            {
                prevRect.anchoredPosition = Vector2.Lerp(mainBase, prevBase, e); // 방금까지 가운데 있던 알약이 위로 올라간다
                prevPillBackground.color = WithAlpha(prevBg, Mathf.Lerp(1f, prevBg.a, e));
                prevPillText.color = WithAlpha(prevText, Mathf.Lerp(1f, prevText.a, e));
            }
            if (u >= 1f) break;
            yield return null;
        }
        pillRoutine = null;
    }

    /// <summary>점수 사유를 결과 화면에 보여 줄 문구로 바꾼다.</summary>
    private static string Label(ScoreReason reason)
    {
        switch (reason)
        {
            case ScoreReason.FlowerLight: return "꽃의 빛";
            case ScoreReason.RequirementMet: return "요구사항 달성";
            case ScoreReason.RequirementOver: return "요구사항 초과 달성";
            case ScoreReason.MoodGood: return "손님 기분 좋음";
            case ScoreReason.Collapse: return "꽃다발 붕괴";
            case ScoreReason.MoodBad: return "손님 기분 나쁨";
            case ScoreReason.RequirementMissed: return "요구사항 미달성";
            case ScoreReason.SupplyMissing: return "예약 용품 미포함";
            default: return reason.ToString();
        }
    }

    private static string Signed(int value) => value > 0 ? $"+{value}" : value.ToString();

    private static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

    /// <summary>빠르게 시작해서 천천히 도착한다.</summary>
    private static float EaseOut(float u) => 1f - Mathf.Pow(1f - u, 3f);

    /// <summary>빨리감기가 켜지면 남은 대기도 짧아진다.</summary>
    private IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds * (fast ? fastScale : 1f); t += GameTime.Delta) yield return null;
    }
}
