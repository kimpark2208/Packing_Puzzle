using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 트레이의 꽃 조각의 드래그 입력. 드래그해서 DayPuzzleController의 알맞은 태그 슬롯에 놓는다.
/// 태그가 다르면 반려(원래 자리로 복귀)한다. 바구니의 꽃은 보관 칸(storeTarget)에 놓아 맡길 수도 있다. 조각의 모양은 FlowerPieceView가 맡는다.
/// 드래그 중 꽃(아이콘)은 손가락 위쪽에 떠서 따라오고, 그 꽃이 겹친 칸이 강조되며 놓으면 그 칸에 들어간다
/// (손가락이 놓일 칸을 가리지 않도록).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class BlockDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("이 조각의 꽃 데이터")]
    public FlowerData blockData;

    public event Action<GameObject> OnPlaced;

    /// <summary>플레이어가 꽃을 집었다(드래그 시작). 컬러휠이 그 꽃의 색을 가리킬 때 쓴다.</summary>
    public static event Action<FlowerData> Grabbed;

    /// <summary>이 꽃을 맡길 수 있는 보관 칸(바구니의 꽃에만 GachaManager가 정해 준다). null이면 맡길 수 없다.</summary>
    public FlowerStorageSlot storeTarget;

    /// <summary>보드가 아니라 보관 칸에 놓았을 때 발행. 조각은 받는 쪽이 보관 칸으로 옮긴다(여기서 없애지 않는다).</summary>
    public event Action<GameObject> OnStored;

    private const float LiftPixels = 150f; // 드래그 중 꽃이 손가락보다 위에 뜨는 거리(캔버스 단위)

    private RectTransform rectTransform;
    private RectTransform iconRect; // 칸과의 겹침을 재는 꽃 그림(FlowerImage). 없으면 조각 전체
    private CanvasGroup canvasGroup;
    private Canvas rootCanvas;

    private Transform originalParent;
    private Vector2 originalAnchoredPos;
    private Vector3 iconOffsetWorld; // 조각 기준점에서 꽃 그림 중심까지의 월드 거리

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        iconRect = GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == "FlowerImage") ?? rectTransform;

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
        iconOffsetWorld = iconRect.TransformPoint(iconRect.rect.center) - rectTransform.position;

        transform.SetParent(rootCanvas.transform, true);
        canvasGroup.blocksRaycasts = false;
        FollowPointer(eventData);
        Grabbed?.Invoke(blockData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (rootCanvas == null) return;
        FollowPointer(eventData);

        DayPuzzleController.Instance?.PreviewHover(blockData, IconScreenRect(eventData.pressEventCamera), eventData.pressEventCamera);
        storeTarget?.SetHover(CanStore(eventData));
    }

    private bool CanStore(PointerEventData eventData)
    {
        return storeTarget != null && storeTarget.IsEmpty && storeTarget.IsOver(IconScreenRect(eventData.pressEventCamera).center, eventData.pressEventCamera);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        bool placed = DayPuzzleController.Instance != null &&
            DayPuzzleController.Instance.TryPlaceAtScreenRect(blockData, IconScreenRect(eventData.pressEventCamera), eventData.pressEventCamera);

        DayPuzzleController.Instance?.ClearHoverPreview();
        storeTarget?.SetHover(false);

        if (placed)
        {
            OnPlaced?.Invoke(gameObject);
            Destroy(gameObject);
            return;
        }

        if (CanStore(eventData))
        {
            OnStored?.Invoke(gameObject);
            return;
        }

        // 자리가 없으면 원래 자리로 반려.
        transform.SetParent(originalParent, true);
        rectTransform.anchoredPosition = originalAnchoredPos;
    }

    /// <summary>꽃 그림 중심이 손가락 위치에서 LiftPixels만큼 위에 오도록 조각을 옮긴다(화면 밖으로는 나가지 않게).</summary>
    private void FollowPointer(PointerEventData eventData)
    {
        Vector2 lifted = eventData.position + Vector2.up * (LiftPixels * rootCanvas.scaleFactor);
        lifted.y = Mathf.Min(lifted.y, Screen.height);

        RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)rootCanvas.transform, lifted, eventData.pressEventCamera, out Vector3 world);
        rectTransform.position = world - iconOffsetWorld;
    }

    /// <summary>지금 꽃 그림이 화면에서 차지하는 사각형. 이 사각형과 겹친 칸이 강조되고 놓인다.</summary>
    private Rect IconScreenRect(Camera eventCamera)
    {
        var corners = new Vector3[4];
        iconRect.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
