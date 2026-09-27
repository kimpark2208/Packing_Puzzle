using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 트레이의 꽃 조각. 드래그해서 WrapperBoardController의 알맞은 태그 슬롯에 놓는다.
/// 태그가 다르면 반려(원래 자리로 복귀)한다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class BlockDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("이 조각의 꽃 데이터")]
    public BlockData blockData;

    public event Action<GameObject> OnPlaced;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Canvas rootCanvas;
    private Image icon;
    private Image colorMarker;
    private TMP_Text nameText;

    private Transform originalParent;
    private Vector2 originalAnchoredPos;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // "FlowerImage" 자식이 있으면(배경 틀 + 꽃 아이콘이 분리된 새 트레이 템플릿) 그쪽에 적용,
        // 없으면(옛 단일 이미지 프리팹) 자기 자신의 Image를 그대로 쓴다.
        Transform flowerImageT = transform.Find("FlowerImage");
        icon = flowerImageT != null ? flowerImageT.GetComponent<Image>() : GetComponent<Image>();
        if (icon == null) icon = gameObject.AddComponent<Image>();
        icon.preserveAspect = true;

        Transform colorMarkerT = transform.Find("ColorMarker_temp");
        colorMarker = colorMarkerT != null ? colorMarkerT.GetComponent<Image>() : null;

        Transform nameT = transform.Find("NameBack/FlowerName");
        nameText = nameT != null ? nameT.GetComponent<TMP_Text>() : null;

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            Debug.LogWarning($"[{name}] 부모 계층에 Canvas가 없습니다.");
            return;
        }
        rootCanvas = parentCanvas.rootCanvas;
    }

    private void Start()
    {
        if (blockData != null) ApplyVisual();
    }

    private void ApplyVisual()
    {
        icon.sprite = blockData.flowerIcon != null ? blockData.flowerIcon : blockData.blockImage;
        icon.color = ColorPalette.ToUnityColor(blockData.color);

        if (colorMarker != null) colorMarker.color = ColorPalette.ToUnityColor(blockData.color);
        if (nameText != null) nameText.text = blockData.flowerName;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (rootCanvas == null) return;

        originalParent = transform.parent;
        originalAnchoredPos = rectTransform.anchoredPosition;

        transform.SetParent(rootCanvas.transform, true);
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (rootCanvas == null) return;
        rectTransform.anchoredPosition += eventData.delta / rootCanvas.scaleFactor;

        WrapperBoardController.Instance?.PreviewHover(eventData.position, eventData.pressEventCamera);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        bool placed = WrapperBoardController.Instance != null &&
            WrapperBoardController.Instance.TryPlaceAtScreenPoint(blockData, eventData.position, eventData.pressEventCamera);

        WrapperBoardController.Instance?.ClearHoverPreview();

        if (placed)
        {
            OnPlaced?.Invoke(gameObject);
            Destroy(gameObject);
            return;
        }

        // 자리가 없으면 원래 자리로 반려.
        transform.SetParent(originalParent, true);
        rectTransform.anchoredPosition = originalAnchoredPos;
    }
}
