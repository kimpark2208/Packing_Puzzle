using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 부적 화면(낮 종료 뒤 꽃 선반): 오늘 밤 찾아올 손님을 꽃에 부적을 붙여 지정한다.
/// - 선반은 꽃 선택 화면(FlowerSelectUI)과 같은 구성이라, 에디터에 놓아 둔 슬롯 자리에 모든 꽃을 역할별로 채운다. 꽃 옆에는 남은 재고를 보여 준다.
/// - 꽃을 누르면 부적이 붙고(보유 부적 -1), 한 번 더 누르면 떼어져 돌아온다. 붙은 꽃은 슬롯의 Charm(부적 뱃지)이 켜진다.
///   재고가 0인 꽃에도 붙일 수 있다. 한 밤에 쓸 수 있는 부적은 maxCharmsPerNight개까지다.
/// 이 화면은 메인 씬의 선반 덩어리(ShelfRoot)이고, 낮 종료 때 스와이프(SwipeParallax)로 들어온다. 밤은 카운터에서 등불을 켤 때 시작하며
/// 그때 MainUI가 CharmedFlowerIds(부적을 붙인 꽃들)를 읽어 간다.
/// </summary>
public class NightRequestUI : MonoBehaviour
{
    [Serializable]
    private class Shelf
    {
        public FlowerData.FlowerRole role;
        public RectTransform slotParent;
    }

    [SerializeField] private Shelf[] shelves;
    [SerializeField] private TMP_Text usedText; // 오른쪽 위 "부적 n / 6"
    [SerializeField] private int maxCharmsPerNight = 6;

    private readonly HashSet<int> charmed = new();
    private readonly Dictionary<int, GameObject> charmBadges = new(); // 꽃 ID별 슬롯의 Charm(부적 뱃지)

    /// <summary>지금 부적을 붙인 꽃들의 ID.</summary>
    public IReadOnlyCollection<int> CharmedFlowerIds => charmed;

    private void Start()
    {
        BuildShelves();
        RefreshUsed();
    }

    /// <summary>에디터에 놓아 둔 슬롯 자리에 역할별로 모든 꽃을 채운다. 꽃보다 자리가 많으면 남는 자리는 숨긴다.</summary>
    private void BuildShelves()
    {
        if (DataRegistry.Instance == null) return;

        foreach (Shelf shelf in shelves)
        {
            var spots = new List<RectTransform>();
            foreach (Transform child in shelf.slotParent) spots.Add((RectTransform)child);

            var flowers = DataRegistry.Instance.Flowers.Where(b => b.flowerRole == shelf.role).ToList();
            for (int i = 0; i < spots.Count; i++)
            {
                bool hasFlower = i < flowers.Count;
                bool used = hasFlower && (CurrencyManager.Instance == null || CurrencyManager.Instance.HasObtainedFlower(flowers[i].flowerID)); // 아직 얻지 못한 꽃은 화병을 끈다(자리는 그대로 비워 둔다)
                spots[i].gameObject.SetActive(used);
                if (used) BindSlot(spots[i], flowers[i]);
                else if (!hasFlower) Debug.LogWarning($"[NightRequestUI] {shelf.role} 선반의 자리가 꽃보다 많습니다: {spots[i].name}");
            }
            if (flowers.Count > spots.Count) Debug.LogWarning($"[NightRequestUI] {shelf.role} 선반의 슬롯 자리({spots.Count}개)보다 꽃이 많습니다.");
        }
    }

    private void BindSlot(RectTransform slot, FlowerData flower)
    {
        slot.name = $"Slot_{flower.flowerID}";
        FlowerPieceView.GetOrAdd(slot.gameObject).Apply(flower, flower.stemSprite, false);

        Transform badge = FlowerPieceView.FindIn(slot, "CountBadge");
        TMP_Text badgeText = badge != null ? badge.GetComponentInChildren<TMP_Text>(true) : null;
        if (badgeText != null) badgeText.text = $"x{Mathf.Max(0, CurrencyManager.Instance != null ? CurrencyManager.Instance.GetFlowerStock(flower.flowerID) : 0)}"; // 재고 표시(0이어도 그대로 보여 준다)

        FlowerPieceView.FindIn(slot, "Check")?.gameObject.SetActive(false); // 꽃 선택 화면용 체크 표시: 여기서는 Charm(부적 뱃지)이 그 역할이라 항상 끈다

        Transform charm = FlowerPieceView.FindIn(slot, "Charm");
        if (charm != null)
        {
            charmBadges[flower.flowerID] = charm.gameObject;
            charm.gameObject.SetActive(false);
        }

        int id = flower.flowerID;
        FlowerPieceView.AttachClick(slot, () => ToggleCharm(id));
    }

    /// <summary>붙어 있으면 떼어 돌려받고, 없으면 붙인다(부적이 없거나 오늘 밤 한도를 다 썼으면 붙이지 못한다).</summary>
    private void ToggleCharm(int flowerId)
    {
        CurrencyManager wallet = CurrencyManager.Instance;
        if (wallet == null) return;

        if (charmed.Remove(flowerId))
        {
            wallet.RefundCharm();
        }
        else
        {
            if (charmed.Count >= maxCharmsPerNight || !wallet.TryUseCharm()) return;
            charmed.Add(flowerId);
        }

        if (charmBadges.TryGetValue(flowerId, out GameObject charm)) charm.SetActive(charmed.Contains(flowerId));
        RefreshUsed();
    }

    private void RefreshUsed()
    {
        if (usedText != null) usedText.text = $"부적 {charmed.Count} / {maxCharmsPerNight}";
    }
}
