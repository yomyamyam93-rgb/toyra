using UnityEngine;

/// 세계의 자 — **1유닛 = 1미터, 축소 없음.**
///
/// ★옛 프로젝트는 `WorldScale.K = 0.1` 로 캐릭터·펫만 1/10 로 줄여 놨었다 (24km 지형을
///   감당하려던 잔재). 그 탓에 캐릭터가 작아 카메라를 멀리 빼야 했고, 화각이 넓어져
///   아이소메트릭 느낌이 안 났고, 새로 잡는 거리마다 잣대가 어긋났다.
///   → **여기서는 줄이지 않는다.** 캐릭터 1.8m, 나무 12m, 시야 32m — 전부 현실 감각.
public static class WorldGrid
{
    /// 사람 키 (m) — 모든 크기의 기준자
    public const float Human = 1.8f;

    /// 한 칸의 한 변 (m)
    /// ★근거: ①직교 카메라 기본 시야가 가로 약 50m — 칸이 그보다 작으면 한 화면에
    ///   여러 칸이 들어와 격자가 눈에 띈다 (160m = 3.2배) ②걸어서 114초(1분 54초),
    ///   뛰어서 40초 — 랜드마크 사이 간격으로 알맞다
    public const float Tile = 160f;

    /// 한 줄에 몇 칸 (홀수여야 정중앙 칸이 하나 나온다 — 거기가 집)
    public const int N = 9;

    /// 맵 한 변 (m) — 1440m. 걸어서 가로지르는 데 약 17분
    public const float Size = Tile * N;

    public static int Home => N / 2;
    public static Vector3 Center => new Vector3(Size * 0.5f, 0f, Size * 0.5f);

    public static Vector3 TileCenter(int gx, int gz)
        => new Vector3((gx + 0.5f) * Tile, 0f, (gz + 0.5f) * Tile);

    public static bool InRange(int gx, int gz) => gx >= 0 && gz >= 0 && gx < N && gz < N;

    /// 칸마다 고정된 난수 씨앗 — 같은 월드 씨앗이면 같은 맵이 나온다
    public static int TileSeed(int worldSeed, int gx, int gz)
    {
        unchecked
        {
            uint h = (uint)worldSeed * 2654435761u;
            h ^= (uint)(gx * 73856093);
            h ^= (uint)(gz * 19349663);
            h *= 2246822519u;
            h ^= h >> 15;
            return (int)(h & 0x7FFFFFFF);
        }
    }
}
