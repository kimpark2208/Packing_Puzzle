using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 패널/팝업/손님이 나타나고 사라질 때의 움직임. 켜지면(SetActive(true)) 스스로 나타나는 모션을 재생하고,
/// 사라질 때는 SetActive(false) 대신 Hide(또는 Close)를 부르면 반대 모션을 재생한 뒤 꺼진다. 모션은 위치(offset)와 투명도(CanvasGroup)로만 한다.
/// - 아래에서 올라옴(RiseFromBelow): 아래에서 올라오며 나타나고, 사라질 때는 반대로 내려가며 사라진다.
/// - 오른쪽에서 들어옴(SlideRightToLeft): 오른쪽에서 들어오며 나타나고, 사라질 때는 왼쪽으로 나가며 사라진다.
/// 위치는 현재 위치에 움직인 만큼만 더하고 빼므로, 다른 스크립트(SwipeParallax 등)가 옮기는 위치와 겹쳐도 된다.
/// 시간은 Time.unscaledDeltaTime으로 센다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public class PanelMotion : MonoBehaviour
{
    public enum Style
    {
        [InspectorName("아래에서 올라옴 / 반대로 사라짐")] RiseFromBelow,
        [InspectorName("오른쪽에서 들어옴 / 왼쪽으로 사라짐")] SlideRightToLeft,
        [InspectorName("위에서 내려옴 / 아래로 사라짐 (고개를 드는 느낌)")] TiltDown,
    }

    [SerializeField] private Style style = Style.RiseFromBelow;
    [SerializeField, Min(0.01f)] private float seconds = 0.3f;
    [SerializeField, Tooltip("움직이는 거리(캔버스 단위).")] private float distance = 300f;
    [SerializeField, Tooltip("나타날 때 목표 위치를 살짝 지나쳤다가 돌아온다(튀어 오르는 느낌).")] private bool bounce;
    [SerializeField, Tooltip("움직이면서 투명도도 바꾼다. 끄면 위치만 움직인다.")] private bool fade = true;
    [SerializeField, Tooltip("켜질 때 나타나는 모션을 자동으로 재생한다.")] private bool playOnEnable = true;

    [Header("소리(선택): 나타날 때와 사라지기 시작할 때 한 번씩 재생한다. 오브젝트가 꺼져도 끝까지 들린다.")]
    [SerializeField] private AudioClip showSound;
    [SerializeField] private AudioClip hideSound;
    [SerializeField, Range(0f, 1f)] private float openSoundVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float closeSoundVolume = 1f;

    private RectTransform rect;
    private CanvasGroup group;
    private Vector2 applied; // 지금 위치에 더해 둔 움직임(원래 위치로 돌아갈 때 이만큼 뺀다)
    private bool hiding;

    public float Seconds => seconds;

    private void Awake()
    {
        rect = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        hiding = false;
        if (playOnEnable) PlayShow();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        Apply(Vector2.zero, 1f); // 다음에 켜질 때 원래 자리에서 시작하게 되돌려 둔다
        hiding = false;
    }

    /// <summary>나타나는 모션을 처음부터 다시 재생한다(이미 켜져 있어도 다시, 예: 새 손님이 왔을 때).</summary>
    public void PlayShow()
    {
        if (!isActiveAndEnabled) return;
        StopAllCoroutines();
        hiding = false;
        Sfx.Play(showSound, openSoundVolume);
        StartCoroutine(Show());
    }

    /// <summary>사라지는 모션을 재생한 뒤 끈다. 꺼져 있거나 이미 사라지는 중이면 아무것도 하지 않는다. 다 끝나면 done을 부른다.</summary>
    public void Hide(Action done = null)
    {
        if (!isActiveAndEnabled)
        {
            done?.Invoke();
            return;
        }
        if (hiding) return;
        StopAllCoroutines();
        hiding = true;
        Sfx.Play(hideSound, closeSoundVolume);
        StartCoroutine(HideThenOff(done));
    }

    /// <summary>모션이 있으면 모션으로, 없으면 그냥 끈다.</summary>
    public static void Close(GameObject target, Action done = null)
    {
        if (target == null) return;
        if (target.TryGetComponent(out PanelMotion motion)) motion.Hide(done);
        else
        {
            target.SetActive(false);
            done?.Invoke();
        }
    }

    private Vector2 EnterFrom => style switch
    {
        Style.SlideRightToLeft => new Vector2(distance, 0f),
        Style.TiltDown => new Vector2(0f, distance),
        _ => new Vector2(0f, -distance),
    };

    private Vector2 ExitTo => style switch
    {
        Style.SlideRightToLeft => new Vector2(-distance, 0f),
        _ => new Vector2(0f, -distance), // 아래에서 올라옴과 위에서 내려옴 모두 아래로 사라진다
    };

    /// <summary>위에서 내려옴은 들어올 때와 나갈 때 같은 곡선을 써서, 동시에 하면 한 덩어리로 이어져 움직인다.</summary>
    private bool Coupled => style == Style.TiltDown;

    private IEnumerator Show()
    {
        group.alpha = 0f; // 위치는 한 프레임 뒤부터 움직인다: 씬이 열리는 프레임에 옮기면 SwipeParallax가 그 어긋난 위치를 원래 자리로 기억해 버린다
        yield return null;
        Apply(EnterFrom, fade ? 0f : 1f);
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float u = t / seconds;
            float eased = Coupled ? Mathf.SmoothStep(0f, 1f, u) : bounce ? EaseOutBack(u) : EaseOutCubic(u);
            Apply(EnterFrom * (1f - eased), fade ? Mathf.Clamp01(u * 1.5f) : 1f);
            yield return null;
        }
        Apply(Vector2.zero, 1f);
    }

    private IEnumerator HideThenOff(Action done)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float u = t / seconds;
            Apply(ExitTo * (Coupled ? Mathf.SmoothStep(0f, 1f, u) : EaseInCubic(u)), fade ? 1f - u : 1f);
            yield return null;
        }
        done?.Invoke(); // 끄면 이 코루틴이 멈추므로 끄기 전에 부른다(같은 프레임이다)
        gameObject.SetActive(false); // OnDisable이 위치와 투명도를 되돌린다
    }

    private void Apply(Vector2 offset, float alpha)
    {
        rect.anchoredPosition += offset - applied;
        applied = offset;
        group.alpha = alpha;
    }

    private static float EaseOutCubic(float u) => 1f - Mathf.Pow(1f - u, 3f);
    private static float EaseInCubic(float u) => u * u * u;

    private static float EaseOutBack(float u)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(u - 1f, 3f) + c1 * Mathf.Pow(u - 1f, 2f);
    }
}
