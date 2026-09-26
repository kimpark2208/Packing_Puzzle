using System;
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

    private Transform originalParent;
    private Vector2 originalAnchoredPos;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        icon = GetComponent<Image>();
        if (icon == null) icon = gameObject.AddComponent<Image>();
        icon.preserveAspect = true;

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
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        bool placed = WrapperBoardController.Instance != null &&
            WrapperBoardController.Instance.TryPlaceAtScreenPoint(blockData, eventData.position, eventData.pressEventCamera);

        if (placed)
        {
            OnPlaced?.Invoke(gameObject);
            Destroy(gameObject);
            return;
        }

        // 태그가 안 맞거나 빈 슬롯이 없으면 원래 자리로 반려.
        transform.SetParent(originalParent, true);
        rectTransform.anchoredPosition = originalAnchoredPos;
    }
}
