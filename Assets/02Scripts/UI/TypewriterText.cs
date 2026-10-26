using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 글자를 한 글자씩 드러내며 보여 주는 TMP 글자(말풍선). Play로 새 대사를 시작하고, 드러나는 동안 화면의 아무 곳이나 누르면 한 번에 전부 나온다.
/// 누른 곳에 버튼이 있으면 그 버튼도 그대로 눌린다(전체 보이기는 따로 일어난다).
/// 말하는 소리(voice)를 두면 글자가 드러나는 동안 반복 재생하고, 다 드러나거나(건너뛰기 포함) 꺼지면 멈춘다.
/// 한 문장(. ! ? 로 끝나는 말. …는 문장 안의 쉼으로 본다)이 끝나면 sentencePause초 쉬었다가 다음 문장을 이어 보여 준다(말하는 소리 voice도 그동안 멈춘다).
/// 켜져 있지 않을 때 Play하면 바로 전부 보이고, 꺼지면 남은 글자도 전부 보인다.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class TypewriterText : MonoBehaviour
{
    [SerializeField, Min(1f), Tooltip("1초에 드러나는 글자 수.")] private float charactersPerSecond = 30f;
    [SerializeField, Tooltip("말하는 소리. 글자가 드러나는 동안 반복 재생한다(Loop를 켜 둔다). 비우면 소리가 없다.")] private AudioSource voice;

    [Header("문장 사이")]
    [SerializeField, Min(0f), Tooltip("한 문장이 끝나고 다음 문장이 시작되기 전에 쉬는 시간(초).")] private float sentencePause = 0.4f;

    private TMP_Text text;
    private readonly List<int> sentenceStarts = new(); // 대사에서 각 문장이 시작하는 글자 번호
    private int nextPause;    // 다음에 쉬어 갈 문장(sentenceStarts 번호). 첫 문장 앞에서는 쉬지 않는다
    private float pause;      // 남은 쉬는 시간(초)
    private float shown;      // 지금까지 드러난 글자 수(소수)
    private int total;        // 대사의 전체 글자 수(태그 제외)
    private bool typing;

    public bool IsTyping => typing;

    private TMP_Text Text => text != null ? text : text = GetComponent<TMP_Text>();

    /// <summary>새 대사를 한 글자씩 보여 주기 시작한다.</summary>
    public void Play(string line)
    {
        Text.text = line;
        if (!isActiveAndEnabled)
        {
            Finish();
            return;
        }
        Text.ForceMeshUpdate(); // 글자 수(태그 제외)를 알려면 메시가 만들어져 있어야 한다
        total = Text.textInfo.characterCount;
        Text.maxVisibleCharacters = 0;
        shown = 0f;
        typing = true;
        if (voice != null) voice.Play();
        FindSentenceStarts();
        nextPause = 1;
        pause = 0f;
    }

    /// <summary>드러나는 중이면 전부 보인다.</summary>
    public void Skip()
    {
        if (typing) Finish();
    }

    private void Update()
    {
        if (!typing) return;

        Pointer pointer = Pointer.current;
        if (pointer != null && pointer.press.wasPressedThisFrame)
        {
            Finish(); // 화면 아무 곳이나 누르면 전부 보인다
            return;
        }

        if (pause > 0f)
        {
            pause -= GameTime.Delta;
            if (pause <= 0f && voice != null) voice.UnPause();
            return;
        }

        float next = shown + charactersPerSecond * GameTime.Delta;
        if (nextPause < sentenceStarts.Count && next >= sentenceStarts[nextPause])
        {
            // 한 문장이 끝났다: 다음 문장의 첫 글자 앞에서 잠시 쉰다
            shown = sentenceStarts[nextPause++];
            Text.maxVisibleCharacters = Mathf.FloorToInt(shown);
            if (sentencePause > 0f)
            {
                pause = sentencePause;
                if (voice != null) voice.Pause();
            }
            return;
        }

        shown = next;
        if (shown >= total)
        {
            Finish();
            return;
        }
        Text.maxVisibleCharacters = Mathf.FloorToInt(shown);
    }

    private void OnDisable()
    {
        if (typing) Finish();
    }

    private void Finish()
    {
        typing = false;
        if (voice != null) voice.Stop();
        Text.maxVisibleCharacters = int.MaxValue;
    }

    /// <summary>문장이 시작하는 글자 번호를 모은다. 마침표류(. ! ?) 뒤의 첫 글자부터 다음 문장이다(마침표가 여러 개 이어져도 한 번).</summary>
    private void FindSentenceStarts()
    {
        sentenceStarts.Clear();
        bool needStart = true;
        TMP_CharacterInfo[] characters = Text.textInfo.characterInfo;
        for (int i = 0; i < total; i++)
        {
            char c = characters[i].character;
            if (char.IsWhiteSpace(c)) continue;

            if (c == '.' || c == '!' || c == '?') needStart = true;
            else if (needStart)
            {
                sentenceStarts.Add(i);
                needStart = false;
            }
        }
    }

}
