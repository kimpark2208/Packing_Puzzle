using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 팝업형 튜토리얼 창(베타 기획서 04 튜토리얼 이미지): 제목(아이콘 + 글씨)과 닫기 버튼, 그림 상자, 설명 글, ◀ n/N ▶ 쪽 넘기기.
/// 페이지가 여러 장이면 ◀ ▶로 넘기고, 닫기 버튼은 언제든 누를 수 있다. 떠 있는 동안은 하루/기분/밤 시간이 멈춘다(배너형 TutorialOverlay와 같다).
/// 같은 id는 실행 중 한 번만 보여 준다(저장하지 않는다). 무엇을 언제 보여 줄지는 부르는 쪽(TutorialLessons)이 정하고, 이 컴포넌트는 보여 주기만 한다.
/// 글로벌 UI 캔버스에 미리 배치된 프리팹(TutorialPopup)을 쓴다. 안내가 없을 때는 Content가 꺼져 있다.
/// </summary>
public class TutorialPopup : MonoBehaviour
{
    /// <summary>팝업의 한 쪽: 그림(없으면 빈 상자)과 설명 글(TMP 리치 텍스트).</summary>
    public readonly struct Page
    {
        public readonly Sprite image;
        public readonly string text;

        public Page(Sprite image, string text)
        {
            this.image = image;
            this.text = text;
        }
    }

    private static readonly HashSet<string> seen = new();

    public static TutorialPopup Instance { get; private set; }

    /// <summary>팝업이 떠 있는 동안 true. 닫힌 뒤에 이어서 할 일은 WaitWhile(() => TutorialPopup.IsShowing)로 기다린다.</summary>
    public static bool IsShowing { get; private set; }

    [SerializeField] private GameObject content;   // 켜고 끄는 영역(막 + 카드)
    [SerializeField] private GameObject card;      // 카드(PanelMotion이 붙은 몸통). 열 때 중앙에서 커지고, 닫을 때 사라진다
    [SerializeField] private Image iconImage;      // 제목 앞의 아이콘(없으면 숨긴다)
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Image pageImage;      // 그림 상자 안의 그림
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text pageNumberText;

    private Page[] pages = Array.Empty<Page>();
    private int index;
    private string currentId; // 지금 떠 있는 안내의 id

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        seen.Clear();
        IsShowing = false;
        Instance = null;
    }

    private void Awake()
    {
        Instance = this;
        content.SetActive(false);
        closeButton.onClick.AddListener(Close);
        previousButton.onClick.AddListener(() => Go(index - 1));
        nextButton.onClick.AddListener(() => Go(index + 1));
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (IsShowing)
        {
            seen.Remove(currentId); // 읽기 전에 씬이 바뀌어 사라진 안내는 다음에 다시 보여 준다
            IsShowing = false;
            if (GameClock.Instance != null) GameClock.Instance.TutorialHold = false;
        }
    }

    public static bool HasSeen(string id) => seen.Contains(id);

    /// <summary>
    /// 팝업을 연다. 같은 id는 한 번만 보여 준다. 이미 다른 팝업이 떠 있거나 배너형 안내가 떠 있으면 아무것도 하지 않는다(id도 쓴 것으로 치지 않으니 나중에 다시 부르면 된다).
    /// 열렸으면 true.
    /// </summary>
    public static bool Play(string id, string title, Sprite icon, params Page[] pages)
    {
        if (Instance == null || pages.Length == 0 || IsShowing || TutorialOverlay.IsShowing || HasSeen(id)) return false;

        seen.Add(id);
        Instance.currentId = id;
        Instance.Open(title, icon, pages);
        return true;
    }

    /// <summary>데이터 에셋(TutorialPopupData)의 내용으로 연다. id는 에셋의 id.</summary>
    public static bool Play(TutorialPopupData data) => Play(data.id, data.title, data.icon, data.ToPages());

    private void Open(string title, Sprite icon, Page[] newPages)
    {
        pages = newPages;
        titleText.text = title;
        iconImage.sprite = icon;
        iconImage.gameObject.SetActive(icon != null);

        IsShowing = true;
        if (GameClock.Instance != null) GameClock.Instance.TutorialHold = true;

        card.SetActive(true); // 닫을 때 카드가 꺼지므로 다시 켠다
        content.SetActive(true);
        Go(0);
    }

    /// <summary>index번째 쪽을 보인다. 첫 쪽에서 ◀, 마지막 쪽에서 ▶는 눌리지 않는다.</summary>
    private void Go(int target)
    {
        index = Mathf.Clamp(target, 0, pages.Length - 1);
        Page page = pages[index];

        pageImage.sprite = page.image;
        pageImage.enabled = page.image != null;
        descriptionText.text = page.text;
        pageNumberText.text = $"{index + 1}/{pages.Length}";
        previousButton.interactable = index > 0;
        nextButton.interactable = index < pages.Length - 1;
    }

    private void Close()
    {
        if (!IsShowing) return; // 닫히는 모션 중에 또 눌렀다

        IsShowing = false;
        if (GameClock.Instance != null) GameClock.Instance.TutorialHold = false;
        PanelMotion.Close(card, () => { if (!IsShowing) content.SetActive(false); }); // 닫히는 동안 새 팝업이 열렸으면 그대로 둔다
    }
}
