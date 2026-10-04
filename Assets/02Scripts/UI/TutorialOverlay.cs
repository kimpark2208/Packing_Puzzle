using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 튜토리얼 안내: 안내 배너(화면 위) + 손가락 + 눌러야 할 곳만 밝게 살리고 나머지는 어둡게 한다.
/// 아무 곳이나 클릭하면 사라지고(단계가 더 있으면 다음 단계로), 같은 id는 한 번만 보여 준다(실행 중에만 기억).
/// 하이라키에 미리 배치된 프리팹(TutorialOverlay)을 쓰고, 평소엔 Content가 꺼져 있다.
/// 안내가 떠 있는 동안은 하루/밤 시간이 멈춘다.
/// </summary>
public class TutorialOverlay : MonoBehaviour
{
    public struct Step
    {
        public RectTransform target; // null이면 어둡게 하지 않고 배너만 보여 준다
        public string text;

        public Step(RectTransform target, string text)
        {
            this.target = target;
            this.text = text;
        }
    }

    private const string EmphasisColor = "#C8501E";
    private const float HandBobPixels = 12f;
    private const float HandBobSpeed = 6f;

    private static readonly HashSet<string> seen = new();

    public static TutorialOverlay Instance { get; private set; }

    /// <summary>안내가 떠 있는 동안 true. 게임 쪽 입력 처리(밤 퍼즐 그리기 등)가 쉬는 데 쓴다.</summary>
    public static bool IsShowing { get; private set; }

    [SerializeField] private RectTransform content; // 켜고 끄는 영역(캔버스 전체)
    [SerializeField] private RectTransform dimTop;
    [SerializeField] private RectTransform dimBottom;
    [SerializeField] private RectTransform dimLeft;
    [SerializeField] private RectTransform dimRight;
    [SerializeField] private RectTransform hand;
    [SerializeField] private TMP_Text bannerText;
    [SerializeField] private float padding = 12f; // 밝게 살린 영역을 대상보다 조금 크게 잡는 여백

    private readonly Queue<Step[]> pending = new();
    private Vector2 handBase;
    private Vector2 handDirection; // 손가락이 가리키는 쪽의 반대 방향(흔들리는 방향)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        seen.Clear();
        IsShowing = false;
        Instance = null;
    }

    private void Awake()
    {
        Instance = this;
        content.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (IsShowing)
        {
            IsShowing = false;
            DayClock.Instance?.SetPaused(false);
        }
    }

    /// <summary>강조할 낱말을 강조색으로 칠한다.</summary>
    public static string Em(string text) => $"<color={EmphasisColor}>{text}</color>";

    /// <summary>같은 id는 한 번만 보여 준다. 단계가 여러 개면 클릭할 때마다 다음 단계로 넘어가고, 이미 다른 안내가 떠 있으면 끝난 뒤에 보여 준다.</summary>
    public static void Play(string id, params Step[] steps)
    {
        if (Instance == null || steps.Length == 0 || !seen.Add(id)) return;

        Instance.pending.Enqueue(steps);
        if (!IsShowing) Instance.StartCoroutine(Instance.Run());
    }

    private IEnumerator Run()
    {
        IsShowing = true;
        DayClock.Instance?.SetPaused(true);

        while (pending.Count > 0)
        {
            foreach (Step step in pending.Dequeue())
            {
                yield return null; // 레이아웃이 자리 잡은 뒤에 위치를 계산한다(이 단계를 띄운 클릭과 겹치지도 않는다)
                Show(step);
                yield return new WaitUntil(() => Input.GetMouseButtonDown(0));
            }
        }

        content.gameObject.SetActive(false);
        IsShowing = false;
        DayClock.Instance?.SetPaused(false);
    }

    private void Show(Step step)
    {
        content.gameObject.SetActive(true);
        bannerText.text = step.text;

        bool hasTarget = step.target != null;
        dimTop.gameObject.SetActive(hasTarget);
        dimBottom.gameObject.SetActive(hasTarget);
        dimLeft.gameObject.SetActive(hasTarget);
        dimRight.gameObject.SetActive(hasTarget);
        hand.gameObject.SetActive(hasTarget);
        if (!hasTarget) return;

        Rect hole = ToLocalRect(step.target);
        hole.xMin -= padding;
        hole.yMin -= padding;
        hole.xMax += padding;
        hole.yMax += padding;

        Rect all = content.rect;
        Place(dimTop, all.xMin, hole.yMax, all.width, all.yMax - hole.yMax);
        Place(dimBottom, all.xMin, all.yMin, all.width, hole.yMin - all.yMin);
        Place(dimLeft, all.xMin, hole.yMin, hole.xMin - all.xMin, hole.height);
        Place(dimRight, hole.xMax, hole.yMin, all.xMax - hole.xMax, hole.height);

        PlaceHand(hole, all);
    }

    /// <summary>손가락은 밝은 영역 아래에서 위를 가리킨다. 아래 공간이 모자라면 위에서 아래를 가리킨다.</summary>
    private void PlaceHand(Rect hole, Rect all)
    {
        bool below = hole.yMin - hand.rect.height >= all.yMin;
        hand.pivot = new Vector2(0.5f, 1f); // 손끝이 기준점이라 180도 돌려도 손끝이 밝은 영역의 가장자리에 닿는다
        hand.localEulerAngles = new Vector3(0f, 0f, below ? 0f : 180f);
        handBase = new Vector2(hole.center.x, below ? hole.yMin : hole.yMax);
        handDirection = below ? Vector2.down : Vector2.up;
        hand.anchoredPosition = handBase;
    }

    private void Update()
    {
        if (!IsShowing || !hand.gameObject.activeInHierarchy) return;
        hand.anchoredPosition = handBase + handDirection * ((Mathf.Sin(Time.unscaledTime * HandBobSpeed) + 1f) * 0.5f * HandBobPixels);
    }

    private static void Place(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height));
    }

    /// <summary>대상 UI의 화면상 사각형을 Content 안의 좌표로 바꾼다(대상이 다른 캔버스에 있어도 된다).</summary>
    private Rect ToLocalRect(RectTransform target)
    {
        Canvas canvas = target.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;

        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(content, min, null, out Vector2 localMin);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(content, max, null, out Vector2 localMax);
        return Rect.MinMaxRect(localMin.x, localMin.y, localMax.x, localMax.y);
    }
}
