using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 한 씬 안에서 화면(페이지)들을 좌우 스와이프로 넘나든다. 페이지는 오른쪽으로 한 화면 폭씩 이어 놓고(ParallaxPage), 왼쪽으로 밀면 다음(오른쪽) 페이지,
/// 오른쪽으로 밀면 이전 페이지가 나온다. 오브젝트마다 ParallaxLayer로 깊이(원경/중경/근경)만 고르면 시차가 생긴다(값은 ParallaxSettings).
/// 글로벌 UI는 별도 캔버스라 밀리지 않는다.
/// - 이 컴포넌트는 Canvas에 붙는다: 화면의 그림(Image) 위에서 시작한 드래그는 계층을 타고 올라와 여기서 받는다(버튼 클릭은 그대로 된다).
/// - 에디터에서는 모든 페이지가 오른쪽으로 이어져 보이도록 놓아 두고(첫 화면 옆에 다음 페이지), 실행하면 이 컴포넌트가 움직인다.
/// - 켜고 끄는 것(Enabled)은 이 화면을 쓰는 쪽이 정한다(예: 낮 종료 메인은 DayMainUI).
/// </summary>
public class SwipeParallax : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private struct Entry
    {
        public RectTransform rect;
        public float baseX;       // 에디터에 놓아 둔 자리(페이지는 오른쪽으로 한 화면 폭씩 떨어져 있다)
        public int page;
        public float factor;
        public bool hasParent;    // 다른 레이어 안에 겹쳐 있으면 부모와의 차이만큼만 더 움직인다
        public float parentFactor;
    }

    [SerializeField] private ParallaxSettings settings;
    [SerializeField, Range(0.05f, 0.5f)] private float commitRatio = 0.2f; // 이만큼 이상 밀면 손을 떼도 넘어간다
    [SerializeField] private float flickSpeed = 30f;                        // 이보다 빨리 쓸면(캔버스 단위/프레임) 조금만 밀어도 넘어간다
    [SerializeField] private float snapSeconds = 0.35f;

    private readonly List<Entry> entries = new();
    private Canvas rootCanvas;
    private int pageCount = 1;
    private float view;       // 지금 보고 있는 위치: 0 = 첫 화면, 1 = 1번 페이지 ...
    private float startView;
    private float lastDeltaX;
    private bool dragging;
    private bool animating;

    /// <summary>스와이프를 받을 수 있는가.</summary>
    public bool Enabled { get; set; }

    /// <summary>첫 화면에 완전히 와 있는가(움직이는 중이 아니다).</summary>
    public bool IsAtHome => !animating && !dragging && view <= 0.001f;

    /// <summary>이 페이지보다 앞(왼쪽)으로는 돌아갈 수 없다. 한 번 넘어가면 되돌아오지 못하는 흐름에서 올린다(기본 0).</summary>
    public int LowestPage { get; set; }

    /// <summary>스와이프가 끝나 한 페이지에 도착했을 때.</summary>
    public event System.Action<int> Arrived;

    private float CanvasWidth => ((RectTransform)rootCanvas.transform).rect.width;

    private void Awake()
    {
        rootCanvas = GetComponent<Canvas>().rootCanvas;
        if (settings == null)
        {
            Debug.LogWarning("[SwipeParallax] ParallaxSettings가 연결되지 않아 스와이프를 쓸 수 없습니다.", this);
            return;
        }
        CollectLayers();
        Apply(0f);
    }

    private void OnDisable()
    {
        dragging = animating = false;
    }

    /// <summary>자식(꺼진 것 포함)의 ParallaxLayer를 모아, 페이지와 깊이 값을 정한다.</summary>
    private void CollectLayers()
    {
        foreach (ParallaxLayer layer in GetComponentsInChildren<ParallaxLayer>(true))
        {
            ParallaxPage pageRoot = layer.GetComponentInParent<ParallaxPage>(true);
            int page = pageRoot != null ? pageRoot.Index : 0;
            ParallaxLayer parent = layer.transform.parent != null ? layer.transform.parent.GetComponentInParent<ParallaxLayer>(true) : null;

            var rect = (RectTransform)layer.transform;
            entries.Add(new Entry
            {
                rect = rect,
                baseX = rect.anchoredPosition.x,
                page = page,
                factor = settings.Factor(layer.Depth, page),
                hasParent = parent != null,
                parentFactor = parent != null ? settings.Factor(parent.Depth, page) : 0f,
            });
            pageCount = Mathf.Max(pageCount, page + 1);
        }
    }

#if UNITY_EDITOR
    private void Update()
    {
        // 에디터에서 마우스 없이도 확인할 수 있게: D = 다음 페이지, A = 이전 페이지.
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !Enabled || animating || dragging) return;

        int page = Mathf.RoundToInt(view);
        if (keyboard.dKey.wasPressedThisFrame && page < pageCount - 1) StartCoroutine(Snap(page + 1));
        else if (keyboard.aKey.wasPressedThisFrame && page > LowestPage) StartCoroutine(Snap(page - 1));
    }
#endif

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragging = Enabled && !animating && pageCount > 1; // 이 화면에는 세로로 밀 것이 없으니 방향은 따지지 않고 가로 움직임만 쓴다
        startView = view;
        lastDeltaX = 0f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging) return;

        lastDeltaX = eventData.delta.x / rootCanvas.scaleFactor;
        view = Mathf.Clamp(view - lastDeltaX / CanvasWidth, LowestPage, pageCount - 1);
        Apply(view);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging) return;
        dragging = false;

        int from = Mathf.RoundToInt(startView);
        float moved = view - startView;
        int target = from;
        if (moved >= commitRatio || lastDeltaX <= -flickSpeed) target = from + 1;
        else if (moved <= -commitRatio || lastDeltaX >= flickSpeed) target = from - 1;
        StartCoroutine(Snap(Mathf.Clamp(target, LowestPage, pageCount - 1)));
    }

    /// <summary>코드로 한 페이지로 넘어간다(자동 이동). 손으로 밀 수 있는지(Enabled)와 상관없이 움직이고, 이미 움직이는 중이면 무시한다.</summary>
    public void MoveTo(int page)
    {
        if (animating || dragging || pageCount <= 1) return;
        StartCoroutine(Snap(Mathf.Clamp(page, LowestPage, pageCount - 1)));
    }

    private IEnumerator Snap(int targetPage)
    {
        animating = true;
        float from = view;
        float duration = Mathf.Max(0.01f, snapSeconds * Mathf.Abs(targetPage - from));
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            view = Mathf.Lerp(from, targetPage, Mathf.SmoothStep(0f, 1f, t / duration));
            Apply(view);
            yield return null;
        }
        view = targetPage;
        Apply(view);
        animating = false;
        Arrived?.Invoke(targetPage);
    }

    /// <summary>카메라 위치(페이지 단위)에 맞춰 레이어를 옮긴다. 레이어는 factor만큼 카메라를 따라 움직이고, 페이지 k의 레이어는 자기 페이지에 도착했을 때(카메라 = k) 에디터에 놓은 자리에 온다.</summary>
    private void Apply(float viewPosition)
    {
        float width = CanvasWidth;
        foreach (Entry entry in entries)
        {
            float offset = entry.hasParent
                ? (entry.page - viewPosition) * width * (entry.factor - entry.parentFactor)                 // 부모 레이어 안: 부모와의 차이만큼
                : -viewPosition * width * entry.factor - entry.page * width * (1f - entry.factor);          // 최상위: 페이지 자리에서 factor만큼 카메라를 따라간다

            Vector2 position = entry.rect.anchoredPosition;
            position.x = entry.baseX + offset;
            entry.rect.anchoredPosition = position;
        }
    }
}
