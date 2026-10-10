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
    [SerializeField] private float moveSeconds = 0.3f; // 막대가 움직이는 시간
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
    private bool fast;
    private bool finished;

    private void Awake()
    {
        tapArea.onClick.AddListener(OnTap);
    }

    public void Show(SettlementResult result, Action closed)
    {
        onClosed = closed;
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
        mainPillBackground.gameObject.SetActive(false);
        prevPillBackground.gameObject.SetActive(false);
        hintText.text = PlayingHint;

        // 감점 사유 → 가점 사유 순서
        var entries = result.Ledger.Entries.Where(e => e.Value < 0).Concat(result.Ledger.Entries.Where(e => e.Value > 0));
        int running = 0;
        bool shownAny = false;

        foreach (var entry in entries)
        {
            if (shownAny) SetPrevPill(mainPillText.text, mainPillBackground.color);
            shownAny = true;

            running += entry.Value;
            SetMainPill($"{SettlementFormatter.Label(entry.Key)}  {SettlementFormatter.Signed(entry.Value)}", entry.Value < 0 ? negativeColor : positiveColor);

            yield return MoveGauge(ScoreTier.Clamp(running));
            yield return Wait(stepSeconds);
        }

        if (shownAny) SetPrevPill(mainPillText.text, mainPillBackground.color);
        SetMainPill($"획득 재화  {result.Payout}", payoutColor);
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

    private IEnumerator MoveGauge(float target)
    {
        float from = gauge.Value;
        float duration = moveSeconds * (fast ? fastScale : 1f);
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            gauge.Value = Mathf.Lerp(from, target, t / duration);
            yield return null;
        }
        gauge.Value = target;
    }

    /// <summary>빨리감기가 켜지면 남은 대기도 짧아진다.</summary>
    private IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds * (fast ? fastScale : 1f); t += Time.unscaledDeltaTime) yield return null;
    }
}
