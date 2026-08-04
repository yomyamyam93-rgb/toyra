using UnityEngine;

/// 땅 그림 — 잔디·흙길·물가를 **한 장의 텍스처에 칠한다.**
///
/// ★왜 텍스처인가: 물이나 길을 3D 물체로 깔면 겹침·정렬 문제가 생기고 드로콜도 늘어난다.
///   땅은 어차피 평평한 판때기 하나이므로, **칠하는 게 제일 싸고 픽셀 화면과도 맞다**
///   (점 필터로 확대되어 텍셀이 그대로 픽셀 덩어리가 된다).
///
/// ★길은 반드시 **불규칙하게** 구불거려야 한다. 사인파처럼 규칙적으로 굽으면 인공적으로
///   보인다 — 주파수가 배수 관계가 아닌 노이즈 세 겹을 겹치고, 진폭 자체도 흔든다.
public static class GroundPaint
{
    // ★파스텔 톤, 거의 단색 (2026-08-04 참고 그림). 땅은 눈길을 끌지 않아야 하고,
    //   얼룩이 자잘하면 화면이 지저분해진다 — 큰 덩어리로 아주 옅게만 나눈다.
    // ★톤을 여섯으로 늘렸다 (2026-08-04 사용자 "잔디 색이 너무 다 똑같아서").
    //   밝기만 흔드는 게 아니라 **누런 쪽 ↔ 푸른 쪽**으로도 조금씩 기울여야
    //   한 화면 안에서 색이 살아 있는 것처럼 보인다.
    static readonly Color[] 잔디톤 =
    {
        new Color(0.47f, 0.63f, 0.36f),   // 짙은 이끼
        new Color(0.52f, 0.69f, 0.39f),
        new Color(0.57f, 0.73f, 0.43f),   // 기본
        new Color(0.62f, 0.77f, 0.45f),
        new Color(0.66f, 0.78f, 0.44f),   // 볕 든 누런 풀
        new Color(0.50f, 0.70f, 0.48f),   // 그늘진 푸른 풀
    };
    static readonly Color 흙   = new Color(0.78f, 0.72f, 0.52f);
    static readonly Color 모래 = new Color(0.84f, 0.80f, 0.62f);
    static readonly Color 물   = new Color(0.42f, 0.62f, 0.68f);
    static readonly Color 깊은물 = new Color(0.32f, 0.52f, 0.60f);

    // ★어디가 잔디인가 — 잔디를 「잔디 위에만」 깔려고 들고 있는다 (2026-08-04 사용자).
    //   땅을 칠할 때 같이 만들어 두면 나중에 물어보기만 하면 된다.
    static byte[] 잔디자리;
    static int 마스크크기;
    static float 마스크m당;

    /// 그 자리가 잔디인가 (길·물·모래 위에는 풀이 안 난다)
    /// 값: 0 = 아님 · 1·2·3 = 잔디(어느 톤인지)
    public static bool 잔디인가(Vector3 w) => 톤(w) > 0;

    /// 그 자리 잔디의 **톤 번호** (1·2·3). 잔디가 아니면 0.
    /// ★풀 색을 발밑 땅색과 맞추려고 둔다 (2026-08-04 사용자 — "초록 땅 부분들 보면
    ///   색깔들이 있는데 그 색과 같게도 넣어줘야 하고")
    public static int 톤(Vector3 w)
    {
        if (잔디자리 == null) return 1;
        int x = Mathf.Clamp(Mathf.FloorToInt(w.x / 마스크m당), 0, 마스크크기 - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(w.z / 마스크m당), 0, 마스크크기 - 1);
        return 잔디자리[y * 마스크크기 + x];
    }

    /// 톤 번호(1부터)에 해당하는 땅색
    public static Color 톤색(int t) => 잔디톤[Mathf.Clamp(t - 1, 0, 잔디톤.Length - 1)];
    public static int 톤수 => 잔디톤.Length;

    /// 맵 전체를 칠한 텍스처를 만든다. `size` 는 한 변의 텍셀 수
    public static Texture2D 만들기(int seed, int size, System.Func<Vector3, WorldGen.Land> 칸종류)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        잔디자리 = new byte[size * size];
        마스크크기 = size;
        마스크m당 = WorldGrid.Size / size;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            name = "땅그림",
            // ★★★점 필터를 쓰면 안 된다 (2026-08-04 사용자 "격자로 잘려있으니까 참").
            //   여기 「텍셀이 곧 픽셀」이라고 적혀 있었는데 **땅에서는 틀린 말이다.**
            //   픽셀 느낌은 저해상도 렌더 타겟이 만든다 (1m = 20텍셀). 그런데 이 그림은
            //   2048px 로 1440m — **한 텍셀이 0.70m = 렌더텍셀 14개 = 화면 42픽셀**이다.
            //   점 필터로 뽑으면 그 42픽셀 덩어리가 그대로 보이고, 카메라가 45° 라
            //   정사각 덩어리가 **마름모**로 읽힌다. 그게 「격자」의 정체였다.
            //   → 이중선형으로 뽑는다. 땅은 월드에서 매끄러워지고, 픽셀로 끊는 일은
            //     렌더 타겟이 그대로 한다. 픽셀 느낌은 하나도 안 잃는다.
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float m당 = WorldGrid.Size / size;       // 텍셀 하나가 몇 미터인가
        var px = new Color32[size * size];

        // 길 — 캠프에서 뻗어 나가는 갈래. 먼저 좌표를 뽑아 두고 아래에서 거리로 칠한다
        var 길 = 길만들기(seed);

        var rnd = new System.Random(seed);
        float o1 = (float)rnd.NextDouble() * 1000f;
        float o2 = (float)rnd.NextDouble() * 1000f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var w = new Vector3((x + 0.5f) * m당, 0f, (y + 0.5f) * m당);

                // ① 잔디 바탕 — **두 겹**으로 흔든다 (2026-08-04).
                //   큰 얼룩만 쓰면(250m 급) 한 화면이 통째로 한 색이라 밋밋하다.
                //   큰 흐름(80m) + 중간 무늬(25m)를 겹쳐야 걸어다닐 때 색이 변한다.
                //   ★그래도 자잘한 잡티는 안 넣는다 — 큰 면으로만.
                float n = Mathf.PerlinNoise(w.x * 0.012f + o1, w.z * 0.012f + o1) * 0.62f
                        + Mathf.PerlinNoise(w.x * 0.040f + o1 * 1.7f, w.z * 0.040f + o1 * 1.7f) * 0.38f;
                // ★★색을 **끊지 않고 이어 붙인다** (2026-08-04 사용자 "바닥은 연속보간해줘").
                //   전엔 여섯 톤 중 하나로 딱 떨어뜨려서, 색이 바뀌는 자리마다 선이 생겼다.
                //   이웃한 두 톤을 섞으면 같은 여섯 색 팔레트를 그대로 쓰면서 경계가 사라진다.
                //   ★팔레트는 그대로다 — 없던 색이 생기는 게 아니라 **사이가 채워질 뿐**이다.
                float f = Mathf.Clamp(n * 톤수 * 1.15f, 0f, 톤수 - 1.0001f);
                int i0 = Mathf.FloorToInt(f);
                var c = Color.Lerp(잔디톤[i0], 잔디톤[Mathf.Min(i0 + 1, 톤수 - 1)], f - i0);

                // ★잔디 포기가 쓸 톤 번호는 여전히 정수다 — 재질을 톤마다 하나씩 쓰기 때문
                //   (`GrassField`). 땅은 이어지고 풀은 가까운 톤을 고른다
                int 톤번호 = Mathf.Clamp(i0 + 1, 1, 톤수);
                잔디자리[y * size + x] = (byte)톤번호;   // 물·모래·길에서 0 으로 지운다

                // ② 물 — 물웅덩이 칸 둘레로 둥글게. 가운데는 깊게
                if (칸종류 != null && 칸종류(w) == WorldGen.Land.물웅덩이)
                {
                    var 중심 = 칸중심(w);
                    float d = new Vector2(w.x - 중심.x, w.z - 중심.z).magnitude;
                    // 물가도 매끈한 원이면 계단으로 보인다 — 두 겹 노이즈로 들쭉날쭉하게
                    float r = 36f + Mathf.PerlinNoise(w.x * 0.02f + o2, w.z * 0.02f + o2) * 18f
                                  + (Mathf.PerlinNoise(w.x * 0.11f + o2, w.z * 0.11f + o2) - 0.5f) * 7f;
                    if (d < r * 0.72f) { c = 깊은물; 잔디자리[y * size + x] = 0; }
                    else if (d < r) { c = 물; 잔디자리[y * size + x] = 0; }
                    else if (d < r * 1.12f) { c = 모래; 잔디자리[y * size + x] = 0; }
                }

                px[y * size + x] = c;
            }
        }

        // ③ 길은 **찍어서** 그린다 (아래 참고) — 픽셀마다 거리를 재면 안 된다
        길찍기(px, size, m당, 길, o2);

        tex.SetPixels32(px);
        tex.Apply(true);
        Debug.Log($"[땅] {size}×{size} 칠하기 {sw.ElapsedMilliseconds}ms");
        return tex;
    }

    static Vector3 칸중심(Vector3 w)
    {
        int gx = Mathf.Clamp(Mathf.FloorToInt(w.x / WorldGrid.Tile), 0, WorldGrid.N - 1);
        int gz = Mathf.Clamp(Mathf.FloorToInt(w.z / WorldGrid.Tile), 0, WorldGrid.N - 1);
        return WorldGrid.TileCenter(gx, gz);
    }

    // ── 길: 캠프에서 사방으로 뻗는 갈래. 점 목록으로 들고 있다가 거리로 칠한다
    static Vector2[][] 길만들기(int seed)
    {
        var rnd = new System.Random(seed ^ 0x51ed);
        int 갈래 = 5;
        var 결과 = new Vector2[갈래][];
        var c = WorldGrid.Center;

        for (int k = 0; k < 갈래; k++)
        {
            float 각 = (k / (float)갈래) * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.6f;
            var dir = new Vector2(Mathf.Cos(각), Mathf.Sin(각));
            float 길이 = WorldGrid.Size * 0.5f * (0.75f + (float)rnd.NextDouble() * 0.35f);

            int 점수 = 64;
            var pts = new Vector2[점수];
            float o = (float)rnd.NextDouble() * 500f;

            for (int i = 0; i < 점수; i++)
            {
                float t = i / (float)(점수 - 1);
                var 앞 = new Vector2(c.x, c.z) + dir * (길이 * t);

                // ★불규칙하게 — 주파수가 배수 관계가 아닌 세 겹 (1 : 2.3 : 5.7)
                float s = t * 길이;
                float wob = Mathf.PerlinNoise(s * 0.010f + o, o) - 0.5f
                          + (Mathf.PerlinNoise(s * 0.023f + o, o + 7f) - 0.5f) * 0.55f
                          + (Mathf.PerlinNoise(s * 0.057f + o, o + 19f) - 0.5f) * 0.28f;

                // 진폭 자체도 흔든다 — 어떤 구간은 거의 곧고 어떤 구간은 크게 굽게
                float amp = 10f + Mathf.PerlinNoise(s * 0.006f + o + 33f, o) * 26f;

                var 옆 = new Vector2(-dir.y, dir.x);
                pts[i] = 앞 + 옆 * wob * amp;
            }
            결과[k] = pts;
        }
        return 결과;
    }

    /// ★길을 **찍어서** 그린다 — 길을 따라 걸으며 그 자리에 동그라미를 찍는다.
    ///
    /// ★왜 이렇게 (2026-08-04 사용자 "처음 게임 켤 때 렉이 심하던데"):
    ///   전에는 **픽셀마다** 모든 길 조각과의 거리를 쟀다. 400만 픽셀 × 조각 315개 =
    ///   **십억 번**이 넘는 계산이라, 켤 때 몇 초씩 멈췄다.
    ///   찍는 방식은 길 길이에만 비례한다 — 수천 번이면 끝난다. **수백 배 빠르다.**
    static void 길찍기(Color32[] px, int size, float m당, Vector2[][] 길, float o)
    {
        foreach (var pts in 길)
        {
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var a = pts[i];
                var b = pts[i + 1];
                float len = Vector2.Distance(a, b);
                int 걸음 = Mathf.Max(1, Mathf.CeilToInt(len / (m당 * 0.7f)));

                for (int s = 0; s <= 걸음; s++)
                {
                    var p = Vector2.Lerp(a, b, s / (float)걸음);

                    // 폭을 자리마다 흔든다 — 매끈한 띠는 텍셀 격자를 따라 계단으로 보인다
                    float 흔들 = (Mathf.PerlinNoise(p.x * 0.09f + o, p.y * 0.09f + o) - 0.5f) * 2.6f
                               + (Mathf.PerlinNoise(p.x * 0.31f + o, p.y * 0.31f + o) - 0.5f) * 1.1f;
                    float 폭 = 3.4f + 흔들;
                    동그라미(px, size, m당, p, 폭);
                }
            }
        }
    }

    /// 그 자리에 흙 동그라미 하나 (가장자리는 옅게 섞는다)
    static void 동그라미(Color32[] px, int size, float m당, Vector2 at, float r)
    {
        float 겉 = r + 1.8f;
        int x0 = Mathf.Max(0, Mathf.FloorToInt((at.x - 겉) / m당));
        int x1 = Mathf.Min(size - 1, Mathf.CeilToInt((at.x + 겉) / m당));
        int y0 = Mathf.Max(0, Mathf.FloorToInt((at.y - 겉) / m당));
        int y1 = Mathf.Min(size - 1, Mathf.CeilToInt((at.y + 겉) / m당));

        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = (x + 0.5f) * m당 - at.x;
                float dy = (y + 0.5f) * m당 - at.y;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 겉) continue;

                int i = y * size + x;
                if (d < r) { px[i] = 흙; 잔디자리[i] = 0; }
                else
                {
                    float t = (d - r) / 1.8f;
                    var cur = (Color)px[i];
                    px[i] = Color.Lerp(흙, cur, t);
                    if (t < 0.5f) 잔디자리[i] = 0;      // 길 가장자리에도 안 난다
                }
            }
    }
}
