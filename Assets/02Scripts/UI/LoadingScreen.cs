using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 로딩 씬: 문구(명언)와 상태 글자를 화면에 보여 주고, 먼저 해 둘 작업(SceneLoader가 넘긴 조각 작업)을 한 조각씩 돌린 뒤
/// 목적지 씬(SceneLoader.Target)을 비동기로 불러오며 막대를 채운다. 조각 하나는 한 프레임 안에서 끝까지 도니(그동안 화면이 멈춘다),
/// 조각이 끝날 때마다 막대와 문구가 갱신되도록 조각 사이에 한 프레임씩 쉰다.
/// 문구와 상태 글자는 각자의 목록에서 changeSeconds마다 무작위로 바뀐다(바로 앞과 같은 것은 건너뛴다).
/// 불러오기가 빨리 끝나도 minSeconds는 보여 준다. 막대의 손잡이(꽃)는 슬라이더 핸들이다. UI는 씬에 미리 배치되어 있고 내용만 채운다.
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    private const float WorkShare = 0.7f; // 작업이 있을 때 막대에서 작업이 차지하는 몫(나머지는 씬 불러오기)

    [SerializeField] private TMP_Text quoteText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Slider bar;
    [SerializeField, Min(0.1f)] private float minSeconds = 1f;  // 불러오기가 이보다 빨리 끝나도 이만큼은 보여 준다(막대는 이 시간에 걸쳐 천천히 찬다)
    [SerializeField] private float changeSeconds = 0.5f; // 문구와 상태 글자가 바뀌는 간격
    [SerializeField, TextArea] private string[] quotes =
    {
        "어떤 꽃다발은 사람보다 오래 기억됩니다.",
        "꽃은 말이 없어도 할 말을 다 합니다.",
        "오늘도 평범한 하루, 평범한 꽃집입니다.",
        "시든 꽃에도 이름은 있습니다.",
        "하루의 끝에는 늘 꽃 한 송이가 남습니다.",
    };
    [SerializeField] private string[] statusLines =
    {
        "꽃집 문을 여는 중",
        "꽃을 다듬는 중",
        "포장지를 펴는 중",
        "화병에 물을 채우는 중",
        "리본을 고르는 중",
    };

    private float elapsed;
    private float nextChange;
    private float shown; // 막대에 보이는 값: 실제 진행률을 따라가되 minSeconds에 1(가득)보다 빨리는 오르지 못한다

    private IEnumerator Start()
    {
        if (bar != null) bar.value = 0f;
        nextChange = changeSeconds;
        Shuffle();
        yield return null; // 로딩 화면을 먼저 그린 뒤에 일을 시작한다(안 그러면 첫 조각 동안 이전 화면이 남는다)

        IEnumerator<float> work = SceneLoader.TakeWork();
        float workPart = work != null ? WorkShare : 0f;
        while (work != null && work.MoveNext())
        {
            Refresh(workPart * Mathf.Clamp01(work.Current));
            yield return null;
        }

        string target = string.IsNullOrEmpty(SceneLoader.Target) ? GameManager.SceneDayMain : SceneLoader.Target; // 로딩 씬만 단독으로 실행해도 메인으로 간다
        AsyncOperation load = SceneManager.LoadSceneAsync(target);
        load.allowSceneActivation = false; // 막대가 다 찰 때까지 열지 않는다

        while (shown < 1f || load.progress < 0.9f)
        {
            Refresh(workPart + (1f - workPart) * Mathf.Clamp01(load.progress / 0.9f)); // 비동기 진행률은 0.9에서 멈춰 기다린다
            yield return null;
        }

        load.allowSceneActivation = true;
    }

    /// <summary>한 프레임 분량을 갱신한다: 문구를 바꿀 때가 되면 바꾸고, 막대는 실제 진행률(actual)을 천천히 따라간다.</summary>
    private void Refresh(float actual)
    {
        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= nextChange)
        {
            Shuffle();
            nextChange = elapsed + changeSeconds; // 조각이 길어 한 프레임이 길었어도 문구가 연달아 바뀌지 않게 지금부터 센다
        }

        shown = Mathf.MoveTowards(shown, actual, Time.unscaledDeltaTime / minSeconds);
        if (bar != null) bar.value = shown;
    }

    /// <summary>문구와 상태 글자를 각자의 목록에서 새로 고른다.</summary>
    private void Shuffle()
    {
        if (quoteText != null) quoteText.text = Pick(quotes, quoteText.text);
        if (statusText != null) statusText.text = Pick(statusLines, statusText.text);
    }

    /// <summary>목록에서 무작위로 하나 고른다. 지금 보이는 것과 같은 것은 피한다(목록이 비면 지금 것을 그대로 둔다).</summary>
    private static string Pick(string[] lines, string current)
    {
        if (lines == null || lines.Length == 0) return current;

        int at = System.Array.IndexOf(lines, current); // 지금 것 다음 칸부터 (목록 길이 - 1)칸 안에서 고르면 지금 것은 절대 다시 뽑히지 않는다
        return lines[at < 0 ? Random.Range(0, lines.Length) : (at + 1 + Random.Range(0, lines.Length - 1)) % lines.Length];
    }
}
