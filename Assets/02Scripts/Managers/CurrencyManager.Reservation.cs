using System.Collections.Generic;
using UnityEngine;

/// <summary>수락한 예약 주문 하나: 어떤 예약이고, 언제 수락했고, 항목마다 얼마나 보관했는가.</summary>
public class ReservationRecord
{
    public readonly ReservationData data;
    public readonly int acceptedDay;
    public readonly int[] stored; // data.items와 같은 순서로, 항목마다 보관한 수

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

    /// <summary>항목의 보유 재고(보관할 수 있는 항목이 아니면 0).</summary>
    public int GetItemStock(ReservationData.Item item)
    {
        if (!item.Storable) return 0;

        switch (item.kind)
        {
            case ReservationData.ItemKind.Flower: return GetFlowerStock(item.id);
            case ReservationData.ItemKind.Wrapper: return GetWrapperStock(item.id);
            default: return GetDecorationStock(item.id);
        }
    }

    /// <summary>항목의 보유 재고를 늘리거나(음수면 줄인다).</summary>
    private void AddItemStock(ReservationData.Item item, int amount)
    {
        if (!item.Storable || amount == 0) return;

        switch (item.kind)
        {
            case ReservationData.ItemKind.Flower: AddFlowerStock(item.id, amount); break;
            case ReservationData.ItemKind.Wrapper: AddWrapperStock(item.id, amount); break;
            default: AddDecorationStock(item.id, amount); break;
        }
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
