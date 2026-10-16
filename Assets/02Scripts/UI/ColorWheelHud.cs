using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 구석의 컬러휠(색 궁합 안내). 룰렛처럼 맨 위에 핀이 고정되어 있고, 판(휠)이 돌아서 핀 아래에 선택한 색의 칸이 오게 한다.
/// 평소에는 숨어 있다가 꽃 선택부터 나타나고(Show), 낮 퍼즐에 들어가면 핀이 활성화된다(ActivatePin).
/// 퍼즐에서 플레이어가 꽃을 집으면(BlockDrag.Grabbed) 휠이 그 꽃의 색 칸이 위로 오도록 부드럽게 돌아간다. 흰 꽃은 휠에 칸이 없어 그대로 둔다.
/// 휠 그림은 맨 위가 빨강이고 시계 방향으로 빨 주 노 파 보 핑이며, 한 칸이 60도다. 휠의 회전은 이 컴포넌트가 정한다(에디터에서 돌려 둔 값은 실행하면 0도가 된다).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ColorWheelHud : MonoBehaviour
{
    private static readonly FlowerData.Color[] WheelOrder =
    {
        FlowerData.Color.Red, FlowerData.Color.Orange, FlowerData.Color.Yellow,
        FlowerData.Color.Blue, FlowerData.Color.Purple, FlowerData.Color.Pink,
    };

    private const float StepDegrees = 60f;

    private static ColorWheelHud instance;

    [SerializeField, Tooltip("컬러휠 그림(돌아가는 판). 맨 위가 빨강, 시계 방향으로 빨 주 노 파 보 핑.")] private RectTransform wheel;
    [SerializeField, Tooltip("판 맨 위에 고정된 핀 그림. 퍼즐에 들어가면 나타난다.")] private Image pin;
    [SerializeField, Min(0.1f), Tooltip("휠이 돌아가는 부드러움(러프). 클수록 빨리 따라간다.")] private float follow = 10f;
    [SerializeField, Min(0.01f), Tooltip("컬러휠과 핀이 나타나는 시간(초).")] private float fadeSeconds = 0.3f;

    private CanvasGroup group;
    private float targetAlpha;
    private float pinTargetAlpha;
    private float angle;
    private float targetAngle;

    /// <summary>컬러휠을 나타낸다(핀은 아직 없다). 컬러휠이 없는 씬이면 아무 일도 없다.</summary>
    public static void Show()
    {
        ColorWheelHud hud = Find();
        if (hud != null) hud.targetAlpha = 1f;
    }

    /// <summary>핀을 활성화한다(컬러휠도 같이 나타난다).</summary>
    public static void ActivatePin()
    {
        ColorWheelHud hud = Find();
        if (hud == null) return;

        hud.targetAlpha = 1f;
        hud.pinTargetAlpha = 1f;
    }

    private static ColorWheelHud Find()
    {
        if (instance == null) instance = FindFirstObjectByType<ColorWheelHud>(); // 같은 오브젝트가 겹쳐 만들어졌다 사라진 경우를 위해 다시 찾는다
        return instance;
    }

    private void Awake()
    {
        instance = this;
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        SetPinAlpha(0f);
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

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Point(FlowerData flower)
    {
        int step = flower != null ? Array.IndexOf(WheelOrder, flower.color) : -1;
        if (step < 0) return;

        targetAngle = step * StepDegrees; // 그 칸이 위로 오려면 휠이 시계 반대 방향으로 돈다(Unity의 +방향)
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, dt / fadeSeconds);
        SetPinAlpha(Mathf.MoveTowards(pin.color.a, pinTargetAlpha, dt / fadeSeconds));

        angle = Mathf.LerpAngle(angle, targetAngle, 1f - Mathf.Exp(-follow * dt));
        wheel.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void SetPinAlpha(float alpha)
    {
        Color color = pin.color;
        color.a = alpha;
        pin.color = color;
    }
}
