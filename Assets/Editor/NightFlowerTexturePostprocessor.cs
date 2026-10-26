using UnityEditor;

/// <summary>
/// 밤 퍼즐 꽃의 꽃잎 그림(Assets/03Images/NightFlower/*/Petal_*.png)은 읽을 수 있게 가져온다.
/// 세로형 꽃의 줄기 꺾임이 꽃송이 그림의 가운데(SpriteBounds.ArtCenter)를 알아야 하는데, 텍스처를 읽을 수 없으면 가운데를 못 구해 꺾인 꽃송이가 줄기에서 떨어져 나간다.
/// 새 꽃 그림을 넣을 때 따로 설정하지 않아도 되게 가져올 때마다 켠다.
/// </summary>
public class NightFlowerTexturePostprocessor : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/03Images/NightFlower/") || !System.IO.Path.GetFileName(assetPath).StartsWith("Petal")) return;

        ((TextureImporter)assetImporter).isReadable = true;
    }
}
