using UnityEngine;

/// <summary>
/// 전등(랜턴)은 낮 영업이 끝난 뒤부터 밤 동안에만 보인다(GameClock.IsNightMode: 낮 종료 화면과 밤 화면).
/// 낮 메인, 낮 퍼즐처럼 낮 영업 중인 화면에서는 숨긴다. 글로벌 UI의 Clock 오브젝트에 붙이고, 전등 영역(LanternArea)을 연결한다.
/// 꺼진 오브젝트에서는 Update가 돌지 않으므로 이 컴포넌트는 항상 켜져 있는 오브젝트에 둔다.
/// </summary>
public class LanternVisibility : MonoBehaviour
{
    [SerializeField] private GameObject lanternArea;

    private void Update()
    {
        GameClock clock = GameClock.Instance;
        bool show = clock != null && clock.IsNightMode;
        if (lanternArea != null && lanternArea.activeSelf != show) lanternArea.SetActive(show);
    }
}
