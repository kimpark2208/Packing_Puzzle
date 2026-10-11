using UnityEngine;

/// <summary>
/// 스와이프로 오가는 화면(페이지) 하나의 뿌리 오브젝트에 붙인다. 페이지는 오른쪽으로 한 화면 폭씩 이어서 놓는다:
/// 0번(첫 화면)은 원래 자리, 1번은 오른쪽 한 화면 폭 옆, 2번은 두 화면 폭 옆. 이 페이지 아래의 ParallaxLayer들이 이 번호를 따른다.
/// 첫 화면의 뿌리에는 붙이지 않아도 된다(ParallaxPage가 없는 레이어는 0번으로 본다).
/// </summary>
[DisallowMultipleComponent]
public class ParallaxPage : MonoBehaviour
{
    [SerializeField, Min(0)] private int index;

    public int Index => index;
}
