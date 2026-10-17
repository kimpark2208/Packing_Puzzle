using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전등의 켜짐/꺼짐 모습: 꺼지면 꺼진 전등 그림에 불빛(빛 셰이더)이 없고, 켜지면 켜진 전등 그림과 불빛이 보인다.
/// 글로벌 UI(LanternArea)에 붙어 있고, 전등을 켜는 쪽(메인 화면, 밤 퍼즐 화면)이 SetLit으로 모습을 정한다. 기본은 꺼진 모습이다.
/// </summary>
public class LanternLight : MonoBehaviour
{
    [SerializeField, Tooltip("전등 그림을 보여 주는 이미지(전등 토글의 이미지).")] private Image lantern;
    [SerializeField] private Sprite unlitSprite;
    [SerializeField] private Sprite litSprite;
    [SerializeField, Tooltip("켜졌을 때만 보이는 불빛(빛 셰이더가 붙은 오브젝트).")] private GameObject glow;

    public void SetLit(bool lit)
    {
        lantern.sprite = lit ? litSprite : unlitSprite;
        glow.SetActive(lit);
    }
}
