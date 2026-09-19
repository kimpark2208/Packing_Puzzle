using UnityEngine;

/// <summary>게임 전역 잡다한 상태(현재는 점수)를 보관하는 최상위 매니저.</summary>
public class GameManager : Singleton<GameManager>
{
    public int score = 0;
}
