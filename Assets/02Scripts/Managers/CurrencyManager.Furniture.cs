using System.Collections.Generic;

/// <summary>
/// CurrencyManager의 가구 부분: 카탈로그에서 주문한 가구는 다음 날 아침에 설치된다(상인이 출근 전에 설치해 두고 간다).
/// 설치된 가구는 설치된 순서대로 쌓이고, 같은 자리(FurnitureData.sceneImageName)에는 나중에 설치된 것이 쓰인다. 저장 시스템이 생기기 전까지는 게임을 켠 동안만 유지된다.
/// </summary>
public partial class CurrencyManager
{
    private readonly List<int> pendingFurniture = new();   // 주문했고 아직 설치 전인 가구 번호
    private readonly List<int> installedFurniture = new(); // 설치된 가구 번호(설치된 순서)

    /// <summary>설치된 가구 번호(설치된 순서).</summary>
    public IReadOnlyList<int> InstalledFurniture => installedFurniture;

    /// <summary>가구를 주문했다: 다음 날 아침에 설치된다. 이미 주문했거나 설치된 가구는 무시한다.</summary>
    public void OrderFurniture(int furnitureId)
    {
        if (IsFurnitureOrdered(furnitureId)) return;

        pendingFurniture.Add(furnitureId);
    }

    /// <summary>주문했거나(설치 전) 이미 설치된 가구인가(카탈로그에서 다시 주문할 수 없다).</summary>
    public bool IsFurnitureOrdered(int furnitureId) => pendingFurniture.Contains(furnitureId) || installedFurniture.Contains(furnitureId);

    /// <summary>하루가 지났다: 주문해 둔 가구가 모두 설치된다.</summary>
    private void InstallPendingFurniture()
    {
        installedFurniture.AddRange(pendingFurniture);
        pendingFurniture.Clear();
    }
}
