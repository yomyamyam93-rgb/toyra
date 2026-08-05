using UnityEditor;
using UnityEngine;

/// 땅 사진 임포트 설정 — **깎지 않는다** (2026-08-05 사용자 "픽셀버전으로 변형하지말고 그대로").
///
/// ★같은 폴더의 `GroundImport` 와 정반대다. 저쪽은 64px·점 필터로 깎아 「픽셀 결」을 만들고,
///   여기는 원본 사진을 **사진인 채로** 들여온다. 그래서 폴더를 갈랐다.
///     · `ground/`       → 깎는다 (옛 회색 결)
///     · `ground/사진/`  → 안 깎는다 (여기)
///
/// ★`isReadable` 은 **안 켠다.** `GroundPhotos` 는 픽셀을 읽는 대신 GPU 로 복사(Blit)해서
///   배열에 넣는다 — 크기가 제각각(360~1250px)이라 어차피 한 크기로 다시 그려야 하고,
///   그 길로 가면 압축된 그림도 그대로 읽히고 메모리도 두 벌 안 든다.
/// ★밉맵은 반드시 켠다 — 사진 한 장이 4m 를 덮으니 멀리서는 한 텍셀이 화면 픽셀보다
///   훨씬 작아진다. 밉맵이 없으면 그 자리에서 자글자글 끓는다.
class GroundPhotoImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("Assets/Game/Resources/ground/사진/")) return;

        var im = (TextureImporter)assetImporter;
        im.textureType = TextureImporterType.Default;
        im.isReadable = false;
        im.mipmapEnabled = true;
        im.filterMode = FilterMode.Bilinear;     // ★사진이다 — 점 필터로 각지게 만들지 않는다
        im.wrapMode = TextureWrapMode.Repeat;    // 이어붙는 타일
        im.npotScale = TextureImporterNPOTScale.None;
        im.maxTextureSize = 2048;                // 원본이 1250 이하라 사실상 안 깎인다
        im.textureCompression = TextureImporterCompression.CompressedHQ;
        im.sRGBTexture = true;
    }
}
