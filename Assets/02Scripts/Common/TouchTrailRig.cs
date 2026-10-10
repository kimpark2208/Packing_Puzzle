using UnityEngine;

/// <summary>
/// 누르고 움직일 때 나오는 이펙트 한 세트(꼬리 선 + 입자 시스템들). TouchTrailInput이 손가락(포인터) 하나당 하나씩 빌려 쓴다.
/// 드래그를 시작할 때마다 팔레트(6색)에서 색 하나를 랜덤으로 골라 꼬리와 입자 모두 그 색으로 낸다.
/// 알파(페이드)는 각 파티클 시스템의 Color over Lifetime / 꼬리의 알파 그라데이션을 그대로 쓴다.
/// </summary>
public class TouchTrailRig : MonoBehaviour
{
    [SerializeField] private TrailRenderer tail;
    [SerializeField] private ParticleSystem[] emitters;
    [SerializeField] private float tailSeconds = 1f; // 손을 뗀 뒤 남은 입자/꼬리가 모두 사라질 때까지 이 세트를 다시 빌려 주지 않는다

    private Color[] palette;
    private bool inUse;
    private float freeAt;

    public bool CanUse => !inUse && Time.time >= freeAt;

    /// <summary>한 번만 호출: 방출을 끄고 팔레트를 받는다. 입자 시스템의 Simulation Space는 프리팹에서 World여야 꼬리가 남는다.</summary>
    public void Initialize(Color[] colors)
    {
        palette = colors;

        foreach (ParticleSystem ps in emitters)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var emission = ps.emission;
            emission.enabled = false;
        }

        tail.emitting = false;
        tail.Clear();
    }

    public void Begin(Vector3 worldPos)
    {
        inUse = true;
        transform.position = worldPos;

        Color head = palette[Random.Range(0, palette.Length)];
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(head, 0f), new GradientColorKey(head, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
        tail.colorGradient = g;
        tail.Clear();
        tail.emitting = true;

        foreach (ParticleSystem ps in emitters)
        {
            ps.Clear(true);
            var main = ps.main;
            main.startColor = head; // 입자도 꼬리와 같은 색 (알파 1, 페이드는 Color over Lifetime이 맡는다)
            var emission = ps.emission;
            emission.enabled = true;
            ps.Play();
        }
    }

    public void MoveTo(Vector3 worldPos)
    {
        transform.position = worldPos;
    }

    public void End()
    {
        inUse = false;
        freeAt = Time.time + tailSeconds;
        tail.emitting = false;
        foreach (ParticleSystem ps in emitters)
        {
            var emission = ps.emission;
            emission.enabled = false; // 이미 나온 입자는 수명이 끝날 때까지 남는다
        }
    }
}
