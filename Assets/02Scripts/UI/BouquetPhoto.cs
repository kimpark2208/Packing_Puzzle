using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 결과 화면의 포토카드 사진 틀에 완성한 꽃다발(꾸미기 포함)을 보여 준다.
/// 꽃다발 이미지(WrapperImage: 포장지·꽃·풀·리본·후광)와 카드/장식이 놓인 레이어를 있는 그대로 복제해 틀 안에 맞춰 넣는다(기울어진 모습도 그대로).
/// 칸 표시(SlotArea)와 되돌리기/다시하기 버튼은 사진에 넣지 않고, 복제본은 클릭을 받지 않는다. 배경이 투명이라 사진 틀의 바탕색이 비친다.
/// </summary>
public class BouquetPhoto : MonoBehaviour
{
    [SerializeField] private RectTransform wrapperImage;    // 꽃다발(PuzzleRoot 아래)
    [SerializeField] private RectTransform decorationLayer; // 카드/장식이 놓인 레이어(PuzzleRoot 아래)
    [SerializeField, Range(0.5f, 1f)] private float fill = 0.9f; // 사진 틀 안에서 꽃다발이 차지하는 비율

    private static readonly string[] HiddenParts = { "SlotArea", "UndoBTN", "RetryBTN" };

    private RectTransform photoRoot;

    /// <summary>지금 꽃다발을 찍는다(이전 사진은 지운다).</summary>
    public void Take()
    {
        if (wrapperImage == null) return;
        if (photoRoot != null) Destroy(photoRoot.gameObject);

        // 원본과 같은 크기의 판을 만들고 그 안에 원본과 같은 자리로 복제한다
        var source = (RectTransform)wrapperImage.parent;
        photoRoot = new GameObject("PhotoRoot", typeof(RectTransform)).GetComponent<RectTransform>();
        photoRoot.SetParent(transform, false);
        photoRoot.anchorMin = photoRoot.anchorMax = photoRoot.pivot = new Vector2(0.5f, 0.5f);
        photoRoot.sizeDelta = source.rect.size;
        photoRoot.anchoredPosition = Vector2.zero;

        RectTransform bouquet = Instantiate(wrapperImage, photoRoot);
        foreach (string part in HiddenParts)
        {
            Transform hidden = bouquet.Find(part);
            if (hidden == null) continue;
            hidden.gameObject.SetActive(false); // Destroy는 프레임 끝에 되므로, 크기를 재기 전에 먼저 끈다
            Destroy(hidden.gameObject);
        }

        RectTransform decorations = decorationLayer != null ? Instantiate(decorationLayer, photoRoot) : null;
        if (decorations != null) Destroy(decorations.GetComponent<DecorationPlacer>()); // 복제본은 꾸미기 동작이 필요 없다

        foreach (PlacedDecoration placed in photoRoot.GetComponentsInChildren<PlacedDecoration>(true)) Destroy(placed);
        foreach (Graphic graphic in photoRoot.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;

        // 꽃다발과 놓인 용품이 모두 들어오는 영역을 재서, 사진 틀 가운데에 맞춘다
        // (결과 화면이 아직 꺼져 있을 때 찍으므로, 켜져 있어야만 제대로 재 주는 RectTransformUtility.CalculateRelativeRectTransformBounds는 쓰지 않는다)
        Bounds bounds = default;
        bool any = false;
        Measure(bouquet, ref bounds, ref any);
        if (decorations != null) Measure(decorations, ref bounds, ref any, skipRoot: true);
        if (!any || bounds.size.x <= 0f || bounds.size.y <= 0f) return;

        Rect frame = ((RectTransform)transform).rect;
        float scale = fill * Mathf.Min(frame.width / bounds.size.x, frame.height / bounds.size.y);
        photoRoot.localScale = new Vector3(scale, scale, 1f);
        photoRoot.anchoredPosition = -(Vector2)bounds.center * scale;
    }

    /// <summary>root 아래에서 자기 자신이 켜져 있는 사각형들이 photoRoot 좌표에서 차지하는 영역을 bounds에 넓혀 담는다(any: bounds에 이미 담긴 것이 있는가).
    /// skipRoot면 root 자신(화면 전체 크기의 레이어)은 빼고 그 자식들만 잰다.</summary>
    private void Measure(RectTransform root, ref Bounds bounds, ref bool any, bool skipRoot = false)
    {
        foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
        {
            if (skipRoot && rect == root) continue;
            if (!EnabledWithin(rect, root)) continue;

            // 월드 좌표는 쓰지 않는다: 꺼져 있는 결과 캔버스는 배율이 0이라 월드 좌표가 한 점으로 뭉친다. photoRoot 안의 로컬 변환만 쌓아서 구한다.
            Matrix4x4 toPhoto = Matrix4x4.identity;
            for (Transform t = rect; t != photoRoot; t = t.parent) toPhoto = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale) * toPhoto;

            Rect r = rect.rect;
            foreach (Vector2 corner in new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin) })
            {
                Vector3 local = toPhoto.MultiplyPoint3x4(corner);
                if (any) bounds.Encapsulate(local);
                else bounds = new Bounds(local, Vector3.zero);
                any = true;
            }
        }
    }

    /// <summary>rect부터 root까지 모두 켜져 있는가(위쪽 결과 화면이 꺼져 있는 것은 따지지 않는다).</summary>
    private static bool EnabledWithin(Transform rect, Transform root)
    {
        for (Transform t = rect; t != null; t = t.parent)
        {
            if (!t.gameObject.activeSelf) return false;
            if (t == root) return true;
        }
        return true;
    }
}
