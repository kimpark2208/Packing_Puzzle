using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 시작 화면(00Start): 게임 이름이 뜨고, 화면을 터치하면 게임이 시작된다(GameManager.StartGame → 로딩 씬 → 첫날 메인).
/// "터치해서 시작" 안내 문구는 천천히 깜빡인다. UI는 씬에 미리 배치되어 있다.
/// </summary>
public class StartScreen : MonoBehaviour
{
    [SerializeField] private Button touchArea;  // 화면 전체
    [SerializeField] private CanvasGroup prompt; // 깜빡이는 안내 문구
    [SerializeField] private float blinkSeconds = 1.8f;
    [SerializeField, Range(0f, 1f)] private float dimmestAlpha = 0.25f;

    private bool started;

    private void Awake()
    {
        touchArea.onClick.AddListener(OnTouched);
    }

    private void Update()
    {
        if (prompt == null) return;
        float wave = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / blinkSeconds) + 1f) * 0.5f;
        prompt.alpha = Mathf.Lerp(dimmestAlpha, 1f, wave);
    }

    private void OnTouched()
    {
        if (started) return;
        started = true; // 로딩 씬이 열리는 동안 다시 눌러도 한 번만
        if (GameManager.Instance != null) GameManager.Instance.StartGame();
    }
}
