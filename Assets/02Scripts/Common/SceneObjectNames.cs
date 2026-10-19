/// <summary>
/// 화면 스크립트가 씬에서 이름으로 찾는 오브젝트들의 이름(SceneFind로 찾고, 못 찾으면 오류를 남긴다).
/// 하이어라키에서 오브젝트 이름을 바꾸면 여기도 같이 바꾼다. 튜토리얼의 하이어라키 경로(TutorialLessons)는 경로 전체가 달라서 따로 둔다.
/// </summary>
public static class SceneObjectNames
{
    // ── 메인 화면(MainUI) ──
    public const string BubbleText = "BubbleText";
    public const string RejectButton = "RejectBTN";
    public const string AcceptButton = "AcceptBTN";
    public const string Telephone = "CounterTelephone";      // 낮/밤 모습이 같은 오브젝트 하나를 쓴다(MainLook이 그림만 바꾼다)
    public const string TelephoneBar = "TelephoneBar";       // 전화기를 누르면 뜨는 메뉴 바(가구배치/상품주문/예약확인)
    public const string CatalogButton = "CatalogBTN";        // 전화기 바의 상품주문 버튼
    public const string BookingButton = "BookingBTN";        // 전화기 바의 예약확인 버튼(달력이 해금돼야 누를 수 있다)
    public const string CatalogPopup = "CatalogPopup";
    public const string ReservationCalendar = "ReservationCalendar"; // 전화기 바의 예약확인 버튼이 여는 달력 화면
    public const string BubbleContainer = "BubbleContainer";
    public const string DayCharacter = "DayCharacterArea";   // 낮 손님을 무작위로 만드는 CharacterGenerator가 붙은 오브젝트
    public const string NightCharacter = "NightCharacterArea";
    public const string NormalChar = "NomalChar";            // DayCharacterArea 안의 세 모습: 무작위 손님(부위를 조합한 모습)과
    public const string SmallChar = "SmallChar";             // 체구가 작은 NPC(한 장짜리 그림)
    public const string BigChar = "BigChar";                 // 체구가 큰 NPC(한 장짜리 큰 그림)
    public const string LanternToggle = "LanternToggle";
    public const string ShelfRoot = "ShelfRoot";             // 메인 옆 페이지: 낮 손님이면 꽃 선택, 낮 종료면 꽃 선반(부적)
    public const string CharmPill = "CharmUse";              // 부적 화면에만 나오는 "부적 n / 6" 표시

    public static readonly string[] CharacterRoots = { DayCharacter, NightCharacter };

    // ── 낮 퍼즐 화면(DayPuzzleUI) ──
    public const string FlowerBucket = "FlowerBucket";
    public const string DecoratePanel = "DecoratePanel";
    public const string UndoButton = "UndoBTN";
    public const string RetryButton = "RetryBTN";
    public const string FinishButton = "ConfirmBTN";         // 작업 완료 버튼
}
