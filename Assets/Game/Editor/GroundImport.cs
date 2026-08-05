using UnityEditor;
using UnityEngine;

/// 땅 결 그림 임포트 설정 — **자동으로** 맞춘다.
///
/// ★`GroundPaint` 가 이 그림의 **원본 픽셀을 읽어서** 땅에 결을 굽는다. 읽기 가능이 아니면
///   조용히 실패하고 결이 안 나온다.
/// ★`GrassImport` 처럼 메뉴로 두지 않은 이유: 누르는 걸 잊으면 "왜 결이 안 보이지" 로
///   한참 헤맨다. 넣는 순간 알아서 맞는 게 낫다.
class GroundImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        var p = assetPath.Replace('\\', '/');
        if (!p.Contains("Assets/Game/Resources/ground/")) return;
        // ★`사진/` 밑은 **원본 그대로** 쓰는 재질이라 여기서 손대면 안 된다 (2026-08-05).
        //   이 함수는 64px·점 필터로 깎는 「픽셀 변환」이다 — `GroundPhotoImport` 가 따로 맡는다.
        if (p.Contains("/ground/사진/")) return;

        var im = (TextureImporter)assetImporter;
        im.textureType = TextureImporterType.Default;
        im.isReadable = true;                    // 원본 픽셀을 읽어야 한다
        // ★밉맵은 **켠다**. 결 타일은 4m 마다 되풀이되므로 멀리서는 한 칸이 화면 픽셀보다
        //   작아진다 — 밉맵이 없으면 그 자리에서 자글자글 끓는다.
        im.mipmapEnabled = true;
        im.filterMode = FilterMode.Point;        // 픽셀아트
        im.wrapMode = TextureWrapMode.Repeat;    // 이어붙는 타일
        im.textureCompression = TextureImporterCompression.Uncompressed;  // 색 단계가 뭉개지면 안 된다
        im.npotScale = TextureImporterNPOTScale.None;
        im.maxTextureSize = 64;
    }
}
