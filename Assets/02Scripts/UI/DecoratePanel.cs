using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 꾸미기 창(낮 퍼즐 화면 왼쪽): 탭 2개("꽃다발 꾸미기"/"꽃다발 모양"), 항목 칩 4개, 카드 그리드. 탭과 칩은 씬에 미리 배치되어 있고,
/// 카드는 꾸미기 용품(DecorationData)마다 카드 프리팹을 복제해 그리드(GridLayoutGroup이 있는 Content)에 채운다.
/// - 꽃다발 꾸미기 탭: 칩이 가리키는 항목(리본/풀/카드/장식)의 카드. 카드를 누르면 그 용품이 꽃다발의 기본 자리에 놓이고, 놓인 용품은 꽃다발 위에서 끌어서 옮긴다(DecorationPlacer).
/// - 꽃다발 모양 탭: 포장지 항목의 카드. 누르면 꽃다발 이미지가 그 포장지 그림으로 바뀐다.
/// </summary>
public class DecoratePanel : MonoBehaviour
{
    private static readonly Color TabOn = new(0.96f, 0.95f, 0.93f), TabOff = new(0.85f, 0.84f, 0.82f);
    private static readonly Color ChipOn = new(0.45f, 0.45f, 0.45f), ChipOff = new(1f, 1f, 1f);
    private static readonly Color CardOn = new(0.6f, 0.6f, 0.6f), CardOff = new(1f, 1f, 1f);
    private static readonly Color TextDark = new(0.25f, 0.25f, 0.25f);
    private static readonly Color RequiredGlow = new(1f, 0.85f, 0.35f); // 아직 놓지 않은 예약 필수 용품의 칩과 카드

    [SerializeField] private Button[] tabs;
    [SerializeField] private Button[] chips;
    [SerializeField] private DecorationCategory[] chipCategories; // chips와 같은 순서: 각 칩이 보여 줄 항목
    [SerializeField] private DecorationData[] decorations;        // 꾸미기 용품 전부(탭/칩마다 자기 항목만 걸러서 보여 준다)
    [SerializeField] private Button cardPrefab;
    [SerializeField] private DecorationPlacer placer;

    [Header("꽃다발 꾸미기 탭(0번): 이 탭일 때만 켜지는 오브젝트들과 카드가 채워지는 그리드")]
    [SerializeField] private GameObject[] decorateTab;
    [SerializeField] private Transform decorateCardParent;
    [Header("꽃다발 모양 탭(1번)")]
    [SerializeField] private GameObject shapeTab;
    [SerializeField] private Transform shapeCardParent;

    private readonly List<Button> cards = new();
    private readonly List<DecorationData> shown = new(); // cards와 같은 순서의 용품
    private int tab;
    private int chip;

    private void Awake()
    {
        Hook(tabs, i =>
        {
            tab = i;
            SpawnCards();
        });
        Hook(chips, i =>
        {
            chip = i;
            SpawnCards();
        });
        if (placer != null) placer.Removed += Refresh;
        SpawnCards();
    }

    private void OnDestroy()
    {
        if (placer != null) placer.Removed -= Refresh;
    }

    /// <summary>고른 탭은 선택 상태(초록색)로 남긴다. 카드나 칩을 누르면 선택이 그쪽으로 옮겨 가 풀리므로 탭으로 되돌려 놓는다.</summary>
    private void LateUpdate()
    {
        EventSystem events = EventSystem.current;
        if (events != null && tab < tabs.Length && events.currentSelectedGameObject != tabs[tab].gameObject) events.SetSelectedGameObject(tabs[tab].gameObject);
    }

    /// <summary>지금 탭(과 칩)에 해당하는 카드로 다시 채운다.</summary>
    private void SpawnCards()
    {
        bool shape = tab == 1;
        foreach (GameObject body in decorateTab) body.SetActive(!shape);
        if (shapeTab != null) shapeTab.SetActive(shape);

        foreach (Button old in cards)
        {
            old.gameObject.SetActive(false); // Destroy는 프레임 끝에 되므로, 그 전에 그리드 배치에서 빠지게 끈다
            Destroy(old.gameObject);
        }
        cards.Clear();
        shown.Clear();

        Transform parent = shape ? shapeCardParent : decorateCardParent;
        if (cardPrefab == null || parent == null)
        {
            Refresh();
            return;
        }
        foreach (Transform sample in parent) Destroy(sample.gameObject); // 에디터에 배치만 해 둔 견본 카드는 실행하면 치운다

        DecorationCategory category = shape ? DecorationCategory.Wrapper : chipCategories[Mathf.Min(chip, chipCategories.Length - 1)];
        foreach (DecorationData data in decorations.Where(d => d != null && d.category == category && IsOwned(d)).OrderBy(d => d.id)) // 가지고 있는 용품만 보여 준다
        {
            DecorationData item = data;
            Button created = Instantiate(cardPrefab, parent);
            created.name = $"Card_{item.name}";
            created.transform.Find("Name").GetComponent<TMP_Text>().text = item.displayName;

            var picture = created.transform.Find("Image").GetComponent<Image>();
            picture.sprite = item.sprite;
            picture.preserveAspect = true; // 카드 칸이 달라도 그림의 가로세로 비율을 지킨다
            picture.raycastTarget = false;

            created.onClick.AddListener(() =>
            {
                if (placer == null) return;
                if (item.category == DecorationCategory.Wrapper) placer.SelectShape(item); // 꽃다발 모양: 포장지 그림으로 바꾼다
                else placer.Place(item);                                                  // 나머지: 기본 자리에 놓는다(그 뒤엔 꽃다발 위에서 끌어 옮긴다)
                Refresh();
            });
            cards.Add(created);
            shown.Add(item);
        }
        Refresh();
    }

    /// <summary>플레이어가 이 용품을 가지고 있는가(카탈로그에서 샀거나 선물로 받았는가). 씬 단독 실행처럼 보유 정보가 없으면 모두 가진 것으로 본다.</summary>
    private static bool IsOwned(DecorationData data) => CurrencyManager.Instance == null || CurrencyManager.Instance.GetDecorationStock(data.id) > 0;

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

    /// <summary>예약 꽃다발을 만드는 중이고, 이 용품이 아직 꽃다발에 놓지 않은 예약 필수 용품인가(놓는 것을 까먹지 않게 빛나게 보여 준다).</summary>
    private bool IsMissingRequiredSupply(DecorationData data)
    {
        GameManager game = GameManager.Instance;
        if (game == null || !game.Crafting) return false;

        return game.Craft.Supplies().Contains(data) && (placer == null || !placer.Contains(data));
    }

    private bool HasMissingRequiredSupply(DecorationCategory category)
    {
        GameManager game = GameManager.Instance;
        return game != null && game.Crafting && game.Craft.Supplies().Any(s => s.category == category && IsMissingRequiredSupply(s));
    }

    private void Refresh()
    {
        for (int i = 0; i < tabs.Length; i++) Paint(tabs[i], i == tab ? TabOn : TabOff, TextDark);
        for (int i = 0; i < chips.Length; i++)
        {
            bool glow = i != chip && HasMissingRequiredSupply(chipCategories[Mathf.Min(i, chipCategories.Length - 1)]);
            Paint(chips[i], glow ? RequiredGlow : i == chip ? ChipOn : ChipOff, i == chip ? Color.white : TextDark);
        }
        for (int i = 0; i < cards.Count; i++)
        {
            bool picked = placer != null && (shown[i].category == DecorationCategory.Wrapper ? placer.IsShapeSelected(shown[i]) : shown[i].category == DecorationCategory.Ribbon && placer.IsPlaced(shown[i])); // 지금 모양인 포장지와 꽃다발에 놓인 리본의 카드만 어둡게
            Paint(cards[i], IsMissingRequiredSupply(shown[i]) ? RequiredGlow : picked ? CardOn : CardOff, TextDark);
            cards[i].transform.Find("Image").GetComponent<Image>().color = picked ? CardOn : Color.white; // 그림도 같이 어둡게
            Outline border = cards[i].GetComponent<Outline>();
            if (border != null) border.effectColor = picked ? new Color(0.35f, 0.35f, 0.35f) : new Color(0.8f, 0.8f, 0.8f);
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
