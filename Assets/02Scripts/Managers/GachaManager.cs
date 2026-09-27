using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// 낮 퍼즐의 "꽃 가챠". 플레이어가 FlowerSelect 화면에서 고른 3종의 꽃 풀에서
/// 랜덤으로 drawCount개를 뽑아 트레이에 보여주고, 배치되면 트레이에서 제거한다.
/// 모든 조각은 동일한 범용 프리팹(BlockDrag)에 데이터만 다르게 주입해서 생성한다.
/// </summary>
public class GachaManager : MonoBehaviour
{
    [SerializeField] private Button gachaButton;
    [SerializeField] private GameObject pieceViewPrefab; // BlockDrag 컴포넌트를 가진 범용 꽃 조각 프리팹
    [SerializeField] private Transform tray;
    [SerializeField] private int drawCount = 3;
    [SerializeField] private int maxRerolls = 3;
    [SerializeField] private List<BlockData> initialPool = new(); // 인스펙터 테스트용 기본 풀

    private List<BlockData> pool = new();
    private int rerollCount = 0;
    private readonly List<GameObject> curSpawned = new();

    /// <summary>트레이에 남은 조각이 하나도 없는가.</summary>
    public bool IsTrayEmpty => curSpawned.Count == 0;

    /// <summary>리롤 횟수를 모두 사용했는가.</summary>
    public bool RerollExhausted => rerollCount >= maxRerolls;

    /// <summary>포장지 레벨에 맞춰 최대 리롤 횟수를 설정한다.</summary>
    public void SetMaxRerolls(int value)
    {
        maxRerolls = Mathf.Max(1, value);
    }

    private void Awake()
    {
        pool = new List<BlockData>(initialPool);
    }

    private void OnEnable()
    {
        EventBus.OnDayAdvanced += HandleDayAdvanced;
    }

    private void OnDisable()
    {
        EventBus.OnDayAdvanced -= HandleDayAdvanced;
    }

    private void Start()
    {
        // pieceViewPrefab이 트레이 안에 미리 배치된 하이라키 템플릿이면(하이라키 안에 그대로 보이던 것),
        // 평소엔 숨겨두고 뽑을 때마다 복제해서 쓴다.
        if (pieceViewPrefab != null && pieceViewPrefab.transform.IsChildOf(tray))
        {
            pieceViewPrefab.SetActive(false);
        }

        if (gachaButton != null) gachaButton.onClick.AddListener(Gacha);

        // 낮 퍼즐 씬 진입 시, 꽃 선택 화면에서 고른 풀을 스스로 가져온다.
        // (DayPuzzleUI 등 다른 스크립트의 Start 순서에 의존하지 않기 위함)
        if (GameFlowController.Instance != null && GameFlowController.Instance.ChosenGachaPool.Count > 0)
        {
            SetPool(GameFlowController.Instance.ChosenGachaPool);
        }
    }

    /// <summary>FlowerSelect 화면에서 플레이어가 고른 3종(가변 개수)의 꽃으로 가챠 풀을 세팅한다.</summary>
    public void SetPool(List<BlockData> newPool)
    {
        pool = new List<BlockData>(newPool);
        rerollCount = 0;
        ClearBlocks();
    }

    public void Gacha()
    {
        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning("[GachaManager] 가챠 풀이 비어 있습니다.");
            return;
        }

        if (rerollCount >= maxRerolls && curSpawned.Count > 0)
        {
            Debug.Log("[GachaManager] 리롤 횟수를 모두 사용했습니다.");
            return;
        }

        ClearBlocks();

        for (int i = 0; i < drawCount; i++)
        {
            BlockData picked = pool[Random.Range(0, pool.Count)];
            GameObject block = Instantiate(pieceViewPrefab, tray, false);
            block.SetActive(true); // pieceViewPrefab이 숨겨진 템플릿이어도 복제본은 보이게

            // FlowerTemplate 자체엔 BlockDrag가 없으므로(선택 화면에서도 같이 쓰는 공용 템플릿) 트레이 조각에서만 붙여준다.
            BlockDrag draggable = block.GetComponent<BlockDrag>();
            if (draggable == null) draggable = block.AddComponent<BlockDrag>();

            draggable.blockData = picked;
            draggable.OnPlaced += HandleBlockPlaced;

            // FlowerImage의 Button은 선택 화면(클릭으로 고르기)용이라 드래그로 놓는 트레이에서는 꺼둔다.
            Transform flowerImageT = block.transform.Find("FlowerImage");
            var flowerButton = flowerImageT != null ? flowerImageT.GetComponent<Button>() : null;
            if (flowerButton != null) flowerButton.enabled = false;

            curSpawned.Add(block);
        }

        rerollCount++;
        Debug.Log($"[GachaManager] 가챠 {rerollCount}/{maxRerolls}");
    }

    private void HandleBlockPlaced(GameObject block)
    {
        curSpawned.Remove(block);
    }

    private void ClearBlocks()
    {
        foreach (var block in curSpawned)
        {
            if (block != null) Destroy(block);
        }
        curSpawned.Clear();
    }

    public void ResetRerollCount()
    {
        rerollCount = 0;
    }

    /// <summary>붕괴로 라운드가 재시작될 때: 리롤 횟수와 트레이를 초기화한다.</summary>
    public void RestartAttempt()
    {
        rerollCount = 0;
        ClearBlocks();
    }

    private void HandleDayAdvanced(int newDay)
    {
        ResetRerollCount();
    }
}
