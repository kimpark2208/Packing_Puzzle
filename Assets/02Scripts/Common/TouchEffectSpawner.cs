using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 화면을 터치(클릭)할 때마다 미리 만들어둔 터치 이펙트 하나를 그 위치로 옮기고 재생한다.
/// 이펙트는 시작할 때 프리팹에서 poolSize개를 만들어 돌려 쓴다(멀티터치 대응). 가장 오래전에 쓴 것부터 다시 쓴다.
/// </summary>
public class TouchEffectSpawner : MonoBehaviour
{
    [SerializeField] private ParticleSystem touchEffectPrefab;
    [SerializeField] private int poolSize = 5;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float distanceFromCamera = 10f; // 카메라 기준 정면 거리(오소그래픽이라 크기엔 영향 없음)

    private ParticleSystem[] pool;
    private int next;

    private void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;

        pool = new ParticleSystem[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            pool[i] = Instantiate(touchEffectPrefab, transform);
            pool[i].name = touchEffectPrefab.name + "_" + i;
            pool[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // 프리팹의 Play On Awake로 켜진 걸 끈다
        }
    }

    private void Update()
    {
        if (targetCamera == null) return;

        bool touched = false;
        Touchscreen screen = Touchscreen.current;
        if (screen != null)
        {
            foreach (var touch in screen.touches)
            {
                if (!touch.press.wasPressedThisFrame) continue;
                Spawn(touch.position.ReadValue());
                touched = true;
            }
        }

        Mouse mouse = Mouse.current;
        if (!touched && mouse != null && mouse.leftButton.wasPressedThisFrame) Spawn(mouse.position.ReadValue());
    }

    private void Spawn(Vector2 screenPos)
    {
        ParticleSystem effect = pool[next];
        next = (next + 1) % pool.Length;

        effect.transform.position = targetCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, distanceFromCamera));
        effect.Clear(true);
        effect.Play(true);
    }
}
