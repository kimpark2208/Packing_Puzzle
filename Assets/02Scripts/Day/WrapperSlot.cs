using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 포장지 템플릿 안의 꽃 슬롯 하나. 정해진 태그의 꽃만 받을 수 있다.
/// WrapperBoardController가 런타임에 절차적으로 생성하고 배치한다.
/// </summary>
public class WrapperSlot : MonoBehaviour
{
    public enum Side { Left, Right, None }

    [HideInInspector] public BlockData.FlowerTag flowerTag;
    [HideInInspector] public Side side;
    [HideInInspector] public int ringIndex;   // 태그 영역(라인/매스/품/필러) 순서
    [HideInInspector] public int orderInRing; // 같은 영역 안에서 배치 순서 (색 조합 인접 판정용)

    public BlockData PlacedFlower { get; private set; }

    private Image background;
    private Image icon;

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
    }

    public void SetEmptyVisual(Color tagColor)
    {
        if (background != null) background.color = tagColor;
        if (icon != null) icon.enabled = false;
    }

    /// <summary>드래그해온 꽃이 이 슬롯에 들어갈 수 있는지 확인.</summary>
    public bool CanAccept(BlockData flower)
    {
        return PlacedFlower == null && flower != null && flower.flowerTag == flowerTag;
    }

    public void Place(BlockData flower)
    {
        PlacedFlower = flower;
        if (icon != null)
        {
            icon.sprite = flower.flowerIcon != null ? flower.flowerIcon : flower.blockImage;
            icon.color = ColorPalette.ToUnityColor(flower.color);
            icon.enabled = true;
        }
    }

    public void Clear()
    {
        PlacedFlower = null;
        if (icon != null) icon.enabled = false;
    }
}
