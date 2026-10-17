using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>끌어서 옮길 수 있는 꽃잎 하나(UI 이미지). 잡으면 맨 앞으로 올라오고, 움직이면 Moved로, 놓으면 Released로 알린다. 놓은 뒤에 어떻게 할지는 이 꽃잎을 쓰는 퍼즐이 정한다.</summary>
[RequireComponent(typeof(Image))]
public class PetalDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public event Action<PetalDrag> Moved;
    public event Action<PetalDrag> Released;

    /// <summary>true면 끌 수 없다(붙었거나 제자리로 돌아가는 중).</summary>
    public bool Locked { get; set; }

    public Image Image => image != null ? image : image = GetComponent<Image>();
    public RectTransform Rect => (RectTransform)transform;

    private Image image;
    private bool dragging;
    private Vector3 grabOffset; // 잡은 곳과 꽃잎 위치의 차이(잡은 자리 그대로 따라오게)

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (Locked) return;

        dragging = true;
        grabOffset = Rect.position - PointerWorld(eventData);
        Rect.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging) return;

        Rect.position = PointerWorld(eventData) + grabOffset;
        Moved?.Invoke(this);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging) return;

        dragging = false;
        Released?.Invoke(this);
    }

    private void OnDisable() => dragging = false;

    private Vector3 PointerWorld(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)Rect.parent, eventData.position, eventData.pressEventCamera, out Vector3 world);
        return world;
    }
}
