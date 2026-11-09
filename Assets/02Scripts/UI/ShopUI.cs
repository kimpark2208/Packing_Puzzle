using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 밤 상점 화면(메인 씬의 카운터 위). 진열대의 꽃을 사고(FlowerShop.TryBuy), 리롤하고, 상점을 마친다.
/// 진열대 칸, 리롤/마치기 버튼은 씬에 미리 배치되어 있고 이 스크립트는 글자와 그림만 채운다. 진열과 리롤 횟수는 FlowerShop이 들고 있다.
/// </summary>
public class ShopUI : MonoBehaviour
{
    [SerializeField] private Button rerollButton;
    [SerializeField] private TMP_Text rerollCountText;
    [SerializeField] private Button closeButton;

    private ShopSlotView[] slots; // 하이라키 순서대로 진열대 1번 칸부터(씬에서 칸을 늘리거나 옮겨도 따라간다)
    private FlowerShop shop;
    private Action onClose;
    private bool closed;

    /// <summary>상점을 연다. onClose는 "마치기"를 눌렀을 때 한 번 불린다.</summary>
    public void Open(FlowerShop visit, Action close)
    {
        shop = visit;
        onClose = close;
        closed = false;
        slots = GetComponentsInChildren<ShopSlotView>(true);

        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveAllListeners();
            rerollButton.onClick.AddListener(OnRerollClicked);
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnCloseClicked);
        }

        gameObject.SetActive(true);
        if (closeButton != null) closeButton.gameObject.SetActive(true); // 마치기 버튼은 상점 밖(카운터 화면)에 있어서, 상점이 열려 있는 동안에만 보인다
        Refresh();
    }

    /// <summary>상점을 숨긴다(마치기 버튼도 같이). 씬이 에디터에서 켜진 채 저장돼 있어도 낮에는 보이지 않게 MainUI가 부른다.</summary>
    public void Hide()
    {
        gameObject.SetActive(false);
        if (closeButton != null) closeButton.gameObject.SetActive(false);
    }

    private void OnEnable() => EventBus.OnMoneyChanged += OnMoneyChanged;

    private void OnDisable()
    {
        EventBus.OnMoneyChanged -= OnMoneyChanged;
        if (closeButton != null) closeButton.gameObject.SetActive(false);
    }

    private void OnMoneyChanged(int money)
    {
        if (shop != null) Refresh();
    }

    private void Refresh()
    {
        CurrencyManager wallet = CurrencyManager.Instance;
        for (int i = 0; i < slots.Length; i++)
        {
            bool filled = i < shop.Shelf.Count;
            slots[i].gameObject.SetActive(filled); // 꽃이 모자란 칸은 비어 보이게 둔다
            if (!filled) continue;

            FlowerData flower = shop.Shelf[i];
            slots[i].Bind(flower, wallet != null && wallet.CurrentMoney >= flower.shopPrice, shop.IsSoldOut(flower), shop.IsNew(flower), () => OnBuyClicked(flower));
        }

        if (rerollButton != null)
        {
            rerollButton.gameObject.SetActive(true);
            rerollButton.interactable = shop.RerollsLeft > 0;
        }
        if (rerollCountText != null) rerollCountText.text = shop.RerollsLeft.ToString();
    }

    /// <summary>신규 꽃 구매 튜토리얼이 가리키는 꽃(지금 꼭 사야 하는 꽃, 없으면 null).</summary>
    public FlowerData ForcedFlower => shop != null && CurrencyManager.Instance != null ? shop.ForcedFlower(CurrencyManager.Instance) : null;

    private void OnBuyClicked(FlowerData flower)
    {
        if (!shop.TryBuy(flower, CurrencyManager.Instance, out bool firstTime)) return;

        string amount = NoticeToast.Highlight($"X{flower.shopBundle}");
        NoticeToast.Show(firstTime ? $"새 꽃 {flower.DisplayName} {amount}" : $"{flower.DisplayName} {amount}");
        Refresh();
    }

    private void OnRerollClicked()
    {
        if (shop.Reroll()) Refresh();
    }

    private void OnCloseClicked()
    {
        if (closed) return; // 두 번 눌러 다음 날이 두 번 시작되지 않게
        if (ForcedFlower != null)
        {
            NoticeToast.Show("새 꽃을 구매해야 마칠 수 있어요");
            return;
        }
        closed = true;
        onClose?.Invoke();
    }
}
