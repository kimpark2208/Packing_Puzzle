using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TutorialLessons의 안내를 위에서부터 훑어, 보여 줄 때가 된 안내를 TutorialOverlay로 보여 준다.
/// 조건이 잠깐 스치듯 참인 것(씬이 막 열렸거나 스와이프 중)은 건너뛰고, SettleSeconds 동안 계속 참일 때만 보여 준다.
/// 조건 검사는 하이라키 경로 찾기를 쓰므로 매 프레임이 아니라 CheckInterval마다 한다.
/// 튜토리얼 오버레이 프리팹에 붙어 있어서, 오버레이가 없는 씬에서는 아무 일도 하지 않는다.
/// </summary>
public class TutorialDirector : MonoBehaviour
{
    [SerializeField] private float settleSeconds = 0.5f;  // 조건이 이만큼 계속 참이어야 보여 준다
    [SerializeField] private float checkInterval = 0.1f;  // 조건을 검사하는 간격

    private readonly Dictionary<string, float> waited = new();
    private float sinceCheck;

    private void Update()
    {
        sinceCheck += Time.unscaledDeltaTime;
        if (sinceCheck < checkInterval) return;

        float elapsed = sinceCheck;
        sinceCheck = 0f;
        if (TutorialOverlay.IsShowing) return;

        foreach (TutorialLessons.Lesson lesson in TutorialLessons.All)
        {
            if (TutorialOverlay.HasSeen(lesson.id)) continue;

            if (!lesson.when())
            {
                waited.Remove(lesson.id);
                continue;
            }

            waited.TryGetValue(lesson.id, out float seconds);
            seconds += elapsed;
            if (seconds < settleSeconds)
            {
                waited[lesson.id] = seconds;
                continue;
            }

            waited.Remove(lesson.id);
            TutorialOverlay.Play(lesson.id, lesson.steps());
            return; // 한 번에 하나만(나머지는 다음에)
        }
    }
}
