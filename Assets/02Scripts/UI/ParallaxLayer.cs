using UnityEngine;

/// <summary>시차의 깊이 4단계: 멀수록(원경) 천천히, 가까울수록(전경) 빨리 움직인다. 실제 값은 ParallaxSettings에서 정한다. 이미 저장된 값이 있으니 순서를 바꾸지 말고 뒤에만 추가할 것.</summary>
public enum ParallaxDepth
{
    [InspectorName("원경 (Far)")] Far,
    [InspectorName("중경 (Mid)")] Mid,
    [InspectorName("근경 (Near)")] Near,
    [InspectorName("전경 (Front)")] Front,
}

/// <summary>
/// 스와이프(SwipeParallax)할 때 시차를 받을 오브젝트에 붙이는 표시. 이 컴포넌트에서는 깊이만 고르면 된다.
/// 어느 페이지에 속하는지는 부모 중 가장 가까운 ParallaxPage가 정하고(없으면 첫 화면), 다른 레이어 안에 겹쳐 둔 레이어는
/// 부모와의 깊이 차이만큼만 더 움직인다. 실제 움직임은 Canvas의 SwipeParallax가 계산한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class ParallaxLayer : MonoBehaviour
{
    [SerializeField] private ParallaxDepth depth = ParallaxDepth.Mid;

    [Header("스와이프 중 가장자리 섞기: 이 레이어의 그림(Image) 끝을 이 폭(캔버스 단위)만큼 서서히 투명하게 풀어 아래/옆 배경과 섞는다. 0이면 또렷하다. 배경 그림에만 쓰고, 그림이 텍스처 전체를 쓰는 스프라이트일 때 정확하다.")]
    [SerializeField, Min(0f)] private float blendLeft;
    [SerializeField, Min(0f)] private float blendRight;

    public ParallaxDepth Depth => depth;
    public float BlendLeft => blendLeft;
    public float BlendRight => blendRight;
}
