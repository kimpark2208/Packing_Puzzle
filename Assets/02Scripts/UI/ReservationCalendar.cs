using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 달력 화면(전화기 &gt; 예약확인). 한 달을 칸으로 보여 주고, 오늘과 예약일 칸을 칸의 색으로 구분한다.
/// 예약일 칸을 누르면 오른쪽에 그 예약의 내용 패널이 나온다: 남은 기한, 항목마다 보관한 수(+버튼은 보관 팝업), 보상금, 예약 취소(취소 확인 팝업).
/// 틀(칸, 패널, 항목 줄 견본, 팝업)은 에디터에 만들어 두고, 코드는 글씨와 항목 줄만 채운다. 달의 길이는 칸의 수이고, 일차 N일이 곧 달력의 날짜다(칸이 31개면 1~31일차가 한 달).
/// 칸(Grid의 자식)은 Frame/Inner 안에 Day(글씨), Today(오늘 표시), Tag(예약 이름 표시, 안에 Text)를 가진다.
/// 칸의 모습은 앞의 세 칸을 견본으로 삼는다: 첫 칸 = 예약이 있는 날, 둘째 칸 = 오늘, 셋째 칸 = 아무 일 없는 날(Inner의 색과 Day의 글씨 색을 따라 쓴다).
/// </summary>
public class ReservationCalendar : MonoBehaviour
{
    private const string Red = "#D8453A";

    [SerializeField, Tooltip("달력 카드(PanelMotion이 붙은 몸통). 막은 제외한다.")] private GameObject card;
    [SerializeField] private Button closeButton;
    [SerializeField, Tooltip("날짜 칸들(자식 순서대로 첫 칸부터). 앞의 세 칸은 모습 견본이다.")] private Transform grid;

    [Header("예약 내용 패널(예약일 칸을 누르면 나온다)")]
    [SerializeField] private GameObject detail;
    [SerializeField] private TMP_Text deadlineText;
    [SerializeField] private RectTransform rowParent;
    [SerializeField, Tooltip("항목 줄 견본. 안에 Icon(Image), Name, Count(글씨), Bar(BarFill, AddBTN)가 있다.")] private GameObject rowTemplate;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private Button cancelButton;
    [SerializeField] private ReservationStoragePopup storagePopup;
    [SerializeField] private ReservationCancelPopup cancelPopup;

    private readonly List<GameObject> rows = new();
    private ReservationRecord selected;
    private int monthStart; // 이번 달 첫날 바로 앞의 일차(첫 달이면 0)
    private Color reservedInner, reservedDay, todayInner, normalInner, normalDay; // 견본 칸에서 읽어 둔 색

    private static CurrencyManager Currency => CurrencyManager.Instance;

    private static Image InnerOf(Transform cell) => cell.Find("Frame/Inner").GetComponent<Image>();

    private static TMP_Text DayOf(Transform cell) => cell.Find("Frame/Inner/Day").GetComponent<TMP_Text>();

    private void Awake()
    {
        rowTemplate.SetActive(false);
        closeButton.onClick.AddListener(Close);
        cancelButton.onClick.AddListener(() => cancelPopup.Open(selected, OnCancelled));
        reservedInner = InnerOf(grid.GetChild(0)).color;
        reservedDay = DayOf(grid.GetChild(0)).color;
        todayInner = InnerOf(grid.GetChild(1)).color;
        normalInner = InnerOf(grid.GetChild(2)).color;
        normalDay = DayOf(grid.GetChild(2)).color;
        for (int i = 0; i < grid.childCount; i++)
        {
            int index = i;
            grid.GetChild(i).GetComponent<Button>().onClick.AddListener(() => OnCellClicked(index));
        }
    }

    private void OnEnable()
    {
        selected = null;
        detail.SetActive(false);
        card.SetActive(true); // 닫을 때 카드가 꺼지므로 다시 켠다
        RefreshCells();
    }

    /// <summary>달력과 내용 패널을 작아지거나 내려가며 닫는다.</summary>
    private void Close()
    {
        PanelMotion.Close(detail);
        PanelMotion.Close(card, () => gameObject.SetActive(false));
    }

    private int Today => Currency != null ? Currency.CurrentDay : 1;

    private ReservationRecord RecordOn(int day) => Currency == null ? null : Currency.Reservations.FirstOrDefault(r => r.DueDay == day);

    private void RefreshCells()
    {
        int today = Today;
        int daysInMonth = grid.childCount;
        monthStart = (today - 1) / daysInMonth * daysInMonth;

        for (int i = 0; i < daysInMonth; i++)
        {
            Transform cell = grid.GetChild(i);
            int day = monthStart + i + 1;
            ReservationRecord record = RecordOn(day);
            bool isToday = day == today;

            Transform inner = cell.Find("Frame/Inner");
            inner.GetComponent<Image>().color = isToday ? todayInner : record != null ? reservedInner : normalInner;
            TMP_Text dayText = DayOf(cell);
            dayText.text = (i + 1).ToString();
            dayText.color = record != null ? reservedDay : normalDay;
            inner.Find("Today").gameObject.SetActive(isToday);
            Transform tag = inner.Find("Tag");
            tag.gameObject.SetActive(record != null);
            if (record != null) tag.GetComponentInChildren<TMP_Text>().text = record.data.calendarLabel;
        }
    }

    private void OnCellClicked(int index)
    {
        ReservationRecord record = RecordOn(monthStart + index + 1);
        if (record == null) return; // 예약이 없는 칸은 눌러도 아무 일이 없다

        selected = record;
        RefreshDetail();
        if (!detail.activeSelf) detail.SetActive(true); // 오른쪽에서 들어온다(PanelMotion)
    }

    private void RefreshDetail()
    {
        ReservationRecord record = selected;
        int remaining = Currency.RemainingDays(record);
        deadlineText.text = remaining > 0
            ? $"기한:   <color=#E58A2B><size=135%><b>{remaining}일</b></size></color>"
            : "기한:   <color=#E58A2B><size=135%><b>D-DAY</b></size></color>";
        rewardText.text = record.data.reward.ToString("N0");
        cancelButton.interactable = Currency.CanCancel(record); // 예약일 당일에는 취소할 수 없다

        foreach (GameObject row in rows) Destroy(row);
        rows.Clear();
        for (int i = 0; i < record.data.items.Length; i++)
        {
            int index = i;
            ReservationData.Item item = record.data.items[i];
            GameObject row = Instantiate(rowTemplate, rowParent, false);
            row.SetActive(true);
            rows.Add(row);

            row.transform.Find("Name").GetComponent<TMP_Text>().text = item.name;
            var icon = row.transform.Find("Icon").GetComponent<Image>();
            Sprite art = item.ResolveIcon();
            if (art != null) icon.sprite = art;

            TMP_Text count = row.transform.Find("Count").GetComponent<TMP_Text>();
            Transform bar = row.transform.Find("Bar");
            Button add = bar.Find("AddBTN").GetComponent<Button>();
            bool storable = item.Storable;
            bar.gameObject.SetActive(storable);
            add.gameObject.SetActive(storable && record.stored[i] < item.count);
            if (storable)
            {
                count.text = $"{record.stored[i]}/{item.count}";
                var fill = (RectTransform)bar.Find("BarFill");
                fill.anchorMax = new Vector2(Mathf.Clamp01((float)record.stored[i] / item.count), 1f);
                add.interactable = Currency.MaxStorable(record, i) > 0; // 보관할 재고가 없으면 눌러도 소용없다
                add.onClick.AddListener(() => storagePopup.Open(record, index, RefreshDetail));
            }
            else if (item.kind == ReservationData.ItemKind.Bouquet) count.text = $"X{item.count}"; // 꽃다발은 예약 당일에 만들 개수다
            else count.text = $"<color={Red}>미보유</color>";
        }
    }

    private void OnCancelled()
    {
        selected = null;
        PanelMotion.Close(detail);
        RefreshCells(); // 취소한 예약일 칸이 아무 일 없는 칸으로 돌아간다
    }
}
