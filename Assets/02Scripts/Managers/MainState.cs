/// <summary>
/// 메인 화면(손님을 만나는 화면)이 보여 주는 상황. GameManager가 씬을 열기 전에 정해 두고, DayMainUI/GameClock이 읽는다.
/// 낮 손님과 낮 종료는 낮 모습, 밤 손님과 밤 종료는 밤 모습이다.
/// </summary>
public enum MainState
{
    DayCustomer,   // 낮 손님이 주문을 말한다(하루 시간이 흐른다)
    DayEnding,     // 하루 시간이 끝나 손님이 마무리 대사를 하고 떠난다. 떠나면 전등을 눌러 밤을 시작한다
    NightCustomer, // 밤 손님이 밤 퍼즐 부탁을 한다(밤 시간이 흐른다)
    NightEnding    // 밤 시간이 끝나 마지막 손님이 외치고 떠난다. 떠나면 하루 결산으로 간다
}
