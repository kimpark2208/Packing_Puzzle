using UnityEngine;

/// <summary>
/// 스와이프 시차(패럴랙스) 값. 오브젝트마다 깊이(원경/중경/근경)만 고르면, 그 깊이의 값이 여기서 정해진다.
/// 값은 "화면 폭 한 번을 밀 때 그 오브젝트가 움직이는 비율"이다. 1이면 화면과 같이, 작을수록 멀리 있는 것처럼 느리게 움직인다.
/// 첫 화면(0번 페이지)의 오브젝트와 그 옆 페이지들의 오브젝트는 따로 정한다: 옆 페이지는 화면 밖에서 들어와 제자리에 도착해야 하므로
/// 1 이상이어야 하고, 클수록 앞에서 더 빨리 들어온다.
/// </summary>
[CreateAssetMenu(fileName = "ParallaxSettings", menuName = "Puzzle/ParallaxSettings", order = 5)]
public class ParallaxSettings : ScriptableObject
{
    [Header("첫 화면(0번 페이지)의 오브젝트: 1 이하")]
    [Range(0f, 1f)] public float homeFar = 0.2f;
    [Range(0f, 1f)] public float homeMid = 0.45f;
    [Range(0f, 1f)] public float homeNear = 0.7f;
    [Range(0f, 1f)] public float homeFront = 1f;

    [Header("옆 페이지(1번 이상)의 오브젝트: 1 이상")]
    [Min(1f)] public float awayFar = 1f;
    [Min(1f)] public float awayMid = 1.1f;
    [Min(1f)] public float awayNear = 1.25f;
    [Min(1f)] public float awayFront = 1.5f;

    [Header("스와이프 모션 블러")]
    [Tooltip("UI_MotionBlur(Shader Graph) 머티리얼. 비우면 블러를 쓰지 않는다.")]
    public Material blurMaterial;
    [Tooltip("셔터가 열려 있는 시간(초). 클수록 길게 번진다.")]
    [Min(0f)] public float blurExposure = 0.012f;
    [Tooltip("번짐 길이의 상한(화면 픽셀).")]
    [Min(0f)] public float maxBlurPixels = 50f;

    /// <summary>깊이와 페이지(0이면 첫 화면)에 해당하는 값.</summary>
    public float Factor(ParallaxDepth depth, int page)
    {
        bool home = page == 0;
        switch (depth)
        {
            case ParallaxDepth.Far: return home ? homeFar : awayFar;
            case ParallaxDepth.Mid: return home ? homeMid : awayMid;
            case ParallaxDepth.Near: return home ? homeNear : awayNear;
            default: return home ? homeFront : awayFront;
        }
    }
}
