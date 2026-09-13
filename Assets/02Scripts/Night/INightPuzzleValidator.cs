using UnityEngine;

/// <summary>
/// 스테이지 타입(일반 주문 / 잉여시간 / 보스)마다 다른 목표 도형 판정 규칙을 주입하기 위한 인터페이스.
/// NightBoardController는 이 인터페이스만 알고, 구체적인 판정 로직은 구현체에 위임한다.
/// </summary>
public interface INightPuzzleValidator
{
    /// <summary>
    /// 해당 좌표가 목표 도형(정답) 범위 안에 있는지 여부.
    /// 보스 스테이지처럼 목표 범위 자체가 없는 자유 드로우 모드에서는 항상 true를 반환할 수 있다.
    /// </summary>
    bool IsCellInTarget(Vector2Int coord);

    /// <summary>
    /// 현재 보드 상태에서 벽으로 지정된 칸인지 여부. 벽 칸은 애초에 드로우 대상에서 제외된다.
    /// </summary>
    bool IsWallCell(Vector2Int coord);

    /// <summary>
    /// 보드 전체가 완성 조건을 만족했는지 여부.
    /// 일반 주문 스테이지: 벽이 아닌 모든 칸이 채워졌는가.
    /// 잉여시간 스테이지: 정답 배치와 정확히 일치하는가.
    /// </summary>
    bool IsPuzzleComplete(bool[,] filledState);
}
