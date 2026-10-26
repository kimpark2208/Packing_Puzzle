using System.Collections;
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
    [SerializeField, Tooltip("예약일에 예약자가 왔는데 수납하지 않은 경우에 뜨는 \"달력 화면으로 갈까요?\" 확인 팝업(문구는 팝업에 적어 둔다).")] private ReservationConfirmPopup storePrompt;

    [Header("예약 수락 연출")]
    [SerializeField, Min(0f), Tooltip("예약을 수락한 직후 달력이 열려 예약일을 보여 주는 시간(초). 달력이 열린 뒤 예약일 표시가 톡 하고 나타나고, 이 시간이 지나면 달력이 닫힌다.")] private float acceptPreviewSeconds = 1f;

    private readonly List<GameObject> rows = new();
    private ReservationRecord hiddenRecord; // 수락 연출에서 아직 표시를 안 보여 준 예약(그 예약일 칸은 일반 칸으로 보인다)
    private ReservationRecord selected;
    private bool promptOnly; // 안내 팝업만 보이는 상태(달력 카드는 아직 안 열렸다)
    private bool closed;     // 달력이 닫혀 꺼졌다(PickupStoreFlow가 기다린다)
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
        if (promptOnly)
        {
            card.SetActive(false); // 안내 팝업만 보인다(달력 카드는 팝업에서 수락하면 PickupStoreFlow가 연다)
            return;
        }

        card.SetActive(true); // 닫을 때 카드가 꺼지므로 다시 켠다
        RefreshCells();
    }

    /// <summary>달력과 내용 패널을 작아지거나 내려가며 닫는다.</summary>
    private void Close()
    {
        PanelMotion.Close(detail);
        PanelMotion.Close(card, () =>
        {
            gameObject.SetActive(false);
            closed = true;
        });
    }

    /// <summary>
    /// 예약일에 예약자가 찾아왔는데 필요한 꽃은 가지고 있으면서 수납하지 않은 경우: 안내 팝업("예약 물품을 수납하지 않았습니다. 달력 화면으로 갈까요?")만 띄운다.
    /// 거절하면 팝업이 닫히고 went(false). 수락하면 달력 화면이 열리고(이 예약이 골라진 채 보관 패널이 열린다) 플레이어가 달력을 닫을 때까지 기다린 뒤 went(true).
    /// 부른 쪽(MainUI)은 이 코루틴이 끝나면 준비 상태를 다시 본다.
    /// </summary>
    public IEnumerator PickupStoreFlow(ReservationRecord record, System.Action<bool> went)
    {
        promptOnly = true;
        gameObject.SetActive(true);
        bool? choice = null;
        storePrompt.Open(null, accepted => choice = accepted);
        while (!choice.HasValue) yield return null;

        promptOnly = false;
        if (!choice.Value)
        {
            yield return GameTime.Wait(0.35f); // 팝업이 닫히는 동안
            gameObject.SetActive(false);
            went(false);
            yield break;
        }

        closed = false;
        card.SetActive(true); // 달력 화면으로 넘어간다
        RefreshCells();
        selected = record;
        RefreshDetail();
        detail.SetActive(true);
        while (!closed) yield return null;
        went(true);
    }

    /// <summary>
    /// 예약을 수락한 직후의 연출: 달력이 열리고, 잠시 뒤 예약일 칸에 예약 표시가 톡 하고 나타나며, acceptPreviewSeconds가 지나면 달력이 닫힌다.
    /// 부른 쪽(ReservationCall)이 이 코루틴이 끝날 때까지 기다린다(그동안 손님이 오지 않고 시간이 멈춰 있다).
    /// </summary>
    public IEnumerator PlayAcceptedPreview(ReservationRecord record)
    {
        hiddenRecord = record; // 열리는 동안은 이 예약일 칸이 아직 일반 칸이다
        gameObject.SetActive(true);
        PanelMotion motion = card.GetComponent<PanelMotion>();
        float moveSeconds = motion != null ? motion.Seconds : 0f;
        yield return GameTime.Wait(moveSeconds + 0.1f);

        hiddenRecord = null;
        RefreshCells(); // 예약일 칸이 예약이 있는 칸의 모습으로 바뀐다
        int index = record.DueDay - 1 - monthStart;
        Transform cell = index >= 0 && index < grid.childCount ? grid.GetChild(index) : null; // 예약일이 다음 달이면 칸이 없다
        float popSeconds = Mathf.Min(0.35f, acceptPreviewSeconds * 0.5f);
        for (float t = 0f; cell != null && t < popSeconds; t += Time.unscaledDeltaTime)
        {
            float u = t / popSeconds;
            cell.localScale = Vector3.one * (1f + 0.35f * (1f - u) * (1f - u)); // 커졌다가 제 크기로 돌아온다
            yield return null;
        }
        if (cell != null) cell.localScale = Vector3.one;
        yield return GameTime.Wait(Mathf.Max(0f, acceptPreviewSeconds - popSeconds));

        if (gameObject.activeSelf) Close();
        yield return GameTime.Wait(moveSeconds);
        hiddenRecord = null;
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
            if (record != null && record == hiddenRecord) record = null;
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
            else count.text = Currency.HasItem(item) ? "보유" : $"<color={Red}>미보유</color>"; // 포장지와 꾸미기 용품은 개수를 따지지 않고 보유 여부만(임시)
        }
    }

    private void OnCancelled()
    {
        selected = null;
        PanelMotion.Close(detail);
        RefreshCells(); // 취소한 예약일 칸이 아무 일 없는 칸으로 돌아간다
    }
}
