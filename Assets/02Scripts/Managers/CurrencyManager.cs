using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 재화 및 상태 관리 (싱글톤)
/// 현재 금액, 일수, 보유 포장지, 획득한 꽃, 요청한 꽃 등을 추적
/// </summary>
public class CurrencyManager : Singleton<CurrencyManager>
{
    [SerializeField] private int initialMoney = 0;
    [SerializeField] private int initialFlowerStock = 5; // 초기 꽃 4종의 시작 보유 수량

    // ========== 기본 상태 ==========
    private int currentMoney;
    private int currentDay;

    // ========== 포장지 (Wrapper) ==========
    private HashSet<int> ownedWrappers = new();

    // ========== 꽃 (Flower) ==========
    private Dictionary<int, bool> obtainedFlowers = new();  // ID → 획득 여부
    private Dictionary<int, int> flowerStock = new();       // ID → 보유 수량(해금 여부와는 별개)

    // ========== 고객 요구사항 ==========
    private CustomerRequirementGenerator.CustomerRequirement currentRequirement;

    // ========== 프로퍼티 (읽기 전용) ==========
    public int CurrentMoney => currentMoney;
    public int CurrentDay => currentDay;
    public int TodayEarned { get; private set; } // 오늘 번 돈 합계(날짜가 넘어가면 0으로 돌아간다)
    public IReadOnlyCollection<int> OwnedWrappers => ownedWrappers;
    public CustomerRequirementGenerator.CustomerRequirement CurrentRequirement => currentRequirement;

    /// <summary>DayPuzzleGenerator가 생성한 오늘의 요구사항을 단일 소스로 등록한다(표시용과 판정용이 어긋나지 않도록).</summary>
    public void SetCurrentRequirement(CustomerRequirementGenerator.CustomerRequirement requirement)
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

        // 초기 포장지: 기획서(포장지 프리셋 PDF)의 프리셋이 들어 있는 2번만 보유한 것으로 친다.
        // 3번(3링) 프리셋은 보드가 아직 지원하지 않는다(WrapperBoardController.SupportedRings 참고).
        ownedWrappers.Add(2);

        // 초기 꽃: 역할(속성)별로 하나씩. 장미-도미노(1, 매스), 튤립-I트로미노(3, 필러), 해바라기-L트로미노(4, 폼), 프리지아-S테트로미노(8, 라인)
        // (BlockRegistry의 1단계 포장지 꽃 ID와 일치해야 함)
        // 8번은 좌우 반전이 실제로 다르게 보이는(거울상) 도형이라, 처음부터 회전/반전 조작을 눈으로 확인할 수 있다.
        foreach (int id in new[] { 1, 3, 4, 8 })
        {
            obtainedFlowers[id] = true;
            flowerStock[id] = initialFlowerStock;
        }

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
            EventBus.RaiseMoneyChanged(currentMoney);
            Debug.Log($"[CurrencyManager] 금액 소비: -{amount} → {currentMoney}");
            return true;
        }

        Debug.LogWarning($"[CurrencyManager] 금액 부족 (필요: {amount}, 보유: {currentMoney})");
        return false;
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

        var wrapperList = new List<int>(ownedWrappers);
        return wrapperList[Random.Range(0, wrapperList.Count)];
    }

    // ========== 꽃 (Flower) 관리 ==========

    /// <summary>보유 수량(없으면 0).</summary>
    public int GetFlowerStock(int flowerId)
    {
        return flowerStock.TryGetValue(flowerId, out int n) ? n : 0;
    }

    /// <summary>보유 수량을 amount만큼 늘리거나(음수면 줄인다). 꽃 선택에서 보유보다 많이 고르면 마이너스가 될 수 있다.</summary>
    public void AddFlowerStock(int flowerId, int amount)
    {
        flowerStock[flowerId] = GetFlowerStock(flowerId) + amount; // 임시: 0 아래(마이너스)도 허용하고 표시만 0으로 한다
    }

    /// <summary>꽃을 해금한다(이미 해금했으면 아무 일도 없다).</summary>
    public void ObtainFlower(int flowerId)
    {
        if (obtainedFlowers.GetValueOrDefault(flowerId)) return;

        obtainedFlowers[flowerId] = true;
        Debug.Log($"[CurrencyManager] 꽃 {flowerId} 획득");
    }

    /// <summary>
    /// 획득 가능한 모든 꽃 ID 반환
    /// </summary>
    public List<int> GetObtainedFlowerIds()
    {
        var result = new List<int>();
        foreach (var kvp in obtainedFlowers)
        {
            if (kvp.Value)
                result.Add(kvp.Key);
        }
        return result;
    }

    // ========== 일수 진행 ==========

    /// <summary>
    /// 다음 날로 진행
    /// </summary>
    public void AdvanceDay()
    {
        currentDay++;
        TodayEarned = 0;

        EventBus.RaiseDayAdvanced(currentDay);
        Debug.Log($"[CurrencyManager] 일수 진행: Day {currentDay}");
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (GUILayout.Button("Debug: +1000 금액"))
            AddMoney(1000);

        if (GUILayout.Button("Debug: 포장지 3 언락"))
            UnlockWrapper(3);

        if (GUILayout.Button("Debug: 다음 날"))
            AdvanceDay();

        if (GUILayout.Button("Debug: 하루 시간 -1분") && DayClock.Instance != null)
            DayClock.Instance.SkipDayTime(60f);

        if (GUILayout.Button("Debug: 밤 퍼즐 스킵(성공)") && GameFlowController.Instance != null)
            GameFlowController.Instance.DebugSkipNightPuzzle();

        if (GUILayout.Button("Debug: 밤 시간 -1분") && DayClock.Instance != null)
            DayClock.Instance.SkipNightTime(60f);

        if (GUILayout.Button("Debug: 손님 기분 -20%") && DayClock.Instance != null)
            DayClock.Instance.DrainMood(0.2f);

        if (GUILayout.Button("Debug: 리롤 횟수 +1"))
            FindFirstObjectByType<GachaManager>()?.AddRerolls(1);

        GUILayout.Label($"현재 금액: {CurrentMoney}\n현재 일수: {CurrentDay}\n보유 포장지: {ownedWrappers.Count}개");
    }
#endif
}
