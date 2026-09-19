using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 밤 메인 화면: 오늘 밤 우선적으로 손질(획득)하고 싶은 꽃을 요청한다.
/// 확인을 누르면 GameFlowController가 요청 꽃 기반의 메인 스테이지 + 잉여 스테이지 큐를 생성한다.
/// 목록 항목은 하이라키에 미리 배치된 템플릿을 복제해서 만든다.
/// </summary>
public class NightRequestUI : MonoBehaviour
{
    private static readonly Color ItemColor = new(0.25f, 0.22f, 0.35f);
    private static readonly Color SelectedColor = new(0.55f, 0.45f, 0.85f);

    [SerializeField] private RectTransform listArea;
    [SerializeField] private RectTransform itemTemplate;
    [SerializeField] private Button confirmButton;

    private readonly HashSet<int> selected = new();
    private readonly Dictionary<int, Image> itemImages = new();

    private void Start()
    {
        itemTemplate.gameObject.SetActive(false);

        var obtainedIds = CurrencyManager.Instance.GetObtainedFlowerIds();
        foreach (int id in obtainedIds)
        {
            CreateFlowerItem(id);
        }

        confirmButton.onClick.AddListener(OnConfirm);
    }

    private void CreateFlowerItem(int flowerId)
    {
        RectTransform itemRT = Instantiate(itemTemplate, listArea);
        itemRT.gameObject.SetActive(true);
        itemRT.name = $"Flower_{flowerId}";

        var img = itemRT.GetComponent<Image>();
        img.color = ItemColor;
        itemImages[flowerId] = img;

        itemRT.GetComponentInChildren<Text>().text = $"꽃 #{flowerId}";
        itemRT.GetComponent<Button>().onClick.AddListener(() => ToggleSelect(flowerId));
    }

    private void ToggleSelect(int flowerId)
    {
        if (selected.Contains(flowerId))
        {
            selected.Remove(flowerId);
            itemImages[flowerId].color = ItemColor;
        }
        else
        {
            selected.Add(flowerId);
            itemImages[flowerId].color = SelectedColor;
        }
    }

    private void OnConfirm()
    {
        GameFlowController.Instance.ConfirmNightRequests(new List<int>(selected));
    }
}
