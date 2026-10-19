using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 게임 재화 및 상태 관리 (싱글톤)
/// 현재 금액, 일수, 부적, 보유 포장지, 획득한 꽃과 꽃 재고, 오늘의 주문 요구사항을 추적한다.
/// 에디터 전용 디버그 메뉴는 CurrencyManager.Debug.cs에 있다.
/// </summary>
public partial class CurrencyManager : Singleton<CurrencyManager>
{
    [SerializeField] private int initialMoney = 0;

    [SerializeField] private int initialCharms = 20; // 시작 부적 수량(저장 시스템이 생기기 전까지는 매번 이 값으로 시작한다)
    [SerializeField] private int maxCharms = 30;     // 부적 보유 한도

    // ========== 기본 상태 ==========
    private int currentMoney;
    private int currentDay;

    // ========== 포장지 (Wrapper) ==========
    private HashSet<int> ownedWrappers = new();
    private readonly Dictionary<int, int> wrapperStock = new(); // 포장지별 재고(소모품)

    // ========== 꽃 (Flower) ==========
    private HashSet<int> obtainedFlowers = new();           // 획득한 꽃 ID
    private Dictionary<int, int> flowerStock = new();       // ID → 보유 수량(해금 여부와는 별개)

    // ========== 고객 요구사항 ==========
    private OrderComposer.CustomerRequirement currentRequirement;

    // ========== 프로퍼티 (읽기 전용) ==========
    public int CurrentMoney => currentMoney;
    public int CurrentDay => currentDay;
    public int Charms { get; private set; }
    public int MaxCharms => maxCharms;
    public int TodayEarned { get; private set; } // 오늘 번 돈 합계(날짜가 넘어가면 0으로 돌아간다)
    public int TodaySpent { get; private set; }  // 오늘 가구·유물·꾸미기 용품 등을 주문하며 쓴 돈 합계(날짜가 넘어가면 0으로 돌아간다)

    private readonly List<int> todayScores = new(); // 오늘 끝낸 퍼즐들의 점수(하루 평점의 재료)

    /// <summary>오늘 끝낸 퍼즐 점수의 평균(퍼즐을 하나도 안 했으면 0).</summary>
    public float TodayAverageScore => todayScores.Count > 0 ? (float)todayScores.Average() : 0f;

    /// <summary>퍼즐 하나를 끝냈을 때 그 점수를 기록한다.</summary>
    public void RecordRoundScore(int score)
    {
        todayScores.Add(score);
    }
    public IReadOnlyCollection<int> OwnedWrappers => ownedWrappers;

    /// <summary>지금 쓸 수 있는 포장지: 재고가 있는 것. 재고가 있는 포장지가 하나도 없으면 막히지 않도록 보유한 포장지 전부(포장지를 사는 곳이 생기기 전까지의 임시 대비).</summary>
    public IReadOnlyCollection<int> UsableWrappers
    {
        get
        {
            List<int> inStock = ownedWrappers.Where(id => GetWrapperStock(id) > 0).ToList();
            return inStock.Count > 0 ? inStock : ownedWrappers.ToList();
        }
    }
    public OrderComposer.CustomerRequirement CurrentRequirement => currentRequirement;

    /// <summary>DayPuzzleGenerator가 생성한 오늘의 요구사항을 단일 소스로 등록한다(표시용과 판정용이 어긋나지 않도록).</summary>
    public void SetCurrentRequirement(OrderComposer.CustomerRequirement requirement)
    {
        currentRequirement = requirement;
    }

    protected override void OnAwake()
    {
        Initialize();
    }

    private void Initialize()
    {
        currentMoney = initialMoney;
        currentDay = 1;
        Charms = Mathf.Min(initialCharms, maxCharms);

        // 초기 포장지: 기획서(포장지 프리셋 PDF)의 프리셋이 들어 있는 1번(전의 2번과 번호를 맞바꿨다)만 보유한 것으로 친다.
        // 3번(3링) 프리셋은 보드가 아직 지원하지 않는다(DayPuzzleController.SupportedRings 참고).
        ownedWrappers.Add(1);

        // 꽃은 하나도 갖고 시작하지 않는다: 밤 손님을 고쳐 받는 복제품(AddFlowerStock)으로만 얻고, 얻은 꽃만 주문에서 부탁받을 수 있다.

        Debug.Log($"[CurrencyManager] 초기화 완료 - 금액: {currentMoney}, 일수: {currentDay}");
    }

    // ========== 금액 관리 ==========

    /// <summary>
    /// 금액 추가
    /// </summary>
    public void AddMoney(int amount)
    {
        currentMoney = Mathf.Max(0, currentMoney + amount);
        if (amount > 0) TodayEarned += amount;
        EventBus.RaiseMoneyChanged(currentMoney);
        Debug.Log($"[CurrencyManager] 금액 변경: +{amount} → {currentMoney}");
    }

    /// <summary>
    /// 금액 소비
    /// </summary>
    public bool TrySpendMoney(int amount)
    {
        if (currentMoney >= amount)
        {
            currentMoney -= amount;
            TodaySpent += amount;
            EventBus.RaiseMoneyChanged(currentMoney);
            Debug.Log($"[CurrencyManager] 금액 소비: -{amount} → {currentMoney}");
            return true;
        }

        Debug.LogWarning($"[CurrencyManager] 금액 부족 (필요: {amount}, 보유: {currentMoney})");
        return false;
    }

    // ========== 부적 관리 ==========

    /// <summary>부적 한 개를 쓴다(꽃에 붙인다). 보유한 부적이 없으면 false.</summary>
    public bool TryUseCharm()
    {
        if (Charms <= 0) return false;
        Charms--;
        EventBus.RaiseCharmsChanged(Charms);
        return true;
    }

    /// <summary>부적을 amount개 받는다(보유 한도까지).</summary>
    public void AddCharms(int amount)
    {
        Charms = Mathf.Min(maxCharms, Charms + amount);
        EventBus.RaiseCharmsChanged(Charms);
    }

    /// <summary>붙였던 부적을 되돌려 받는다(보유 한도까지).</summary>
    public void RefundCharm()
    {
        Charms = Mathf.Min(maxCharms, Charms + 1);
        EventBus.RaiseCharmsChanged(Charms);
    }

    // ========== 포장지 (Wrapper) 관리 ==========

    /// <summary>
    /// 포장지 언락
    /// </summary>
    public void UnlockWrapper(int wrapperId)
    {
        if (!ownedWrappers.Contains(wrapperId))
        {
            ownedWrappers.Add(wrapperId);
            Debug.Log($"[CurrencyManager] 포장지 {wrapperId} 언락");
        }
    }

    /// <summary>포장지(소모품)의 재고를 amount개 늘린다(줄일 때는 음수). 재고가 0 아래로 내려가지 않는다. 받은 포장지는 쓸 수 있게 언락된다.</summary>
    public void AddWrapperStock(int wrapperId, int amount)
    {
        wrapperStock[wrapperId] = Mathf.Max(0, GetWrapperStock(wrapperId) + amount);
        UnlockWrapper(wrapperId);
        Debug.Log($"[CurrencyManager] 포장지 {wrapperId} 재고 {amount:+#;-#}개 → {wrapperStock[wrapperId]}개");
    }

    /// <summary>포장지의 재고 수(받은 적이 없으면 0). 낮 퍼즐을 마치면 쓴 포장지가 하나 줄어든다(GameManager.ConsumeUsedWrapper).</summary>
    public int GetWrapperStock(int wrapperId) => wrapperStock.TryGetValue(wrapperId, out int count) ? count : 0;

    /// <summary>
    /// 포장지 소유 여부 확인
    /// </summary>
    public bool HasWrapper(int wrapperId)
    {
        return ownedWrappers.Contains(wrapperId);
    }

    /// <summary>
    /// 랜덤 포장지 선택 (보유 중인 것 중에서)
    /// </summary>
    public int GetRandomOwnedWrapperID()
    {
        if (ownedWrappers.Count == 0)
        {
            Debug.LogError("[CurrencyManager] 보유한 포장지가 없습니다!");
            return -1;
        }

        var wrapperList = new List<int>(UsableWrappers);
        return wrapperList[Random.Range(0, wrapperList.Count)];
    }

    // ========== 꽃 (Flower) 관리 ==========

    /// <summary>보유 수량(없으면 0).</summary>
    public int GetFlowerStock(int flowerId)
    {
        return flowerStock.TryGetValue(flowerId, out int n) ? n : 0;
    }

    /// <summary>보유 수량을 amount만큼 늘리거나(음수면 줄인다). 꽃 선택에서 보유보다 많이 고르면 마이너스가 될 수 있다. 처음 늘어난 꽃은 획득한 꽃이 된다.</summary>
    public void AddFlowerStock(int flowerId, int amount)
    {
        flowerStock[flowerId] = GetFlowerStock(flowerId) + amount; // 임시: 0 아래(마이너스)도 허용하고 표시만 0으로 한다
        if (amount > 0) ObtainFlower(flowerId);
    }

    /// <summary>이 꽃을 이미 획득했는가.</summary>
    public bool HasObtainedFlower(int flowerId) => obtainedFlowers.Contains(flowerId);

    /// <summary>꽃을 해금한다(이미 해금했으면 아무 일도 없다).</summary>
    public void ObtainFlower(int flowerId)
    {
        if (!obtainedFlowers.Add(flowerId)) return;

        Debug.Log($"[CurrencyManager] 꽃 {flowerId} 획득");
    }

    /// <summary>
    /// 획득 가능한 모든 꽃 ID 반환
    /// </summary>
    public List<int> GetObtainedFlowerIds()
    {
        return obtainedFlowers.ToList();
    }

    // ========== 일수 진행 ==========

    /// <summary>
    /// 다음 날로 진행
    /// </summary>
    public void AdvanceDay()
    {
        currentDay++;
        TodayEarned = 0;
        TodaySpent = 0;
        todayScores.Clear();

        EventBus.RaiseDayAdvanced(currentDay);
        Debug.Log($"[CurrencyManager] 일수 진행: Day {currentDay}");
    }
}
