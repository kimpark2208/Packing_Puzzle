using System.Linq;
using UnityEngine;

/// <summary>
/// GameManager의 진행: 시작 꽃 지급과 포장지 해금(게임 개편 2 기획서).
/// 꽃은 시작 꽃(획득 일차 0)만 처음에 지급하고, 나머지는 밤 상점에서 산다(GameManager.Shop).
/// 포장지 해금은 기획서 조건대로다: 1번은 시작부터, 2번은 라인 꽃을 처음 얻은 다음 날, 3·4번은 폼 꽃을 처음 얻은 다음 날, 5번은 4번을 해금한 지 20일 뒤.
/// (용품 상인이 찾아와 해금해 주는 연출은 아직 없고, 날이 바뀔 때 조건을 보고 바로 해금한다.)
/// </summary>
public partial class GameManager
{
    [Header("시작 지급")]
    [Tooltip("시작 꽃(획득 일차 0인 꽃)을 처음에 이만큼씩 준다. 0이면 자동으로 주지 않고 상점에서 사야 한다")]
    [Min(0)] [SerializeField] private int startingFlowerStock = 20;

    private const int StartingDecorationStock = 5; // 임시: 꾸미기 용품 전부를 처음에 이만큼씩 준다
    private const int FinalWrapperDelayDays = 20; // 포장지 4번을 해금한 지 이만큼 지나면 5번을 해금한다

    private int finalWrapperDay = -1; // 포장지 5번을 해금할 일차(4번을 해금하면 정해진다. 아직이면 -1)

    /// <summary>게임을 시작할 때 한 번: 시작 꽃(획득 일차 0)을 얻은 꽃으로 친다(주문과 상점 진열대에 나온다). startingFlowerStock(인스펙터)이 있으면 그만큼 지급한다.</summary>
    private void GrantStartingFlowers()
    {
        CurrencyManager wallet = CurrencyManager.Instance;
        DataRegistry registry = DataRegistry.Instance;
        if (wallet == null || registry == null) return;

        foreach (FlowerData flower in registry.Flowers.Where(f => f != null && f.acquireDay <= 0))
        {
            wallet.ObtainFlower(flower.flowerID);
            if (startingFlowerStock <= 0) continue;

            wallet.AddFlowerStock(flower.flowerID, startingFlowerStock);
            NoticeToast.Show($"새 꽃 {flower.DisplayName} {NoticeToast.Highlight($"X{startingFlowerStock}")}"); // 알림은 메인 화면의 토스트가 열리면 차례로 뜬다
        }
    }

    /// <summary>게임을 시작할 때 한 번: 지금 있는 꾸미기 용품 전부를 기본으로 지급한다(손님은 얻은 용품만 주문한다).</summary>
    private void GrantStartingDecorations()
    {
        CurrencyManager wallet = CurrencyManager.Instance;
        DataRegistry registry = DataRegistry.Instance;
        if (wallet == null || registry == null) return;

        foreach (DecorationData decoration in registry.Decorations.Where(d => d != null)) wallet.AddDecorationStock(decoration.id, StartingDecorationStock);
    }

    /// <summary>day일차를 시작할 때: 해금 조건이 된 포장지를 해금한다.</summary>
    private void ApplyDailyProgression(int day)
    {
        CurrencyManager wallet = CurrencyManager.Instance;
        DataRegistry registry = DataRegistry.Instance;
        if (wallet == null || registry == null) return;

        if (HasObtainedRole(wallet, registry, FlowerData.FlowerRole.Line)) UnlockWrapperIfSupported(2);
        if (HasObtainedRole(wallet, registry, FlowerData.FlowerRole.Form))
        {
            UnlockWrapperIfSupported(3);
            if (UnlockWrapperIfSupported(4)) finalWrapperDay = day + FinalWrapperDelayDays;
        }
        if (finalWrapperDay >= 0 && day >= finalWrapperDay) UnlockWrapperIfSupported(5);
    }

    /// <summary>포장지를 해금한다. 단, 퍼즐 보드가 아직 지원하지 않는 링 수의 포장지(4링: DayPuzzleController.SupportedRings)는 보드가 준비될 때까지 해금하지 않는다. 새로 해금했으면 true.</summary>
    private static bool UnlockWrapperIfSupported(int wrapperId)
    {
        WrapperData wrapper = DataRegistry.Instance.GetWrapper(wrapperId);
        if (wrapper == null || wrapper.ringCount > DayPuzzleController.SupportedRings || CurrencyManager.Instance.HasWrapper(wrapperId)) return false;

        CurrencyManager.Instance.UnlockWrapper(wrapperId);
        return true;
    }

    /// <summary>그 역할의 꽃을 하나라도 획득했는가.</summary>
    private static bool HasObtainedRole(CurrencyManager wallet, DataRegistry registry, FlowerData.FlowerRole role)
    {
        return registry.Flowers.Any(f => f != null && f.flowerRole == role && wallet.HasObtainedFlower(f.flowerID));
    }
}
