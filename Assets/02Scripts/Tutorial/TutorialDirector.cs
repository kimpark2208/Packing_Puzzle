using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// TutorialLessons의 안내를 위에서부터 훑어, 보여 줄 때가 된 안내를 보여 준다: 팝업형(PopupTriggers + popups 에셋)을 먼저, 그다음 배너형(All, TutorialOverlay).
/// 조건이 잠깐 스치듯 참인 것(씬이 막 열렸거나 스와이프 중)은 건너뛰고, 연달아 두 번 검사해서 모두 참일 때만 보여 준다.
/// 조건 검사는 하이라키 경로 찾기를 쓰므로 매 프레임이 아니라 CheckInterval마다 한다.
/// 튜토리얼 오버레이 프리팹에 붙어 있어서, 오버레이가 없는 씬에서는 아무 일도 하지 않는다.
/// </summary>
public class TutorialDirector : MonoBehaviour
{
    [SerializeField] private float checkInterval = 0.5f; // 조건을 검사하는 간격(연달아 두 번 참이어야 보여 주므로 이만큼 계속 참이어야 한다)
    [SerializeField, Tooltip("팝업형 튜토리얼 에셋들(TutorialLessons.PopupTriggers의 id와 에셋의 id가 같은 것을 보여 준다).")] private TutorialPopupData[] popups;

    private readonly HashSet<string> trueLastCheck = new();
    private float sinceCheck;

    private void Update()
    {
        sinceCheck += Time.unscaledDeltaTime;
        if (sinceCheck < checkInterval) return;
        sinceCheck = 0f;

        if (TutorialOverlay.IsShowing || TutorialPopup.IsShowing || SceneTransition.Busy) return; // 씬이 바뀌는 동안(페이드)에는 띄우지 않는다. 곧 사라질 화면에서 안내가 소모되지 않게

        foreach (var trigger in TutorialLessons.PopupTriggers)
        {
            if (TutorialPopup.HasSeen(trigger.id)) continue;

            TutorialPopupData data = popups != null ? popups.FirstOrDefault(p => p != null && p.id == trigger.id) : null;
            if (data == null || !Ready(trigger.id, trigger.when)) continue;

            TutorialPopup.Play(data);
            return; // 한 번에 하나만(나머지는 다음에)
        }

        foreach (var lesson in TutorialLessons.All)
        {
            if (TutorialOverlay.HasSeen(lesson.id)) continue;
            if (!Ready(lesson.id, lesson.when)) continue;

            TutorialOverlay.Play(lesson.id, lesson.steps());
            return;
        }
    }

    /// <summary>조건이 연달아 두 번 참인가.</summary>
    private bool Ready(string id, System.Func<bool> when)
    {
        if (!when())
        {
            trueLastCheck.Remove(id);
            return false;
        }
        if (trueLastCheck.Add(id)) return false; // 처음 참이다: 다음 검사에서도 참이어야 한다

        trueLastCheck.Remove(id);
        return true;
    }
}
