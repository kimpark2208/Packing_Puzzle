using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BlockView가 참조하는 폴백 스프라이트 제공자.
/// 실제로는 BlockData.blockImage/flowerWholeImage(디자인이 채워 넣은 통짜 이미지)를 우선 사용하고,
/// 아직 채워지지 않았을 때만 이 클래스가 절차적으로 임시 이미지를 만들어 대신 보여준다.
///   - 블록 모드 폴백: BlockTextureUtil로 만든 단색 실루엣(생성기와 동일한 스타일)
///   - 꽃 모드 폴백: 점유 칸마다 BlockData.flowerIcon을 찍어넣은 콜라주 (추후 실제 꽃다발 이미지로 교체 예정)
/// </summary>
public static class BlockSpriteBaker
{
    private const int ReferenceCellPx = BlockTextureUtil.ReferenceCellPx;

    private static readonly Dictionary<(BlockData, bool), Sprite> cache = new();

    public static Sprite Bake(BlockData data, bool flowerMode)
    {
        if (data == null || data.shapeGrid == null || data.shapeGrid.Length == 0) return null;

        var key = (data, flowerMode);
        if (cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

        Texture2D tex = flowerMode ? BuildFlowerCollage(data) : BlockTextureUtil.CreateSolidBlockTexture(data);
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        cache[key] = sprite;
        return sprite;
    }

    private static Texture2D BuildFlowerCollage(BlockData data)
    {
        int rows = data.shapeGrid.Length;
        int cols = data.shapeGrid[0].cols.Length;
        int texW = cols * ReferenceCellPx;
        int texH = rows * ReferenceCellPx;

        var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var clear = new Color32(0, 0, 0, 0);
        var pixels = new Color32[texW * texH];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        tex.SetPixels32(pixels);

        Texture2D iconTex = data.flowerIcon != null ? data.flowerIcon.texture : null;
        int pad = Mathf.RoundToInt(ReferenceCellPx * 0.06f);
        int cellInner = ReferenceCellPx - pad * 2;

        for (int r = 0; r < rows; r++)
        {
            bool[] row = data.shapeGrid[r].cols;
            if (row == null) continue;

            for (int c = 0; c < row.Length; c++)
            {
                if (!row[c]) continue;

                int px0 = c * ReferenceCellPx + pad;
                int py0 = (rows - 1 - r) * ReferenceCellPx + pad;

                if (iconTex != null) StampIcon(tex, iconTex, px0, py0, cellInner, cellInner);
            }
        }

        tex.Apply();
        return tex;
    }

    private static void StampIcon(Texture2D tex, Texture2D icon, int x0, int y0, int w, int h)
    {
        for (int y = 0; y < h; y++)
        {
            float v = (float)y / h;
            for (int x = 0; x < w; x++)
            {
                float u = (float)x / w;
                tex.SetPixel(x0 + x, y0 + y, icon.GetPixelBilinear(u, v));
            }
        }
    }
}
