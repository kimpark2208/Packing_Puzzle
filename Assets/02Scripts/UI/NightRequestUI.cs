using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 밤 메인 화면: 오늘 밤 우선적으로 손질(획득)하고 싶은 꽃을 요청한다.
/// 확인을 누르면 GameFlowController가 요청 꽃 기반의 메인 스테이지 + 잉여 스테이지 큐를 생성한다.
/// </summary>
public class NightRequestUI : MonoBehaviour
{
    private static readonly Color BgColor = new(0.12f, 0.10f, 0.20f);
    private static readonly Color ItemColor = new(0.25f, 0.22f, 0.35f);
    private static readonly Color SelectedColor = new(0.55f, 0.45f, 0.85f);
    private static readonly Color ConfirmColor = new(0.85f, 0.65f, 0.35f);

    private readonly HashSet<int> selected = new();
    private readonly Dictionary<int, Image> itemImages = new();

    private void Start()
    {
        Canvas canvas = UIFactory.EnsureCanvas();
        RectTransform root = UIFactory.CreateFullStretchPanel("NightRequestRoot", canvas.transform, BgColor);

        var header = CreateBox(root, new Vector2(0, 0.88f), new Vector2(1, 1f));
        UIFactory.CreateText(header, "오늘 밤, 어떤 꽃이 부족했나요?", 26, Color.white);

        RectTransform listArea = CreateBox(root, new Vector2(0.05f, 0.2f), new Vector2(0.95f, 0.86f));
        var grid = listArea.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(200, 80);
        grid.spacing = new Vector2(12, 12);
        grid.childAlignment = TextAnchor.UpperCenter;

        var obtainedIds = CurrencyManager.Instance.GetObtainedFlowerIds();
        foreach (int id in obtainedIds)
        {
            CreateFlowerItem(listArea, id);
        }

        Button confirmButton = UIFactory.CreateButton(root, "확인 (요청 없이 진행 가능)", ConfirmColor, Color.black);
        var confirmRect = (RectTransform)confirmButton.transform;
        confirmRect.anchorMin = new Vector2(0.25f, 0.04f);
        confirmRect.anchorMax = new Vector2(0.75f, 0.14f);
        confirmRect.offsetMin = Vector2.zero;
        confirmRect.offsetMax = Vector2.zero;
        confirmButton.onClick.AddListener(OnConfirm);
    }

    private void CreateFlowerItem(Transform parent, int flowerId)
    {
        var go = new GameObject($"Flower_{flowerId}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = ItemColor;
        itemImages[flowerId] = img;

        UIFactory.CreateText(go.transform, $"꽃 #{flowerId}", 20, Color.white);
        go.GetComponent<Button>().onClick.AddListener(() => ToggleSelect(flowerId));
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

    private static RectTransform CreateBox(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject("Box", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }
}
