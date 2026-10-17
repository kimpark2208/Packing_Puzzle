using UnityEngine;

/// <summary>
/// 화면 구석의 컬러휠(색 궁합 안내). 룰렛처럼 맨 위에 핀이 고정되어 있고, 판(휠)이 돌아서 핀 아래에 선택한 색의 칸이 오게 한다.
/// 평소에는 꺼져 있다가 꽃 선택부터 켜지고(Show), 낮 퍼즐에 들어가면 핀이 켜진다(ActivatePin). 에디터에서는 켜 둔 채로 두면 되고, 실행하면 시작할 때 꺼진다.
/// 퍼즐에서 플레이어가 꽃을 집으면(BlockDrag.Grabbed) 휠이 그 꽃의 색 칸이 위로 오도록 부드럽게 돌아간다. 흰 꽃은 휠에 칸이 없어 그대로 둔다.
/// 휠 그림은 맨 위가 빨강이고 시계 방향으로 빨 주 노 파 보 핑이며, 한 칸이 60도다(FlowerData.Color의 앞 6개 순서와 같다).
/// 휠의 회전은 이 컴포넌트가 정한다(에디터에서 돌려 둔 값은 실행하면 0도가 된다).
/// 꺼진 오브젝트는 스스로 켜질 수 없으므로, 이 컴포넌트는 컬러휠 영역이 아니라 항상 켜져 있는 부모 오브젝트에 둔다.
/// </summary>
public class ColorWheelHud : MonoBehaviour
{
    private const float StepDegrees = 60f;

    private static ColorWheelHud instance;

    [SerializeField, Tooltip("컬러휠 영역(휠과 핀을 담은 오브젝트). 평소에는 꺼 둔다.")] private GameObject area;
    [SerializeField, Tooltip("컬러휠 그림(돌아가는 판). 맨 위가 빨강, 시계 방향으로 빨 주 노 파 보 핑.")] private RectTransform wheel;
    [SerializeField, Tooltip("판 맨 위에 고정된 핀. 퍼즐에 들어가면 켜진다.")] private GameObject pin;
    [SerializeField, Min(0.1f), Tooltip("휠이 돌아가는 부드러움(러프). 클수록 빨리 따라간다.")] private float follow = 10f;

    private float angle;
    private float targetAngle;

    /// <summary>컬러휠을 켠다(핀은 아직 없다). 컬러휠이 없는 씬이면 아무 일도 없다.</summary>
    public static void Show()
    {
        ColorWheelHud hud = Find();
        if (hud != null) hud.area.SetActive(true);
    }

    /// <summary>핀을 켠다(컬러휠도 같이 켜진다).</summary>
    public static void ActivatePin()
    {
        ColorWheelHud hud = Find();
        if (hud == null) return;

        hud.area.SetActive(true);
        hud.pin.SetActive(true);
    }

    private static ColorWheelHud Find()
    {
        if (instance == null) instance = FindFirstObjectByType<ColorWheelHud>(); // 사라진 씬의 것은 Unity가 null로 보므로 새로 찾는다
        return instance;
    }

    private void Awake()
    {
        area.SetActive(false);
        pin.SetActive(false);
        wheel.localRotation = Quaternion.identity; // 맨 위가 빨강인 모습
    }

    private void OnEnable()
    {
        BlockDrag.Grabbed += Point;
    }

    private void OnDisable()
    {
        BlockDrag.Grabbed -= Point;
    }

    private void Point(FlowerData flower)
    {
        if (flower == null || flower.color == FlowerData.Color.White) return;

        targetAngle = (int)flower.color * StepDegrees; // 그 칸이 위로 오려면 휠이 시계 반대 방향으로 돈다(Unity의 +방향)
    }

    private void Update()
    {
        if (!area.activeSelf) return;

        angle = Mathf.LerpAngle(angle, targetAngle, 1f - Mathf.Exp(-follow * Time.unscaledDeltaTime));
        wheel.localRotation = Quaternion.Euler(0f, 0f, angle);
    }
}
