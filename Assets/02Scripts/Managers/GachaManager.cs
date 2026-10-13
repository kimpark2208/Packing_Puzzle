using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// 낮 퍼즐의 "꽃 가챠". FlowerBucket 안에 미리 배치해 둔 자리(자식 FlowerTemplate)만큼 꽃을 뽑아 보여준다.
/// - 풀(FlowerPool): 플레이어가 꽃 선택 화면에서 고른 꽃 종류들. 뽑는 규칙(프리셋의 역할 비율, 재고 한도)은 FlowerPool이 정한다.
/// - 꽃 하나를 놓으면 버켓의 나머지는 사라지고 풀에서 새로 뽑는다.
/// - 뽑힌 꽃은 wiltChance 확률로 시든 꽃(회색)이 되어 놓을 수 없다. 시든 꽃은 리롤하거나 다른 꽃을 놓아 새로 뽑으면 사라진다.
/// - 보관 칸: 바구니의 꽃 하나를 보관 칸에 맡기면(보드에 놓은 것과 같은 한 차례) 바구니는 새로 뽑힌다. 맡긴 꽃은 나중에 보드로 끌어와 놓을 수 있고,
///   그때도 한 차례로 쳐서 바구니가 새로 뽑힌다. 보관 칸은 1개이고, 차 있는 동안은 다른 꽃을 맡길 수 없다. 바구니의 꽃은 다음 차례로 넘어가지 않는다.
/// - 리롤: 버켓의 꽃을 모두 치우고 다른 꽃을 뽑는다. 퍼즐 전체에서 maxRerolls번.
/// 모든 조각은 자리 템플릿을 복제해 데이터만 다르게 주입한다.
/// </summary>
public class GachaManager : MonoBehaviour
{
    [SerializeField] private Button gachaButton;     // 리롤 버튼
    [SerializeField] private RectTransform bucket;   // 자식 FlowerTemplate들이 꽃이 나올 자리
    [SerializeField] private FlowerStorageSlot storageSlot; // 바구니 옆 보관 칸(1개). 없으면 보관 기능이 꺼진다
    [SerializeField] private int maxRerolls = 2;
    [SerializeField, Range(0f, 1f)] private float wiltChance = 0.08f; // 뽑힌 꽃 하나가 시든 꽃일 확률
    [SerializeField] private List<FlowerData> initialPool = new(); // 인스펙터 테스트용 기본 풀

    public static readonly Color WiltedColor = new(0.55f, 0.55f, 0.55f); // 시든 꽃 색(튜토리얼이 시든 꽃을 알아볼 때도 쓴다)

    private readonly List<GameObject> slotTemplates = new();
    private readonly List<GameObject> shown = new();
    private FlowerPool pool;
    private int rerollCount;
    private TMP_Text rerollBadge; // 리롤 버튼 안의 텍스트(남은 횟수)

    public int RerollsLeft => Mathf.Max(0, maxRerolls - rerollCount);

    /// <summary>디버그용: 이번 퍼즐의 리롤 가능 횟수를 늘린다.</summary>
    public void AddRerolls(int amount)
    {
        maxRerolls += amount;
        RefreshBadge();
    }

    private void Awake()
    {
        pool = NewPool(initialPool);
    }

    /// <summary>역할 비율은 오늘 확정된 프리셋을, 재고는 보유 수량을 따른다(씬 단독 실행 등으로 없으면 제한 없이).</summary>
    private static FlowerPool NewPool(IEnumerable<FlowerData> flowers)
    {
        var order = GameManager.Instance != null ? GameManager.Instance.CurrentDayOrder : null;
        System.Func<FlowerData, int> stockOf = CurrencyManager.Instance != null ? f => CurrencyManager.Instance.GetFlowerStock(f.blockID) : null;
        return new FlowerPool(flowers, order != null ? order.preset : null, stockOf);
    }

    private void Start()
    {
        // 버켓에 미리 배치된 자식은 자리 표시용 템플릿이라 숨겨두고, 뽑을 때마다 복제해서 쓴다.
        foreach (Transform slot in bucket)
        {
            if (gachaButton != null && slot == gachaButton.transform) continue; // 리롤 버튼이 버켓 안에 있어도 자리 템플릿이 아니다
            if (slot.GetComponent<FlowerStorageSlot>() != null) continue; // 보관 칸도 마찬가지
            slotTemplates.Add(slot.gameObject);
            slot.gameObject.SetActive(false);
        }

        if (gachaButton != null) gachaButton.onClick.AddListener(Reroll);
        rerollBadge = gachaButton != null ? gachaButton.GetComponentInChildren<TMP_Text>(true) : null; // 버튼 안의 글자(이름이 바뀌어도 찾는다)
        RefreshBadge();

        // 낮 퍼즐 씬 진입 시, 꽃 선택 화면에서 고른 풀을 스스로 가져온다.
        // (DayPuzzleUI 등 다른 스크립트의 Start 순서에 의존하지 않기 위함)
        if (GameManager.Instance != null && GameManager.Instance.ChosenGachaPool.Count > 0)
        {
            SetPool(GameManager.Instance.ChosenGachaPool);
        }
        else
        {
            Draw(null);
        }
    }

    /// <summary>FlowerSelect 화면에서 플레이어가 고른 꽃으로 풀을 세팅하고 첫 꽃들을 뽑는다.</summary>
    public void SetPool(List<FlowerData> newPool)
    {
        pool = NewPool(newPool);
        if (storageSlot != null) storageSlot.Clear();
        rerollCount = 0;
        Draw(null);
        RefreshBadge();
    }

    /// <summary>붕괴로 퍼즐을 다시 시작할 때: 새로 뽑는다. 리롤 횟수는 돌아오지 않는다.</summary>
    public void Redraw()
    {
        pool.ResetPlaced(); // 다시 시작하면 놓은 꽃(맡긴 꽃 포함)은 소모되지 않았다
        if (storageSlot != null) storageSlot.Clear();
        Draw(null);
    }

    public void Reroll()
    {
        if (rerollCount >= maxRerolls || pool.IsEmpty) return;

        rerollCount++;
        Draw(ShownFlowers());
        RefreshBadge();
        Debug.Log($"[GachaManager] 리롤 {rerollCount}/{maxRerolls}");
    }

    private void RefreshBadge()
    {
        if (rerollBadge != null) rerollBadge.text = RerollsLeft.ToString();
    }

    /// <summary>버켓을 비우고 풀에서 자리 수만큼 서로 다른 종류를 새로 뽑는다. avoid(방금 보여준 꽃)는 가능하면 피한다.</summary>
    private void Draw(List<FlowerData> avoid)
    {
        ClearShown();

        List<FlowerData> picks = pool.Draw(DrawCount(), avoid);
        for (int i = 0; i < picks.Count; i++) Spawn(slotTemplates[i], picks[i]);
    }

    /// <summary>한 번에 뽑을 꽃 개수: 오늘 고른 포장지 데이터의 값. 버킷에 슬롯 오브젝트가 있는 만큼까지만 보여줄 수 있다.</summary>
    private int DrawCount()
    {
        var order = GameManager.Instance != null ? GameManager.Instance.CurrentDayOrder : null;
        WrapperData data = order != null ? order.wrapper : null;
        return data != null ? Mathf.Min(data.flowersPerDraw, slotTemplates.Count) : slotTemplates.Count;
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
        draggable.OnStored += HandleBlockStored;
        draggable.storeTarget = storageSlot;

        FlowerPieceView view = FlowerPieceView.GetOrAdd(block);
        view.Apply(flower, flower.iconSprite != null ? flower.iconSprite : flower.dayPieceSprite, true);

        // FlowerImage의 Button은 선택 화면(클릭으로 고르기)용이라 드래그로 놓는 버켓에서는 꺼둔다.
        Transform flowerImageT = block.transform.Find("FlowerImage");
        var flowerButton = flowerImageT != null ? flowerImageT.GetComponent<Button>() : null;
        if (flowerButton != null) flowerButton.enabled = false;

        if (Random.value < wiltChance) Wilt(draggable, view);

        shown.Add(block);
    }

    /// <summary>시든 꽃: 회색으로 칠하고 드래그할 수 없게 한다(비활성 컴포넌트는 드래그 이벤트를 받지 않는다).</summary>
    private static void Wilt(BlockDrag draggable, FlowerPieceView view)
    {
        draggable.enabled = false;
        view.SetIconColor(WiltedColor);
    }

    /// <summary>꽃 하나를 놓으면 그만큼 재고에서 빼고, 버켓의 나머지는 사라지고 풀에서 새 꽃들을 뽑는다.</summary>
    private void HandleBlockPlaced(GameObject block)
    {
        pool.MarkPlaced(block.GetComponent<BlockDrag>().blockData);
        shown.Remove(block);
        Draw(null);
    }

    /// <summary>바구니의 꽃을 보관 칸에 맡겼다: 그 꽃은 재고에서 따로 잡아 두고(놓은 것으로 센다), 바구니의 나머지는 사라지고 새로 뽑는다.</summary>
    private void HandleBlockStored(GameObject block)
    {
        BlockDrag draggable = block.GetComponent<BlockDrag>();
        pool.MarkPlaced(draggable.blockData);

        draggable.OnPlaced -= HandleBlockPlaced;
        draggable.OnStored -= HandleBlockStored;
        draggable.storeTarget = null; // 보관 칸의 꽃은 다시 맡길 수 없고, 보드에만 놓을 수 있다
        draggable.OnPlaced += HandleStoredBlockPlaced;

        shown.Remove(block);
        storageSlot.Hold(block);
        Draw(null);
    }

    /// <summary>보관 칸의 꽃을 보드에 놓았다: 칸이 비고, 바구니의 꽃 대신 놓은 한 차례이므로 바구니는 새로 뽑는다(재고는 맡길 때 이미 잡았다).</summary>
    private void HandleStoredBlockPlaced(GameObject block)
    {
        storageSlot.Release();
        Draw(null);
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
