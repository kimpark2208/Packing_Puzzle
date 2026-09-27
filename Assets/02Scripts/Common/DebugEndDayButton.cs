using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 임시 테스트용 버튼. 낮 퍼즐 씬에서 하루를 강제로 끝내고 DayToNight 화면으로 넘어간다.
/// </summary>
public class DebugEndDayButton : MonoBehaviour
{
    [SerializeField] private Button button;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(OnClicked);
    }

    private void OnClicked()
    {
        // GameFlowController 없이(부트스트랩 없이) 이 씬만 단독 실행해도 즉시 넘어가야 하는 테스트용 버튼이라
        // 싱글턴을 거치지 않고 씬을 직접 로드한다.
        UnityEngine.SceneManagement.SceneManager.LoadScene(GameFlowController.SceneDayToNight);
    }
}
