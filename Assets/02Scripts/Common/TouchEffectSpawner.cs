using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 화면을 터치(클릭)할 때마다 미리 만들어둔 터치 이펙트 하나를 그 위치로 옮기고 재생한다.
/// 이펙트는 "Effect" 레이어에 있고 EffectCamera(오버레이 카메라)로만 렌더링되어 UI 위에 보인다.
/// </summary>
public class TouchEffectSpawner : MonoBehaviour
{
    [SerializeField] private ParticleSystem touchEffect; // 씬에 미리 배치된 단일 이펙트 인스턴스
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float distanceFromCamera = 10f; // 카메라 기준 정면 거리(오소그래픽이라 크기엔 영향 없음)

    private void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
    }

    private void Update()
    {
        if (touchEffect == null || targetCamera == null) return;
        if (Pointer.current == null || !Pointer.current.press.wasPressedThisFrame) return;

        Vector2 screenPos = Pointer.current.position.ReadValue();
        Vector3 worldPos = targetCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, distanceFromCamera));

        touchEffect.transform.position = worldPos;
        touchEffect.Clear(true);
        touchEffect.Play(true);
    }
}
