using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 예약 쇼카드 팝업. 전화를 받으면 뜨고, 기한·필요 항목·보상을 보여 준다. 수락이나 거절을 누르기 전에는 닫을 수 없다(뒤의 투명한 막이 다른 곳의 터치를 막는다).
/// 틀(카드, 제목, 항목 줄 견본, 버튼)은 에디터에 만들어 두고, 코드는 글씨와 항목 줄만 채운다. 항목 줄 견본(RowTemplate)은 실행하면 숨기고 복제해서 쓴다.
/// </summary>
public class ReservationShowCard : MonoBehaviour
{
    private const string Orange = "#E58A2B";

    [SerializeField, Tooltip("카드(PanelMotion이 붙은, 막은 제외한 팝업 몸통). 이 오브젝트가 중앙에서 커지며 나타나고, 닫힐 때 작아지며 사라진다.")] private GameObject card;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text deadlineText;
    [SerializeField] private RectTransform rowParent;
    [SerializeField, Tooltip("항목 줄 견본. 안에 Icon(Image), Name(글씨), Count(글씨)가 있다.")] private GameObject rowTemplate;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private TMP_Text depositText;
    [SerializeField] private TMP_Text totalText;
    [SerializeField] private Button rejectButton;
    [SerializeField] private Button acceptButton;

    private readonly List<GameObject> rows = new();
    private Action<bool> decided;

    private void Awake()
    {
        rowTemplate.SetActive(false);
        rejectButton.onClick.AddListener(() => Decide(false));
        acceptButton.onClick.AddListener(() => Decide(true));
    }

    /// <summary>팝업을 열어 data의 내용을 보여 준다. 수락이면 true, 거절이면 false로 decided를 부르고 닫는다.</summary>
    public void Show(ReservationData data, Action<bool> decided)
    {
        this.decided = decided;
        rejectButton.interactable = !(TutorialOverlay.Instance != null && !TutorialOverlay.HasSeen(TutorialLessons.ReservationCallLesson)); // 처음 걸려 온 예약은 튜토리얼이 수락하게 한다(안내를 한 번 본 뒤에는 거절할 수 있다)

        titleText.text = data.title;
        deadlineText.text = $"기한:   <color={Orange}><size=135%><b>{data.deadlineDays}일</b></size></color>";
        rewardText.text = data.reward.ToString("N0");
        depositText.text = data.deposit.ToString("N0");
        totalText.text = data.Total.ToString("N0");

        foreach (GameObject row in rows) Destroy(row);
        rows.Clear();
        foreach (ReservationData.Item item in data.items)
        {
            GameObject row = Instantiate(rowTemplate, rowParent, false);
            row.SetActive(true);
            row.transform.Find("Name").GetComponent<TMP_Text>().text = item.name;
            bool owned = CurrencyManager.Instance == null || CurrencyManager.Instance.HasItem(item);
            row.transform.Find("Count").GetComponent<TMP_Text>().text = !owned ? "미보유" : item.Storable ? $"X{item.count}" : "보유"; // 꽃은 필요한 수, 포장지와 용품은 보유 여부만(임시)
            var icon = row.transform.Find("Icon").GetComponent<Image>();
            Sprite art = item.ResolveIcon();
            if (art != null) icon.sprite = art;
            rows.Add(row);
        }

        card.SetActive(true); // 닫을 때 카드가 꺼지므로 다시 켠다
        gameObject.SetActive(true);
    }

    private void Decide(bool accepted)
    {
        if (decided == null) return; // 닫히는 모션 중에 또 눌렀다

        PanelMotion.Close(card, () => gameObject.SetActive(false)); // 작아지며 사라진 뒤 막까지 끈다
        Action<bool> callback = decided;
        decided = null;
        callback?.Invoke(accepted);
    }
}
