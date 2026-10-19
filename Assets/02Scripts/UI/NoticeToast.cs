using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 획득 알림 토스트(리본 패널 하나와 글씨). 글로벌 UI의 NoticeToast에 붙인다. 왼쪽 밖에서 미끄러져 들어와 잠시 머문 뒤 다시 왼쪽으로 들어간다.
/// 알림이 겹치면 차례로 보여 준다. 알림을 띄울 때 이 토스트가 있는 씬이 아직 안 열려 있으면 열릴 때까지 기다렸다가 보여 준다.
/// 놓일 자리는 에디터에서 잡아 둔 위치다(실행하면 그 왼쪽 밖에서 시작한다). 움직이는 시간과 머무는 시간은 인스펙터에서 정한다.
/// 패널은 화면 왼쪽 끝에 붙여 두는 것을 기준으로 한다(숨을 때 패널 너비만큼 왼쪽으로 나간다).
/// </summary>
public class NoticeToast : MonoBehaviour
{
    private static NoticeToast instance;
    private static readonly Queue<string> waiting = new();

    [SerializeField] private TMP_Text label;

    [Header("움직임(초)")]
    [SerializeField] private float slideInSeconds = 0.3f;  // 왼쪽에서 들어오는 시간
    [SerializeField] private float holdSeconds = 2.5f;     // 다 나온 채로 머무는 시간
    [SerializeField] private float slideOutSeconds = 0.3f; // 왼쪽으로 들어가는 시간

    private RectTransform rect;
    private Vector2 restPosition;
    private Coroutine routine;

    /// <summary>알림 글씨에서 눈에 띄게 할 부분(수량 등)을 주황색 굵은 글씨로 만든다.</summary>
    public static string Highlight(string text) => $"<color=#E58A2B><b>{text}</b></color>";

    /// <summary>알림을 띄운다. 글씨는 TMP 리치 텍스트다(예: "분홍색 포장지 &lt;b&gt;X30&lt;/b&gt;").</summary>
    public static void Show(string text)
    {
        waiting.Enqueue(text);
        if (instance != null) instance.Pump();
    }

    private void Awake()
    {
        instance = this;
        rect = (RectTransform)transform;
        restPosition = rect.anchoredPosition;
        rect.anchoredPosition = HiddenPosition;
    }

    private void Start()
    {
        Pump();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private Vector2 HiddenPosition => restPosition - new Vector2(rect.rect.width + 20f, 0f);

    private void Pump()
    {
        if (routine == null && waiting.Count > 0) routine = StartCoroutine(PlayAll());
    }

    private IEnumerator PlayAll()
    {
        while (waiting.Count > 0)
        {
            if (label != null) label.text = waiting.Dequeue();
            yield return Slide(HiddenPosition, restPosition, slideInSeconds);
            yield return new WaitForSecondsRealtime(holdSeconds);
            yield return Slide(restPosition, HiddenPosition, slideOutSeconds);
        }
        routine = null;
    }

    private IEnumerator Slide(Vector2 from, Vector2 to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            rect.anchoredPosition = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
            yield return null;
        }
        rect.anchoredPosition = to;
    }
}
