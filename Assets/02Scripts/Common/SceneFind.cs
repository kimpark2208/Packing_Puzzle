using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 안의 오브젝트를 이름으로 찾는다(꺼져 있는 오브젝트도 찾는다). 인스펙터 참조는 UI를 갈아엎을 때 끊어지기 쉬워서,
/// 화면 스크립트가 필요한 오브젝트를 이름으로 받아올 때 쓴다. 같은 이름이 여럿이면 모두 돌려주고, 하나도 없으면 경고를 남긴다.
/// </summary>
public static class SceneFind
{
    /// <summary>이름이 name인 오브젝트들의 T 컴포넌트(T가 없는 오브젝트는 건너뛴다). 하나도 없으면 경고하고 빈 목록을 돌려준다.</summary>
    public static List<T> All<T>(Scene scene, string name) where T : Component
    {
        var found = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (T component in root.GetComponentsInChildren<T>(true))
            {
                if (component.name == name) found.Add(component);
            }
        }
        if (found.Count == 0) Debug.LogWarning($"[SceneFind] {scene.name} 씬에서 '{name}' 오브젝트({typeof(T).Name})를 찾지 못했습니다.");
        return found;
    }

    /// <summary>이름이 name인 첫 오브젝트의 T 컴포넌트. 없으면 경고하고 null.</summary>
    public static T One<T>(Scene scene, string name) where T : Component
    {
        List<T> found = All<T>(scene, name);
        return found.Count > 0 ? found[0] : null;
    }
}
