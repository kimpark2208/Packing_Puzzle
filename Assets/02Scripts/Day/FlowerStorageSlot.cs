using UnityEngine;

/// <summary>
/// 바구니 옆의 보관 칸(1개, 사용 횟수 제한 없음). 바구니의 꽃 하나를 끌어다 맡겨 두었다가, 나중에 보드로 끌어와 놓을 수 있다.
/// 칸 자리와 모양은 씬에 미리 둔 오브젝트이고, 이 컴포넌트는 맡긴 꽃 조각을 들고 있기만 한다(맡기고 꺼내는 흐름은 GachaManager가 정한다).
/// </summary>
public class FlowerStorageSlot : MonoBehaviour
{
    [Tooltip("맡긴 꽃 조각이 놓이는 자리. 비워 두면 이 오브젝트 자신이다")]
    [SerializeField] private RectTransform holder;

    private GameObject stored;

    public bool IsEmpty => stored == null;

    /// <summary>화면 좌표가 이 칸 위인가(드래그하던 꽃을 놓은 자리가 보관 칸인지 판정한다).</summary>
    public bool IsOver(Vector2 screenPoint, Camera eventCamera)
    {
        return RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, screenPoint, eventCamera);
    }

    /// <summary>꽃 조각을 칸 가운데에 맡는다.</summary>
    public void Hold(GameObject block)
    {
        stored = block;
        var rect = (RectTransform)block.transform;
        rect.SetParent(holder != null ? holder : (RectTransform)transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    /// <summary>맡았던 꽃이 보드에 놓여 칸이 비었다(조각은 놓은 쪽이 없앤다).</summary>
    public void Release()
    {
        stored = null;
    }

    /// <summary>퍼즐을 처음부터 다시 할 때: 맡은 꽃을 치운다.</summary>
    public void Clear()
    {
        if (stored != null) Destroy(stored);
        stored = null;
    }
}
