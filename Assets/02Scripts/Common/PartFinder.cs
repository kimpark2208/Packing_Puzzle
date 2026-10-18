using System.Linq;
using UnityEngine;

/// <summary>꽃송이 상자(Head) 아래에서 이름 규칙(접두사_1 ~ 접두사_6)으로 꽃잎 6장 몫의 컴포넌트를 차례로 모은다.</summary>
public static class PartFinder
{
    public static T[] Children<T>(Transform head, string prefix) where T : Component
        => Enumerable.Range(1, FlowerData.PetalPartCount).Select(i => head.Find($"{prefix}_{i}").GetComponent<T>()).ToArray();
}
