using System.Collections.Generic;
using UnityEngine.SceneManagement;

/// <summary>
/// 로딩이 긴 씬으로 갈 때 로딩 씬을 거쳐 간다. Load로 목적지와(있다면) 먼저 해 둘 작업을 정해 두고 로딩 씬(LoadingScreen)을 열면,
/// 로딩 씬이 작업을 돌린 뒤 목적지를 비동기로 불러온다.
/// 작업은 한 번에 몰아서 하면 화면이 얼어붙으니 조각으로 나눈 IEnumerator&lt;float&gt;로 넘긴다: MoveNext 한 번이 한 조각이고,
/// 조각을 마칠 때마다 지금까지의 진행률(0~1)을 yield 한다. 조각 사이에 화면(막대와 문구)이 갱신된다.
/// </summary>
public static class SceneLoader
{
    public const string LoadingScene = "00Loading";

    /// <summary>로딩이 끝나면 열 씬. 작업 도중에 바꿔도 된다(작업 결과에 따라 목적지가 달라질 때).</summary>
    public static string Target { get; set; }

    private static IEnumerator<float> work;

    public static void Load(string target, IEnumerator<float> loadingWork = null)
    {
        Target = target;
        work = loadingWork;
        SceneManager.LoadScene(LoadingScene);
    }

    /// <summary>로딩 씬이 먼저 돌릴 작업을 한 번 가져간다(없으면 null).</summary>
    public static IEnumerator<float> TakeWork()
    {
        IEnumerator<float> taken = work;
        work = null;
        return taken;
    }
}
