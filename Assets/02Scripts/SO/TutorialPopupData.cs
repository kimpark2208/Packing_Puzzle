using System;
using UnityEngine;

/// <summary>
/// 팝업형 튜토리얼 하나(베타 기획서 04 튜토리얼 이미지): 제목, 제목 아이콘, 쪽(그림 + 설명)들.
/// 언제 보여 줄지는 TutorialLessons의 PopupTriggers가 id로 정한다. 그림과 문구는 이 에셋에서 고친다.
/// </summary>
[CreateAssetMenu(fileName = "TutorialPopup", menuName = "Puzzle/TutorialPopupData", order = 9)]
public class TutorialPopupData : ScriptableObject
{
    [Serializable]
    public struct PageData
    {
        [Tooltip("그림 상자에 들어가는 그림(없으면 빈 상자).")] public Sprite image;
        [TextArea(2, 4), Tooltip("설명 글. TMP 리치 텍스트(<b>굵게</b>, <color=#C8501E>강조</color>)를 쓸 수 있다.")] public string text;
    }

    [Tooltip("TutorialLessons.PopupTriggers의 id와 같게. 같은 id는 한 번만 보인다.")] public string id;
    [Tooltip("팝업 맨 위 제목.")] public string title;
    [Tooltip("제목 앞의 아이콘(선택).")] public Sprite icon;
    [Tooltip("쪽들(◀ ▶로 넘긴다).")] public PageData[] pages = Array.Empty<PageData>();

    /// <summary>TutorialPopup이 쓰는 쪽 모양으로.</summary>
    public TutorialPopup.Page[] ToPages()
    {
        var result = new TutorialPopup.Page[pages.Length];
        for (int i = 0; i < pages.Length; i++) result[i] = new TutorialPopup.Page(pages[i].image, pages[i].text);
        return result;
    }
}
