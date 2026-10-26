using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 안내 화면: 안내 배너(화면 위) + 손가락 + 눌러야 할 곳만 밝게 살리고 나머지는 어둡게 한다.
/// 배너 오른쪽 아래의 확인 버튼을 눌러야 다음 단계로 넘어가거나 사라진다. 안내가 떠 있는 동안은 하루/기분/밤 시간이 멈춘다.
/// 같은 id는 실행 중 한 번만 보여 준다(저장하지 않는다). 무엇을 언제 보여 줄지는 TutorialLessons/TutorialDirector가 정하고, 이 컴포넌트는 보여 주기만 한다.
/// 글로벌 UI 캔버스에 미리 배치된 프리팹(TutorialOverlay)을 쓴다. 안내가 없을 때는 Content가 꺼져 있다(씬 편집에 방해되지 않게 프리팹에도 꺼 둔 채로 저장한다).
/// </summary>
public class TutorialOverlay : MonoBehaviour
{
    /// <summary>안내 한 장면. targets가 없거나 못 찾으면 어둡게 하지 않고 배너만 보여 준다.</summary>
    public readonly struct Step
    {
        public readonly string text;
        public readonly Func<IEnumerable<RectTransform>> targets; // 보여 주는 순간에 찾는다(레이아웃이 자리 잡은 뒤의 위치를 쓰려고)
        public readonly bool skipIfHidden;                        // 대상이 없거나 화면 밖이면 이 장면을 건너뛴다

        private Step(string text, Func<IEnumerable<RectTransform>> targets, bool skipIfHidden = false)
        {
            this.text = text;
            this.targets = targets;
            this.skipIfHidden = skipIfHidden;
        }

        /// <summary>배너만 보여 준다.</summary>
        public static Step Banner(string text) => new(text, null);

        /// <summary>대상 하나를 밝게 살린다(대상이 없으면 배너만 보여 준다).</summary>
        public static Step On(string text, Func<RectTransform> target) => new(text, () => new[] { target() });

        /// <summary>여러 대상을 모두 감싸는 영역을 밝게 살린다.</summary>
        public static Step On(string text, Func<IEnumerable<RectTransform>> targets) => new(text, targets);

        /// <summary>대상 하나를 밝게 살린다. 대상이 없거나 화면 밖이면 이 장면은 건너뛴다(화면에 따라 있을 수도 없을 수도 있는 것).</summary>
        public static Step OnIfShown(string text, Func<RectTransform> target) => new(text, () => new[] { target() }, true);
    }

    private const string EmphasisColor = "#C8501E";
    private const float ConfirmGapFromBanner = 20f; // 배너와 확인 버튼 사이의 간격
    private const float BannerMargin = 30f;        // 배너와 화면 가장자리 사이의 간격
    private const float ScreenMargin = 10f;        // 확인 버튼이 화면 가장자리에서 떨어지는 간격
    private const float Padding = 12f;             // 밝게 살린 영역을 대상보다 조금 크게 잡는 여백
    private const float HandBobPixels = 12f;
    private const float HandBobSpeed = 6f;

    private static readonly HashSet<string> seen = new();

    public static TutorialOverlay Instance { get; private set; }

    /// <summary>안내가 떠 있는 동안 true. 화면 입력을 직접 읽는 곳(밤 퍼즐 그리기 등)이 쉬는 데 쓴다.</summary>
    public static bool IsShowing { get; private set; }

    [SerializeField] private RectTransform content; // 켜고 끄는 영역(캔버스 전체)
    [SerializeField] private RectTransform dimTop;
    [SerializeField] private RectTransform dimBottom;
    [SerializeField] private RectTransform dimLeft;
    [SerializeField] private RectTransform dimRight;
    [SerializeField] private RectTransform holeBlocker; // 밝은 영역 위에 깔리는 투명 막: 확인을 누르기 전엔 밝은 곳도 눌리지 않게 한다
    [SerializeField] private RectTransform hand;
    [SerializeField] private RectTransform banner;
    [SerializeField] private TMP_Text bannerText;
    [SerializeField] private Button confirmButton;

    private bool confirmed;
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
        confirmButton.onClick.AddListener(() => confirmed = true);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (IsShowing)
        {
            IsShowing = false;
            if (GameClock.Instance != null) GameClock.Instance.TutorialHold = false;
        }
    }

    /// <summary>강조할 낱말을 강조색으로 칠한다.</summary>
    public static string Em(string text) => $"<color={EmphasisColor}>{text}</color>";

    public static bool HasSeen(string id) => seen.Contains(id);

    /// <summary>같은 id는 한 번만 보여 준다. 다른 안내가 떠 있으면 아무것도 하지 않는다(id도 쓴 것으로 치지 않으니 나중에 다시 부르면 된다).</summary>
    public static void Play(string id, params Step[] steps)
    {
        if (Instance == null || steps.Length == 0 || IsShowing || TutorialPopup.IsShowing || !seen.Add(id)) return;

        Instance.StartCoroutine(Instance.Run(steps));
    }

    private IEnumerator Run(Step[] steps)
    {
        IsShowing = true;
        if (GameClock.Instance != null) GameClock.Instance.TutorialHold = true;

        foreach (Step step in steps)
        {
            yield return null; // 레이아웃이 자리 잡은 뒤에 위치를 계산한다
            confirmed = false;
            if (!Show(step)) continue;
            yield return new WaitUntil(() => confirmed);
        }

        content.gameObject.SetActive(false);
        IsShowing = false;
        if (GameClock.Instance != null) GameClock.Instance.TutorialHold = false;
    }

    /// <summary>한 장면을 보여 준다. 건너뛰어야 하는 장면이면 아무것도 바꾸지 않고 false를 돌려준다.</summary>
    private bool Show(Step step)
    {
        content.gameObject.SetActive(true);
        Rect all = content.rect;

        RectTransform[] targets = step.targets != null ? step.targets().Where(t => t != null && t.gameObject.activeInHierarchy).ToArray() : null;
        bool hasTarget = targets != null && targets.Length > 0;

        Rect hole = default;
        if (hasTarget)
        {
            hole = ToLocalRect(targets[0]);
            foreach (RectTransform target in targets.Skip(1))
            {
                Rect other = ToLocalRect(target);
                hole = Rect.MinMaxRect(Mathf.Min(hole.xMin, other.xMin), Mathf.Min(hole.yMin, other.yMin), Mathf.Max(hole.xMax, other.xMax), Mathf.Max(hole.yMax, other.yMax));
            }
        }
        if (step.skipIfHidden && (!hasTarget || !all.Contains(hole.center))) return false;

        bannerText.text = step.text;
        foreach (RectTransform dim in new[] { dimTop, dimBottom, dimLeft, dimRight }) dim.gameObject.SetActive(hasTarget);
        hand.gameObject.SetActive(hasTarget);
        holeBlocker.gameObject.SetActive(true); // 대상이 없으면 화면 전체를 덮어 확인을 누르기 전엔 뒤의 화면이 눌리지 않게 한다

        if (!hasTarget)
        {
            PlaceBanner(true);
            Place(holeBlocker, all.xMin, all.yMin, all.width, all.height);
            PlaceConfirm(null, true);
            return true;
        }

        hole = Rect.MinMaxRect(hole.xMin - Padding, hole.yMin - Padding, hole.xMax + Padding, hole.yMax + Padding);

        Place(dimTop, all.xMin, hole.yMax, all.width, all.yMax - hole.yMax);
        Place(dimBottom, all.xMin, all.yMin, all.width, hole.yMin - all.yMin);
        Place(dimLeft, all.xMin, hole.yMin, hole.xMin - all.xMin, hole.height);
        Place(dimRight, hole.xMax, hole.yMin, all.xMax - hole.xMax, hole.height);
        Place(holeBlocker, hole.xMin, hole.yMin, hole.width, hole.height);

        bool bannerTop = hole.center.y <= 0f; // 대상이 화면 아래쪽이면 배너는 위에, 위쪽이면 아래에 둬서 대상을 가리지 않게 한다
        PlaceBanner(bannerTop);
        PlaceHand(hole, all);
        PlaceConfirm(hole, bannerTop);
        return true;
    }

    private void PlaceBanner(bool top)
    {
        float y = top ? 1f : 0f;
        banner.anchorMin = new Vector2(0.5f, y);
        banner.anchorMax = new Vector2(0.5f, y);
        banner.pivot = new Vector2(0.5f, y);
        banner.anchoredPosition = new Vector2(0f, top ? -BannerMargin : BannerMargin);
    }

    /// <summary>
    /// 확인 버튼은 안내 배너에서 조금 떨어진 곳에 둔다(배너가 위에 있으면 밑에, 아래에 있으면 위에). 배너의 오른쪽 끝에 맞추고,
    /// 밝은 영역을 가리면 왼쪽 끝에 맞춘다. 어느 쪽이든 가리면 오른쪽 끝에 둔다.
    /// </summary>
    private void PlaceConfirm(Rect? hole, bool bannerTop)
    {
        var rt = (RectTransform)confirmButton.transform;
        rt.pivot = new Vector2(1f, bannerTop ? 1f : 0f);

        var corners = new Vector3[4];
        banner.GetWorldCorners(corners); // 0 왼쪽 아래, 1 왼쪽 위, 2 오른쪽 위, 3 오른쪽 아래
        Vector3 right = content.InverseTransformPoint(bannerTop ? corners[3] : corners[2]);
        Vector3 left = content.InverseTransformPoint(bannerTop ? corners[0] : corners[1]);
        float y = bannerTop ? right.y - ConfirmGapFromBanner : right.y + ConfirmGapFromBanner;

        float width = rt.rect.width, height = rt.rect.height;
        float bottom = bannerTop ? y - height : y;
        var pos = new Vector2(right.x, y);
        var rightRect = new Rect(right.x - width, bottom, width, height);
        var leftRect = new Rect(left.x, bottom, width, height);
        if (hole.HasValue && rightRect.Overlaps(hole.Value) && !leftRect.Overlaps(hole.Value)) pos.x = left.x + width;

        // 화면 밖으로 잘리지 않게 안으로 들인다
        Rect all = content.rect;
        pos.x = Mathf.Clamp(pos.x, all.xMin + width + ScreenMargin, all.xMax - ScreenMargin);
        pos.y = bannerTop
            ? Mathf.Clamp(pos.y, all.yMin + height + ScreenMargin, all.yMax - ScreenMargin)
            : Mathf.Clamp(pos.y, all.yMin + ScreenMargin, all.yMax - height - ScreenMargin);
        rt.anchoredPosition = pos;
    }

    /// <summary>
    /// 손가락은 밝은 영역 아래에서 위를 가리키거나, 위에서 아래를 가리킨다. 화면 안에 들어가고 배너에 가리지 않는 쪽을 고르고,
    /// 둘 다 안 되면 밝은 영역 안쪽에서 가운데를 가리킨다.
    /// </summary>
    private void PlaceHand(Rect hole, Rect all)
    {
        var corners = new Vector3[4];
        banner.GetWorldCorners(corners);
        Vector3 bannerMin = content.InverseTransformPoint(corners[0]);
        Vector3 bannerMax = content.InverseTransformPoint(corners[2]);
        var bannerRect = Rect.MinMaxRect(bannerMin.x, bannerMin.y, bannerMax.x, bannerMax.y);

        float w = hand.rect.width, h = hand.rect.height;
        float x = Mathf.Clamp(hole.center.x, all.xMin + w * 0.5f, all.xMax - w * 0.5f);
        var belowRect = new Rect(x - w * 0.5f, hole.yMin - h, w, h);
        var aboveRect = new Rect(x - w * 0.5f, hole.yMax, w, h);

        bool below;
        float tipY;
        if (Fits(belowRect, all, bannerRect)) { below = true; tipY = hole.yMin; }
        else if (Fits(aboveRect, all, bannerRect)) { below = false; tipY = hole.yMax; }
        else { below = true; tipY = hole.center.y; }

        hand.pivot = new Vector2(0.5f, 1f); // 손끝이 기준점이라 180도 돌려도 손끝이 밝은 영역의 가장자리에 닿는다
        hand.localEulerAngles = new Vector3(0f, 0f, below ? 0f : 180f);
        handBase = new Vector2(x, tipY);
        handDirection = below ? Vector2.down : Vector2.up;
        hand.anchoredPosition = handBase;
    }

    private static bool Fits(Rect rect, Rect all, Rect avoid)
    {
        return rect.xMin >= all.xMin && rect.xMax <= all.xMax && rect.yMin >= all.yMin && rect.yMax <= all.yMax && !rect.Overlaps(avoid);
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
        Camera contentCam = content.GetComponentInParent<Canvas>().rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? content.GetComponentInParent<Canvas>().rootCanvas.worldCamera : null;

        // 대상이 기울어 있거나 뒤집혀 있어도(꽃다발이 기운다) 네 모서리를 모두 감싸는 사각형을 쓴다
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
        foreach (Vector3 corner in corners)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, corner);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screen, contentCam, out Vector2 local);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
