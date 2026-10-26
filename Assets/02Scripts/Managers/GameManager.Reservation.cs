using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameManager의 예약 꽃다발 제작 부분. 예약일에 예약자가 준비됐는지 묻고 "네"를 받으면 하루 종일 예약 꽃다발만 만든다(손님은 더 오지 않는다).
/// 꽃다발 하나 = 포장지 선택 → 꽃 선택 → 퍼즐 → 결과 화면이고, 결과 화면이 끝나면 다시 포장지 선택으로 돌아온다(메인 화면의 옆 페이지).
/// 모두 만들거나 하루 시간이 끝나면 메인 화면에서 예약자가 평균 점수 등급에 맞는 말을 하고(Reacting), 보상을 받은 뒤 남은 시간이 빠르게 흘러 낮 종료로 간다.
/// </summary>
public partial class GameManager
{
    public enum ReservationPhase
    {
        None,
        Crafting, // 꽃다발을 만드는 중
        Reacting, // 예약자가 결과에 맞는 말을 하는 중
    }

    /// <summary>오늘 만드는 예약 꽃다발(없으면 null).</summary>
    public ReservationCraft Craft { get; private set; }

    public ReservationPhase Phase { get; private set; }

    /// <summary>예약 꽃다발을 만드는 중인가.</summary>
    public bool Crafting => Craft != null && Phase == ReservationPhase.Crafting;

    private bool bouquetInProgress;   // 꽃다발 하나의 퍼즐을 하는 중
    private bool craftTimeUpPending;  // 퍼즐을 하는 중에 하루 시간이 끝나, 그 꽃다발을 마치면 반응으로 가야 한다
    private bool skipEndingCustomer;  // 예약 반응 뒤의 낮 종료는 마무리 손님이 없다

    /// <summary>예약자가 "준비됐다"는 답을 받았다: 오늘은 예약 꽃다발만 만든다. 포장지 선택 화면부터 시작한다.</summary>
    public void BeginReservationCraft(ReservationRecord record)
    {
        record.pickedUp = true;
        Craft = new ReservationCraft(record);
        Phase = ReservationPhase.Crafting;
        bouquetInProgress = false;
        craftTimeUpPending = false;
        PrepareReservationBouquet();
        SceneTransition.Go(SceneDayMain);
    }

    /// <summary>다음 꽃다발의 주문(필수 꽃, 포장지, 프리셋)을 정한다.</summary>
    private void PrepareReservationBouquet()
    {
        orderInProgress = false;
        dayEndPending = false;
        MainState = MainState.DayCustomer;
        pendingCustomerLine = null;
        pendingRejectResponseLine = DefaultRejectResponseLine;
        PreviousNpc = null;
        PreviousBondDialogueId = null;

        CurrentDayOrder = DayPuzzleGenerator.GenerateReservationOrder(CurrencyManager.Instance.CurrentDay, Craft);
        CurrencyManager.Instance.SetCurrentRequirement(CurrentDayOrder.requirement);
    }

    /// <summary>꽃다발을 만들 때 쓸 수 있는 꽃 수: 예약의 필수 꽃은 보관해 둔 것만, 나머지는 보유한 만큼.</summary>
    public int CraftFlowerStock(int flowerId)
    {
        if (Crafting && Craft.IsRequired(flowerId)) return Craft.Remaining(flowerId);

        return CurrencyManager.Instance != null ? CurrencyManager.Instance.GetFlowerStock(flowerId) : 0;
    }

    /// <summary>꽃다발 하나를 마쳤다(결과 화면이 끝났다): 점수를 쌓고, 다 만들었거나 시간이 끝났으면 예약자의 반응으로, 아니면 다음 꽃다발의 포장지 선택으로 돌아간다.</summary>
    public void FinishReservationBouquet(int score)
    {
        Craft.AddScore(score);
        bouquetInProgress = false;
        if (Craft.Finished || craftTimeUpPending)
        {
            BeginReservationReaction();
            return;
        }

        PrepareReservationBouquet();
        SceneTransition.Go(SceneDayMain);
    }

    private void HandleCraftTimeUp()
    {
        if (Phase != ReservationPhase.Crafting) return;

        if (bouquetInProgress) craftTimeUpPending = true; // 퍼즐을 마친 뒤에 넘어간다
        else BeginReservationReaction();
    }

    private void BeginReservationReaction()
    {
        Phase = ReservationPhase.Reacting;
        craftTimeUpPending = false;
        orderInProgress = false;
        dayEndPending = false;
        MainState = MainState.DayCustomer;
        SceneTransition.Go(SceneDayMain);
    }

    /// <summary>예약자의 반응을 듣고 보상을 받는다: 보상금 × 등급 배율을 받고, 쓰고 남은 보관 꽃은 재고로 돌아오며, 예약은 끝난다. 받은 금액을 돌려준다.</summary>
    public int CollectReservationReward()
    {
        int payout = Mathf.RoundToInt(Craft.Data.reward * ReservationGrading.Multiplier(Craft.Grade));
        CurrencyManager.Instance.FinishReservation(Craft.record, payout);
        return payout;
    }

    /// <summary>예약 반응이 끝나고 남은 시간이 다 흘렀다: 예약을 마무리하고 낮 종료(부적 화면)로 간다.</summary>
    public void EndReservationDay()
    {
        Craft = null;
        Phase = ReservationPhase.None;
        skipEndingCustomer = true;
        BeginDayEnding();
    }

    /// <summary>예약일이 끝나고 열린 낮 종료면 한 번 true(마무리 손님 없이 바로 부적 화면). 꺼내 쓰면 지운다.</summary>
    public bool ConsumeSkipEndingCustomer()
    {
        bool skip = skipEndingCustomer;
        skipEndingCustomer = false;
        return skip;
    }
}
