using System.Collections.Generic;
using UnityEngine;

/// 절차 생성 — 칸(`WorldGrid`)마다 랜드마크를 하나씩 앉힌다.
///
/// ★방식: 조각은 손으로, 조립은 기계가. 무작위 지형을 뽑는 게 아니라 **정해진 종류를
///   격자 위에 규칙대로 흩뿌린다.** 품질은 만든 만큼 나오고 배치만 매번 달라진다.
///
/// ★지금은 전부 **색칠한 상자**다. 진짜 모델로 바꾸는 자리는 아래 「교체 자리」 —
///   프리팹을 끌어다 넣으면 그때부터 상자 대신 그게 나온다. 코드는 안 고쳐도 된다.
///
/// ★땅은 지형(Terrain)이 아니라 **판때기 하나**다. 완전 평지라 높이 데이터가 필요 없고,
///   옛 프로젝트를 무겁게 하던 16MB 짜리 높이 파일도 없다.
public class WorldGen : MonoBehaviour
{
    public enum Land { 빈들판, 숲, 바위지대, 물웅덩이, 폐허, 둥지, 캠프 }

    [Header("씨앗")]
    [Tooltip("0 = 켤 때마다 새 맵 · 다른 숫자 = 그 숫자의 맵이 항상 똑같이 나온다")]
    public int worldSeed = 0;

    [Header("칸 종류가 뽑히는 비율")]
    public float w빈들판 = 4f, w숲 = 2.5f, w바위 = 2f, w물 = 1f, w폐허 = 0.8f, w둥지 = 0.8f;

    [Header("★교체 자리 — 프리팹을 넣으면 상자 대신 그게 나온다")]
    public GameObject[] 나무프리팹;
    public GameObject[] 바위프리팹;
    public GameObject[] 폐허프리팹;
    public GameObject 둥지프리팹;
    public GameObject 부화터프리팹;
    public GameObject 물프리팹;

    // 상자 색 — 「무엇인지 색으로 안다」
    public static readonly Color C땅 = new Color(0.24f, 0.30f, 0.19f);
    static readonly Color C잎 = new Color(0.20f, 0.42f, 0.20f);
    static readonly Color C줄기 = new Color(0.30f, 0.22f, 0.14f);
    static readonly Color C바위 = new Color(0.42f, 0.42f, 0.40f);
    static readonly Color C폐허 = new Color(0.33f, 0.32f, 0.30f);
    static readonly Color C물 = new Color(0.18f, 0.38f, 0.50f);
    static readonly Color C둥지 = new Color(0.70f, 0.42f, 0.15f);
    static readonly Color C알 = new Color(0.90f, 0.87f, 0.80f);
    static readonly Color C캠프 = new Color(0.75f, 0.28f, 0.28f);

    Land[,] kinds;
    Transform holder;

    // ══════════════════════════════════════════════════════════
    // ★★★**씬(과 유니티가 메모리에 들고 있는 값)이 코드 기본값을 이긴다.**
    //   필드 기본값을 고쳐도 **이미 열려 있는 씬의 부품에는 안 먹는다** — 유니티가 컴파일할
    //   때마다 부품 값을 저장했다 되살리기 때문이다. 그래서 「씬을 다시 열면 괜찮아지는」
    //   이상한 일이 생긴다 (2026-08-05 사용자 — "sample scene 여니까 또 괜찮은거 같고
    //   계속 열어줘야하니 혹시?"). 씬을 여닫는 건 사용자가 할 일이 아니다.
    //   → `PixelScreen` 과 같은 방식: **정본 번호**를 두고, 씬에 적힌 번호가 낮으면
    //     아래 값들을 코드 것으로 덮어쓴다. 숫자를 새로 정할 때 번호를 하나 올리면 된다.
    //   ★플레이 중 인스펙터로 만지는 건 그대로 먹는다 (그때는 이미 덮어쓴 뒤다).
    const int 정본지금 = 18;
    [Tooltip("코드가 정한 값으로 맞춘 번호 — 건드리지 않는다")] public int 정본 = 0;

    void 정본맞추기()
    {
        if (정본 >= 정본지금) return;
        // ★★땅은 **두 겹**이다 (2026-08-05 사용자 — "크게 얼룩덜룩은 그대로 두고,
        //   잘게도 재질을 작게 텍스처도 넣어서"):
        //     ①큰 얼룩덜룩 = 노이즈 (11m) — 색이 스르르 짙어졌다 옅어진다
        //     ②잘은 결     = 사진 타일 (3m 에 32칸 = 한 칸 9cm) — 표면의 결
        //   ★잘은 것은 **아주 약해야** 한다. 앞서 세 번 「지저분하다」고 한 건 잘아서가
        //     아니라 **세서**였다 (0.35 → 자국이 또렷해져 때로 읽혔다). 0.12 면 밝기가
        //     ±5% 라 자국이 안 잡히고 표면의 결로만 읽힌다.
        //   ☆한 번 껐었다 (2026-08-05). 0.35 → 0.12 → 0.06 으로 낮춰도 "지저분하다" 가
        //     계속 나왔는데, 알고 보니 자글거림의 정체가 **픽셀 화면의 디더링**이었다.
        //     디더링을 끄고 나니 이 결이 비로소 제 몫을 한다 — 사용자가 "추가로 자글자글이
        //     있어야함" 이라 한 그 자리다. 그래서 되켠다.
        //   ☆★결론: **사진에서 뽑은 결로는 안 된다** (2026-08-05 사용자 — "자글자글은
        //     넣기만하면 왜이렇게 오류가 날까 … 재질 자체 문제인거같긴한데". 맞는 진단이었다).
        //     사진을 줄이면 픽셀 하나하나가 옆 칸과 무관한 값이 되어 **잡음**이 된다.
        //     사람이 찍는 픽셀은 두세 칸이 뭉쳐 하나의 자국이 되고 그 사이가 **비어 있다.**
        //     세기를 낮추면 잡음의 세기가 줄 뿐, 잡음이라는 성질은 그대로다.
        땅결 = 0f;
        땅결칸 = 3f;
        땅얼룩 = 0f;        // 땅 그림에 굽는 방식 — 0.7m 가 한계라 안 쓴다
        // ★★큰 것과 잔 것 (2026-08-05 사용자 — "60m정도로 큰거는 넣어주고,
        //   작은거는 1m까지 못줄이나?"). 120m 는 한 화면에 하나뿐이라 안 보였다 → 60m.
        // ☆채도를 키우면 **황토색이 주황빛으로 탁해진다** (2026-08-05 "색도 탁해졌고").
        //   진하기 방식도 세게 주면 못 쓴다 — 아주 옅게만 남기고, 제대로 된 방식은 셰이더로.
        큰무늬 = 0.12f;
        큰무늬칸 = 45f;
        잔무늬 = 0.06f;
        잔무늬칸 = 2f;
        // ★★★**10cm 짜리는 땅 그림에 못 굽는다** (2026-08-05 "작은 자글자글은 지금보다
        //   10분의1 정도"). 1440m 를 10cm 로 그리려면 36000픽셀짜리 그림이 필요하다.
        //   → 그 크기는 **재질에 타일로 깔아야** 한다. 해상도와 무관해지기 때문이다.
        //   이제 그 타일은 사진이 아니라 **노이즈로 찍은 것**이라 잘아도 잡음이 안 된다.
        //   6.4m 마다 되풀이되고 얼룩 하나가 **10cm** 다.
        // ★잔결도 **진하기**로 바꿨다 — 타일이 파랑만 빼도록 구워져 있어서,
        //   어느 땅색이든 색조 그대로 진해지기만 한다 (밝기는 거의 안 변한다).
        // ★이제 **땅 전용 셰이더**가 자리마다 다른 결을 깐다 (`Toyra/Ground`).
        //   결 uv 가 월드 좌표라 땅 그림 해상도와 무관하다 — 크기를 마음대로 정할 수 있다.
        땅결 = 0.35f;
        땅결칸 = 2f;        // 결 한 장이 2m 를 덮는다 (32칸이면 한 칸 6cm)
        // 2048 로 되돌린다 — 제일 잔 무늬가 재질 타일로 옮겨 갔으므로 해상도를 올릴 이유가
        // 없어졌다. 4배 느려질 일도 없다.
        땅해상도 = 2048;
        정본 = 정본지금;
    }

    public void Generate()
    {
        정본맞추기();
        int seed = worldSeed != 0 ? worldSeed : Random.Range(1, int.MaxValue);
        var save = Random.state;

        Blocker.Clear();
        PickKinds(seed);
        Build(seed);

        Random.state = save;
        Debug.Log($"[월드] 씨앗 {seed} · {WorldGrid.N}×{WorldGrid.N}칸 · 한 변 {WorldGrid.Size}m");
    }

    // ── ① 어느 칸에 무엇이 오나
    void PickKinds(int seed)
    {
        int n = WorldGrid.N, home = WorldGrid.Home;
        kinds = new Land[n, n];

        for (int x = 0; x < n; x++)
            for (int z = 0; z < n; z++)
            {
                if (x == home && z == home) { kinds[x, z] = Land.캠프; continue; }
                Random.InitState(WorldGrid.TileSeed(seed, x, z));

                // 집 옆 여덟 칸은 트여 있어야 한다 — 나가자마자 벽이면 답답하다
                if (Mathf.Abs(x - home) <= 1 && Mathf.Abs(z - home) <= 1)
                { kinds[x, z] = Random.value < 0.6f ? Land.빈들판 : Land.숲; continue; }

                kinds[x, z] = Roll();
            }

        // 물은 뭉쳐야 호수로 보인다
        var add = new List<Vector2Int>();
        for (int x = 0; x < n; x++)
            for (int z = 0; z < n; z++)
                if (kinds[x, z] == Land.빈들판 && Neighbors(x, z, Land.물웅덩이) >= 2)
                    add.Add(new Vector2Int(x, z));
        foreach (var p in add) kinds[p.x, p.y] = Land.물웅덩이;

        // 바위가 넉 칸 넘게 뭉치면 벽이 된다 — 하나를 튼다
        for (int x = 0; x < n; x++)
            for (int z = 0; z < n; z++)
                if (kinds[x, z] == Land.바위지대 && Neighbors(x, z, Land.바위지대) >= 3)
                    kinds[x, z] = Land.빈들판;
    }

    int Neighbors(int x, int z, Land k)
    {
        int c = 0;
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0) continue;
                if (WorldGrid.InRange(x + dx, z + dz) && kinds[x + dx, z + dz] == k) c++;
            }
        return c;
    }

    Land Roll()
    {
        float total = w빈들판 + w숲 + w바위 + w물 + w폐허 + w둥지;
        float r = Random.value * total;
        if ((r -= w빈들판) < 0) return Land.빈들판;
        if ((r -= w숲) < 0) return Land.숲;
        if ((r -= w바위) < 0) return Land.바위지대;
        if ((r -= w물) < 0) return Land.물웅덩이;
        if ((r -= w폐허) < 0) return Land.폐허;
        return Land.둥지;
    }

    // ── ② 실제로 세운다
    void Build(int seed)
    {
        Clear();
        holder = new GameObject("월드").transform;
        holder.SetParent(transform, false);

        MakeGround(seed);

        int n = WorldGrid.N;
        for (int x = 0; x < n; x++)
            for (int z = 0; z < n; z++)
            {
                var k = kinds[x, z];

                // ★어느 칸이든 돌·나무를 조금씩 흩뿌린다 (2026-08-04 사용자 "돌이랑 나무도
                //   좀 넣어줘봐"). 빈 들판이 정말로 텅 비어 있으면 걸어갈 맛이 없다.
                Random.InitState(WorldGrid.TileSeed(seed, x, z) ^ 0x77aa);
                if (k != Land.캠프 && k != Land.물웅덩이) 흩뿌리기(x, z, k);

                if (k == Land.빈들판) continue;

                Random.InitState(WorldGrid.TileSeed(seed, x, z) ^ 0x5f3a);

                // 칸 한가운데가 아니라 랜덤하게 밀어서 — 이게 격자를 숨긴다
                var c = WorldGrid.TileCenter(x, z);
                if (k != Land.캠프)
                {
                    c.x += Random.Range(-1f, 1f) * WorldGrid.Tile * 0.25f;
                    c.z += Random.Range(-1f, 1f) * WorldGrid.Tile * 0.25f;
                }

                switch (k)
                {
                    case Land.숲: Forest(c); break;
                    case Land.바위지대: Rocks(c); break;
                    case Land.물웅덩이: Water(c); break;
                    case Land.폐허: Ruin(c); break;
                    case Land.둥지: Nest(c); break;
                    case Land.캠프: Camp(c); break;
                }
            }
    }

    public void Clear()
    {
        var old = transform.Find("월드");
        if (old != null)
        {
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
        }
        holder = null;
    }

    [Header("땅 그림")]
    [Tooltip("땅 텍스처 한 변의 텍셀 수 (클수록 곱다). 2048 = 텍셀 하나가 0.7m")]
    public int 땅해상도 = 2048;
    [Tooltip("잔디·흙길·물가를 칠한다. 끄면 민무늬 초록")]
    public bool 땅칠하기 = true;
    // ★★★결은 땅 그림에 **굽지 않는다** (2026-08-05 사용자 "여전히 깨지는데, 훨씬 재질이
    //   작게작게 적용돼야하지않을까"). 땅 그림은 1440m 를 2048칸으로 덮어 **한 칸이 0.7m** 고,
    //   격자로 안 보이게 **부드럽게 늘려서** 뽑는다. 그러니 아무리 잘게 그려도 0.7m 가
    //   최소 단위이고 그게 다시 뭉개진다 — 재질이 아니라 얼룩이 되는 이유가 이것이다.
    //   → 결은 **재질에 따로 깔아** 몇 미터마다 되풀이시킨다. 땅 그림 해상도와 무관해진다.
    [Tooltip("결 타일 한 장이 덮는 크기 (m) — 작을수록 재질이 잘아진다")]
    [Range(0.5f, 16f)] public float 땅결칸 = 4f;
    // ★★★기본은 **끔**이다 (2026-08-05 사용자 — 세 번 다듬었는데 세 번 다 "지저분해").
    //   사진에서 뽑은 결은 아무리 잘게·드물게 해도 **불규칙**하다. 그런데 화면의 나머지
    //   전부(장난감 펫 · 뚜렷한 외곽선 · 크게 나눈 색면)가 **규칙적이고 깔끔한 것**이라,
    //   그 사이에 낀 불규칙한 얼룩은 재질이 아니라 **때**로 읽힌다.
    //   → 방법을 더 다듬을 문제가 아니라 **전제가 틀린 것**이었다. 장치는 남기고 끈다.
    //   ☆땅에 무언가가 필요하다고 느껴지면 표면이 아니라 **잔디 포기**(`GrassField`)를
    //     늘리는 쪽이 맞다 — 그건 규칙적인 모양이라 이 화면에 어울린다.
    [Tooltip("결의 세기 (0 = 끔). 사진 결은 이 스타일과 안 맞아서 꺼 뒀다")]
    [Range(0f, 1f)] public float 땅결 = 0f;
    // 굽는 방식은 지우지 않고 꺼 둔다 (은퇴는 삭제가 아니라 스위치).
    // 켜면 0.7m 짜리 큰 얼룩이 생긴다 — 결이 아니라 「땅 무늬」가 필요할 때 쓸 자리다.
    [Tooltip("땅 그림에 직접 굽는 사진 결 (0 = 안 씀)")]
    [Range(0f, 0.5f)] public float 땅얼룩 = 0f;

    // ★★"은은한 얼룩덜룩" 은 **사진이 아니라 노이즈**로 낸다 (`GroundPaint.잔얼룩얹기` 참고).
    //   부드럽게 이어지는 값이라 자국이 안 생긴다 — 색이 스르르 짙어졌다 옅어질 뿐이다.
    // ★★큰 것과 잔 것은 **따로** 잡는다. 한 손잡이에 묶으면 큰 걸 키울 때 잔 게 묻힌다
    [Tooltip("큰 흐름의 세기 (0.28 = 밝기 ±14%)")]
    [Range(0f, 0.6f)] public float 큰무늬 = 0.28f;
    [Tooltip("큰 흐름 하나의 크기 (m)")]
    [Range(20f, 400f)] public float 큰무늬칸 = 120f;
    [Tooltip("잔 결의 세기 (0.14 = 밝기 ±7%)")]
    [Range(0f, 0.4f)] public float 잔무늬 = 0.14f;
    [Tooltip("잔 결 하나의 크기 (m) — 땅 그림 한 칸이 0.7m 라 그 아래로는 못 내려간다")]
    [Range(1.5f, 20f)] public float 잔무늬칸 = 4.5f;

    /// 땅 — 지형이 아니라 판때기 하나 (완전 평지). 잔디·길·물은 **칠해서** 넣는다
    void MakeGround(int seed)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
        g.name = "땅";
        g.transform.SetParent(holder, true);
        g.transform.position = new Vector3(WorldGrid.Size * 0.5f, 0f, WorldGrid.Size * 0.5f);
        g.transform.localScale = Vector3.one * (WorldGrid.Size / 10f);   // Plane 은 한 변 10m

        // ★★180도 돌린다 (2026-08-04). 유니티 기본 Plane 은 UV 가 **양쪽 축 모두 거꾸로**다
        //   (world +X,+Z 모서리가 uv 0,0). 그대로 두면 땅 그림이 점대칭으로 뒤집혀 붙어서,
        //   화면의 흙길과 코드가 아는 흙길의 자리가 **정반대**가 된다.
        //   → 실제로 "흙길에 잔디가 난다" 는 버그가 났다. 180도 돌리면 딱 맞는다.
        g.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        Grey.Strip(g);

        var mr = g.GetComponent<MeshRenderer>();
        if (!땅칠하기) { mr.sharedMaterial = Grey.Mat(C땅); return; }

        var tex = GroundPaint.만들기(seed, Mathf.Clamp(땅해상도, 256, 4096), KindAt,
                                     땅얼룩, 큰무늬, 큰무늬칸, 잔무늬, 잔무늬칸);
        // ★★땅 전용 셰이더가 있으면 그걸 쓴다 — **자리마다 다른 결**을 깔 수 있는 건 이쪽뿐이다.
        //   없으면 예전대로 유니티 기본 재질로 돌아간다 (은퇴는 삭제가 아니라 스위치).
        var 땅셰이더 = Shader.Find("Toyra/Ground");
        Material m;
        if (땅셰이더 != null)
        {
            m = new Material(땅셰이더) { name = "땅" };
            m.SetTexture("_BaseMap", tex);
            if (GroundPaint.땅마스크 != null) m.SetTexture("_MaskMap", GroundPaint.땅마스크);
            결텍스처(m, "_GrassTex", "ground/결_잔디");
            결텍스처(m, "_DirtTex", "ground/결_흙");
            결텍스처(m, "_SandTex", "ground/결_모래");
            // 결 uv 는 월드 좌표라, 1m 에 몇 장 깔리나로 준다
            m.SetFloat("_DetailTiling", 1f / Mathf.Max(0.05f, 땅결칸));
            m.SetFloat("_DetailStrength", 땅결);
        }
        else
        {
            Debug.LogWarning("[땅] Toyra/Ground 셰이더를 못 찾았다 — 기본 재질로 간다");
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "땅" };
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Smoothness", 0.03f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            결깔기(m);
        }
        mr.sharedMaterial = m;

        // ★무엇이 실제로 걸렸는지 남긴다 — 짐작으로 값을 만지지 않으려고 (2026-08-05).
        //   화면만 보고는 「사진 결」과 「노이즈 잔얼룩」을 헷갈리기 쉽다.
        Debug.Log($"[땅] 큰 {큰무늬:0.00}({큰무늬칸:0}m) · 중 {잔무늬:0.00}({잔무늬칸:0.0}m)" +
                  $" · 잔 {땅결:0.00}({땅결칸 * 100f / 64f:0}cm)");
    }

    /// 결 그림 한 장을 꽂는다. 없으면 회색(= 아무것도 안 함)이 기본값이라 그냥 비워 둔다
    static void 결텍스처(Material m, string 자리, string 길)
    {
        var t = Resources.Load<Texture2D>(길);
        if (t != null) m.SetTexture(자리, t);
        else Debug.LogWarning("[땅] 결 그림 없음: " + 길);
    }

    /// ★땅에 **결을 깐다** — 땅 그림과 따로, 몇 미터마다 되풀이되는 작은 타일로.
    ///
    /// ★URP 의 detail 은 **「밑색 × (결 × 2)」**로 곱한다. 그래서 결 그림은 회색이고
    ///   평균이 정확히 0.5 다 (0.5 × 2 = 1 = 아무것도 안 함). 덕분에 팔레트 색도,
    ///   땅 전체 밝기도 하나도 안 변하고 **얼룩만** 생긴다.
    /// ★세기는 `_DetailAlbedoMapScale` 하나로 조절한다 — 그림을 다시 안 만들고
    ///   인스펙터에서 바로 맞출 수 있어야 하므로 대비는 그림에 넉넉히 넣어 뒀다.
    void 결깔기(Material m)
    {
        if (땅결 <= 0.001f) return;
        // ★사진에서 뽑은 결(`결_잔디`)이 아니라 **노이즈로 찍은 결**을 쓴다.
        //   사진은 픽셀끼리 무관해져 잡음이 됐다 — 노이즈는 잘아도 옆 칸과 이어진다.
        var 결 = Resources.Load<Texture2D>("ground/결_노이즈");
        if (결 == null) { Debug.LogWarning("[땅] Resources/ground/결_노이즈 를 못 찾았다"); return; }
        if (!m.HasProperty("_DetailAlbedoMap")) return;

        m.EnableKeyword("_DETAIL_MULX2");        // 이걸 안 켜면 셰이더가 결을 아예 안 읽는다
        m.SetTexture("_DetailAlbedoMap", 결);
        float 반복 = WorldGrid.Size / Mathf.Max(0.5f, 땅결칸);
        m.SetTextureScale("_DetailAlbedoMap", Vector2.one * 반복);
        if (m.HasProperty("_DetailAlbedoMapScale")) m.SetFloat("_DetailAlbedoMapScale", 땅결);

        // ★물 위엔 결을 안 깐다 — 마스크의 알파가 곧 결의 세기다
        if (GroundPaint.땅마스크 != null && m.HasProperty("_DetailMask"))
            m.SetTexture("_DetailMask", GroundPaint.땅마스크);
    }

    // ══════════════════════════════════════════════════════════
    //  랜드마크 (크기는 전부 사람 1.8m 기준)
    // ══════════════════════════════════════════════════════════

    void Forest(Vector3 c)
    {
        int count = Random.Range(24, 46);
        float spread = Random.Range(35f, 60f);
        for (int i = 0; i < count; i++)
        {
            var p = Scatter(c, spread);
            if (Swap(나무프리팹, p, true, 0.5f)) continue;
            float h = Random.Range(9f, 15f);              // 나무 9~15m — 사람의 5~8배
            float w = Random.Range(4f, 7f);
            float tr = Random.Range(0.4f, 0.7f);
            var trunk = Grey.Box(holder, p + Vector3.up * (h * 0.3f), new Vector3(tr, h * 0.6f, tr), C줄기, "나무_줄기");
            var leaf = Grey.Box(holder, p + Vector3.up * (h * 0.75f), new Vector3(w, h * 0.5f, w), C잎, "나무_잎");
            leaf.transform.SetParent(trunk.transform, true);   // 줄기를 캐면 잎도 같이 사라진다
            Blocker.Add(p, tr * 0.7f);                         // 줄기 굵기만 막는다

            // ★선 나무를 패면 자원이 아니라 **쓰러진다.** 나무는 통나무를 패야 나온다
            var fall = trunk.AddComponent<TreeFall>();
            fall.통나무값 = Mathf.RoundToInt(4f + h * 0.5f);      // 큰 나무일수록 많이
            fall.선자리 = p;

            var hv = trunk.AddComponent<Harvest>();
            hv.kind = Stock.Kind.나무;
            hv.hits = 4; hv.perHit = 0;        // 선 나무를 팬다고 나무가 나오진 않는다
            hv.blockAt = p; hv.장애물치우기 = false;
            hv.쓰러짐 = fall;

            // ★나무가 750그루라 그루당 서너 포기면 2천 개가 넘는다 — 켤 때 그게 곧 렉이다
            밑동풀(p, tr * 1.6f, Random.Range(0, 3));
        }
    }

    void Rocks(Vector3 c)
    {
        int count = Random.Range(7, 15);
        float spread = Random.Range(15f, 28f);
        for (int i = 0; i < count; i++)
        {
            var p = i == 0 ? c : Scatter(c, spread);
            if (Swap(바위프리팹, p, true, 1.2f)) continue;
            float w = i == 0 ? Random.Range(3.5f, 5.5f) : Random.Range(1.2f, 2.8f);
            float h = w * Random.Range(0.7f, 1.4f);
            var rock = Grey.Box(holder, p + Vector3.up * (h * 0.42f),
                     new Vector3(w, h, w * Random.Range(0.7f, 1.3f)), C바위, "바위",
                     w * 0.5f, Random.value * 360f);

            var hv = rock.AddComponent<Harvest>();
            hv.kind = Stock.Kind.돌; hv.hits = Mathf.RoundToInt(3f + w); hv.perHit = 2; hv.blockAt = p;

            밑동풀(p, w * 0.7f, Random.Range(1, 4));
        }
    }

    void Ruin(Vector3 c)
    {
        int count = Random.Range(5, 9);
        float r = Random.Range(8f, 14f);
        float a0 = Random.value * Mathf.PI * 2f;
        for (int i = 0; i < count; i++)
        {
            float a = a0 + i * Mathf.PI * 2f / count + Random.Range(-0.16f, 0.16f);
            var p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
            if (Swap(폐허프리팹, p, true, 0.8f)) continue;
            float w = Random.Range(0.8f, 1.4f), h = Random.Range(3.5f, 6f);
            Grey.Box(holder, p + Vector3.up * (h * 0.45f), new Vector3(w, h, w), C폐허, "돌기둥",
                     w * 0.6f, Random.value * 360f);
        }
        // 하나쯤은 쓰러져 있어야 폐허로 읽힌다
        var f = Scatter(c, r * 0.5f);
        Grey.Box(holder, f + Vector3.up * 0.5f, new Vector3(Random.Range(3f, 5f), 1f, 1.2f),
                 C폐허, "쓰러진돌", 1.2f, Random.value * 360f);
    }

    void Nest(Vector3 c)
    {
        if (Swap(둥지프리팹 != null ? new[] { 둥지프리팹 } : null, c, false, 2.5f)) return;
        Grey.Box(holder, c + Vector3.up * 0.4f, new Vector3(8f, 0.8f, 8f), C둥지, "둥지", 3.4f);
        int eggs = Random.Range(2, 5);
        for (int i = 0; i < eggs; i++)
            Grey.Box(holder, Scatter(c, 2.2f) + Vector3.up * 1.1f,
                     new Vector3(0.7f, 0.9f, 0.7f), C알, "알");
    }

    void Camp(Vector3 c)
    {
        Swap(부화터프리팹 != null ? new[] { 부화터프리팹 } : null, c, false, 4f);
        // ★★모델이 없으면 **아무것도 안 세운다** (2026-08-05 사용자 "바닥에 네모 바닥
        //   시작 바닥같은거 지우라니까"). 처음엔 10×4×10 빨간 덩어리, 그다음엔 낮은
        //   바닥판으로 줄였는데 그것도 눈에 거슬린다. 자리 표시가 화면을 더럽히면
        //   표시로서 값을 못 한다 — 진짜 부화터 모델이 생기면 위 `Swap` 이 세운다.
    }

    /// 물은 **땅에 칠한다** (`GroundPaint`) — 겹침·정렬 문제가 없고 픽셀 화면과도 맞는다.
    /// 여기서는 물가에 돌만 몇 개 놓는다
    void Water(Vector3 c)
    {
        if (Swap(물프리팹 != null ? new[] { 물프리팹 } : null, c, false, 0f)) return;
        if (!땅칠하기)
        {
            // 칠하기를 껐으면 옛날처럼 파란 원반으로
            float r0 = Random.Range(14f, 26f);
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = "물웅덩이";
            g.transform.SetParent(holder, true);
            g.transform.localScale = new Vector3(r0 * 2f, 0.04f, r0 * 2f);
            g.transform.position = c + Vector3.up * 0.03f;
            Grey.Strip(g);
            g.GetComponent<MeshRenderer>().sharedMaterial = Grey.Mat(C물);
            return;
        }

        int n = Random.Range(3, 7);
        for (int i = 0; i < n; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            var p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(40f, 48f);
            float w = Random.Range(0.8f, 2.2f), h = w * Random.Range(0.6f, 1.1f);
            Grey.Box(holder, p + Vector3.up * (h * 0.4f), new Vector3(w, h, w), C바위, "물가돌",
                     w * 0.5f, Random.value * 360f);
        }
    }

    /// 그 자리가 어떤 칸인가 — 야생 구성이 이걸 보고 갈린다
    public Land KindAt(Vector3 p)
    {
        if (kinds == null) return Land.빈들판;
        int x = Mathf.Clamp(Mathf.FloorToInt(p.x / WorldGrid.Tile), 0, WorldGrid.N - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt(p.z / WorldGrid.Tile), 0, WorldGrid.N - 1);
        return kinds[x, z];
    }

    [Header("벌판에 흩뿌리기")]
    [Tooltip("칸 하나에 흩뿌릴 돌 수 (최대)")] public int 흩뿌린돌 = 9;
    [Tooltip("칸 하나에 흩뿌릴 나무 수 (최대)")] public int 흩뿌린나무 = 3;

    /// 칸 전체에 돌·나무를 성기게 흩뿌린다 — 길·물 위에는 안 놓는다
    void 흩뿌리기(int gx, int gz, Land k)
    {
        var 중심 = WorldGrid.TileCenter(gx, gz);
        float 반 = WorldGrid.Tile * 0.48f;

        int 돌 = Random.Range(2, 흩뿌린돌 + 1);
        if (k == Land.바위지대) 돌 *= 2;
        for (int i = 0; i < 돌; i++)
        {
            var p = 중심 + new Vector3(Random.Range(-반, 반), 0f, Random.Range(-반, 반));
            if (!GroundPaint.잔디인가(p)) continue;
            float w = Random.Range(0.5f, 1.8f);
            float h = w * Random.Range(0.5f, 1.0f);
            Grey.Box(holder, p + Vector3.up * (h * 0.4f),
                     new Vector3(w, h, w * Random.Range(0.7f, 1.3f)), C바위, "돌멩이",
                     w * 0.4f, Random.value * 360f);
        }

        int 나무 = Random.Range(0, 흩뿌린나무 + 1);
        if (k == Land.숲) 나무 = 0;                       // 숲은 이미 빽빽하다
        for (int i = 0; i < 나무; i++)
        {
            var p = 중심 + new Vector3(Random.Range(-반, 반), 0f, Random.Range(-반, 반));
            if (!GroundPaint.잔디인가(p)) continue;
            나무하나(p, Random.Range(7f, 13f));
        }
    }

    /// 나무 한 그루 (줄기 + 잎 + 밑동풀 + 벌목)
    void 나무하나(Vector3 p, float h)
    {
        float w = h * Random.Range(0.42f, 0.55f);
        float tr = h * Random.Range(0.04f, 0.06f);

        var trunk = Grey.Box(holder, p + Vector3.up * (h * 0.3f), new Vector3(tr, h * 0.6f, tr), C줄기, "나무_줄기");
        var leaf = Grey.Box(holder, p + Vector3.up * (h * 0.75f), new Vector3(w, h * 0.5f, w), C잎, "나무_잎");
        leaf.transform.SetParent(trunk.transform, true);
        Blocker.Add(p, tr * 0.7f);

        var fall = trunk.AddComponent<TreeFall>();
        fall.통나무값 = Mathf.RoundToInt(4f + h * 0.5f);
        fall.선자리 = p;

        var hv = trunk.AddComponent<Harvest>();
        hv.kind = Stock.Kind.나무;
        hv.hits = 4; hv.perHit = 0;
        hv.blockAt = p; hv.장애물치우기 = false;
        hv.쓰러짐 = fall;

        밑동풀(p, tr * 1.6f, Random.Range(0, 3));
    }

    /// ★물체 밑동에 잔디 덤불 (2026-08-04 참고 그림 — 들판을 고르게 덮는 게 아니라
    ///   바위·나무 **밑동에 모여 있다**). 이것만으로 물체가 땅에 「심긴」 것처럼 보인다.
    void 밑동풀(Vector3 c, float 반경, int 수)
    {
        for (int i = 0; i < 수; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            var p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 반경 * Random.Range(0.55f, 1.15f);
            float h = Random.Range(0.45f, 0.95f);
            float w = Random.Range(0.35f, 0.7f);
            var col = Random.value < 0.5f ? C밑동풀A : C밑동풀B;
            Grey.Box(holder, p + Vector3.up * (h * 0.5f), new Vector3(w, h, w * Random.Range(0.7f, 1.3f)),
                     col, "풀", 0f, Random.value * 360f);
        }
    }

    static readonly Color C밑동풀A = new Color(0.42f, 0.60f, 0.33f);
    static readonly Color C밑동풀B = new Color(0.36f, 0.53f, 0.29f);

    // ── 부품
    Vector3 Scatter(Vector3 c, float radius)
    {
        float a = Random.value * Mathf.PI * 2f;
        return c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * Mathf.Sqrt(Random.value);
    }

    bool Swap(GameObject[] set, Vector3 pos, bool randomYaw, float blockR)
    {
        if (set == null || set.Length == 0) return false;
        var pf = set[Random.Range(0, set.Length)];
        if (pf == null) return false;
        var rot = randomYaw ? Quaternion.Euler(0f, Random.value * 360f, 0f) : Quaternion.identity;
        Instantiate(pf, pos, rot, holder);
        if (blockR > 0f) Blocker.Add(pos, blockR);
        return true;
    }
}
