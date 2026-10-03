using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 트레이의 꽃 조각의 드래그 입력. 드래그해서 WrapperBoardController의 알맞은 태그 슬롯에 놓는다.
/// 태그가 다르면 반려(원래 자리로 복귀)한다. 조각의 모양은 FlowerPieceView가 맡는다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class BlockDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("이 조각의 꽃 데이터")]
    public FlowerData blockData;

    public event Action<GameObject> OnPlaced;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Canvas rootCanvas;

    private Transform originalParent;
    private Vector2 originalAnchoredPos;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            Debug.LogWarning($"[{name}] 부모 계층에 Canvas가 없습니다.");
            return;
        }
        rootCanvas = parentCanvas.rootCanvas;
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

        WrapperBoardController.Instance?.PreviewHover(blockData, eventData.position, eventData.pressEventCamera);
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
