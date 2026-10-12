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
/// 그때 DayMainUI가 CharmedFlowerIds(부적을 붙인 꽃들)를 읽어 간다.
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
        if (BlockRegistry.Instance == null) return;

        foreach (Shelf shelf in shelves)
        {
            var spots = new List<RectTransform>();
            foreach (Transform child in shelf.slotParent) spots.Add((RectTransform)child);

            var flowers = BlockRegistry.Instance.AllBlocks.Where(b => b.flowerRole == shelf.role).ToList();
            for (int i = 0; i < spots.Count; i++)
            {
                bool used = i < flowers.Count;
                spots[i].gameObject.SetActive(used);
                if (used) BindSlot(spots[i], flowers[i]);
                else Debug.LogWarning($"[NightRequestUI] {shelf.role} 선반의 자리가 꽃보다 많습니다: {spots[i].name}");
            }
            if (flowers.Count > spots.Count) Debug.LogWarning($"[NightRequestUI] {shelf.role} 선반의 슬롯 자리({spots.Count}개)보다 꽃이 많습니다.");
        }
    }

    private void BindSlot(RectTransform slot, FlowerData flower)
    {
        slot.name = $"Slot_{flower.blockID}";
        FlowerPieceView.GetOrAdd(slot.gameObject).Apply(flower, flower.stemSprite, false);

        Transform badge = FindIn(slot, "CountBadge");
        TMP_Text badgeText = badge != null ? badge.GetComponentInChildren<TMP_Text>(true) : null;
        if (badgeText != null) badgeText.text = $"x{Mathf.Max(0, CurrencyManager.Instance != null ? CurrencyManager.Instance.GetFlowerStock(flower.blockID) : 0)}"; // 재고 표시(0이어도 그대로 보여 준다)

        Transform charm = FindIn(slot, "Charm");
        if (charm != null)
        {
            charmBadges[flower.blockID] = charm.gameObject;
            charm.gameObject.SetActive(false);
        }

        // 꽃 선택 화면과 같은 이유로, 안쪽 버튼은 끄고 슬롯 맨 위에서 클릭을 받는다.
        foreach (Button inner in slot.GetComponentsInChildren<Button>(true)) inner.enabled = false;
        Button click = slot.GetComponent<Button>();
        if (click == null)
        {
            click = slot.gameObject.AddComponent<Button>();
            click.transition = Selectable.Transition.None;
        }
        int id = flower.blockID;
        click.onClick.AddListener(() => ToggleCharm(id));
    }

    private static Transform FindIn(RectTransform slot, string childName)
    {
        return slot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == childName);
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
