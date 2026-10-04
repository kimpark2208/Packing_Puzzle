using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// 낮 퍼즐의 "꽃 가챠". FlowerBucket 안에 미리 배치해 둔 자리(자식 FlowerTemplate)만큼 꽃을 뽑아 보여준다.
/// - 풀: 플레이어가 꽃 선택 화면에서 고른 꽃 중 아직 놓지 않은 것. 퍼즐에 놓은 꽃만 풀에서 빠진다.
/// - 꽃 하나를 놓으면 버켓의 나머지는 사라지고(풀에는 남는다) 새 꽃들이 나온다. 칸 수만큼 놓으면 풀이 비어 끝난다.
/// - 리롤: 버켓의 꽃을 모두 치우고 다른 꽃을 뽑는다. 퍼즐 전체에서 maxRerolls번, 치운 꽃은 풀에 남는다.
/// 모든 조각은 자리 템플릿을 복제해 데이터만 다르게 주입한다.
/// </summary>
public class GachaManager : MonoBehaviour
{
    [SerializeField] private Button gachaButton;     // 리롤 버튼
    [SerializeField] private RectTransform bucket;   // 자식 FlowerTemplate들이 꽃이 나올 자리
    [SerializeField] private int maxRerolls = 2;
    [SerializeField] private List<FlowerData> initialPool = new(); // 인스펙터 테스트용 기본 풀

    private readonly List<GameObject> slotTemplates = new();
    private readonly List<GameObject> shown = new();
    private List<FlowerData> pool = new();
    private List<FlowerData> fullPool = new(); // 처음 고른 풀(붕괴해서 다시 시작할 때 돌려놓는다)
    private int rerollCount;
    private TMP_Text rerollBadge; // 리롤 버튼의 CountBadge 텍스트(남은 횟수)

    public int RerollsLeft => Mathf.Max(0, maxRerolls - rerollCount);

    /// <summary>디버그용: 이번 퍼즐의 리롤 가능 횟수를 늘린다.</summary>
    public void AddRerolls(int amount)
    {
        maxRerolls += amount;
        RefreshBadge();
    }

    private void Awake()
    {
        pool = new List<FlowerData>(initialPool);
        fullPool = new List<FlowerData>(initialPool);
    }

    private void Start()
    {
        // 버켓에 미리 배치된 자식은 자리 표시용 템플릿이라 숨겨두고, 뽑을 때마다 복제해서 쓴다.
        foreach (Transform slot in bucket)
        {
            slotTemplates.Add(slot.gameObject);
            slot.gameObject.SetActive(false);
        }

        if (gachaButton != null) gachaButton.onClick.AddListener(Reroll);
        Transform badge = gachaButton != null ? gachaButton.transform.Find("CountBadge") : null;
        rerollBadge = badge != null ? badge.GetComponentInChildren<TMP_Text>(true) : null;
        RefreshBadge();

        // 낮 퍼즐 씬 진입 시, 꽃 선택 화면에서 고른 풀을 스스로 가져온다.
        // (DayPuzzleUI 등 다른 스크립트의 Start 순서에 의존하지 않기 위함)
        if (GameFlowController.Instance != null && GameFlowController.Instance.ChosenGachaPool.Count > 0)
        {
            SetPool(GameFlowController.Instance.ChosenGachaPool);
        }
        else
        {
            Draw(null);
        }
    }

    /// <summary>FlowerSelect 화면에서 플레이어가 고른 꽃으로 풀을 세팅하고 첫 꽃들을 뽑는다.</summary>
    public void SetPool(List<FlowerData> newPool)
    {
        pool = new List<FlowerData>(newPool);
        fullPool = new List<FlowerData>(newPool);
        rerollCount = 0;
        Draw(null);
        RefreshBadge();
    }

    /// <summary>붕괴로 퍼즐을 다시 시작할 때: 놓아서 소모됐던 꽃이 풀로 돌아오고 새로 뽑는다. 리롤 횟수는 돌아오지 않는다.</summary>
    public void RestorePool()
    {
        pool = new List<FlowerData>(fullPool);
        Draw(null);
    }

    public void Reroll()
    {
        if (rerollCount >= maxRerolls || pool.Count == 0) return;

        rerollCount++;
        Draw(ShownFlowers());
        RefreshBadge();
        Debug.Log($"[GachaManager] 리롤 {rerollCount}/{maxRerolls}");
    }

    private void RefreshBadge()
    {
        if (rerollBadge != null) rerollBadge.text = RerollsLeft.ToString();
    }

    /// <summary>버켓을 비우고 풀에서 자리 수만큼 새로 뽑는다. avoid(방금 보여준 꽃)는 가능하면 피한다.</summary>
    private void Draw(List<FlowerData> avoid)
    {
        ClearShown();

        var fresh = new List<FlowerData>(pool);
        if (avoid != null) foreach (var f in avoid) fresh.Remove(f);

        int count = DrawCount();
        var picks = new List<FlowerData>();
        TakeRandom(fresh, picks, count);

        // 새 꽃이 모자라면 방금 보여준 꽃으로 채운다.
        var rest = new List<FlowerData>(pool);
        foreach (var f in picks) rest.Remove(f);
        TakeRandom(rest, picks, count);

        for (int i = 0; i < picks.Count; i++) Spawn(slotTemplates[i], picks[i]);
    }

    /// <summary>한 번에 뽑을 꽃 개수: 오늘 고른 포장지 데이터의 값. 버킷에 슬롯 오브젝트가 있는 만큼까지만 보여줄 수 있다.</summary>
    private int DrawCount()
    {
        var order = GameFlowController.Instance != null ? GameFlowController.Instance.CurrentDayOrder : null;
        WrapperData data = order != null && order.isFinalized && WrapperRegistry.Instance != null ? WrapperRegistry.Instance.GetById(order.wrapperId) : null;
        return data != null ? Mathf.Min(data.flowersPerDraw, slotTemplates.Count) : slotTemplates.Count;
    }

    private static void TakeRandom(List<FlowerData> from, List<FlowerData> into, int total)
    {
        while (into.Count < total && from.Count > 0)
        {
            int i = Random.Range(0, from.Count);
            into.Add(from[i]);
            from.RemoveAt(i);
        }
    }

    private void Spawn(GameObject template, FlowerData flower)
    {
        GameObject block = Instantiate(template, bucket, false);
        block.SetActive(true); // 템플릿은 숨겨져 있어도 복제본은 보이게

        // FlowerTemplate 자체엔 BlockDrag가 없으므로(선택 화면에서도 같이 쓰는 공용 템플릿) 버켓 조각에서만 붙여준다.
        BlockDrag draggable = block.GetComponent<BlockDrag>();
        if (draggable == null) draggable = block.AddComponent<BlockDrag>();

        draggable.blockData = flower;
        draggable.OnPlaced += HandleBlockPlaced;

        FlowerPieceView view = block.GetComponent<FlowerPieceView>();
        if (view == null) view = block.AddComponent<FlowerPieceView>();
        view.Apply(flower, flower.iconSprite != null ? flower.iconSprite : flower.dayPieceSprite, true);

        // FlowerImage의 Button은 선택 화면(클릭으로 고르기)용이라 드래그로 놓는 버켓에서는 꺼둔다.
        Transform flowerImageT = block.transform.Find("FlowerImage");
        var flowerButton = flowerImageT != null ? flowerImageT.GetComponent<Button>() : null;
        if (flowerButton != null) flowerButton.enabled = false;

        shown.Add(block);
    }

    /// <summary>꽃 하나를 놓으면 그 꽃만 풀에서 빠지고, 안 쓴 나머지는 사라진 뒤 새 꽃들이 나온다.</summary>
    private void HandleBlockPlaced(GameObject block)
    {
        var placed = block.GetComponent<BlockDrag>();
        if (placed != null) pool.Remove(placed.blockData);
        shown.Remove(block);

        Draw(ShownFlowers());
    }

    private List<FlowerData> ShownFlowers()
    {
        return shown.Where(b => b != null).Select(b => b.GetComponent<BlockDrag>().blockData).ToList();
    }

    private void ClearShown()
    {
        foreach (var block in shown)
        {
            if (block != null) Destroy(block);
        }
        shown.Clear();
    }
}
