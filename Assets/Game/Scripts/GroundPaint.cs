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

    /// ★★**잰 평균색**을 쓴다 (2026-08-05 사용자 — "잔디색은 해당타일의 평균색과 같으면될듯한데").
    ///   팔레트 값(`잔디톤`)은 **칠하기 전의 색**이다. 실제 땅은 그 위에 큰 무늬·잔 무늬가
    ///   얹혀 있어서 팔레트와 다르다. 그래서 다 칠한 **뒤에 재서** 그 평균을 쓴다.
    ///   → 잔디 포기가 발밑 땅에서 뜨지 않는다.
    static Color[] 잰톤색;
    static Color 잰흙색 = new Color(0.78f, 0.72f, 0.52f);
    static Color 잰모래색 = new Color(0.84f, 0.80f, 0.62f);

    /// 톤 번호(1부터)에 해당하는 땅색 — 잰 값이 있으면 그것
    public static Color 톤색(int t)
    {
        int i = Mathf.Clamp(t - 1, 0, 잔디톤.Length - 1);
        return 잰톤색 != null ? 잰톤색[i] : 잔디톤[i];
    }
    public static int 톤수 => 잔디톤.Length;

    /// 그 자리가 무엇인가 — 0 물 · 1 잔디 · 2 모래 · 3 흙(길)
    public static int 종류(Vector3 w)
    {
        if (결종류 == null) return 1;
        int x = Mathf.Clamp(Mathf.FloorToInt(w.x / 마스크m당), 0, 마스크크기 - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(w.z / 마스크m당), 0, 마스크크기 - 1);
        return 결종류[y * 마스크크기 + x];
    }

    /// 잔디가 아닌 자리(흙길·모래)의 잰 평균색 — 그 위에 선 풀이 여기 맞춰 마른 색이 된다
    public static Color 종류색(int k) => k == 3 ? 잰흙색 : 잰모래색;

    /// 다 칠한 **뒤에** 자리마다 평균색을 잰다
    static void 평균색재기(Color32[] px, int size)
    {
        int n = 잔디톤.Length;
        var 합 = new Vector3[n]; var 수 = new int[n];
        Vector3 흙합 = Vector3.zero; int 흙수 = 0;
        Vector3 모래합 = Vector3.zero; int 모래수 = 0;

        for (int i = 0; i < px.Length; i++)
        {
            var v = new Vector3(px[i].r, px[i].g, px[i].b) / 255f;
            byte k = 결종류[i];
            if (k == 3) { 흙합 += v; 흙수++; }
            else if (k == 2) { 모래합 += v; 모래수++; }
            else if (k == 1)
            {
                int t = Mathf.Clamp(잔디자리[i] - 1, 0, n - 1);
                합[t] += v; 수[t]++;
            }
        }

        잰톤색 = new Color[n];
        for (int t = 0; t < n; t++)
            잰톤색[t] = 수[t] > 0 ? new Color(합[t].x / 수[t], 합[t].y / 수[t], 합[t].z / 수[t]) : 잔디톤[t];
        if (흙수 > 0) 잰흙색 = new Color(흙합.x / 흙수, 흙합.y / 흙수, 흙합.z / 흙수);
        if (모래수 > 0) 잰모래색 = new Color(모래합.x / 모래수, 모래합.y / 모래수, 모래합.z / 모래수);
    }

    // ══════════════════════════════════════════ 땅의 결
    //
    // ★★★**색은 그대로 두고 명암 무늬만 곱한다** (2026-08-05 사용자가 A안 선택).
    //   여섯 색 잔디 팔레트는 직접 고르신 것이고 「바닥은 연속보간」도 그대로 살아야 한다.
    //   그래서 사진에서 뽑은 결은 **색을 갖지 않는다** — 밝고 어두운 얼룩으로만 곱해진다.
    //   결의 평균을 1 로 맞추므로 **전체 밝기는 한 톨도 안 변한다.**
    //
    // ★★크기: **결 그림 한 칸 = 땅 텍셀 하나 = 0.70m** 이다. 64칸짜리라 무늬는 45m 마다
    //   되풀이되고 얼룩 하나가 **0.7~1.4m** — 사람 키의 절반 이하다.
    //   ☆처음엔 그림을 3x3 으로 뭉개서 만들었더니 얼룩이 2~4m 짜리 **위장 무늬**가 됐다
    //     (2026-08-05 사용자 "과하게 뻥튀기해서 넣었어"). 뭉갤 이유였던 「무지개 잡티」는
    //     밝기만 끊는 방식으로 이미 사라졌었다 — 없앤 문제의 대책을 그대로 들고 있던 것이다.
    // ★칸 종류마다 다른 그림이 걸린다 — 잔디엔 잔디결, 물가 모래엔 모래결, 길엔 흙결.
    //   물에는 안 얹는다 (물은 결이 있으면 물로 안 보인다).

    /// 텍셀마다 어떤 결을 얹을까 — 0 물(안 얹음) · 1 잔디 · 2 모래 · 3 흙
    static byte[] 결종류;
    static Color32[][] 결그림; static int[] 결폭; static float[] 결평균, 결진폭;

    static void 결준비()
    {
        if (결그림 != null) return;
        string[] 이름 = { "ground/땅_잔디", "ground/땅_모래", "ground/땅_흙" };
        결그림 = new Color32[이름.Length][];
        결폭 = new int[이름.Length]; 결평균 = new float[이름.Length]; 결진폭 = new float[이름.Length];

        for (int i = 0; i < 이름.Length; i++)
        {
            var t = Resources.Load<Texture2D>(이름[i]);
            if (t == null) { Debug.LogWarning("[땅] 결 그림 없음: " + 이름[i]); continue; }
            try { 결그림[i] = t.GetPixels32(); }
            catch { Debug.LogWarning("[땅] 결 그림을 못 읽는다(읽기 가능이 꺼져 있다): " + 이름[i]); continue; }

            결폭[i] = t.width;
            // ★평균과 진폭을 **재 둔다** — 그래야 「세기」가 곧 밝기 흔들림의 폭이 된다.
            //   0.5 를 기준으로 삼으면 어두운 그림은 땅을 통째로 어둡게 만든다.
            float 합 = 0f;
            for (int k = 0; k < 결그림[i].Length; k++) 합 += 밝기(결그림[i][k]);
            결평균[i] = 합 / 결그림[i].Length;
            float 최대 = 0f;
            for (int k = 0; k < 결그림[i].Length; k++)
                최대 = Mathf.Max(최대, Mathf.Abs(밝기(결그림[i][k]) - 결평균[i]));
            결진폭[i] = Mathf.Max(0.001f, 최대);
        }
    }

    static float 밝기(Color32 c) => (c.r * 0.299f + c.g * 0.587f + c.b * 0.114f) / 255f;

    /// ★★**칸 마스크** — 땅 셰이더가 「이 자리는 무엇인가」를 읽는 그림.
    ///   R = 잔디 · G = 흙(길) · B = 모래 · A = 물이 아님(0 이면 결을 안 얹는다).
    ///   이게 있어야 **자리마다 다른 결**이 걸린다 (유니티 기본 재질은 결이 한 장뿐이라 못 했다).
    public static Texture2D 땅마스크 { get; private set; }

    /// 다 칠한 땅 그림 — 잔디 포기가 **자기 발밑을 찍어 보는 데** 쓴다
    public static Texture2D 땅그림 { get; private set; }

    static void 마스크만들기(int size)
    {
        var px = new Color32[size * size];
        for (int i = 0; i < px.Length; i++)
        {
            byte k = 결종류[i];
            px[i] = new Color32(
                k == 1 ? (byte)255 : (byte)0,   // 잔디
                k == 3 ? (byte)255 : (byte)0,   // 흙(길)
                k == 2 ? (byte)255 : (byte)0,   // 모래
                k == 0 ? (byte)0 : (byte)255);  // 물이면 0
        }

        땅마스크 = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "땅마스크",
            // ★부드럽게 뽑는다 — 잔디와 흙길 경계에서 결이 뚝 끊기면 그 선이 보인다
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        땅마스크.SetPixels32(px);
        땅마스크.Apply(false);
    }

    static void 결얹기(Color32[] px, int size, float 세기)
    {
        if (세기 <= 0.001f) return;
        결준비();
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                int k = 결종류[i] - 1;
                if (k < 0 || k >= 결그림.Length || 결그림[k] == null) continue;

                int w = 결폭[k];
                float l = 밝기(결그림[k][(y % w) * w + (x % w)]);
                float 곱 = 1f + (l - 결평균[k]) / 결진폭[k] * 세기;

                var c = px[i];
                px[i] = new Color32(
                    (byte)Mathf.Clamp(c.r * 곱, 0f, 255f),
                    (byte)Mathf.Clamp(c.g * 곱, 0f, 255f),
                    (byte)Mathf.Clamp(c.b * 곱, 0f, 255f), c.a);
            }
    }

    /// 맵 전체를 칠한 텍스처를 만든다. `size` 는 한 변의 텍셀 수
    // ══════════════════════════════════════════ 잔얼룩
    //
    // ★★★"약간의 얼룩덜룩한 잔디와 땅의 재질" 을 **사진이 아니라 노이즈로** 낸다
    //   (2026-08-05 사용자 — "아주 은은하게 그걸 재질로 해결하려했는데").
    //
    //   사진에서 뽑은 결은 세 번 다듬어도 「때」로 읽혔다. 원인은 **불규칙**이다.
    //   노이즈는 **부드럽게 이어지는 값**이라 자국이라는 게 생기지 않는다 — 색이 스르르
    //   짙어졌다 옅어질 뿐이다. 그게 원하던 "은은한 얼룩덜룩" 이다.
    //
    // ★왜 지금은 안 보였나: 땅색을 흔드는 겹이 **83m 와 25m 두 개뿐**이라 한 화면(약 110m)에
    //   한두 번밖에 안 출렁인다. **사람 크기쯤(10m 안팎)의 겹**이 빠져 있었다.
    //
    // ★색이 아니라 **밝기만** 흔든다. 팔레트를 건드리지 않으므로 없던 색이 생기지 않는다.
    //   물에는 안 얹는다 (물이 얼룩덜룩하면 물로 안 보인다).
    /// ★★★**fBm — 노이즈를 겹겹이 쌓는다** (2026-08-05 사용자 — "아주 큰 불규칙한 무늬에,
    ///   또 그안에 작은 불규칙한 무늬 두가지가 겹쳐지는거야", "실제로 많이 사용되는 방식으로").
    ///   게임·영화에서 지형과 재질 무늬를 만들 때 거의 언제나 쓰는 방식이다.
    ///   한 겹 내려갈 때마다 **크기는 2.7배 잘아지고 세기는 절반**이 된다. 큰 흐름이 판을
    ///   깔고 잔 겹이 그 위에 결을 얹는다 — 자연에 있는 무늬가 대개 이런 모양이다.
    ///
    /// ★★사진과 결정적으로 다른 점: **노이즈는 작아져도 부드럽다.** 옆 칸과 이어져 있어서
    ///   아무리 잘게 해도 「잡음」이 되지 않는다. 사진은 줄이는 순간 픽셀끼리 무관해져
    ///   반드시 잡음이 됐다 — 오늘 네 번 실패한 이유가 그것이었다.
    ///
    /// ★크기가 2.7배씩(2배가 아니라) 잘아지는 이유: 배수가 정확히 2 면 겹끼리 마루가
    ///   같은 자리에 겹쳐 격자무늬가 비친다. 어긋난 배수여야 불규칙하게 섞인다.
    /// ★★한 덩어리 fBm 이 아니라 **두 가지를 따로** 둔다 (2026-08-05 사용자 — "큰 흐름은
    ///   훨씬더 커야함 … 그리고 잔결은 하나도 안보이네?").
    ///   겹마다 세기를 반씩 줄이는 정석 fBm 은 **가장 잔 겹이 전체의 12% 밖에 안 된다** —
    ///   큰 것을 키우면 잔 것은 자동으로 안 보인다. 둘을 한 손잡이에 묶은 게 문제였다.
    ///   → 「큰 무늬」와 「잔 무늬」에 **각각 크기와 세기**를 준다. 서로 간섭하지 않는다.
    ///
    /// ★각각은 안에서 두 겹을 쓴다 (그 크기 + 2.7배 잔 것). 한 겹만 쓰면 규칙적인
    ///   물결로 보이기 때문이다. 2.7 인 이유는 정확히 2 면 마루가 겹쳐 격자가 비쳐서다.
    static float 두겹(float wx, float wz, float 주파, float o)
    {
        float a = Mathf.PerlinNoise(wx * 주파 + o, wz * 주파 + o) - 0.5f;
        float b = Mathf.PerlinNoise(wx * 주파 * 2.7f + o * 1.3f, wz * 주파 * 2.7f + o * 1.3f) - 0.5f;
        return (a + b * 0.5f) / 1.5f;             // -0.5 ~ 0.5
    }

    static void 무늬얹기(Color32[] px, int size, float m당, int seed,
                         float 큰세기, float 큰칸, float 잔세기, float 잔칸)
    {
        if (큰세기 <= 0.001f && 잔세기 <= 0.001f) return;
        float o = (seed & 0xFFFF) * 0.017f + 313f;
        float 큰주파 = 1f / Mathf.Max(1f, 큰칸);
        float 잔주파 = 1f / Mathf.Max(0.5f, 잔칸);

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                if (결종류[i] == 0) continue;                 // 물은 건너뛴다

                float wx = (x + 0.5f) * m당, wz = (y + 0.5f) * m당;

                // ★★★**밝기가 아니라 「진하기」를 흔든다** (2026-08-05 사용자 — "이 검게
                //   그리고 회색빛으로 조금씩 물드는게 너무 지저분해보여, 황토색이면 조금더
                //   진한 황토색, 연두색이면 조금더 짙은 연두색 같이 애니메이션 스타일처럼").
                //
                //   밝기를 곱하면 색이 **흰색이나 검정 쪽으로 끌려간다** — 그래서 회색빛으로
                //   물들고 때처럼 보였다. 밝게만 해도 마찬가지다 (흰색 쪽으로 바래니까).
                //   → 밝기는 **그대로 두고**, 색이 회색에서 얼마나 떨어져 있나(채도)만 키운다.
                //     색조는 한 톨도 안 변하고 **진해지기만** 한다. 애니메이션 채색이 이 방식이다.
                float t = (두겹(wx, wz, 큰주파, o) + 0.5f) * 큰세기
                        + (두겹(wx, wz, 잔주파, o + 511f) + 0.5f) * 잔세기;

                var c = px[i];
                float 밝기 = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                float k = 1f + t;                     // 회색에서 멀어지는 배수
                px[i] = new Color32(
                    (byte)Mathf.Clamp(밝기 + (c.r - 밝기) * k, 0f, 255f),
                    (byte)Mathf.Clamp(밝기 + (c.g - 밝기) * k, 0f, 255f),
                    (byte)Mathf.Clamp(밝기 + (c.b - 밝기) * k, 0f, 255f), c.a);
            }
    }

    public static Texture2D 만들기(int seed, int size, System.Func<Vector3, WorldGen.Land> 칸종류,
                                   float 결세기 = 0f,
                                   float 큰무늬 = 0.28f, float 큰무늬칸 = 120f,
                                   float 잔무늬 = 0.14f, float 잔무늬칸 = 4.5f)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        잔디자리 = new byte[size * size];
        결종류 = new byte[size * size];
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
        var 길 = 길만들기(seed, 칸종류);

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
                결종류[y * size + x] = 1;                // 잔디결 (물·모래·길에서 바뀐다)

                // ★★★② 물 — **도장으로 안 찍는다** (2026-08-06 사용자 — "호수를 만들어줬는데
                //   똑같은 사이즈로 여기저기 찍어둔게 많은데").
                //
                //   전에는 물웅덩이 「칸」의 한가운데에 반경 36~54m 짜리 **원**을 그렸다.
                //   그래서 호수가 전부 같은 크기였고, 칸이 붙으면 원 두 개가 겹친 눈사람이 됐다.
                //
                //   ★마인크래프트는 물을 안 찍는다 — `noise` 단계에서 **지형이 해수면보다
                //     낮으면 그게 곧 물**이다. 그래서 웅덩이부터 바다까지 크기가 저절로 다 다르고,
                //     두 번 같은 모양이 안 나오며, 칸 경계를 넘어 자연스럽게 이어진다.
                //   → 우리도 같은 방식으로 간다. `절차.물기` 하나가 온 세계의 물을 정한다.
                float 물기 = WorldGen.물기(w.x, w.z);
                if (물기 > 0f)
                {
                    // 물엔 결을 안 얹는다 (0) — 결이 있으면 물로 안 보인다
                    if (물기 > 0.55f) { c = 깊은물; 잔디자리[y * size + x] = 0; 결종류[y * size + x] = 0; }
                    else if (물기 > 0.18f) { c = 물; 잔디자리[y * size + x] = 0; 결종류[y * size + x] = 0; }
                    else { c = 모래; 잔디자리[y * size + x] = 0; 결종류[y * size + x] = 2; }   // 물가 모래
                }

                px[y * size + x] = c;
            }
        }

        // ③ 길은 **찍어서** 그린다 (아래 참고) — 픽셀마다 거리를 재면 안 된다
        길찍기(px, size, m당, 길, o2);

        // ④ 결 — **맨 마지막에** 얹는다. 물·모래·길이 다 정해진 뒤라야 칸마다 맞는 결이 걸린다
        결얹기(px, size, 결세기);

        // ④' 무늬 — 큰 흐름과 잔 결, 두 가지를 밝기로만 얹는다
        무늬얹기(px, size, m당, seed, 큰무늬, 큰무늬칸, 잔무늬, 잔무늬칸);

        // ⑤ 칸 마스크 — 셰이더가 자리마다 다른 결을 고르는 데 쓴다
        마스크만들기(size);

        // ⑥ 자리마다 평균색을 잰다 — 잔디 포기가 이 색을 따라간다
        평균색재기(px, size);

        tex.SetPixels32(px);
        tex.Apply(true);
        땅그림 = tex;          // 잔디 셰이더가 발밑을 찍어 보려고 쓴다
        Debug.Log($"[땅] {size}×{size} 칠하기 {sw.ElapsedMilliseconds}ms");
        return tex;
    }

    static Vector3 칸중심(Vector3 w)
    {
        int gx = Mathf.Clamp(Mathf.FloorToInt(w.x / WorldGrid.Tile), 0, WorldGrid.N - 1);
        int gz = Mathf.Clamp(Mathf.FloorToInt(w.z / WorldGrid.Tile), 0, WorldGrid.N - 1);
        return WorldGrid.TileCenter(gx, gz);
    }

    /// 길 하나 — 점들과 **밟힌 정도**(폭을 정한다)
    public struct 길줄 { public Vector2[] 점; public float 밟힘; }

    // ───────────────────────────────── 길
    //
    // ★★★**길은 이유가 있어야 난다** (2026-08-06 사용자 — "지금 우리 길이 맘에안들어서,
    //   다양하지가않고 계속 똑같길래 뭔 기준이 없는거같아서, 그렇다고 전부 길로 이으면안돼겠지만").
    //
    //   전에는 **캠프에서 사방으로 다섯 갈래**를 뻗었다. 각도만 조금 흔들 뿐이라
    //   씨앗을 바꿔도 늘 같은 별 모양이었고, **왜 그리로 길이 났는지** 설명이 없었다.
    //   ☆헌법 9-0: "모든 결과에는 인과와 행위가 있어야 한다." 길도 마찬가지다 —
    //     원시시대의 길은 **사람과 짐승이 자주 다녀서 밟혀 생긴 자국**이다.
    //
    //   → 그래서 이제 길은 **「갈 이유가 있는 곳」끼리** 잇는다:
    //       집 · 폐허(제작법) · 둥지(알) · 바위지대(돌·굴) · 물가(목마름)
    //
    //   ★그물처럼 다 잇지 않는다. **최소 신장 트리**로 잇는다 — 각 자리로 가는 길이
    //     딱 하나씩만 생기는 가장 짧은 그물이다. 게다가:
    //       ①너무 먼 것은 아예 안 잇는다  → **길이 없는 오지**가 생긴다
    //       ②끝자락 몇 개는 일부러 끊는다 → 발길이 뜸한 곳이 생긴다
    //     둘 다 「전부 길로 이으면 안 된다」는 말에 대한 답이다.
    //
    //   ★폭은 **밟힘**에서 나온다 — 그 길로 몇 군데를 지나가느냐. 집 가까운 줄기는 넓고
    //     끝자락은 좁다. 이유가 폭에까지 드러난다.
    static 길줄[] 길만들기(int seed, System.Func<Vector3, WorldGen.Land> 칸종류)
    {
        var rnd = new System.Random(seed ^ 0x51ed);
        var 집 = WorldGrid.Center;

        // ── ① 갈 이유가 있는 자리를 모은다 (집이 0번)
        var 점 = new System.Collections.Generic.List<Vector2> { new Vector2(집.x, 집.z) };
        int n = WorldGrid.N;
        for (int x = 0; x < n; x++)
            for (int z = 0; z < n; z++)
            {
                var c = WorldGrid.TileCenter(x, z);
                if (Vector2.Distance(new Vector2(c.x, c.z), new Vector2(집.x, 집.z)) < 40f) continue;

                bool 갈만한가 = false;
                if (칸종류 != null)
                {
                    var k = 칸종류(c);
                    갈만한가 = k == WorldGen.Land.폐허 || k == WorldGen.Land.둥지 || k == WorldGen.Land.바위지대;
                }
                // 물가 — 목마름이 랜드마크를 만든다 (기획 5-5)
                if (!갈만한가 && WorldGen.물기(c.x, c.z) > 0.18f) 갈만한가 = true;
                if (!갈만한가) continue;

                // 칸 한가운데에 딱 찍으면 격자가 보인다 — 흔들어 놓는다
                var p = new Vector2(c.x + ((float)rnd.NextDouble() - 0.5f) * WorldGrid.Tile * 0.5f,
                                    c.z + ((float)rnd.NextDouble() - 0.5f) * WorldGrid.Tile * 0.5f);
                점.Add(마른자리(p));
            }
        if (점.Count < 2) return new 길줄[0];

        // ── ② 최소 신장 트리 (프림) — 집에서부터 가까운 것을 하나씩 끌어들인다
        //
        // ★★한계를 **칸 크기에 매단다** (2026-08-06). 330m 로 못박았더니 길이 **한 줄도**
        //   안 났다 — `PickKinds` 가 「집 옆 여덟 칸은 트여 있어야 한다」고 빈들판·숲으로
        //   두는 바람에, 집에서 제일 가까운 「갈 이유가 있는 곳」이 351m 였기 때문이다.
        //   ☆숫자를 손으로 박으면 이렇게 다른 규칙과 어긋난다. 칸 크기에서 뽑으면 따라온다.
        float 최대간선 = WorldGrid.Tile * 2.8f;   // 이보다 멀면 안 잇는다 → 길 없는 오지
        int m = 점.Count;
        var 든것 = new bool[m];
        var 어디서 = new int[m];
        var 거리 = new float[m];
        for (int i = 0; i < m; i++) { 거리[i] = float.MaxValue; 어디서[i] = -1; }
        거리[0] = 0f;

        var 간선 = new System.Collections.Generic.List<(int a, int b)>();
        for (int 회 = 0; 회 < m; 회++)
        {
            int 다음 = -1; float 제일 = float.MaxValue;
            for (int i = 0; i < m; i++) if (!든것[i] && 거리[i] < 제일) { 제일 = 거리[i]; 다음 = i; }
            // ★집에서 나가는 **첫 길만은** 한계를 안 본다 — 집에 길이 하나도 없으면 안 된다
            if (다음 < 0) break;
            if (제일 > 최대간선 && 간선.Count > 0) break;   // 남은 건 전부 너무 멀다 → 오지로 남긴다
            든것[다음] = true;
            if (어디서[다음] >= 0) 간선.Add((어디서[다음], 다음));
            for (int i = 0; i < m; i++)
            {
                if (든것[i]) continue;
                float d = Vector2.Distance(점[다음], 점[i]);
                if (d < 거리[i]) { 거리[i] = d; 어디서[i] = 다음; }
            }
        }

        // ── ③ 밟힘 — 자식이 많은 줄기일수록 넓다
        var 자식수 = new int[m];
        for (int i = m - 1; i >= 0; i--) { }
        foreach (var (a, b) in 간선) { }
        var 밟힘 = new float[m];
        for (int i = 0; i < m; i++) 밟힘[i] = 1f;
        // 잎에서 뿌리로 거슬러 올라가며 더한다 (간선은 부모→자식 순으로 쌓여 있다)
        for (int i = 간선.Count - 1; i >= 0; i--)
            밟힘[간선[i].a] += 밟힘[간선[i].b];

        // ── ④ 끝자락 몇 개는 끊는다 — 발길이 뜸한 곳이 있어야 한다
        var 남길 = new System.Collections.Generic.List<(int a, int b)>();
        foreach (var (a, b) in 간선)
        {
            bool 잎 = 밟힘[b] <= 1.001f;
            if (잎 && rnd.NextDouble() < 0.35) continue;
            남길.Add((a, b));
        }

        Debug.Log($"[길] 갈 이유가 있는 자리 {점.Count}곳 · 이은 길 {남길.Count}줄 · 오지 {점.Count - 1 - 간선.Count}곳");

        // ── ⑤ 실제 선으로 — 굽이치고, 물을 피한다
        var 결과 = new 길줄[남길.Count];
        for (int e = 0; e < 남길.Count; e++)
        {
            var a = 점[남길[e].a]; var b = 점[남길[e].b];
            결과[e] = 한줄긋기(a, b, (float)rnd.NextDouble() * 500f, 밟힘[남길[e].b]);
        }
        return 결과;
    }

    /// 두 자리를 잇는 굽이진 길 하나 — 물은 피해서 돌아간다
    ///
    /// ★★★**길이 자로 그은 선이면 안 된다** (2026-08-06 사용자 — "길들이 그냥 일자로만
    ///   쭉쭉 뻗어잇는곳들이 많던데").
    ///   전에는 펄린 노이즈를 그대로 옆으로 밀었는데, 펄린은 **0.5 언저리에 몰려 있어서**
    ///   실제 흔들림이 ±0.2 밖에 안 됐다. 실측하니 **350m 가는 동안 옆으로 3~5m** —
    ///   굽이가 1.01배, 사실상 직선이었다.
    ///
    ///   → 고친 것 둘:
    ///     ①**진폭을 길이에 비례**시킨다 — 300m 길이면 30~50m 는 휘어야 「굽이」로 보인다.
    ///       짧은 길은 조금, 긴 길은 크게 휜다.
    ///     ②흔들림을 **사인 세 겹**으로 만든다 — 펄린과 달리 **꽉 찬 범위**를 쓴다.
    ///       주파수가 배수 관계가 아니라(1 : 2.3 : 4.7) 같은 무늬가 반복되지 않는다.
    ///   ☆양 끝은 `sin(t·π)` 로 0 이 되어 자리에 **정확히** 닿는다.
    static 길줄 한줄긋기(Vector2 a, Vector2 b, float o, float 밟힘)
    {
        float 길이 = Vector2.Distance(a, b);
        int 점수 = Mathf.Clamp(Mathf.CeilToInt(길이 / 8f), 8, 140);
        var pts = new Vector2[점수];
        var dir = (b - a).normalized;
        var 옆 = new Vector2(-dir.y, dir.x);

        // 길이에 비례한 진폭 (짧은 길이 과하게 휘면 우스워지므로 위아래를 막는다)
        float 진폭 = Mathf.Clamp(길이 * 0.17f, 10f, 70f);
        // 씨앗에서 위상 셋 — 길마다 다른 모양이 나온다
        float p1 = o * 0.017f, p2 = o * 0.041f + 1.3f, p3 = o * 0.093f + 2.7f;
        // 큰 굽이의 세기도 길마다 다르다 (어떤 길은 거의 곧다)
        float 세기 = 0.45f + Mathf.PerlinNoise(o * 0.13f, o * 0.07f) * 1.15f;

        // ★★**굽는 쪽을 물이 적은 쪽으로 고른다** (2026-08-06 — 지도에 길이 호수를
        //   가로지르는 게 보였다). `마른자리` 는 점 하나를 옆으로 미는 것뿐이라
        //   **호수 한가운데**에서는 밀어낼 마른 데가 없다. 애초에 **반대쪽으로 휘면** 된다.
        //   ☆두 방향을 다 그려 보고 물에 잠긴 점이 적은 쪽을 쓴다. 계산은 점 수십 개뿐이라 싸다.
        System.Func<float, int> 젖은수 = (부호) =>
        {
            int 젖음 = 0;
            for (int i = 0; i < 점수; i++)
            {
                float t = i / (float)(점수 - 1);
                var q = Vector2.Lerp(a, b, t) + 옆 * 굽값(t, p1, p2, p3) * 진폭 * 세기 * 부호;
                if (WorldGen.물기(q.x, q.y) > 0.18f) 젖음++;
            }
            return 젖음;
        };
        float 쪽 = 젖은수(-1f) < 젖은수(1f) ? -1f : 1f;

        for (int i = 0; i < 점수; i++)
        {
            float t = i / (float)(점수 - 1);
            pts[i] = 마른자리(Vector2.Lerp(a, b, t) + 옆 * 굽값(t, p1, p2, p3) * 진폭 * 세기 * 쪽);
        }
        return new 길줄 { 점 = pts, 밟힘 = 밟힘 };
    }

    /// 굽이 모양 — 사인 세 겹(1 : 2.3 : 4.7). 양 끝은 0 이라 자리에 정확히 닿는다
    static float 굽값(float t, float p1, float p2, float p3)
        => (Mathf.Sin(t * Mathf.PI * 1.0f + p1) * 1.00f
          + Mathf.Sin(t * Mathf.PI * 2.3f + p2) * 0.48f
          + Mathf.Sin(t * Mathf.PI * 4.7f + p3) * 0.20f) * Mathf.Sin(t * Mathf.PI);

    /// 물이면 옆으로 밀어 마른 자리를 찾는다 — 길이 호수를 가로지르면 안 된다
    static Vector2 마른자리(Vector2 p)
    {
        if (WorldGen.물기(p.x, p.y) <= 0.18f) return p;
        for (float r = 12f; r <= 180f; r += 12f)         // 큰 호수도 돌아 나갈 만큼 멀리 찾는다
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI * 0.25f;
                var q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (WorldGen.물기(q.x, q.y) <= 0.18f) return q;
            }
        return p;                                  // 온통 물이면 그냥 지난다 (여울)
    }

    /// ★길을 **찍어서** 그린다 — 길을 따라 걸으며 그 자리에 동그라미를 찍는다.
    ///
    /// ★왜 이렇게 (2026-08-04 사용자 "처음 게임 켤 때 렉이 심하던데"):
    ///   전에는 **픽셀마다** 모든 길 조각과의 거리를 쟀다. 400만 픽셀 × 조각 315개 =
    ///   **십억 번**이 넘는 계산이라, 켤 때 몇 초씩 멈췄다.
    ///   찍는 방식은 길 길이에만 비례한다 — 수천 번이면 끝난다. **수백 배 빠르다.**
    static void 길찍기(Color32[] px, int size, float m당, 길줄[] 길, float o)
    {
        foreach (var 줄 in 길)
        {
            var pts = 줄.점;
            // ★폭은 **밟힘**에서 나온다 — 집 가까운 줄기는 넓고 끝자락은 좁다.
            //   로그로 눌러야 한두 곳 차이로 폭이 튀지 않는다.
            // ★대비를 키운다 (2026-08-06 사용자 "길 두께도 다 똑같고").
            //   전에는 3.8~7.0 이라 눈으로 안 갈렸다. 이제 **2.2(끝자락) ~ 8.5(큰 줄기)**.
            float 바탕폭 = Mathf.Clamp(1.5f + Mathf.Log(1f + 줄.밟힘) * 2.3f, 2.2f, 8.5f);
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
                    float 흔들 = (Mathf.PerlinNoise(p.x * 0.09f + o, p.y * 0.09f + o) - 0.5f) * 2.0f
                               + (Mathf.PerlinNoise(p.x * 0.31f + o, p.y * 0.31f + o) - 0.5f) * 0.9f;

                    // ★★길은 **중간중간 희미해진다** — 원시시대의 길은 끝까지 또렷하지 않다.
                    //   낮은 주파수 노이즈가 얕은 구간을 만들어, 같은 길인데도 구간마다 다르게 보인다.
                    float 진함 = Mathf.PerlinNoise(p.x * 0.004f + o + 61f, p.y * 0.004f + o + 61f);
                    if (진함 < 0.22f) continue;                       // 여기는 풀에 덮여 길이 끊겼다
                    // ★희미해지는 몫을 줄였다 — 너무 크면 「굵은 길/가는 길」 차이를 덮어 버린다
                    float 폭 = (바탕폭 + 흔들) * Mathf.Lerp(0.75f, 1f, Mathf.InverseLerp(0.22f, 0.5f, 진함));
                    if (폭 < 1.0f) continue;
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
                if (d < r) { px[i] = 흙; 잔디자리[i] = 0; 결종류[i] = 3; }   // 길엔 흙결
                else
                {
                    float t = (d - r) / 1.8f;
                    var cur = (Color)px[i];
                    px[i] = Color.Lerp(흙, cur, t);
                    // ★★흙이 **조금이라도 섞인 자리**엔 풀을 안 낸다 (2026-08-05 사용자 —
                    //   "흙으로 넘어가는 잔디들이있어"). 전에는 절반(t<0.5)만 지워서, 바깥
                    //   절반에 남은 풀이 갈색 길 위에 초록으로 서 있었다.
                    if (t < 0.92f) { 잔디자리[i] = 0; 결종류[i] = 3; }
                }
            }
    }
}
