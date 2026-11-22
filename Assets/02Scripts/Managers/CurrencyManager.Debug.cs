using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>CurrencyManager의 에디터 전용 디버그 메뉴(OnGUI). 빌드에는 들어가지 않는다. 새 디버그 기능은 여기에 한 줄씩 더한다.</summary>
public partial class CurrencyManager
{
#if UNITY_EDITOR
    private bool debugOpen;

    // S 키 = 스크린샷. 프로젝트 루트 Screenshots/ 폴더(Assets 밖이라 임포트·커밋에 안 섞임)에 저장.
    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.sKey.wasPressedThisFrame) return;

        string dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "Screenshots");
        System.IO.Directory.CreateDirectory(dir);
        string path = System.IO.Path.Combine(dir, $"shot_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"스크린샷 저장: {path}");
    }

    private void OnGUI()
    {
        if (GUILayout.Button(debugOpen ? "Debug ▲ 닫기" : "Debug ▼ 열기")) debugOpen = !debugOpen;
        if (!debugOpen) return;

        if (GUILayout.Button("Debug: +1000 금액"))
            AddMoney(1000);

        if (GUILayout.Button("Debug: 일수 +1(하루 정산 없이 다음 날로, 새 꽃과 포장지 해금도 그날 기준으로)") && GameManager.Instance != null)
            GameManager.Instance.DebugAdvanceDay();

        if (GUILayout.Button("Debug: 예약 전화 걸기(메인 화면에서)"))
            FindFirstObjectByType<ReservationCall>()?.Begin();

        if (GUILayout.Button("Debug: 하루 시간 -1분") && GameClock.Instance != null)
            GameClock.Instance.SkipDayTime(60f);

        if (GUILayout.Button("Debug: 상점으로(그날 아이템 지급, 상점 앞 대화 생략)"))
            GameManager.Instance?.DebugGoShop();

        if (GUILayout.Button("Debug: 손님 기분 -20%") && GameClock.Instance != null)
            GameClock.Instance.DrainMood(0.2f);

        if (GUILayout.Button("Debug: 낮 퍼즐 스킵(주문 수락 뒤부터, 퍼즐 씬이면 강제 제출)"))
            GameManager.Instance?.DebugSkipDayPuzzle();

        if (GUILayout.Button("Debug: 캐릭터 새로 뽑기(밤이면 파란 피부 귀신)"))
        {

            bool night = GameClock.Instance != null && GameClock.Instance.IsNightMode;
            FindFirstObjectByType<CharacterGenerator>()?.Randomize(night);
        }

        if (GUILayout.Button($"Debug: 배속 x{Time.timeScale:0.#} (x1 → x2 → x4 → x8)"))
            Time.timeScale = Time.timeScale >= 8f ? 1f : Time.timeScale * 2f;

        GUILayout.Label($"현재 금액: {CurrentMoney}\n현재 일수: {CurrentDay}\n포장지 재고: {string.Join(", ", ownedWrappers.OrderBy(id => id).Select(id => $"{id}번 {GetWrapperStock(id)}"))}");
    }
#endif
}
