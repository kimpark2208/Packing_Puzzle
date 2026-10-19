using UnityEngine;

/// <summary>스프라이트 그림의 위치를 재는 도구. 같은 캔버스 안에서 제자리에 그려진 조각 그림(꽃잎 등)의 중심을 알아야 할 때 쓴다.</summary>
public static class SpriteBounds
{
    /// <summary>스프라이트가 그려진 불투명한 부분의 중심 위치. 스프라이트 사각형(원래 캔버스)의 중심을 (0, 0)으로 한 픽셀 단위이고 위쪽이 +y다.
    /// 임포트가 그림 사각형을 잘라 낸 스프라이트는 잘라 낸 사각형의 중심을 쓰고, 잘라 내지 않은 스프라이트(FullRect)는 텍스처를 읽어 알파 0.1 초과인 부분의 중심을 쓴다
    /// (텍스처를 읽을 수 없게 임포트했으면 (0, 0)).</summary>
    public static Vector2 ArtCenter(Sprite sprite)
    {
        if (sprite == null) return Vector2.zero;

        Rect rect = sprite.rect;
        Rect art = sprite.textureRect;
        bool trimmed = art.size.x < rect.size.x - 0.5f || art.size.y < rect.size.y - 0.5f;
        if (trimmed) return sprite.textureRectOffset + art.size * 0.5f - rect.size * 0.5f;

        Texture2D texture = sprite.texture;
        if (!texture.isReadable) return Vector2.zero;

        int x0 = Mathf.RoundToInt(rect.x), y0 = Mathf.RoundToInt(rect.y), width = Mathf.RoundToInt(rect.width), height = Mathf.RoundToInt(rect.height);
        Color32[] pixels = texture.GetPixels32();
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (pixels[(y0 + y) * texture.width + x0 + x].a <= 25) continue;

                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }
        return maxX < 0 ? Vector2.zero : new Vector2((minX + maxX + 1) * 0.5f - width * 0.5f, (minY + maxY + 1) * 0.5f - height * 0.5f);
    }
}
