using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 재화 및 상태 관리 (싱글톤)
/// 현재 금액, 일수, 보유 포장지, 획득한 꽃, 요청한 꽃 등을 추적
/// </summary>
public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    [SerializeField] private int initialMoney = 0;

    // ========== 기본 상태 ==========
    private int currentMoney;
    private int currentDay;

    // ========== 포장지 (Wrapper) ==========
    private HashSet<int> ownedWrappers = new();

    // ========== 꽃 (Flower) ==========
    private Dictionary<int, bool> obtainedFlowers = new();  // ID → 획득 여부

    // ========== 일일 요청 ==========
    private List<int> requestedFlowerIdsForTonight = new();  // 오늘 밤에 얻을 꽃

    // ========== 프로퍼티 (읽기 전용) ==========
    public int CurrentMoney => currentMoney;
    public int CurrentDay => currentDay;
    public IReadOnlyCollection<int> OwnedWrappers => ownedWrappers;
    public IReadOnlyDictionary<int, bool> ObtainedFlowers => obtainedFlowers;
    public IReadOnlyList<int> RequestedFlowerIdsForTonight => requestedFlowerIdsForTonight;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Initialize()
    {
        currentMoney = initialMoney;
        currentDay = 1;

        // 초기 포장지: ID 1 (5x5) 언락
        ownedWrappers.Add(1);

        // 초기 꽃: A(ID 101), B(ID 102), C(ID 103) 획득 가능
        obtainedFlowers[101] = true;
        obtainedFlowers[102] = true;
        obtainedFlowers[103] = true;

        requestedFlowerIdsForTonight.Clear();

        Debug.Log($"[CurrencyManager] 초기화 완료 - 금액: {currentMoney}, 일수: {currentDay}");
    }

    // ========== 금액 관리 ==========

    /// <summary>
    /// 금액 추가
    /// </summary>
    public void AddMoney(int amount)
    {
        currentMoney = Mathf.Max(0, currentMoney + amount);
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
            EventBus.RaiseWrapperUnlocked(wrapperId);
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

    /// <summary>
    /// 꽃 획득
    /// </summary>
    public void ObtainFlower(int flowerId)
    {
        if (!obtainedFlowers.ContainsKey(flowerId))
        {
            obtainedFlowers[flowerId] = true;
            EventBus.RaiseFlowerObtained(flowerId);
            Debug.Log($"[CurrencyManager] 꽃 {flowerId} 획득");
        }
        else if (!obtainedFlowers[flowerId])
        {
            obtainedFlowers[flowerId] = true;
            EventBus.RaiseFlowerObtained(flowerId);
            Debug.Log($"[CurrencyManager] 꽃 {flowerId} 획득");
        }
    }

    /// <summary>
    /// 꽃 획득 여부 확인
    /// </summary>
    public bool HasFlower(int flowerId)
    {
        return obtainedFlowers.ContainsKey(flowerId) && obtainedFlowers[flowerId];
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

    // ========== 일일 요청 (Daily Request) ==========

    /// <summary>
    /// 꽃 요청 (오늘 밤에 얻을 꽃)
    /// </summary>
    public void RequestFlowerForTonight(int flowerId)
    {
        if (!requestedFlowerIdsForTonight.Contains(flowerId))
        {
            requestedFlowerIdsForTonight.Add(flowerId);
            Debug.Log($"[CurrencyManager] 꽃 {flowerId} 요청 (밤 퍼즐용)");
        }
    }

    /// <summary>
    /// 일일 요청 목록 초기화 (낮 시간 종료 시)
    /// </summary>
    public int[] ClearDailyRequestsAndGet()
    {
        var result = requestedFlowerIdsForTonight.ToArray();
        requestedFlowerIdsForTonight.Clear();
        return result;
    }

    // ========== 일수 진행 ==========

    /// <summary>
    /// 다음 날로 진행
    /// </summary>
    public void AdvanceDay()
    {
        currentDay++;
        requestedFlowerIdsForTonight.Clear();
        EventBus.RaiseDayAdvanced(currentDay);
        Debug.Log($"[CurrencyManager] 일수 진행: Day {currentDay}");
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (GUILayout.Button("Debug: +1000 금액"))
            AddMoney(1000);

        if (GUILayout.Button("Debug: 포장지 2 언락"))
            UnlockWrapper(2);

        if (GUILayout.Button("Debug: 꽃 104 획득"))
            ObtainFlower(104);

        if (GUILayout.Button("Debug: 다음 날"))
            AdvanceDay();

        GUILayout.Label($"현재 금액: {CurrentMoney}\n현재 일수: {CurrentDay}\n보유 포장지: {ownedWrappers.Count}개");
    }
#endif
}
