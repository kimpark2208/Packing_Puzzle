using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 모든 BlockData의 "배치 중" 통짜 이미지를 코드로 생성해 실제 PNG 파일로 저장하는 에디터 툴.
/// Assets/03Images/Block의 기존 I_Three.png 등과 같은 스타일(단색 칸 + 검은 테두리/격자선)을 따른다.
/// 블록 모양/색이 바뀌면 메뉴에서 다시 실행해 재생성하면 된다.
/// </summary>
public static class BlockImageGenerator
{
    private const string OutputFolder = "Assets/03Images/Block/Generated";

    [MenuItem("Tools/Blocks/Generate Block Images")]
    public static void GenerateAll()
    {
        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.CreateFolder("Assets/03Images/Block", "Generated");
        }

        string[] guids = AssetDatabase.FindAssets("t:BlockData", new[] { "Assets/05SO/Block" });
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<BlockData>(path);
            if (data == null || data.shapeGrid == null || data.shapeGrid.Length == 0) continue;

            GenerateForBlock(data);
            count++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[BlockImageGenerator] 블록 이미지 {count}개 생성 및 할당 완료 ({OutputFolder})");
    }

    private static void GenerateForBlock(BlockData data)
    {
        Texture2D tex = BlockTextureUtil.CreateSolidBlockTexture(data);
        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        string fileName = $"Block_{data.blockID:00}_{data.name}.png";
        string assetPath = $"{OutputFolder}/{fileName}";
        File.WriteAllBytes(assetPath, png);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        data.blockImage = sprite;
        EditorUtility.SetDirty(data);
    }
}
