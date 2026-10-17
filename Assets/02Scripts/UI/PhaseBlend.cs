using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 배경처럼 큰 이미지(Image)가 두 그림(예: 낮 → 노을)으로 서서히 바뀔 때, 정해 둔 피벗에서 원/타원 모양으로 번지며 섞이게 한다.
/// 누가 얼마나 섞을지는 밖(MainLook)이 Show로 알려 주고(from, to, 진행도 0~1), 이 컴포넌트는 섞는 동안에만 섞기 머티리얼로 바꿔 끼운다.
/// 스와이프 중에는 SwipeParallax가 BeginBlur/EndBlur로 알려 주므로, 섞는 중이면 "섞기 + 모션 블러" 머티리얼을, 아니면 일반 블러 머티리얼을 쓴다.
/// 두 그림은 같은 크기로 같은 자리를 채우는 스프라이트여야 한다(텍스처의 같은 칸을 쓴다).
/// </summary>
[RequireComponent(typeof(Image))]
public class PhaseBlend : MonoBehaviour
{
    private static readonly int BlendTexId = Shader.PropertyToID("_BlendTex");
    private static readonly int PivotXId = Shader.PropertyToID("_PivotX");
    private static readonly int PivotYId = Shader.PropertyToID("_PivotY");
    private static readonly int ScaleXId = Shader.PropertyToID("_ScaleX");
    private static readonly int ScaleYId = Shader.PropertyToID("_ScaleY");
    private static readonly int Edge0Id = Shader.PropertyToID("_Edge0");
    private static readonly int Edge1Id = Shader.PropertyToID("_Edge1");
    private static readonly int BlurPixelsId = Shader.PropertyToID("_BlurPixels");

    [Header("섞임의 모양: 이미지마다 따로 정한다")]
    [SerializeField, Tooltip("번지기 시작하는 중심. 그림 안의 위치(0~1): (0,0) = 왼쪽 아래, (1,1) = 오른쪽 위.")] private Vector2 pivot = new(0.5f, 0.5f);
    [SerializeField, Tooltip("가로·세로로 퍼지는 비율. (1,1) = 원, (1,0.5) = 가로로 긴 타원.")] private Vector2 ellipse = Vector2.one;
    [SerializeField, Range(0.05f, 1f), Tooltip("경계가 번지는 폭(퍼지는 거리에 대한 비율). 클수록 부드럽다.")] private float softness = 0.4f;

    [Header("머티리얼: UI_PhaseBlend / UI_MotionBlurPhase")]
    [SerializeField] private Material blendMaterial;
    [SerializeField] private Material blurBlendMaterial;

    private Image image;
    private Sprite from, to;
    private float progress;
    private bool blurring;
    private Material sharedBlur;       // 섞지 않을 때 스와이프에서 쓰는 일반 블러 머티리얼(SwipeParallax가 준다)
    private Material blend, blurBlend; // 이 이미지만의 사본
    private Material applied;          // 지금 이미지에 끼워진 머티리얼(null = 기본)

    private void OnDestroy()
    {
        if (blend != null) Destroy(blend);
        if (blurBlend != null) Destroy(blurBlend);
    }

    /// <summary>from 그림에서 to 그림으로 progress(0~1)만큼 섞는다. 0이면 from, 1이면 to만 보인다(섞기 머티리얼도 쓰지 않는다). to가 없으면 from만 보인다.</summary>
    public void Show(Sprite fromSprite, Sprite toSprite, float amount)
    {
        from = fromSprite;
        to = toSprite;
        progress = Mathf.Clamp01(amount);
        Refresh();
    }

    public void BeginBlur(Material shared)
    {
        blurring = true;
        sharedBlur = shared;
        Refresh();
    }

    public void EndBlur()
    {
        blurring = false;
        sharedBlur = null;
        Refresh();
    }

    private void LateUpdate()
    {
        // 블러 길이는 SwipeParallax가 일반 블러 머티리얼에 매 프레임 넣어 두므로, 섞기 + 블러 사본이 그 값을 따라간다
        if (blurring && applied == blurBlend && blurBlend != null && sharedBlur != null) blurBlend.SetFloat(BlurPixelsId, sharedBlur.GetFloat(BlurPixelsId));
    }

    private void Refresh()
    {
        if (image == null) image = GetComponent<Image>();
        if (from == null && to == null) return;

        Sprite start = from != null ? from : to;
        bool mixing = from != null && to != null && from != to && progress > 0.001f && progress < 0.999f;
        Sprite shown = to != null && progress >= 0.999f ? to : start;
        if (image.sprite != shown) image.sprite = shown;

        Material wanted = null;
        if (mixing)
        {
            wanted = blurring ? Ensure(ref blurBlend, blurBlendMaterial) : Ensure(ref blend, blendMaterial);
            if (wanted != null) Configure(wanted, start);
        }
        else if (blurring) wanted = sharedBlur;

        if (applied != wanted)
        {
            applied = wanted;
            image.material = wanted; // null이면 기본 머티리얼
        }
    }

    private static Material Ensure(ref Material instance, Material source)
    {
        if (instance == null && source != null) instance = new Material(source);
        return instance;
    }

    /// <summary>스프라이트(그림 칸)와 이미지 비율로 거리 계산에 필요한 값을 정한다. 피벗에서 가장 먼 모서리까지 퍼지면 진행도 1이다.</summary>
    private void Configure(Material m, Sprite start)
    {
        m.SetTexture(BlendTexId, to.texture);

        Rect r = start.rect;
        Texture2D tex = start.texture;
        Vector2 uvPos = new(r.x / tex.width, r.y / tex.height);
        Vector2 uvSize = new(r.width / tex.width, r.height / tex.height);

        Vector2 size = ((RectTransform)transform).rect.size;
        float aspect = size.x / Mathf.Max(size.y, 1f); // 화면에서 원이 찌그러지지 않게 가로를 비율만큼 늘려 잰다
        Vector2 e = new(Mathf.Max(ellipse.x, 0.01f), Mathf.Max(ellipse.y, 0.01f));

        m.SetFloat(PivotXId, uvPos.x + pivot.x * uvSize.x);
        m.SetFloat(PivotYId, uvPos.y + pivot.y * uvSize.y);
        m.SetFloat(ScaleXId, aspect / (uvSize.x * e.x));
        m.SetFloat(ScaleYId, 1f / (uvSize.y * e.y));

        float reach = 0f;
        foreach (Vector2 corner in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
        {
            Vector2 d = corner - pivot;
            reach = Mathf.Max(reach, Mathf.Sqrt(Mathf.Pow(d.x * aspect / e.x, 2f) + Mathf.Pow(d.y / e.y, 2f)));
        }
        float edge1 = progress * reach * (1f + softness);
        m.SetFloat(Edge1Id, edge1);
        m.SetFloat(Edge0Id, edge1 - softness * reach);
    }
}
