using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 모든 WrapperData(포장지별 링 수와 프리셋)를 포장지 번호(wrapperId)로 찾기 위한 목록.
/// 부트스트랩 씬의 매니저 오브젝트에 배치한다. 상점 시스템과는 별개로 번호만 맞춘다.
/// </summary>
public class WrapperRegistry : Singleton<WrapperRegistry>
{
    [SerializeField] private List<WrapperData> allWrappers = new();

    public WrapperData GetById(int wrapperId)
    {
        return allWrappers.FirstOrDefault(w => w != null && w.wrapperId == wrapperId);
    }
}
