using UnityEngine;

/// <summary>CurrencyManager의 에디터 전용 디버그 메뉴(OnGUI). 빌드에는 들어가지 않는다. 새 디버그 기능은 여기에 한 줄씩 더한다.</summary>
public partial class CurrencyManager
{
#if UNITY_EDITOR
    private bool debugOpen;

    private void OnGUI()
    {
        if (GUILayout.Button(debugOpen ? "Debug ▲ 닫기" : "Debug ▼ 열기")) debugOpen = !debugOpen;
        if (!debugOpen) return;

        if (GUILayout.Button("Debug: +1000 금액"))
            AddMoney(1000);

        if (GUILayout.Button("Debug: 포장지 3 언락"))
            UnlockWrapper(3);

        if (GUILayout.Button("Debug: 다음 날"))
            AdvanceDay();

        if (GUILayout.Button("Debug: 하루 시간 -1분") && GameClock.Instance != null)
            GameClock.Instance.SkipDayTime(60f);

        if (GUILayout.Button("Debug: 밤 퍼즐 스킵(성공)") && GameManager.Instance != null)
            GameManager.Instance.DebugSkipNightPuzzle();

        if (GUILayout.Button("Debug: 밤 시간 -1분") && GameClock.Instance != null)
            GameClock.Instance.SkipNightTime(60f);

        if (GUILayout.Button("Debug: 손님 기분 -20%") && GameClock.Instance != null)
            GameClock.Instance.DrainMood(0.2f);

        if (GUILayout.Button("Debug: 리롤 횟수 +1"))
            FindFirstObjectByType<GachaManager>()?.AddRerolls(1);

        if (GUILayout.Button("Debug: 낮 퍼즐 강제 제출"))
            FindFirstObjectByType<DayPuzzleUI>()?.DebugForceSubmit();

        if (GUILayout.Button("Debug: 캐릭터 새로 뽑기"))
            FindFirstObjectByType<CharacterGenerator>()?.Randomize();

        if (GUILayout.Button($"Debug: 배속 x{Time.timeScale:0.#} (x1 → x2 → x4 → x8)"))
            Time.timeScale = Time.timeScale >= 8f ? 1f : Time.timeScale * 2f;

        GUILayout.Label($"현재 금액: {CurrentMoney}\n현재 일수: {CurrentDay}\n보유 포장지: {ownedWrappers.Count}개");
    }
#endif
}
