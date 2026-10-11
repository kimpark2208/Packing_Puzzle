using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 꾸미기 창(낮 퍼즐 화면 왼쪽): 탭 2개, 항목 칩 3개, 카드 그리드. 탭과 칩은 씬에 미리 배치되어 있고,
/// 카드는 카드 프리팹을 필요한 개수만큼 복제해 그리드(GridLayoutGroup이 있는 Content)에 채운다.
/// 지금은 보이는 동작만 한다: 탭/칩/카드를 누르면 선택 표시가 바뀌고, 탭을 바꾸면 카드 이름이 바뀐다. 카드의 실제 기능은 아직 없다.
/// </summary>
public class DecoratePanel : MonoBehaviour
{
    private static readonly Color TabOn = new(0.96f, 0.95f, 0.93f), TabOff = new(0.85f, 0.84f, 0.82f);
    private static readonly Color ChipOn = new(0.45f, 0.45f, 0.45f), ChipOff = new(1f, 1f, 1f);
    private static readonly Color CardOn = new(0.92f, 0.92f, 0.9f), CardOff = new(1f, 1f, 1f);
    private static readonly Color TextDark = new(0.25f, 0.25f, 0.25f);
    private static readonly string[] CardNames = { "이름이름이름", "모양모양모양" }; // 탭마다 카드 이름이 달라 보이게 하는 임시 문구

    [SerializeField] private Button[] tabs;
    [SerializeField] private Button[] chips;
    [SerializeField] private Button cardPrefab;
    [SerializeField] private int cardCount = 12; // 지금은 임시 개수. 꾸미기 용품 데이터가 연결되면 그 개수로 만든다

    private readonly List<Button> cards = new();
    private Transform cardParent; // 카드가 채워지는 그리드(GridLayoutGroup이 있는 자식)
    private int tab;
    private int chip;
    private int card = -1; // 고른 카드(없으면 -1)

    private void Awake()
    {
        Hook(tabs, i => tab = i);
        Hook(chips, i => chip = i);
        SpawnCards(cardCount);
        Refresh();
    }

    /// <summary>카드를 count개 만든다(이미 있던 카드는 치우고 다시 채운다).</summary>
    private void SpawnCards(int count)
    {
        foreach (Button old in cards) Destroy(old.gameObject);
        cards.Clear();
        card = -1;

        if (cardPrefab == null) return;
        if (cardParent == null) cardParent = GetComponentInChildren<GridLayoutGroup>(true).transform;

        for (int i = 0; i < count; i++)
        {
            int index = i;
            Button created = Instantiate(cardPrefab, cardParent);
            created.name = $"Card_{i + 1}";
            created.onClick.AddListener(() =>
            {
                card = card == index ? -1 : index; // 고른 카드를 다시 누르면 해제
                Refresh();
            });
            cards.Add(created);
        }
    }

    private void Hook(Button[] buttons, System.Action<int> select)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            buttons[i].onClick.AddListener(() =>
            {
                select(index);
                Refresh();
            });
        }
    }

    private void Refresh()
    {
        for (int i = 0; i < tabs.Length; i++) Paint(tabs[i], i == tab ? TabOn : TabOff, TextDark);
        for (int i = 0; i < chips.Length; i++) Paint(chips[i], i == chip ? ChipOn : ChipOff, i == chip ? Color.white : TextDark);
        for (int i = 0; i < cards.Count; i++)
        {
            Paint(cards[i], i == card ? CardOn : CardOff, TextDark);
            Outline border = cards[i].GetComponent<Outline>();
            if (border != null) border.effectColor = i == card ? new Color(0.35f, 0.35f, 0.35f) : new Color(0.8f, 0.8f, 0.8f);

            Transform nameLabel = cards[i].transform.Find("Name");
            if (nameLabel != null) nameLabel.GetComponent<TMP_Text>().text = CardNames[Mathf.Min(tab, CardNames.Length - 1)];
        }
    }

    /// <summary>버튼 배경색을 칠하고, 안의 첫 글자(TMP)를 지정한 색으로 바꾼다. 카드는 이름 글자만 바꾼다.</summary>
    private static void Paint(Button button, Color background, Color text)
    {
        ((Image)button.targetGraphic).color = background;
        TMP_Text label = button.transform.Find("Name") != null ? button.transform.Find("Name").GetComponent<TMP_Text>() : button.GetComponentInChildren<TMP_Text>();
        if (label != null) label.color = text;
    }
}
