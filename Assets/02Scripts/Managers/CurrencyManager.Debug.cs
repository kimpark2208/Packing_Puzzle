using System.Linq;
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

        if (GUILayout.Button("Debug: 일수 +1(하루 정산 없이 다음 날 낮으로, 그날 밤 새 꽃 지급)") && GameManager.Instance != null)
            GameManager.Instance.DebugAdvanceDay();

        if (GUILayout.Button("Debug: 하루 시간 -1분") && GameClock.Instance != null)
            GameClock.Instance.SkipDayTime(60f);

        if (GUILayout.Button("Debug: 현재 밤 손님 스킵(진단 씬이면 정답 처리)") && GameManager.Instance != null)
            GameManager.Instance.DebugSkipNightPuzzle();

        if (GUILayout.Button("Debug: 밤 시간 -1분") && GameClock.Instance != null)
            GameClock.Instance.SkipNightTime(60f);

        if (GUILayout.Button("Debug: 손님 기분 -20%") && GameClock.Instance != null)
            GameClock.Instance.DrainMood(0.2f);

        if (GUILayout.Button("Debug: 낮 퍼즐 강제 제출"))
            FindFirstObjectByType<DayPuzzleUI>()?.DebugForceSubmit();

        if (GUILayout.Button("Debug: 캐릭터 새로 뽑기(밤이면 파란 피부 귀신)"))
        {
            MainState? state = GameManager.Instance != null ? GameManager.Instance.MainState : null;
            bool night = state == MainState.NightCustomer || state == MainState.NightEnding;
            FindFirstObjectByType<CharacterGenerator>()?.Randomize(night);
        }

        if (GUILayout.Button($"Debug: 배속 x{Time.timeScale:0.#} (x1 → x2 → x4 → x8)"))
            Time.timeScale = Time.timeScale >= 8f ? 1f : Time.timeScale * 2f;

        GUILayout.Label($"현재 금액: {CurrentMoney}\n현재 일수: {CurrentDay}\n포장지 재고: {string.Join(", ", ownedWrappers.OrderBy(id => id).Select(id => $"{id}번 {GetWrapperStock(id)}"))}");
    }
#endif
}
