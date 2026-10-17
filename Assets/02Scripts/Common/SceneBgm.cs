using UnityEngine;

/// <summary>이 씬이 열리면 정해 둔 배경음악을 튼다(Bgm). 이미 같은 음악이 나오고 있으면 이어서 나온다.</summary>
public class SceneBgm : MonoBehaviour
{
    [SerializeField] private AudioClip clip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.4f;

    private void Start()
    {
        Bgm.Play(clip, volume);
    }
}
