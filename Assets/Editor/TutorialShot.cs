using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 튜토리얼 팝업 그림을 만드는 에디터 도구: 게임 화면 캡처(PNG) 한 장에서 필요한 부분을 잘라 팝업 그림 상자 비율에 맞춰 Assets/03Images/Tutorial/에 스프라이트로 저장한다.
/// 위치는 캡처 전체에 대한 비율(0~1, 왼쪽 위가 원점)로 준다. 자른 부분이 상자 비율과 다르면 가운데를 지켜 넓히거나 줄인다(늘리거나 찌그러뜨리지 않는다).
/// 손가락 그림(hand)을 원하는 곳(자른 그림 안의 비율 위치, 손끝이 기준)에 겹쳐 "누르세요" 그림도 만들 수 있다.
/// </summary>
public static class TutorialShot
{
    public const string Folder = "Assets/03Images/Tutorial";
    public const int OutWidth = 880, OutHeight = 800; // 팝업 그림 상자(440x400)의 두 배

    /// <summary>
    /// 캡처에서 정사각형으로 잘라 저장한다(팝업 그림 상자가 정사각형이라 찌그러뜨리지 않으려고). 중심(centerU, centerV)과 한 변(sideV, 캡처 높이에 대한 비율)을 정한다. 화면 밖으로 나가면 안으로 밀어 넣는다.
    /// 손가락은 캡처 전체에 대한 위치(handU, handV: 손끝이 닿는 곳, 왼쪽 위가 원점)로 준다.
    /// </summary>
    public static string Square(string sourcePng, float centerU, float centerV, float sideV, string name, float handU = -1f, float handV = -1f, float handHeightRatio = 0.3f)
    {
        var probe = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        probe.LoadImage(File.ReadAllBytes(sourcePng));
        float sw = probe.width, sh = probe.height;
        Object.DestroyImmediate(probe);

        float side = Mathf.Min(sideV * sh, Mathf.Min(sw, sh));
        float x0 = Mathf.Clamp(centerU * sw - side * 0.5f, 0f, sw - side);
        float top = Mathf.Clamp(centerV * sh - side * 0.5f, 0f, sh - side); // 위에서부터의 픽셀
        float u0 = x0 / sw, u1 = (x0 + side) / sw, v0 = top / sh, v1 = (top + side) / sh;

        float relU = -1f, relV = -1f;
        if (handU >= 0f)
        {
            relU = (handU * sw - x0) / side;
            relV = (handV * sh - top) / side;
        }
        return Crop(sourcePng, u0, v0, u1, v1, name, relU, relV, handHeightRatio, 1f);
    }

    /// <returns>저장한 에셋 경로.</returns>
    public static string Crop(string sourcePng, float u0, float v0, float u1, float v1, string name, float handU = -1f, float handV = -1f, float handHeightRatio = 0.45f, float aspect = 0f)
    {
        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        src.LoadImage(File.ReadAllBytes(sourcePng));
        float sw = src.width, sh = src.height;

        // 픽셀 사각형(왼쪽 아래가 원점이라 v를 뒤집는다)
        float x0 = u0 * sw, x1 = u1 * sw, y0 = (1f - v1) * sh, y1 = (1f - v0) * sh;
        float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f, w = x1 - x0, h = y1 - y0;
        if (aspect > 0f) // 비율을 정했으면 가운데 기준으로 맞춘다(화면 밖으로 나가면 줄인다)
        {
            if (w / h < aspect) w = h * aspect; else h = w / aspect;
            if (w > sw) { w = sw; h = w / aspect; }
            if (h > sh) { h = sh; w = h * aspect; }
        }
        w = Mathf.Min(w, sw); h = Mathf.Min(h, sh);
        x0 = Mathf.Clamp(cx - w * 0.5f, 0f, sw - w); y0 = Mathf.Clamp(cy - h * 0.5f, 0f, sh - h);

        // 출력 크기: 잘라낸 픽셀 크기 그대로(너무 크면 줄인다)
        float scale = Mathf.Min(1f, 800f / Mathf.Max(w, h));
        int outW = Mathf.Max(2, Mathf.RoundToInt(w * scale)), outH = Mathf.Max(2, Mathf.RoundToInt(h * scale));
        var rt = RenderTexture.GetTemporary(outW, outH, 0, RenderTextureFormat.ARGB32);
        src.filterMode = FilterMode.Bilinear;
        Graphics.Blit(src, rt, new Vector2(w / sw, h / sh), new Vector2(x0 / sw, y0 / sh));

        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var result = new Texture2D(outW, outH, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, outW, outH), 0, 0);
        result.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        if (handU >= 0f) DrawHand(result, handU, handV, handHeightRatio);

        Directory.CreateDirectory(Folder);
        string path = $"{Folder}/{name}.png";
        File.WriteAllBytes(path, result.EncodeToPNG());
        Object.DestroyImmediate(src);
        Object.DestroyImmediate(result);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return path;
    }

    /// <summary>튜토리얼 배너의 손가락 그림(손끝이 위)을 손끝이 (u, v)에 오도록 겹친다.</summary>
    private static void DrawHand(Texture2D target, float u, float v, float heightRatio)
    {
        Sprite hand = null;
        foreach (string guid in AssetDatabase.FindAssets("KakaoTalk_20261004_214716091_01 t:Sprite"))
        {
            hand = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
            if (hand != null) break;
        }
        if (hand == null) return;

        float handH = target.height * heightRatio, handW = handH * hand.rect.width / hand.rect.height;
        var rt = RenderTexture.GetTemporary(Mathf.RoundToInt(handW), Mathf.RoundToInt(handH), 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(hand.texture, rt);
        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var handTex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        handTex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        handTex.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        int left = Mathf.RoundToInt(u * target.width - handW * 0.5f);
        int top = Mathf.RoundToInt((1f - v) * target.height); // 손끝(그림의 위쪽 가운데)이 이 높이에 온다
        for (int y = 0; y < handTex.height; y++)
        {
            int ty = top - 1 - y;
            if (ty < 0 || ty >= target.height) continue;
            for (int x = 0; x < handTex.width; x++)
            {
                int tx = left + x;
                if (tx < 0 || tx >= target.width) continue;
                Color over = handTex.GetPixel(x, handTex.height - 1 - y);
                if (over.a <= 0f) continue;
                Color under = target.GetPixel(tx, ty);
                target.SetPixel(tx, ty, Color.Lerp(under, over, over.a));
            }
        }
        target.Apply();
        Object.DestroyImmediate(handTex);
    }
}
