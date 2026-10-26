using System.Collections;
using UnityEngine;

/// <summary>
/// 게임 진행용 시간. 튜토리얼 안내(배너·팝업)가 떠 있는 동안은 흐르지 않는다(GameClock.TutorialHold).
/// 화면 효과(PanelMotion, 씬 전환, 튜토리얼 자신)는 Time.unscaledDeltaTime을 그대로 쓰고, 안내가 떠 있는 동안 뒤에서 멈춰야 하는 것
/// (전화 벨 제한 시간, 대사 글자, 손님 사이의 쉬는 시간, 밤 퍼즐의 기억 순서나 물 채우기 같은 시간 제한)만 이 시간을 쓴다.
/// </summary>
public static class GameTime
{
    /// <summary>튜토리얼 때문에 게임이 멈춰 있는가.</summary>
    public static bool Held => GameClock.Instance != null && GameClock.Instance.TutorialHold;

    /// <summary>이번 프레임에 흐른 게임 시간(초). 멈춰 있으면 0.</summary>
    public static float Delta => Held ? 0f : Time.unscaledDeltaTime;

    /// <summary>seconds 동안 기다린다(Time.timeScale과 상관없고, 튜토리얼이 떠 있는 동안은 세지 않는다). <c>yield return GameTime.Wait(1f);</c></summary>
    public static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Delta) yield return null;
    }
}
