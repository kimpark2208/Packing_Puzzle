using UnityEngine;

/// <summary>
/// BlockData.shapeGrid로부터 "칸을 색으로 채우고 검은 테두리/격자선을 그리는" 스타일의 텍스처를 만든다.
/// Assets/03Images/Block의 기존 I_Three.png / O_Four.png / T_Four.png와 같은 스타일을 코드로 재현한 것.
/// BlockImageGenerator(에디터 툴, 실제 PNG 파일로 저장)와 BlockSpriteBaker(런타임 폴백)가 공유한다.
/// </summary>
public static class BlockTextureUtil
{
    public const int ReferenceCellPx = 128;
    private const float BorderRatio = 0.05f;

    public static Texture2D CreateSolidBlockTexture(BlockData data)
    {
        BlockRow[] shape = data.shapeGrid;
        int rows = shape.Length;
        int cols = shape[0].cols.Length;
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

        Color32 fill = ColorPalette.ToUnityColor(data.color);
        Color32 border = new Color32(25, 25, 25, 255);
        int borderPx = Mathf.Max(2, Mathf.RoundToInt(ReferenceCellPx * BorderRatio));

        for (int r = 0; r < rows; r++)
        {
            bool[] row = shape[r].cols;
            if (row == null) continue;

            for (int c = 0; c < row.Length; c++)
            {
                if (!row[c]) continue;

                int px0 = c * ReferenceCellPx;
                int py0 = (rows - 1 - r) * ReferenceCellPx; // 텍스처 y=0이 아래쪽이라 행을 뒤집어 배치

                for (int y = 0; y < ReferenceCellPx; y++)
                {
                    bool edgeY = y < borderPx || y >= ReferenceCellPx - borderPx;
                    for (int x = 0; x < ReferenceCellPx; x++)
                    {
                        bool edgeX = x < borderPx || x >= ReferenceCellPx - borderPx;
                        tex.SetPixel(px0 + x, py0 + y, (edgeX || edgeY) ? border : fill);
                    }
                }
            }
        }

        tex.Apply();
        return tex;
    }
}
