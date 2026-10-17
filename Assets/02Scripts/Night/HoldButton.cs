using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>꾹 누르고 있는 버튼. 누르고 있는 동안 IsHeld가 true이고, 떼거나 손가락이 버튼 밖으로 나가면 Released가 한 번 일어난다.</summary>
public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public bool IsHeld { get; private set; }

    public event Action Released;

    public void OnPointerDown(PointerEventData eventData) => IsHeld = true;

    public void OnPointerUp(PointerEventData eventData) => Release();

    public void OnPointerExit(PointerEventData eventData) => Release();

    private void OnDisable() => IsHeld = false;

    private void Release()
    {
        if (!IsHeld) return;

        IsHeld = false;
        Released?.Invoke();
    }
}
