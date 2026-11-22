using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 대화(Dialogue) 하나를 말풍선에 한 줄씩 재생한다. 응답이 있는 줄은 응답 버튼(문구를 그 줄의 응답으로 바꿔서)을 눌러야 넘어가고,
/// 응답이 없는 줄은 글자가 다 나온 뒤 화면 아무 곳이나 누르면 넘어간다. 줄이 끝날 때마다 그 줄의 효과를 일으킨다(ScenarioEffects).
/// 응답 버튼 둘은 화면에서 왼쪽에 있는 쪽이 응답1, 오른쪽이 응답2다(응답이 하나면 오른쪽 하나만 보인다).
/// 버튼을 누르는 것은 화면 스크립트(MainUI)가 받아서 Answer()로 알려 주고, 손님이 나가고 다음으로 가는 일은 재생이 끝난 뒤 화면 스크립트가 맡는다.
/// </summary>
public class DialoguePlayer
{
    private readonly CustomerSpeech speech;
    private readonly GameObject leftButton, rightButton;
    private readonly TMP_Text leftLabel, rightLabel;
    private bool answered;

    public bool Playing { get; private set; }

    /// <summary>줄의 화자가 바뀌어 새 줄을 보이기 직전에 화자 이름(CSV 화자 열)과 함께 불린다. 화면 스크립트가 말하는 캐릭터 그림으로 바꾸는 데 쓰고, 돌려준 코루틴이 끝나면 대사가 나온다(바뀌는 모션을 기다린다).</summary>
    public System.Func<string, IEnumerator> SpeakerChanged;

    public DialoguePlayer(CustomerSpeech speech, GameObject buttonA, GameObject buttonB)
    {
        this.speech = speech;
        bool aIsLeft = buttonA != null && buttonB != null && buttonA.transform.position.x <= buttonB.transform.position.x;
        leftButton = aIsLeft ? buttonA : buttonB;
        rightButton = aIsLeft ? buttonB : buttonA;
        leftLabel = LabelOf(leftButton);
        rightLabel = LabelOf(rightButton);
        HideBadge(leftButton);
        HideBadge(rightButton);
    }

    private const string BadgeName = "DiaBadge"; // 응답 버튼 안의 다이아 표시(다이아 아이콘 + 개수 글씨). 다이아가 드는 응답에만 보인다
    private const string BadgeTextName = "Text";

    /// <summary>버튼의 문구 글씨(버튼 바로 아래의 TMP 글씨. 다이아 표시 안의 글씨와 섞이지 않게 표시 안은 뺀다).</summary>
    private static TMP_Text LabelOf(GameObject button)
    {
        if (button == null) return null;

        Transform badge = button.transform.Find(BadgeName);
        return button.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => badge == null || !t.transform.IsChildOf(badge));
    }

    private int shownAnswerCount; // 지금 보이는 응답 버튼 수(응답이 하나면 오른쪽 버튼 하나뿐이다)
    private int choice;           // 가장 최근 선택에서 누른 응답 번호(1: 왼쪽 버튼=응답1, 2: 오른쪽 버튼=응답2). 선택이 없었으면 0
    private int[] shownCosts = System.Array.Empty<int>(); // 지금 보이는 응답마다 드는 다이아

    /// <summary>
    /// 응답 버튼이 눌렸다(재생 중일 때만 뜻이 있다). 어느 버튼인지로 응답 번호를 기억해 조건이 붙은 다음 줄을 고른다.
    /// 다이아가 드는 응답이면 누르는 순간 다이아를 쓴다. 모자라면 알림만 띄우고 응답하지 않는다.
    /// </summary>
    public void Answer(GameObject button)
    {
        if (!Playing || shownAnswerCount == 0 || answered) return;

        int picked = shownAnswerCount >= 2 && button == rightButton ? 2 : 1;
        int cost = shownCosts.Length >= shownAnswerCount ? shownCosts[shownAnswerCount >= 2 ? picked - 1 : 0] : 0;
        if (cost > 0 && CurrencyManager.Instance != null && !CurrencyManager.Instance.TrySpendDiamonds(cost))
        {
            NoticeToast.Show("다이아가 부족해요");
            return;
        }

        choice = picked;
        answered = true;
    }

    public IEnumerator Play(Dialogue dialogue)
    {
        string leftOriginal = leftLabel != null ? leftLabel.text : null;
        string rightOriginal = rightLabel != null ? rightLabel.text : null;
        Playing = true;
        choice = 0;
        string speaker = null;

        foreach (DialogueLine line in dialogue.lines)
        {
            if (line.condition != 0 && line.condition != choice) continue; // 고르지 않은 응답에 붙은 줄은 건너뛴다
            if (line.memory.Any(m => !m.Holds)) continue; // 기억해 둔 값이 조건과 다른 줄도 건너뛴다

            if (!string.IsNullOrEmpty(line.speaker) && line.speaker != speaker)
            {
                speaker = line.speaker;
                if (SpeakerChanged != null) yield return SpeakerChanged(speaker);
            }

            ShowAnswers(System.Array.Empty<string>(), System.Array.Empty<int>()); // 대사가 다 나올 때까지는 버튼을 숨긴다
            speech.Show(line.text);
            answered = false;

            if (line.answers.Length > 0)
            {
                while (speech.IsTyping) yield return null; // 대사가 다 나온 뒤에 선택 버튼이 나온다(글자 나오는 중에 화면을 누르면 한 번에 다 나온다)
                ShowAnswers(line.answers, line.answerCosts);
                yield return new WaitUntil(() => answered);
            }
            else yield return WaitForTap();

            foreach (DialogueEffect effect in line.effects)
            {
                if (effect.kind == DialogueEffect.Remember) ScenarioMemory.Remember(effect.target, choice); // `이름`이면 이 줄에서 누른 응답을, `이름=값`이면 그 값을 기억한다
                else ScenarioEffects.Apply(effect);
            }
        }

        Playing = false;
        shownCosts = System.Array.Empty<int>();
        SetBadge(leftButton, 0);
        SetBadge(rightButton, 0);
        if (leftLabel != null) leftLabel.text = leftOriginal;
        if (rightLabel != null) rightLabel.text = rightOriginal;
    }

    private void ShowAnswers(string[] answers, int[] costs)
    {
        shownAnswerCount = answers.Length;
        shownCosts = costs;
        if (leftButton != null) leftButton.SetActive(answers.Length >= 2);
        if (rightButton != null) rightButton.SetActive(answers.Length >= 1);
        if (answers.Length >= 2 && leftLabel != null) leftLabel.text = answers[0];
        if (answers.Length >= 1 && rightLabel != null) rightLabel.text = answers[answers.Length - 1]; // 하나면 그것, 둘이면 응답2

        int leftCost = answers.Length >= 2 && costs.Length >= 1 ? costs[0] : 0;
        int rightCost = answers.Length >= 1 && costs.Length >= answers.Length ? costs[answers.Length - 1] : 0;
        SetBadge(leftButton, leftCost);
        SetBadge(rightButton, rightCost);
    }

    /// <summary>응답 버튼의 다이아 표시: 다이아가 들면(cost &gt; 0) 개수와 함께 보이고, 아니면 숨긴다. 가진 다이아가 모자라면 버튼을 눌러지지 않게 한다.</summary>
    private static void SetBadge(GameObject button, int cost)
    {
        if (button == null) return;

        Transform badge = button.transform.Find(BadgeName);
        if (badge != null)
        {
            badge.gameObject.SetActive(cost > 0);
            TMP_Text text = badge.Find(BadgeTextName) != null ? badge.Find(BadgeTextName).GetComponent<TMP_Text>() : badge.GetComponentInChildren<TMP_Text>(true);
            if (text != null && cost > 0) text.text = $"X {cost}"; // 씬에 놓아 둔 "X 20" 모양에 맞춘다
        }

        if (button.TryGetComponent(out Button ui)) ui.interactable = cost <= 0 || CurrencyManager.Instance == null || CurrencyManager.Instance.CurrentDiamonds >= cost;
    }

    private static void HideBadge(GameObject button) => SetBadge(button, 0);

    /// <summary>글자가 다 나온 뒤에 화면 아무 곳이나 누를 때까지 기다린다(글자를 한 번에 보이게 한 눌림은 쓰지 않는다).</summary>
    private IEnumerator WaitForTap()
    {
        while (speech.IsTyping) yield return null;
        yield return null;
        while (Pointer.current == null || !Pointer.current.press.wasPressedThisFrame) yield return null;
    }
}
