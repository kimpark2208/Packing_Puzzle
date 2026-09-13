using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 밤 퍼즐 그리드의 셀 하나.
/// NightCell(빈 오브젝트, 패딩용) 의 자식인 CellFilled 이미지를 이 스크립트가 제어한다.
/// </summary>
public class NightCellView : MonoBehaviour
{
    public enum CellVisualState
    {
        Empty,          // 아무것도 그려지지 않음
        PreviewValid,   // 드래그 중, 목표 범위 안 (작게 표시)
        PreviewInvalid, // 드래그 중, 목표 범위 밖 (작게+붉게 표시)
        Confirmed       // 손을 뗀 뒤 확정된 상태 (정상 크기)
    }

    [Header("References")]
    [SerializeField] private RectTransform filledRect; // CellFilled의 RectTransform
    [SerializeField] private Image filledImage;         // CellFilled의 Image

    [Header("Visual Settings")]
    [SerializeField] private float previewScale = 0.55f;
    [SerializeField] private float confirmedScale = 1f;
    [SerializeField] private Color validPreviewColor = new Color(1f, 1f, 1f, 0.55f);
    [SerializeField] private Color invalidPreviewColor = new Color(1f, 0.3f, 0.3f, 0.55f);
    [SerializeField] private Color confirmedColorDefault = Color.white;

    [Header("Pop Motion")]
    [SerializeField] private float popDuration = 0.12f;
    [SerializeField] private float popOvershoot = 1.08f;

    public Vector2Int Coord { get; private set; }
    public CellVisualState State { get; private set; } = CellVisualState.Empty;
    public bool IsWall { get; private set; }

    private Sprite pendingFlowerSprite;
    private Color pendingFlowerColor;
    private Coroutine popRoutine;

    public void Initialize(Vector2Int coord)
    {
        Coord = coord;
        IsWall = false;
        SetEmpty();
    }

    public void SetAsWall(Sprite wallSprite)
    {
        IsWall = true;
        State = CellVisualState.Confirmed;

        filledImage.sprite = wallSprite;
        filledImage.color = confirmedColorDefault;
        filledImage.enabled = true;

        filledRect.localScale = Vector3.one * confirmedScale;
    }

    public void SetEmpty()
    {
        if (IsWall) return;

        State = CellVisualState.Empty;

        filledImage.enabled = false;
        filledRect.localScale = Vector3.one * previewScale;
    }

    public void ShowPreview(bool isValid, Sprite flowerSprite, Color flowerColor)
    {
        if (IsWall) return;
        if (State == CellVisualState.Confirmed) return;

        pendingFlowerSprite = flowerSprite;
        pendingFlowerColor = flowerColor;

        State = isValid ? CellVisualState.PreviewValid : CellVisualState.PreviewInvalid;

        filledImage.sprite = flowerSprite;
        filledImage.color = isValid ? validPreviewColor : invalidPreviewColor;
        filledImage.enabled = true;

        filledRect.localScale = Vector3.one * previewScale;
    }

    public void CancelPreview()
    {
        if (IsWall) return;
        if (State == CellVisualState.Confirmed) return;

        SetEmpty();
    }

    public void Confirm()
    {
        if (IsWall) return;

        State = CellVisualState.Confirmed;

        filledImage.sprite = pendingFlowerSprite;
        filledImage.color = confirmedColorDefault;
        filledImage.enabled = true;

        if (popRoutine != null)
        {
            StopCoroutine(popRoutine);
        }

        popRoutine = StartCoroutine(PopRoutine());
    }

    public void ResetToEmpty()
    {
        if (IsWall) return;

        if (popRoutine != null)
        {
            StopCoroutine(popRoutine);
            popRoutine = null;
        }

        SetEmpty();
    }

    private System.Collections.IEnumerator PopRoutine()
    {
        float t = 0f;

        while (t < popDuration)
        {
            t += Time.deltaTime;
            float progress = t / popDuration;

            float scale = progress < 0.6f
                ? Mathf.Lerp(previewScale, confirmedScale * popOvershoot, progress / 0.6f)
                : Mathf.Lerp(confirmedScale * popOvershoot, confirmedScale, (progress - 0.6f) / 0.4f);

            filledRect.localScale = Vector3.one * scale;
            yield return null;
        }

        filledRect.localScale = Vector3.one * confirmedScale;
    }
}
