using System.Collections.Generic;
using UnityEngine;

/// <summary>수락한 예약 주문 하나: 어떤 예약이고, 언제 수락했고, 항목마다 얼마나 보관했는가.</summary>
public class ReservationRecord
{
    public readonly ReservationData data;
    public readonly int acceptedDay;
    public readonly int[] stored; // data.items와 같은 순서로, 항목마다 보관한 수

    /// <summary>예약일에 예약자가 찾아와 준비가 되었다고 확인한 예약(꽃다발 제작을 기다린다. 제작 시스템은 아직 없다).</summary>
    public bool pickedUp;

    public ReservationRecord(ReservationData data, int acceptedDay)
    {
        this.data = data;
        this.acceptedDay = acceptedDay;
        stored = new int[data.items.Length];
    }

    /// <summary>예약일(수락한 날 + 기한).</summary>
    public int DueDay => acceptedDay + data.deadlineDays;
}

/// <summary>CurrencyManager의 예약 주문 부분: 수락한 예약, 보관, 취소. 저장 시스템이 생기기 전까지는 게임을 켠 동안만 유지된다.</summary>
public partial class CurrencyManager
{
    /// <summary>취소 때 이 일수 이상 남았으면 그냥 취소되고, 그보다 적게 남았으면 예약금의 1.5배를 물어야 한다.</summary>
    public const int FreeCancelDays = 3;
    public const float LateCancelFee = 1.5f;

    private readonly List<ReservationRecord> reservations = new();

    public IReadOnlyList<ReservationRecord> Reservations => reservations;

    /// <summary>예약을 수락했다: 예약금을 받고 예약이 접수된다.</summary>
    public ReservationRecord AcceptReservation(ReservationData data)
    {
        var record = new ReservationRecord(data, currentDay);
        reservations.Add(record);
        AddMoney(data.deposit);
        return record;
    }

    /// <summary>예약일까지 남은 일수(예약일 당일이면 0).</summary>
    public int RemainingDays(ReservationRecord record) => Mathf.Max(0, record.DueDay - currentDay);

    /// <summary>이 예약을 취소할 수 있는가(예약일 당일에는 취소할 수 없다).</summary>
    public bool CanCancel(ReservationRecord record) => RemainingDays(record) > 0;

    /// <summary>지금 취소하면 물어야 하는 금액(3일 이상 남았으면 0, 2일 이하면 예약금의 1.5배).</summary>
    public int CancelFee(ReservationRecord record) =>
        RemainingDays(record) >= FreeCancelDays ? 0 : Mathf.RoundToInt(record.data.deposit * LateCancelFee);

    /// <summary>예약을 취소한다. 위약금을 내고, 보관해 둔 꽃과 포장지는 재고로 돌아온다. 취소할 수 없으면 false.</summary>
    public bool CancelReservation(ReservationRecord record)
    {
        if (!reservations.Contains(record) || !CanCancel(record)) return false;

        AddMoney(-CancelFee(record));
        for (int i = 0; i < record.data.items.Length; i++) AddItemStock(record.data.items[i], record.stored[i]);
        reservations.Remove(record);
        return true;
    }

    /// <summary>예약일에 예약자가 찾아왔을 때의 준비 상태.</summary>
    public enum PickupState
    {
        Ready,        // 필요한 꽃을 모두 보관했고 포장지와 꾸미기 용품도 가지고 있다
        NeedsStoring, // 꽃은 수량만큼 가지고 있지만 아직 다 보관(수납)하지 않았다: 보관하면 준비가 끝난다
        NotReady,     // 보관해도 준비할 수 없다(꽃이 모자라거나 포장지·꾸미기 용품이 없다)
    }

    /// <summary>미준비로 예약일을 넘기면 예약금의 이 배수를 돌려줘야 한다(PDF의 "예약금의 2배 환불", 지불하는 것으로 처리).</summary>
    public const int NoShowFeeMultiplier = 2;

    /// <summary>미준비일 때 다음 예약 주문이 늦게 걸려 오는 일수.</summary>
    public const int NoShowDelayDays = 5;

    public PickupState GetPickupState(ReservationRecord record)
    {
        bool allStored = true, canStore = true;
        for (int i = 0; i < record.data.items.Length; i++)
        {
            ReservationData.Item item = record.data.items[i];
            if (!item.Storable)
            {
                if (!HasItem(item)) return PickupState.NotReady; // 포장지와 꾸미기 용품은 가지고 있는지만 본다
                continue;
            }

            if (record.stored[i] >= item.count) continue;

            allStored = false;
            if (record.stored[i] + GetFlowerStock(item.id) < item.count) canStore = false;
        }
        return allStored ? PickupState.Ready : canStore ? PickupState.NeedsStoring : PickupState.NotReady;
    }

    /// <summary>예약일에 준비하지 못했다: 예약금의 NoShowFeeMultiplier배를 돌려주고(돈이 모자라면 0까지), 보관해 둔 꽃은 재고로 돌아오며, 예약은 없어진다.</summary>
    public void FailReservation(ReservationRecord record)
    {
        AddMoney(-record.data.deposit * NoShowFeeMultiplier);
        for (int i = 0; i < record.data.items.Length; i++) AddItemStock(record.data.items[i], record.stored[i]);
        reservations.Remove(record);
    }

    /// <summary>예약 꽃다발 제작을 마쳤다: 보상(payout)을 받고, 쓰고 남은 보관 꽃은 재고로 돌아오며, 예약은 끝난다.</summary>
    public void FinishReservation(ReservationRecord record, int payout)
    {
        AddMoney(payout);
        for (int i = 0; i < record.data.items.Length; i++) AddItemStock(record.data.items[i], record.stored[i]);
        reservations.Remove(record);
    }

    /// <summary>꽃 항목의 보유 재고(보관할 수 있는 항목이 아니면 0).</summary>
    public int GetItemStock(ReservationData.Item item) => item.Storable ? GetFlowerStock(item.id) : 0;

    /// <summary>항목을 가지고 있는가(임시 규칙): 꽃은 재고가 있으면, 포장지는 얻었으면, 꾸미기 용품은 샀거나 받았으면. 연결하지 않은 항목(id 0)은 미보유다.</summary>
    public bool HasItem(ReservationData.Item item)
    {
        if (item.id <= 0) return false;

        switch (item.kind)
        {
            case ReservationData.ItemKind.Flower: return GetFlowerStock(item.id) > 0;
            case ReservationData.ItemKind.Wrapper: return HasWrapper(item.id);
            default: return GetDecorationStock(item.id) > 0;
        }
    }

    /// <summary>꽃 항목의 보유 재고를 늘리거나(음수면 줄인다).</summary>
    private void AddItemStock(ReservationData.Item item, int amount)
    {
        if (!item.Storable || amount == 0) return;

        AddFlowerStock(item.id, amount);
    }

    /// <summary>지금 보관할 수 있는 최대 개수: 재고와, 아직 더 필요한 수 중 작은 쪽.</summary>
    public int MaxStorable(ReservationRecord record, int itemIndex)
    {
        ReservationData.Item item = record.data.items[itemIndex];
        return Mathf.Max(0, Mathf.Min(GetItemStock(item), item.count - record.stored[itemIndex]));
    }

    /// <summary>항목을 amount개 보관한다(재고에서 빠져 예약용으로 따로 둔다. 한 번 보관한 것은 다시 꺼낼 수 없다). 못 하면 false.</summary>
    public bool StoreForReservation(ReservationRecord record, int itemIndex, int amount)
    {
        if (amount <= 0 || amount > MaxStorable(record, itemIndex)) return false;

        AddItemStock(record.data.items[itemIndex], -amount);
        record.stored[itemIndex] += amount;
        return true;
    }
}
