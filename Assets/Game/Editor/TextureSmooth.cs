using UnityEditor;
using UnityEngine;

/// 텍스처 밉맵 켜기 — **멀리서 볼 때 지글거리는 것을 막는다.**
///
/// ★왜 (2026-08-04): 저해상도 픽셀 화면에서는 큰 텍스처가 몇 픽셀로 줄어든다.
///   밉맵이 없으면 그때마다 **엉뚱한 텍셀 하나**를 집어 오므로 카메라가 조금만
///   움직여도 색이 확확 바뀐다. 밉맵은 미리 줄여 둔 그림을 쓰므로 이게 사라진다.
///
/// 모델을 새로 넣을 때마다 한 번씩 돌리면 된다.
public static class TextureSmooth
{
    [MenuItem("Tools/토이라기/모델 텍스처 밉맵 켜기", priority = 32)]
    public static void Run()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[]
        {
            "Assets/Game/Models", "Assets/Game/Resources"
        });

        int 고침 = 0;
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var im = AssetImporter.GetAtPath(path) as TextureImporter;
                if (im == null) continue;
                if (im.mipmapEnabled && im.filterMode == FilterMode.Trilinear && im.anisoLevel >= 4) continue;

                im.mipmapEnabled = true;
                im.filterMode = FilterMode.Trilinear;
                im.anisoLevel = 4;
                im.SaveAndReimport();
                고침++;
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }

        Debug.Log($"[텍스처] {고침}장에 밉맵을 켰습니다 (전체 {guids.Length}장). 멀리서 지글거리는 게 줄어듭니다.");
    }
}
