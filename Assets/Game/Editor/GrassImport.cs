using UnityEditor;
using UnityEngine;

/// 잔디 그림 임포트 설정 — 픽셀아트라 **점 필터**여야 하고, 한 장으로 묶으려면
/// **읽기 가능**이어야 한다 (`Texture2D.PackTextures` 가 원본 픽셀을 읽는다).
public static class GrassImport
{
    [MenuItem("Tools/토이라기/㉣ 잔디 그림 설정 맞추기", priority = 33)]
    public static void Run()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Game/Resources/grass" });
        int n = 0;
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var im = AssetImporter.GetAtPath(path) as TextureImporter;
            if (im == null) continue;

            im.textureType = TextureImporterType.Default;
            im.isReadable = true;                    // 아틀라스로 묶으려면 필요
            im.alphaIsTransparency = true;
            im.mipmapEnabled = true;                 // 멀어질 때 지글거리지 않게
            im.filterMode = FilterMode.Point;        // 픽셀아트
            im.wrapMode = TextureWrapMode.Clamp;
            im.textureCompression = TextureImporterCompression.Uncompressed;  // 알파가 뭉개지면 안 된다
            im.SaveAndReimport();
            n++;
        }
        Debug.Log($"[잔디] 그림 {n}장 설정 완료 (읽기 가능 · 점 필터 · 무압축)");
    }
}
