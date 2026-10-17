using UnityEngine;

/// <summary>
/// 터치 이펙트 프리팹에 붙는 소리. 화면을 터치(클릭)할 때 TouchEffectSpawner가 부르면 목록 중 하나를 무작위로 재생한다.
/// </summary>
public class TouchSound : MonoBehaviour
{
    [SerializeField] private AudioClip[] clips;
    [SerializeField, Range(0f, 1f)] private float volume = 0.5f;

    public void Play() => Sfx.PlayRandom(clips, volume);
}
