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

    private const string NightNote = "Canvas/Puzzle/NoteBack";
    private const string NightGrid = "Canvas/Puzzle/NightGrid";
    private const string NightResult = "Canvas/ResultPopup";

    // ── 안내 목록(보여 주는 순서) ───────────────────────────────────────────────
    public static readonly (string id, Func<bool> when, Func<Step[]> steps)[] All =
    {
        // 낮 손님: 첫 주문
        ("welcome", () => In(GameManager.SceneDayMain) && Main == MainState.DayCustomer && Active(AcceptButton), () => new[]
        {
            On($"손님이 원하는 {Em("포장지")}와 {Em("꽃 요구사항")}을 말해요.\n이 주문대로 꽃다발을 만들어요.", () => Rt(SpeechBubble)),
            On($"{Em($"[{Label(AcceptButton)}]")}로 주문을 받으면 손님이 기다리기 시작해요.\n오래 기다릴수록 손님의 기분이 깎여요.", () => Rt(AcceptButton)),
        }),

        // 꽃 선택 페이지(주문을 받으면 자동으로 넘어온다)
        ("wrapper", () => In(GameManager.SceneDayMain) && Main == MainState.DayCustomer && Visible(WrapperConfirm), () => new[]
        {
            On($"주문의 {Em("포장지")}를 골라요.\n포장지마다 필요한 꽃의 수가 달라요.", () => Children(WrapperBoxes)),
        }),
        ("flowers", () => In(GameManager.SceneDayMain) && Main == MainState.DayCustomer && Visible(FlowerConfirm), () => new[]
        {
            On($"꽃은 네 역할로 나뉘어요. {Em("꽃병")}에 {Em("꽃의 재고")}가 표시돼요.\n최대 개수를 다 채우지 않아도, 역할마다 하나 이상 선택하면 {Em($"[{Label(FlowerConfirm)}]")}로 넘어갈 수 있어요.", () => Rt(FlowerShelves)),
        }),

        // 낮 퍼즐: 꽃 놓기
        ("puzzle-board", () => In(GameManager.SceneDayPuzzle) && Active(FlowerBucket), () => new[]
        {
            On($"이웃한 꽃과 {Em("색 궁합")}이 좋으면 꽃이 {Em("빛나고")}, 안 맞으면 {Em("흐려져요")}.\n빛나는 꽃이 많을수록 꽃다발의 상태가 좋아져요.", () => Rt(SlotArea)),
            OnIfShown($"{Em("별 칸")}에 놓은 꽃은 기본적으로 {Em("빛나요")}.", () => SpecialSlot(SpecialCell.Star)),
            OnIfShown($"{Em("그늘 칸")}에 놓은 꽃은 기본적으로 {Em("흐려져요")}.\n이웃한 꽃이 빛나면 그늘이 사라져요.", () => SpecialSlot(SpecialCell.Shade)),
            On($"칸의 색은 놓을 수 있는 꽃의 {Em("역할")}이에요.", () => Rt(SlotArea)),
            On($"바구니의 꽃을 {Em("끌어서")} 테두리와 같은 색 칸에 놓으세요.", () => Rt(FlowerBucket)),
            On($"양쪽에 꽃을 고르게 놓으세요.\n한쪽이 {Em("2개 이상")} 많아지면 꽃다발이 {Em("무너져요!")}", () => Rt(Board)),
            OnIfShown($"원하는 꽃이 안 나오면 {Em("[다시 뽑기]")}를 눌러요.\n숫자는 남은 횟수예요.", () => Rt(RerollButton)),
            On($"{Em("보관 칸")}에 꽃 하나를 맡겨 두었다가 나중에 꺼내 쓸 수 있어요.", () => Rt(StorageSlot)),
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
        ("day-end", () => In(GameManager.SceneDayMain) && Main == MainState.DayEnding && Active(AcceptButton), () => new[]
        {
            Banner($"{Em("낮 영업")} 끝! 화면을 {Em("밀면")} 옆의 {Em("꽃 선반")}으로 갈 수 있어요."),
        }),
        ("charm", () => In(GameManager.SceneDayMain) && Main == MainState.DayEnding && Visible(CharmUse), () => new[]
        {
            On($"{Em("얻고 싶은 꽃")}에 {Em("부적")}을 붙여요. 최대 {Em("6개")}지만, 다 붙이지 않아도 돼요.\n그 꽃을 만들어줄 {Em("손님")}이 밤에 와요. 끝나면 밀어서 다시 카운터로 돌아와요.", () => Rt(FlowerShelves)),
        }),
        ("lantern", () => In(GameManager.SceneDayMain) && Main == MainState.DayEnding && HasSeen("charm") && LanternReady(), () => new[] // 선반에서 부적을 붙여 본 뒤에
        {
            On($"{Em("전등")}을 켜면 {Em("밤 영업")}이 시작돼요.", () => Rt(Lantern)),
        }),

        // 밤
        ("night-customer", () => In(GameManager.SceneDayMain) && Main == MainState.NightCustomer && Active(AcceptButton), () => new[]
        {
            On($"밤에는 {Em("특별한 손님")}이 부적을 붙인 꽃을 얻을 수 있는 {Em("퍼즐")}을 부탁해요.\n{Em($"[{Label(AcceptButton)}]")}를 누르면 시작해요.", () => Rt(AcceptButton)),
        }),
        ("night-puzzle", () => In(GameManager.SceneNightPuzzle) && Active(NightGrid), () => new[]
        {
            On($"왼쪽 {Em("수첩")} 위의 위의 블럭을 {Em("오른쪽 칸")}에 {Em("그려 넣어요")}.", () => Rt(NightNote)),
            On($"블럭은 {Em("좌우반전")}이나 {Em("회전")}한 모양으로 그려도 돼요. 그린 블럭을 다시 누르면 지워요.\n{Em("어두운 칸")}은 그릴 수 없어요. 빈칸을 {Em("빈틈없이")} 채우면 완성!", () => Rt(NightGrid)),
        }),
        ("night-result", () => In(GameManager.SceneNightPuzzle) && Active(NightResult), () => new[]
        {
            Banner($"퍼즐을 풀면 {Em("꽃")}을 얻어요. 얻은 꽃은 다음 날 꽃 선택에서 쓸 수 있어요."),
        }),
    };

    // ── 찾기 도우미 ─────────────────────────────────────────────────────────────
    private static bool In(string scene) => SceneManager.GetActiveScene().name == scene;

    private static MainState Main => GameManager.Instance != null ? GameManager.Instance.MainState : MainState.DayCustomer;

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
