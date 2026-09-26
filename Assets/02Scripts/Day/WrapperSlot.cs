using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 포장지 템플릿 안의 꽃 영역 하나(태그+면 조합당 1칸). 정해진 태그의 꽃만 받을 수 있고,
/// 필요한 개수(requiredCount)만큼 반복해서 놓을 수 있다(마지막에 놓은 꽃의 아이콘 + "채운 개수/필요 개수" 표시).
/// 하이라키에 미리 배치된 슬롯(9개, 태그 x 면 조합의 최대치)이며, WrapperBoardController가
/// 현재 포장지 레벨에 필요한 것만 활성화하고 requiredCount를 채워 넣는다.
/// </summary>
public class WrapperSlot : MonoBehaviour
{
    public enum Side { Left, Right, None }

    public BlockData.FlowerTag flowerTag;
    public Side side;
    public int requiredCount = 1;

    private readonly List<BlockData> placed = new();

    public IReadOnlyList<BlockData> PlacedFlowers => placed;
    public int FilledCount => placed.Count;
    public bool IsFull => placed.Count >= requiredCount;
    public BlockData LastPlaced => placed.Count > 0 ? placed[^1] : null;

    private Image background;
    private Image icon;
    private TMP_Text countLabel;

    private void Awake()
    {
        background = GetComponent<Image>();

        Transform iconT = transform.Find("Icon");
        if (iconT == null)
        {
            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(transform, false);
            var rt = (RectTransform)iconGO.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(6f, 6f);
            rt.offsetMax = new Vector2(-6f, -6f);
            iconT = iconGO.transform;
        }
        icon = iconT.GetComponent<Image>();
        icon.enabled = false;
        icon.preserveAspect = true;

        Transform labelT = transform.Find("CountLabel");
        if (labelT == null)
        {
            var labelGO = new GameObject("CountLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGO.transform.SetParent(transform, false);
            var rt = (RectTransform)labelGO.transform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(40f, 24f);
            rt.anchoredPosition = new Vector2(-2f, 2f);
            labelT = labelGO.transform;
        }
        countLabel = labelT.GetComponent<TextMeshProUGUI>();
        countLabel.fontSize = 20f;
        countLabel.alignment = TextAlignmentOptions.BottomRight;
        countLabel.color = Color.black;
    }

    public void SetEmptyVisual(Color tagColor)
    {
        if (background != null) background.color = tagColor;
        if (icon != null) icon.enabled = false;
        RefreshCountLabel();
    }

    /// <summary>드래그해온 꽃이 이 슬롯에 들어갈 수 있는지 확인.</summary>
    public bool CanAccept(BlockData flower)
    {
        return !IsFull && flower != null && flower.flowerTag == flowerTag;
    }

    public void Place(BlockData flower)
    {
        placed.Add(flower);
        if (icon != null)
        {
            icon.sprite = flower.flowerIcon != null ? flower.flowerIcon : flower.blockImage;
            icon.color = ColorPalette.ToUnityColor(flower.color);
            icon.enabled = true;
        }
        RefreshCountLabel();
    }

    public void Clear()
    {
        placed.Clear();
        if (icon != null) icon.enabled = false;
        RefreshCountLabel();
    }

    private void RefreshCountLabel()
    {
        if (countLabel == null) return;
        countLabel.text = requiredCount > 1 ? $"{FilledCount}/{requiredCount}" : "";
    }
}
