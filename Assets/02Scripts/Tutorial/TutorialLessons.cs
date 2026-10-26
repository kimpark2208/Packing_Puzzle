using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static TutorialOverlay;
using static TutorialOverlay.Step;

/// <summary>
/// 튜토리얼 내용 전부. 대상은 타이쿤 게임의 문법(손님, 주문, 돈, 시계, 카탈로그 같은 것)은 아는 게이머라서,
/// 화면의 UI를 하나하나 설명하지 않고 이 게임만의 규칙(역할 칸, 균형, 꽃의 빛, 부적과 밤 퍼즐)만 첫날 흐름을 따라 짚는다.
/// 안내를 더하거나 빼려면 All 목록의 항목(id, 언제 보여 줄지, 어떤 장면들인지)만 고친다. 다른 화면의 코드는 건드리지 않는다.
/// - when: 그 안내를 보여 줄 때가 됐는지(화면이 자리 잡을 때까지 TutorialDirector가 잠시 기다린 뒤 보여 준다). 같은 id는 한 번만 보인다.
/// - steps: 배너 하나가 한 장면이다. On(문구, 대상)은 그 대상만 밝게 살리고, Banner(문구)는 배너만 보여 준다.
/// 대상은 하이라키 경로로 찾는다(꺼져 있는 오브젝트는 못 찾는다). 오브젝트 이름이나 위치를 바꾸면 아래 경로 상수도 같이 바꾼다.
/// </summary>
public static class TutorialLessons
{
    // ── 하이라키 경로(씬 루트부터) ──────────────────────────────────────────────
    private const string Global = "GlobalUICanvas/GlobalUI/";
    private const string Lantern = Global + "Clock/LanternArea/LanternToggle";

    private const string MainRoot = "Canvas/DayMainRoot/";
    private const string SpeechBubble = MainRoot + "BubbleContainer/SpeechBubble";
    private const string AcceptButton = MainRoot + "BubbleContainer/AcceptBTN";

    private const string WrapperArea = "Canvas/ShelfRoot/WrapperArea/";
    private const string WrapperBoxes = WrapperArea + "Boxes";
    private const string WrapperConfirm = WrapperArea + "ConfirmBTN"; // 포장지 페이지가 화면에 와 있는지 보는 기준

    private const string FlowerArea = "Canvas/ShelfRoot/FlowerArea/";
    private const string FlowerShelves = FlowerArea + "FlowerPopupBack";
    private const string FlowerConfirm = FlowerArea + "ConfirmBTN"; // 꽃 선택 페이지가 화면에 와 있는지 보는 기준
    private const string CharmUse = FlowerArea + "CharmUse";       // 부적 선반이 화면에 와 있는지 보는 기준

    private const string Puzzle = "Canvas/PuzzleRoot/";
    private const string Board = Puzzle + "WrapperImage";
    private const string SlotArea = Board + "/SlotArea";
    private const string FlowerBucket = Puzzle + "FlowerBucket";
    private const string RerollButton = FlowerBucket + "/RerollBTN";
    private const string StorageSlot = FlowerBucket + "/StorageSlot";
    private const string DecoratePanelPath = Puzzle + "DecoratePanel";
    private const string CollapsePopup = "Canvas/CollapsePopup";
    private const string ResultGauge = "ResultCanvas/DayResultPopup/Gauge";

    private const string ReservationAccept = MainRoot + "ReservationPopup/Card/ReservationAcceptBTN"; // 예약 쇼카드의 수락 버튼

    /// <summary>예약 쇼카드 안내의 id. 이 안내를 보기 전의 첫 예약은 거절할 수 없다(ReservationShowCard).</summary>
    public const string ReservationCallLesson = "reservation-call";

    // ── 팝업형 안내(베타 기획서 04 튜토리얼 이미지) ──────────────────────────────
    // 제목, 그림, 설명은 TutorialPopupData 에셋(id가 같은 것)에 있고, 여기서는 언제 보여 줄지만 정한다. 배너형(아래 All)보다 먼저 검사한다.
    public static readonly (string id, Func<bool> when)[] PopupTriggers =
    {
        // 낮
        ("day-flower", () => In(GameManager.SceneDayMain) && Main == MainState.DayCustomer && Visible(FlowerConfirm)),                       // 꽃 선택 페이지에 처음 오면
        ("day-puzzle", () => In(GameManager.SceneDayPuzzle) && Active(FlowerBucket)),                                                        // 낮 퍼즐에 처음 들어오면
        ("charm", () => In(GameManager.SceneDayMain) && Main == MainState.DayEnding),                                                        // 낮 장사가 끝나면(낮 종료에는 수락 버튼이 없다)
        ("furniture", () => In(GameManager.SceneDayMain) && Main == MainState.DayCustomer && PhoneUnlocked() && !MerchantTalking() && !Crafting && Visible(AcceptButton)), // 2일차 가구 상인이 떠난 뒤(다음 손님이 말할 때)
        ("reservation-prep", () => In(GameManager.SceneDayMain) && Main == MainState.DayCustomer && !Crafting && HasReservation() && !ReservationCall.Busy && Visible(AcceptButton)), // 예약을 수락한 뒤 다음 손님이 오면
        ("reservation-craft", () => In(GameManager.SceneDayMain) && Crafting && Visible(FlowerConfirm)),                                      // 예약일에 꽃 선택 페이지로 넘어가면

        // 밤
        ("night-diagnosis", () => In(GameManager.SceneNightPuzzle) && !SceneTransition.Busy && Game != null && Game.CurrentNightCase != null && UnityEngine.Object.FindFirstObjectByType<NightDiagnosisUI>() != null), // 진단 화면에서 꽃을 받으면
        ("night-loss", () => RepairOpen(FlowerData.Ailment.PetalLoss)),
        ("night-stem", () => RepairOpen(FlowerData.Ailment.StemBend)),
        ("night-scatter", () => RepairOpen(FlowerData.Ailment.PetalScatter)),
        ("night-wilt", () => RepairOpen(FlowerData.Ailment.Wilt)),
        ("night-faded", () => RepairOpen(FlowerData.Ailment.FadedMemory)),
    };

    // ── 배너형 안내 목록(보여 주는 순서) ────────────────────────────────────────
    public static readonly (string id, Func<bool> when, Func<Step[]> steps)[] All =
    {
        // 예약 쇼카드(베타 기획서: 이전 튜토리얼 방식): 처음 걸려 온 예약은 수락하게 한다(ReservationShowCard가 이 안내를 보기 전까지 거절 버튼을 막는다)
        (ReservationCallLesson, () => In(GameManager.SceneDayMain) && Active(ReservationAccept), () => new[]
        {
            Banner($"예약 주문이 걸려왔어요! 필요한 꽃과 용품들을 {Em("예약일까지 준비")}하면 일반 주문보다 훨씬 더 많은 돈을 받아요."),
            On($"{Em("[수락]")}을 눌러 예약을 받아요.", () => Rt(ReservationAccept)),
        }),

        // 꽃 선택 페이지(주문을 받으면 자동으로 넘어온다)
        ("wrapper", () => In(GameManager.SceneDayMain) && Main == MainState.DayCustomer && Visible(WrapperConfirm), () => new[]
        {
            On($"주문의 {Em("포장지")}를 골라요.\n포장지마다 필요한 꽃의 수가 달라요.", () => Children(WrapperBoxes)),
        }),
        // (꽃 역할 소개와 꽃다발 만들기 기본은 팝업형 day-flower, day-puzzle이 맡는다)

        // 낮 퍼즐: 화면에 따라 나오는 것들만 짚는다
        ("puzzle-special", () => In(GameManager.SceneDayPuzzle) && Active(FlowerBucket) && TutorialPopup.HasSeen("day-puzzle"), () => new[] // 팝업형 day-puzzle을 본 뒤에
        {
            OnIfShown($"{Em("별 칸")}에 놓은 꽃은 기본적으로 {Em("빛나요")}.", () => SpecialSlot(SpecialCell.Star)),
            OnIfShown($"{Em("그늘 칸")}에 놓은 꽃은 기본적으로 {Em("흐려져요")}.\n이웃한 꽃이 빛나면 그늘이 사라져요.", () => SpecialSlot(SpecialCell.Shade)),
            OnIfShown($"원하는 꽃이 안 나오면 {Em("[다시 뽑기]")}를 눌러요.\n숫자는 남은 횟수예요.", () => Rt(RerollButton)),
        }),
        ("wilted", () => In(GameManager.SceneDayPuzzle) && WiltedFlower() != null, () => new[]
        {
            On($"회색 꽃은 {Em("시든 꽃")}이에요. 놓을 수 없어요.\n{Em("[다시 뽑기]")}를 누르거나 다른 꽃을 놓으면 새 꽃이 나와요.", () => WiltedFlower()),
        }),
        ("collapse", () => In(GameManager.SceneDayPuzzle) && Active(CollapsePopup), () => new[]
        {
            Banner($"꽃다발이 {Em("무너졌어요!")} 이번 꽃다발은 {Em("감점")}돼요.\n처음부터 다시 만들어요."),
        }),

        // 낮 퍼즐: 꾸미기와 결과
        ("decorate", () => In(GameManager.SceneDayPuzzle) && Active(DecoratePanelPath), () => new[]
        {
            On($"{Em("리본과 풀")}은 카드를 누르면 위치에 놓아지고,\n{Em("카드와 장식")}은 드래그로 끌어다 놓아요.\n{Em("[꽃다발 모양]")}에서는 포장지를 바꿔요.", () => Rt(DecoratePanelPath)),
        }),
        ("result", () => In(GameManager.SceneDayPuzzle) && Active(ResultGauge), () => new[]
        {
            On($"{Em("꽃다발 점수")}가 하나씩 나오며 게이지가 움직여요.\n화면을 누르면 빨리 넘어가요.", () => Rt(ResultGauge)),
        }),

        // 낮이 끝난 뒤: 부적과 전등
        // (낮 영업이 끝났다는 안내와 부적 소개는 팝업형 charm이 맡는다)
        ("charm", () => In(GameManager.SceneDayMain) && Main == MainState.DayEnding && Visible(CharmUse), () => new[]
        {
            On($"{Em("얻고 싶은 꽃")}에 {Em("부적")}을 붙여요. 최대 {Em("6개")}지만, 다 붙이지 않아도 돼요.\n그 꽃을 만들어줄 {Em("손님")}이 밤에 와요. 끝나면 밀어서 다시 카운터로 돌아와요.", () => Rt(FlowerShelves)),
        }),
        ("lantern", () => In(GameManager.SceneDayMain) && Main == MainState.DayEnding && HasSeen("charm") && LanternReady(), () => new[] // 선반에서 부적을 붙여 본 뒤에
        {
            On($"{Em("전등")}을 켜면 {Em("밤 영업")}이 시작돼요.", () => Rt(Lantern)),
        }),

        // (밤 안내는 팝업형 night-*이 맡는다. 첫 주문·밤 손님 소개 배너와 옛 밤 퍼즐 씬의 그림 그리기 안내는 시나리오 대사와 겹쳐서 뺐다)
    };

    // ── 찾기 도우미 ─────────────────────────────────────────────────────────────
    private static bool In(string scene) => SceneManager.GetActiveScene().name == scene;

    private static MainState Main => GameManager.Instance != null ? GameManager.Instance.MainState : MainState.DayCustomer;

    private static GameManager Game => GameManager.Instance;

    /// <summary>예약 꽃다발을 만드는 날인가.</summary>
    private static bool Crafting => Game != null && Game.Crafting;

    /// <summary>수락한 예약이 있는가.</summary>
    private static bool HasReservation() => CurrencyManager.Instance != null && CurrencyManager.Instance.Reservations.Count > 0;

    /// <summary>오늘부터 전화기를 쓸 수 있는가(시나리오의 해금 일차. 시나리오가 없으면 처음부터 쓸 수 있으니 안내하지 않는다).</summary>
    private static bool PhoneUnlocked() => ScenarioManager.Instance != null && CurrencyManager.Instance != null && ScenarioManager.Instance.IsPhoneUnlocked(CurrencyManager.Instance.CurrentDay);

    /// <summary>지금 가구 상인이 대화하는 중인가(상인이 떠나야 가구 주문 안내를 한다).</summary>
    private static bool MerchantTalking()
    {
        DayVisit? visit = Game != null ? Game.CurrentScriptedVisit : null;
        return visit.HasValue && visit.Value.dialogueId != null && visit.Value.dialogueId.StartsWith("merchant");
    }

    /// <summary>밤 수리 퍼즐에서 그 아픔의 퍼즐이 지금 열려 있는가.</summary>
    private static bool RepairOpen(FlowerData.Ailment ailment)
    {
        return In(GameManager.SceneNightPuzzle) && UnityEngine.Object.FindObjectsByType<RepairPuzzlePanel>(FindObjectsSortMode.None).Any(p => p.Ailment == ailment && p.gameObject.activeInHierarchy);
    }

    /// <summary>경로의 오브젝트(켜져 있을 때만. GameObject.Find는 경로로 찾으면 꺼진 것도 돌려주므로 직접 거른다).</summary>
    private static GameObject Go(string path)
    {
        GameObject go = GameObject.Find(path);
        return go != null && go.activeInHierarchy ? go : null;
    }

    private static bool Active(string path) => Go(path) != null;

    private static RectTransform Rt(string path) => Go(path)?.transform as RectTransform;

    /// <summary>버튼에 적힌 글자(씬에서 글자를 바꿔도 안내가 따라가도록).</summary>
    private static string Label(string path) => Go(path)?.GetComponentInChildren<TMPro.TMP_Text>()?.text.Trim() ?? "확인";

    private static IEnumerable<RectTransform> Children(string path)
    {
        RectTransform parent = Rt(path);
        return parent != null ? parent.Cast<RectTransform>() : Enumerable.Empty<RectTransform>();
    }

    /// <summary>켜져 있고 화면 가운데 쪽에 있는가(옆 페이지는 켜져 있어도 화면 밖에 있다).</summary>
    private static bool Visible(string path)
    {
        RectTransform rt = Rt(path);
        if (rt == null) return false;

        Canvas canvas = rt.GetComponentInParent<Canvas>();
        Camera cam = canvas != null ? canvas.rootCanvas.worldCamera : null;
        float width = cam != null ? cam.pixelWidth : Screen.width;
        float x = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center)).x;
        return x > width * 0.05f && x < width * 0.95f;
    }

    private static bool LanternReady()
    {
        Toggle lantern = Rt(Lantern)?.GetComponent<Toggle>();
        return lantern != null && lantern.interactable;
    }

    /// <summary>이 퍼즐에서 그 종류의 특수 칸(없으면 null).</summary>
    private static RectTransform SpecialSlot(SpecialCell kind)
    {
        WrapperSlot slot = UnityEngine.Object.FindObjectsByType<WrapperSlot>(FindObjectsSortMode.None).FirstOrDefault(s => s.special == kind);
        return slot != null ? (RectTransform)slot.transform : null;
    }

    /// <summary>바구니에 나온 시든(회색) 꽃 조각(없으면 null).</summary>
    private static RectTransform WiltedFlower()
    {
        foreach (RectTransform piece in Children(FlowerBucket))
        {
            if (!piece.gameObject.activeSelf) continue;

            Image icon = piece.GetComponentsInChildren<Image>().FirstOrDefault(i => i.name.StartsWith("FlowerImage"));
            if (icon != null && icon.color == GachaManager.WiltedColor) return piece;
        }
        return null;
    }
}
