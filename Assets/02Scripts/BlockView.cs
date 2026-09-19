using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 블록(=꽃) 하나를 셀 단위로 쪼개지 않고, 통짜 스프라이트 하나(Image 하나)로 표시한다.
///   - 블록 모드(배치 중): BlockData.blockImage (BlockImageGenerator가 미리 만들어 둔 PNG)
///   - 꽃 모드(완성 후): BlockData.flowerWholeImage (추후 직접 제작해 채워 넣을 슬롯)
/// 둘 중 아직 비어있는 슬롯이 있으면 BlockSpriteBaker가 임시로 대신 그려준다.
/// 회전/반전은 이 컴포넌트가 붙은 RectTransform 자체를 돌리는 방식(BlockDrag)에 맡긴다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class BlockView : MonoBehaviour
{
    private Image image;
    private BlockData data;
    private bool flowerMode;

    private void Awake()
    {
        image = GetComponent<Image>();
        if (image == null)
        {
            image = gameObject.AddComponent<Image>();
        }
        image.raycastTarget = true;
    }

    public void Build(BlockData blockData, float cellSize)
    {
        if (image == null) Awake();

        data = blockData;
        flowerMode = false;

        BlockRow[] shape = blockData.shapeGrid;
        if (shape == null || shape.Length == 0) return;

        int rowsCount = shape.Length;
        int colsCount = shape[0].cols.Length;

        RectTransform rt = (RectTransform)transform;
        rt.sizeDelta = new Vector2(colsCount * cellSize, rowsCount * cellSize);

        ApplyVisual();
    }

    /// <summary>true면 완성 후 꽃다발 이미지, false면 배치 중 블록 이미지로 표시.</summary>
    public void SetFlowerMode(bool showFlower)
    {
        flowerMode = showFlower;
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        if (data == null || image == null) return;

        Sprite sprite = flowerMode
            ? (data.flowerWholeImage != null ? data.flowerWholeImage : BlockSpriteBaker.Bake(data, true))
            : (data.blockImage != null ? data.blockImage : BlockSpriteBaker.Bake(data, false));

        image.sprite = sprite;
        image.color = Color.white;
    }

    /// <summary>겹침 경고 등으로 색을 강제로 덮어씌운다. null을 넘기면 원래 색으로 되돌린다.</summary>
    public void SetTintOverride(Color? overrideColor)
    {
        if (image == null) return;
        image.color = overrideColor ?? Color.white;
    }
}
