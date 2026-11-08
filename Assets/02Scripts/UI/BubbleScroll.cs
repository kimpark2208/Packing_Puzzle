using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 말풍선에 대사가 길면 스크롤바로 볼 수 있게 한다. 말풍선(ScrollRect) 안에서 대사(TMP_Text)가 바뀌면 맨 위로 되돌린다.
/// 대사 글자가 말풍선보다 길어지는 만큼 대사 칸이 늘어나는 것은 ContentSizeFitter가 한다.
/// </summary>
[RequireComponent(typeof(ScrollRect))]
public class BubbleScroll : MonoBehaviour
{
    [SerializeField] private TMP_Text text;

    private ScrollRect scroll;
    private string lastText;

    private void Awake() => scroll = GetComponent<ScrollRect>();

    private void LateUpdate()
    {
        if (text == null || text.text == lastText) return;

        lastText = text.text;
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 1f;
    }
}
