using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 손님 기분이 나빠지면 오른쪽 아래에 재촉 메시지가 토스트로 뜬다(글로벌 UI의 ToastMessage에 붙인다). 오른쪽 밖에서 왼쪽으로 튀어나오고, 잠시 머문 뒤 다시 오른쪽으로 들어간다.
/// 기분 좋음에서는 아무것도 뜨지 않고, 보통과 나쁨은 각자의 간격(초)마다 자기 목록에서 무작위로 한 줄을 보여 준다(바로 앞과 같은 줄은 피한다).
/// 그 기분 단계에 들어서자마자 한 번 뜨고, 그다음부터 그 단계의 간격마다 뜬다(간격은 단계에 들어선 때부터 센다). 기분 시간이 실제로 줄고 있을 때만 센다(GameClock.MoodFlowing: 주문 수락 뒤, 설정/결과로 멈춰 있지 않을 때).
/// 토스트의 배경 이미지는 손님 기분 남은 시간(MoodRatio)에 비례해 원래 색에서 urgentColor로 물든다.
/// 기분이 나쁨 단계가 되면 글씨 색이 badTextColor(베이지)로 바뀐다.
/// 토스트가 놓일 자리는 에디터에서 잡아 둔 위치다(실행하면 그 오른쪽 밖에서 시작한다). 움직이는 시간과 머무는 시간, 메시지와 간격은 인스펙터에서 정한다.
/// </summary>
public class MoodToast : MonoBehaviour
{
    [Serializable]
    private class MoodLines
    {
        [Min(1f)] public float intervalSeconds = 30f;
        [TextArea] public string[] lines;
    }

    [SerializeField] private TMP_Text label;

    [Header("움직임(초)")]
    [SerializeField] private float slideInSeconds = 0.3f;  // 오른쪽에서 튀어나오는 시간
    [SerializeField] private float holdSeconds = 3f;       // 다 나온 채로 머무는 시간
    [SerializeField] private float slideOutSeconds = 0.3f; // 오른쪽으로 들어가는 시간

    [Header("기분별 메시지와 간격(좋음은 아무것도 안 뜬다)")]
    [SerializeField] private MoodLines normal = new()
    {
        intervalSeconds = 30f,
        lines = new[] { "포장은 아직인가요?", "시간이 꽤 걸리네요.", "슬슬 기다리기 지치는데요." },
    };
    [SerializeField] private MoodLines bad = new()
    {
        intervalSeconds = 15f,
        lines = new[] { "아직 멀었어요?", "저 정말 바쁜데요!", "이러다 해 지겠어요.", "빨리 좀 해 주세요!" },
    };

    [Header("배경 색: 손님 기분 남은 시간이 줄수록 이 색으로 물든다(가득 찼을 때는 배경 이미지의 원래 색)")]
    [SerializeField] private Color urgentColor = Color.red;

    [Header("글씨 색: 기분이 나쁨 단계부터 이 색으로 바뀐다")]
    [SerializeField] private Color badTextColor = new(0.96f, 0.90f, 0.78f); // 베이지

    private Image background;
    private Color baseColor;
    private Color baseTextColor;
    private RectTransform rect;
    private Vector2 shownPosition;
    private Vector2 hiddenPosition;
    private MoodLevel level;
    private float timer;
    private string lastLine;
    private Coroutine showing;

    private void Awake()
    {
        rect = (RectTransform)transform;
        shownPosition = rect.anchoredPosition;

        // 숨는 자리: 놓아 둔 자리에서 오른쪽으로, 토스트가 부모의 오른쪽 가장자리 밖으로 완전히 나가는 만큼
        float parentWidth = ((RectTransform)rect.parent).rect.width;
        hiddenPosition = new Vector2(parentWidth * (1f - rect.anchorMax.x) + rect.rect.width * rect.pivot.x, shownPosition.y);
        rect.anchoredPosition = hiddenPosition;

        background = GetComponent<Image>();
        if (background != null) baseColor = background.color;
        if (label != null) baseTextColor = label.color;
    }

    private void Update()
    {
        GameClock clock = GameClock.Instance;
        if (clock == null) return;
        if (background != null) background.color = Color.Lerp(urgentColor, baseColor, clock.MoodRatio); // 기분 남은 시간에 비례해 물든다
        if (label != null) label.color = clock.Mood == MoodLevel.Bad ? badTextColor : baseTextColor;
        if (!clock.MoodFlowing) return;

        bool entered = clock.Mood != level; // 새 단계에 막 들어섰다
        if (entered)
        {
            level = clock.Mood;
            timer = 0f; // 그때부터 간격을 센다
        }

        MoodLines current = level == MoodLevel.Normal ? normal : level == MoodLevel.Bad ? bad : null;
        if (current == null || current.lines == null || current.lines.Length == 0) return;

        if (!entered)
        {
            timer += Time.deltaTime;
            if (timer < current.intervalSeconds) return;
            timer = 0f;
        }
        Show(Pick(current.lines)); // 단계에 들어서자마자 한 번, 그다음부터는 간격마다
    }

    private string Pick(string[] lines)
    {
        int at = Array.IndexOf(lines, lastLine);
        lastLine = lines[at < 0 ? UnityEngine.Random.Range(0, lines.Length) : (at + 1 + UnityEngine.Random.Range(0, lines.Length - 1)) % lines.Length];
        return lastLine;
    }

    private void Show(string message)
    {
        if (label != null) label.text = message;
        if (showing != null) StopCoroutine(showing);
        showing = StartCoroutine(Slide());
    }

    private IEnumerator Slide()
    {
        yield return Move(hiddenPosition, shownPosition, slideInSeconds);
        yield return Move(shownPosition, shownPosition, holdSeconds); // 같은 자리에서 머문다
        yield return Move(shownPosition, hiddenPosition, slideOutSeconds);
        showing = null;
    }

    /// <summary>from에서 to로 seconds에 걸쳐 옮긴다(from과 to가 같으면 그 시간만큼 머문다).</summary>
    private IEnumerator Move(Vector2 from, Vector2 to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            rect.anchoredPosition = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
            yield return null;
        }
        rect.anchoredPosition = to;
    }
}
