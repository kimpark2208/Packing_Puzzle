using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 진단 결과 연출(노트의 체크리스트 위에서 그린다). 내가 체크한 항목만 또렷하게 두고 나머지 글씨는 알파를 낮춘 뒤, 위에서부터 아래로 내려가며 채점한다:
/// 맞게 체크한 항목은 체크박스가 빛나며 체크 표시, 잘못 체크한 항목은 체크박스에 가위표와 글씨에 취소선, 마지막으로 정답인데 체크하지 않은 항목은 체크 표시 없이 글씨만 깜빡인다.
/// 연출 중 화면을 한 번 누르면 끝난 모습으로 빨리감기 한다. 줄(Row)은 FlowerData.Ailment 순서대로 둔다. 칸과 표시 오브젝트는 에디터에서 미리 놓아 두고, 코드는 켜고 끄고 움직이기만 한다.
/// </summary>
public class DiagnosisResultView : MonoBehaviour
{
    [Serializable]
    private class Row
    {
        [Tooltip("항목 글씨.")] public TMP_Text label;
        [Tooltip("체크박스 안의 체크 표시(진단에서 체크했을 때, 그리고 맞게 체크한 항목을 채점할 때 나타난다).")] public Image check;
        [Tooltip("체크박스 안의 가위표(잘못 체크했을 때 나타난다).")] public Image cross;
        [Tooltip("체크박스 뒤에서 퍼지는 빛(맞게 체크했을 때).")] public Image glow;
        [Tooltip("글씨 위로 그어지는 취소선(잘못 체크했을 때). Pivot X를 0(왼쪽)으로 두고 높이만 정해 둔다. 너비는 코드가 글씨 너비에 맞춘다.")] public RectTransform strike;
    }

    [SerializeField, Tooltip("FlowerData.Ailment 순서대로: 꽃잎 결손, 줄기 꺾임, 꽃잎 흩어짐, 메마름, 기억 흐려짐.")] private Row[] rows;
    [SerializeField, Range(0f, 1f), Tooltip("체크하지 않은 항목 글씨의 알파(그리고 깜빡임의 가장 흐린 값).")] private float dimAlpha = 0.3f;
    [SerializeField, Min(0f), Tooltip("한 항목을 채점하고 다음 항목으로 넘어가기 전 쉬는 시간(초).")] private float rowDelay = 0.7f;
    [SerializeField, Min(0.01f), Tooltip("체크 표시와 가위표가 튀어나오는 시간(초).")] private float markSeconds = 0.35f;
    [SerializeField, Min(0.01f), Tooltip("체크박스 뒤 빛이 퍼지며 사라지는 시간(초).")] private float glowSeconds = 0.6f;
    [SerializeField, Min(1f), Tooltip("빛이 퍼지는 크기(처음 크기의 몇 배까지).")] private float glowScale = 2.4f;
    [SerializeField, Min(0.01f), Tooltip("취소선이 왼쪽에서 오른쪽으로 그어지는 시간(초).")] private float strikeSeconds = 0.35f;
    [SerializeField, Min(0.01f), Tooltip("놓친 정답이 깜빡이는 시간(초).")] private float blinkSeconds = 1.2f;
    [SerializeField, Min(1), Tooltip("놓친 정답이 깜빡이는 횟수.")] private int blinkCount = 3;
    [SerializeField, Min(0f), Tooltip("연출이 끝난 뒤 결과를 보여 주는 시간(초).")] private float holdSeconds = 1.2f;

    [Header("소리")]
    [SerializeField, Tooltip("체크 표시나 가위표가 나타날 때 나는 소리(진단에서 체크할 때, 채점 연출에서 체크·가위표가 나타날 때).")] private AudioClip checkSound;
    [SerializeField, Range(0f, 1f)] private float checkVolume = 1f;
    [SerializeField, Tooltip("취소선이 그어지기 시작할 때 나는 소리.")] private AudioClip strikeSound;
    [SerializeField, Range(0f, 1f)] private float strikeVolume = 1f;

    private bool playing;
    private bool skip;

    private void Update()
    {
        if (playing && Input.GetMouseButtonDown(0)) skip = true;
    }

    /// <summary>진단에서 항목을 체크하거나 해제할 때: 체크박스의 체크 표시를 켜고 끈다(켤 때 체크 소리가 난다).</summary>
    public void SetChecked(int index, bool on)
    {
        SetMark(rows[index].check, on);
        if (on) Sfx.Play(checkSound, checkVolume);
    }

    /// <summary>결과 연출을 처음부터 끝까지 보여 준다(끝난 뒤 holdSeconds까지, 빨리감기를 눌러도 끝난 모습을 holdSeconds 동안 보여 준다).</summary>
    /// <param name="answer">꽃의 정답 아픔.</param>
    /// <param name="picked">내가 체크한 아픔.</param>
    public IEnumerator Play(ICollection<FlowerData.Ailment> answer, ICollection<FlowerData.Ailment> picked)
    {
        playing = true;
        skip = false;

        for (int i = 0; i < rows.Length; i++)
        {
            Row row = rows[i];
            var ailment = (FlowerData.Ailment)i;
            row.label.alpha = picked.Contains(ailment) ? 1f : dimAlpha;
            row.check.gameObject.SetActive(false);
            row.cross.gameObject.SetActive(false);
            row.glow.gameObject.SetActive(false);
            SetStrikeWidth(row, 0f);
        }

        // 위에서부터 내가 체크한 항목을 채점한다
        for (int i = 0; i < rows.Length && !skip; i++)
        {
            var ailment = (FlowerData.Ailment)i;
            if (!picked.Contains(ailment)) continue;

            yield return Wait(rowDelay);
            if (skip) break;
            yield return answer.Contains(ailment) ? Correct(rows[i]) : Wrong(rows[i]);
        }

        // 마지막으로 정답인데 체크하지 않은 항목의 글씨를 깜빡여 알려 준다
        for (int i = 0; i < rows.Length && !skip; i++)
        {
            var ailment = (FlowerData.Ailment)i;
            if (picked.Contains(ailment) || !answer.Contains(ailment)) continue;

            yield return Wait(rowDelay);
            if (skip) break;
            yield return Missed(rows[i]);
        }

        if (skip)
        {
            ShowFinal(answer, picked);
            skip = false;
        }
        yield return Wait(holdSeconds);
        playing = false;
    }

    /// <summary>모든 항목을 연출이 끝난 모습으로 만든다(빨리감기).</summary>
    private void ShowFinal(ICollection<FlowerData.Ailment> answer, ICollection<FlowerData.Ailment> picked)
    {
        for (int i = 0; i < rows.Length; i++)
        {
            Row row = rows[i];
            var ailment = (FlowerData.Ailment)i;
            bool isPicked = picked.Contains(ailment), isAnswer = answer.Contains(ailment);

            row.glow.gameObject.SetActive(false);
            row.label.alpha = isPicked || isAnswer ? 1f : dimAlpha;
            SetMark(row.check, isPicked && isAnswer);
            SetMark(row.cross, isPicked && !isAnswer);
            SetStrikeWidth(row, isPicked && !isAnswer ? row.label.preferredWidth : 0f);
        }
    }

    /// <summary>맞게 체크했다: 체크박스 뒤에서 빛이 퍼지고 체크 표시가 튀어나온다.</summary>
    private IEnumerator Correct(Row row)
    {
        row.glow.gameObject.SetActive(true);
        StartCoroutine(Glow(row.glow));
        yield return Pop(row.check);
    }

    /// <summary>잘못 체크했다: 체크박스에 가위표가 튀어나오고 글씨에 취소선이 그어진다.</summary>
    private IEnumerator Wrong(Row row)
    {
        StartCoroutine(Strike(row));
        yield return Pop(row.cross);
    }

    /// <summary>정답인데 체크하지 않았다: 체크 표시 없이 글씨만 깜빡이다가 또렷하게 남는다.</summary>
    private IEnumerator Missed(Row row)
    {
        yield return Tween(blinkSeconds, t => row.label.alpha = Mathf.Lerp(dimAlpha, 1f, Mathf.Abs(Mathf.Sin(Mathf.PI * blinkCount * t))));
        row.label.alpha = 1f;
    }

    private IEnumerator Glow(Image glow)
    {
        Color color = glow.color;
        yield return Tween(glowSeconds, t =>
        {
            glow.transform.localScale = Vector3.one * Mathf.Lerp(1f, glowScale, t);
            glow.color = new Color(color.r, color.g, color.b, color.a * (1f - t));
        });
        glow.gameObject.SetActive(false);
        glow.transform.localScale = Vector3.one;
        glow.color = color;
    }

    private IEnumerator Strike(Row row)
    {
        Sfx.Play(strikeSound, strikeVolume);
        float width = row.label.preferredWidth;
        yield return Tween(strikeSeconds, t => SetStrikeWidth(row, width * t));
    }

    /// <summary>표시가 커지며 튀어나온다(살짝 넘쳤다가 제 크기로).</summary>
    private IEnumerator Pop(Image mark)
    {
        Sfx.Play(checkSound, checkVolume);
        SetMark(mark, true);
        yield return Tween(markSeconds, t => mark.transform.localScale = Vector3.one * EaseOutBack(t));
        mark.transform.localScale = Vector3.one;
    }

    private static void SetMark(Image mark, bool on)
    {
        mark.gameObject.SetActive(on);
        mark.transform.localScale = Vector3.one;
    }

    private static void SetStrikeWidth(Row row, float width)
    {
        row.strike.gameObject.SetActive(width > 0f);
        row.strike.sizeDelta = new Vector2(width, row.strike.sizeDelta.y);
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    /// <summary>seconds 동안 apply(0~1)를 매 프레임 부른다. 빨리감기를 누르면 바로 1로 끝낸다.</summary>
    private IEnumerator Tween(float seconds, Action<float> apply)
    {
        for (float time = 0f; time < seconds && !skip; time += Time.unscaledDeltaTime)
        {
            apply(time / seconds);
            yield return null;
        }
        apply(1f);
    }

    /// <summary>seconds 동안 기다린다. 빨리감기를 누르면 바로 끝낸다.</summary>
    private IEnumerator Wait(float seconds)
    {
        for (float time = 0f; time < seconds && !skip; time += Time.unscaledDeltaTime) yield return null;
    }
}
