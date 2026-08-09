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
    // ★`w물` 은 0 이다 — 물은 칸이 아니라 노이즈 장(`물기`)이 정한다 (2026-08-06).
    //   손잡이는 `물칸`·`물문턱`. 여긴 은퇴한 자리라 지우지 않고 0 으로 둔다.
    public float w빈들판 = 4f, w숲 = 2.5f, w바위 = 2f, w물 = 0f, w폐허 = 0.8f, w둥지 = 0.8f;

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

        // ★★물 칸을 뭉치던 규칙은 **은퇴**했다 (2026-08-06). 물은 이제 칸이 아니라
        //   **노이즈 장**(`물기`)이 정한다 — 칸을 뭉칠 이유가 없어졌고, 칸으로 두면
        //   「칸은 물인데 실제로는 뭍」 같은 어긋남이 생긴다.
        //   ☆`w물` 도 0 으로 내렸다. 손잡이는 `물칸`·`물문턱` 쪽이다.

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

                    // ★★물이 「장」이 된 뒤로는 랜드마크가 호수 위에 앉을 수 있다 (2026-08-06).
                    //   마른 자리를 몇 번 찔러 보고, 끝내 못 찾으면 이 칸은 물이 차지한 것이다.
                    var 원래 = c;
                    bool 마름 = !물인가(c);
                    for (int t = 0; t < 8 && !마름; t++)
                    {
                        c = 원래 + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * WorldGrid.Tile * 0.42f;
                        마름 = !물인가(c);
                    }
                    if (!마름) continue;              // 온통 물이면 아무것도 안 세운다
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
        자잼 = false;          // 손잡이를 만지고 다시 지으면 노이즈 자도 다시 잰다
    }

    [Header("땅 그림")]
    [Tooltip("땅 텍스처 한 변의 텍셀 수 (클수록 곱다). 2048 = 텍셀 하나가 0.7m")]
    public int 땅해상도 = 2048;
    [Tooltip("잔디·흙길·물가를 칠한다. 끄면 민무늬 초록")]
    public bool 땅칠하기 = true;
    // ★★잔디·흙길·물의 **경계만** 칸 단위로 끊는다 (2026-08-09 사용자 "길과 잔디등등이
    //   분리되는 기준이 격자형식으로"). 색은 하나도 안 바뀐다 — 칠하는 자리를 칸 가운데로
    //   스냅할 뿐이다. `격자바닥` 의 `칸` 과 같은 값을 써야 칸이 어긋나지 않는다.
    [Tooltip("경계를 끊는 칸 크기 (m) — 0 이면 예전처럼 매끄럽게")]
    public float 격자칸 = 1.4f;
    [Tooltip("칸마다 밝기가 조금씩 다르게 — 0 이면 균일")] [Range(0f, 0.4f)]
    public float 칸색흔들 = 0.10f;
    [Tooltip("넓은 기복의 높이 차 (m) — 어디가 높은 지대인가. 캐릭터·펫도 이 높이를 딛는다")]
    public float 칸높이폭 = 0.07f;
    // ★★격자를 눈에 보이게 하는 건 이쪽이다. 기복만 있으면 이웃 칸끼리 높이차가
    //   **평균 0.6cm** 라 벽이 6mm 여서 아무것도 안 보인다 (2026-08-09 실측).
    [Tooltip("칸마다 제멋대로 흔드는 폭 (m) — 칸 경계에 벽이 생겨 격자가 보인다")]
    public float 칸흔들 = 0.045f;
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

    // ══════════════════════════════════════════ 땅 사진 (2026-08-05)
    //
    // ★★사용자가 넣어 둔 진짜 재질 13장을 **깎지 않고 그대로** 쓴다
    //   ("픽셀버전으로 변형하지말고 그대로"). 사진이 색을 쥐고, 팔레트는 물에만 남는다.
    // ★자리마다 다른 사진이 걸리는 건 **함수**가 한다 — 칸 배정이 아니다 (`GroundPhotos`).
    //   씨앗을 바꾸면 세상의 주인 사진이 바뀐다.
    [Header("땅 사진 — 원본 재질 그대로")]
    [Tooltip("끄면 옛 방식(팔레트 색 × 회색 결)으로 돌아간다")]
    public bool 땅사진 = true;
    [Tooltip("사진 한 장이 덮는 크기 (m). 작을수록 결이 잘고 되풀이가 잦다")]
    [Range(1f, 24f)] public float 사진칸 = 5f;
    [Tooltip("배열로 묶을 때 맞추는 한 변 (px). 원본이 1250 이하라 1024면 넉넉하다")]
    public int 사진크기 = 1024;
    // ★★얼룩을 크게 잡는다 (2026-08-05 사용자 "너무 자주 바뀌지 않게, 3배 5배까지 넓게넓게").
    //   한 화면이 가로 약 120m 다. 얼룩이 그보다 작으면 **한 화면 안에서 여러 번 바뀌어**
    //   지역이 아니라 무늬로 읽힌다. 맵 한 변은 1440m.
    [Tooltip("얼룩 하나의 작은 쪽 (m) — 한 화면 가로가 약 120m 다")]
    [Range(20f, 400f)] public float 얼룩작게 = 75f;
    [Tooltip("얼룩 하나의 큰 쪽 (m) — 맵 한 변이 1440m 라 450 이면 지도를 서너 덩어리로 나눈다")]
    [Range(60f, 900f)] public float 얼룩크게 = 450f;
    [Tooltip("섞이는 폭 — 작으면 또렷하게 갈리고 크면 뭉근하게 번진다")]
    [Range(0.02f, 0.6f)] public float 사진섞임 = 0.18f;
    [Tooltip("큰 명암 흔들기 (색은 안 건드린다)")]
    [Range(0f, 0.5f)] public float 사진명암 = 0.12f;
    [Tooltip("바위지대에서 돌바닥이 드러나는 정도")]
    [Range(0f, 1f)] public float 바위지대돌 = 0.75f;
    // ★풀이 땅 사진을 찍을 때 쓰는 흐림 정도. 낮으면 결까지 따라와 풀이 지저분해지고,
    //   높으면 사진 평균에 가까워져 다시 납작해진다. 5 면 1024px 사진의 32×32 쯤이다.
    [Tooltip("풀이 땅을 찍을 때의 흐림(밉) — 낮으면 결까지 따라오고 높으면 납작해진다")]
    [Range(0f, 10f)] public float 잔디밉 = 5f;

    /// 바위지대가 어디인가 — 셰이더가 「여기는 돌바닥을 섞어라」로 읽는다.
    /// ★칸(160m) 단위라 9칸짜리 그림이면 충분하다. 부드럽게 늘려 뽑으므로 경계선이 안 보인다.
    Texture2D 바위지도만들기()
    {
        int n = WorldGrid.N;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            name = "바위지도", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
        };
        var px = new Color32[n * n];
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                byte v = kinds[x, z] == Land.바위지대 ? (byte)Mathf.RoundToInt(바위지대돌 * 255f) : (byte)0;
                px[z * n + x] = new Color32(v, v, v, 255);
            }
        t.SetPixels32(px);
        t.Apply(false);
        return t;
    }

    /// 땅 — 지형이 아니라 판때기 하나 (완전 평지). 잔디·길·물은 **칠해서** 넣는다
    void MakeGround(int seed)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
        g.name = "땅";
        g.transform.SetParent(holder, true);
        // ★판때기를 칸 높이만큼 내린다 — 안 내리면 낮은 칸을 뚫고 올라와 격자를 덮는다
        g.transform.position = new Vector3(WorldGrid.Size * 0.5f,
                                           -(칸높이폭 * 0.5f + 칸흔들 * 0.5f + 0.05f),
                                           WorldGrid.Size * 0.5f);
        g.transform.localScale = Vector3.one * (WorldGrid.Size / 10f);   // Plane 은 한 변 10m

        // ★★180도 돌린다 (2026-08-04). 유니티 기본 Plane 은 UV 가 **양쪽 축 모두 거꾸로**다
        //   (world +X,+Z 모서리가 uv 0,0). 그대로 두면 땅 그림이 점대칭으로 뒤집혀 붙어서,
        //   화면의 흙길과 코드가 아는 흙길의 자리가 **정반대**가 된다.
        //   → 실제로 "흙길에 잔디가 난다" 는 버그가 났다. 180도 돌리면 딱 맞는다.
        g.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        Grey.Strip(g);

        var mr = g.GetComponent<MeshRenderer>();
        if (!땅칠하기) { mr.sharedMaterial = Grey.Mat(C땅); return; }

        // ★칸 하나가 여기서 정해지고, 색 경계·높이·격자 메시가 **전부 이 값을 쓴다**
        GroundPaint.격자칸 = 격자칸;                 // 만들기 안에서 텍셀 정수배로 스냅된다
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
            사진꽂기(m, seed);
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
        // ★칸 격자를 재질과 높이 함수에 물린다 — 셋이 같은 값을 봐야 어긋나지 않는다
        if (m.HasProperty("_CellSize")) m.SetFloat("_CellSize", GroundPaint.격자칸);
        if (m.HasProperty("_CellVary")) m.SetFloat("_CellVary", 칸색흔들);
        땅격자.칸 = GroundPaint.격자칸;
        땅격자.높이폭 = 칸높이폭;
        땅격자.칸흔들 = 칸흔들;

        mr.sharedMaterial = m;

        // ★무엇이 실제로 걸렸는지 남긴다 — 짐작으로 값을 만지지 않으려고 (2026-08-05).
        //   화면만 보고는 「사진 결」과 「노이즈 잔얼룩」을 헷갈리기 쉽다.
        Debug.Log($"[땅] 큰 {큰무늬:0.00}({큰무늬칸:0}m) · 중 {잔무늬:0.00}({잔무늬칸:0.0}m)" +
                  $" · 잔 {땅결:0.00}({땅결칸 * 100f / 64f:0}cm)");
    }

    /// 사진 갈래를 켠다. 사진이 하나도 없으면 `_PhotoNum = 0` 이라 셰이더가 옛 갈래로 간다.
    void 사진꽂기(Material m, int seed)
    {
        if (!땅사진 || !GroundPhotos.준비(seed, Mathf.Clamp(사진크기, 128, 2048),
                                        Mathf.Min(얼룩작게, 얼룩크게), Mathf.Max(얼룩작게, 얼룩크게)))
        {
            m.SetFloat("_PhotoNum", 0f);
            Shader.SetGlobalFloat("_GPhotoNum", 0f);      // 풀도 옛 갈래로 돌아간다
            return;
        }

        var 바위지도 = 바위지도만들기();

        m.SetTexture("_PhotoArr", GroundPhotos.배열);
        m.SetTexture("_RockMap", 바위지도);
        m.SetVectorArray("_PhotoParams", GroundPhotos.파라미터);
        m.SetVectorArray("_PhotoGroup", GroundPhotos.무리);
        m.SetFloat("_PhotoNum", GroundPhotos.개수);
        m.SetFloat("_PhotoTiling", 1f / Mathf.Max(0.5f, 사진칸));
        m.SetFloat("_PhotoBand", 사진섞임);
        m.SetFloat("_PhotoShade", 사진명암);

        // ★★풀에게도 같은 배합을 넘긴다 (2026-08-05 사용자 "잔디 색이 땅 텍스처 색상을
        //   못 따라가네"). **전역으로** 넘기는 이유는 순서 때문이다 — `GrassField` 가 언제
        //   재질을 만들든 상관없이 값이 이미 놓여 있다. 재질에 직접 꽂으면 누가 먼저
        //   시작하느냐에 따라 어떤 판에서만 풀이 옛 색으로 나온다.
        // ★이름 앞에 `_G` 를 붙인다 — `_MaskMap` 같은 흔한 이름을 전역으로 놓으면
        //   그 이름을 쓰는 **다른 셰이더까지 물든다.**
        Shader.SetGlobalFloat("_GPhotoNum", GroundPhotos.개수);
        Shader.SetGlobalFloat("_GPhotoBand", 사진섞임);
        // ★★풀도 **사진을 직접 찍는다** (2026-08-05 사용자 "뚝뚝 끊어지면서 색이 변해서
        //   잔디가, 바닥은 그라디언트인데"). 평균색 한 덩어리를 쓰면 한 지역이 통째로
        //   **납작한 한 색**이 되어, 결이 있는 땅 옆에서 계단처럼 읽힌다.
        //   → 흐린 밉을 찍으면 결은 안 보이면서 **자리마다 다른 색**이 나온다.
        Shader.SetGlobalTexture("_GPhotoArr", GroundPhotos.배열);
        Shader.SetGlobalFloat("_GPhotoTiling", 1f / Mathf.Max(0.5f, 사진칸));
        Shader.SetGlobalFloat("_GPhotoMip", 잔디밉);
        // ★큰 명암도 **같이** 넘긴다 — 땅에만 얹고 풀에 안 얹어서 어긋나 있었다
        Shader.SetGlobalFloat("_GPhotoShade", 사진명암);
        Shader.SetGlobalVectorArray("_GPhotoParams", GroundPhotos.파라미터);
        Shader.SetGlobalVectorArray("_GPhotoGroup", GroundPhotos.무리);
        Shader.SetGlobalVectorArray("_GPhotoAvg", GroundPhotos.평균색);
        if (GroundPaint.땅마스크 != null) Shader.SetGlobalTexture("_GMaskMap", GroundPaint.땅마스크);
        Shader.SetGlobalTexture("_GRockMap", 바위지도);
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
            나무하나(p, Random.Range(6f, 10f));            // ★9~15 → 6~10m (2026-08-07 "나무가 전체적으로 너무 커" — 캐릭터 2.68m 의 2~4배)
        }

        // ★★★**심지** — 숲 안에 **큰 놈이 못 들어오는 덤불**을 몇 군데 박는다 (헌법 7번).
        //   *"지형이 무기다 — 다만 「높낮이」가 아니라 「폭」이다."*
        for (int k = 0; k < 심지수; k++)
            심지심기(Scatter(c, spread * 0.7f));
    }

    // ★★재 보고 정한 값이다 (2026-08-06). 통과 조건은
    //   **나무 간격 > 2 × (나무반지름 + 몸반지름)** 이다:
    //     다람쥐(r0.3) 1.4m · 늑대(r0.4) 1.6m · **티라노(r1.2) 3.1m**
    //   → 간격 2.4m 면 **티라만 못 지나간다.** 이게 「도망칠 곳」이다.
    //   ☆전에는 숲 칸 160×160m 에 나무가 106그루(평균 간격 15.5m)뿐이라
    //     티라가 95% 를 그냥 지나갔다. 넓게 촘촘히가 아니라 **좁은 데 몰아야** 한다.
    [Header("★숲의 심지 — 큰 놈이 못 들어오는 덤불 (헌법 7번)")]
    // ★실측 (씨앗 12345 · 숲 칸에서 무작위 1600점):
    //     심지 0군데 → 티라 5% · 늑대 2% 막힘   (사실상 아무 데나 지나간다)
    //     심지 3군데 15m 2.4m → **티라 10% · 늑대 4%** — 티라만 2.5배 막힌다
    //   ☆숲 전체를 막지 않는 게 맞다. **숨을 주머니**가 군데군데 있으면 된다.
    [Tooltip("숲 하나에 심지를 몇 군데")] [Range(0, 6)] public int 심지수 = 3;
    [Tooltip("심지 반경 (m) — 너무 크면 숲이 통째로 벽이 된다")] [Range(4f, 30f)] public float 심지반경 = 15f;
    [Tooltip("심지 안 나무 간격 (m) — 2.4 면 티라만 못 지난다")] [Range(1.6f, 6f)] public float 심지간격 = 2.4f;

    void 심지심기(Vector3 c)
    {
        if (심지수 <= 0 || 심지반경 < 1f) return;
        float s = Mathf.Max(1.6f, 심지간격);
        int r = Mathf.CeilToInt(심지반경 / s);
        for (int ix = -r; ix <= r; ix++)
            for (int iz = -r; iz <= r; iz++)
            {
                // ★흔들어 심되 **간격의 4분의 1까지만** — 크게 흔들면 틈이 생겨 규칙이 깨진다
                var p = c + new Vector3(ix * s + Random.Range(-0.25f, 0.25f) * s, 0f,
                                        iz * s + Random.Range(-0.25f, 0.25f) * s);
                float d = new Vector2(p.x - c.x, p.z - c.z).magnitude;
                if (d > 심지반경) continue;
                // 가장자리는 성글게 — 경계가 원으로 보이지 않게
                if (d > 심지반경 * 0.72f && Random.value < 0.45f) continue;
                if (!GroundPaint.잔디인가(p)) continue;

                // 심지는 어리고 가늘다 — 빛을 다툰다
                나무하나(p, Random.Range(4.5f, 7f), 심지: true);   // 심지는 어리다
            }
    }

    void Rocks(Vector3 c)
    {
        int count = Random.Range(7, 15);
        float spread = Random.Range(15f, 28f);
        for (int i = 0; i < count; i++)
        {
            var p = i == 0 ? c : Scatter(c, spread);
            // 바위지대의 큰 놈은 막고, 작은 놈은 넘어 다닌다 (위 「막는돌지름」과 같은 잣대)
            if (Swap(바위프리팹, p, true, -1f)) continue;   // 크기가 판정을 정한다
            float w = i == 0 ? Random.Range(3.5f, 5.5f) : Random.Range(1.2f, 2.8f);
            float h = w * Random.Range(0.7f, 1.4f);
            var rock = Grey.Box(holder, p + Vector3.up * (h * 0.42f),
                     new Vector3(w, h, w * Random.Range(0.7f, 1.3f)), C바위, "바위",
                     w >= 막는돌지름 ? w * 0.5f : 0f, Random.value * 360f);

            var hv = rock.AddComponent<Harvest>();
            hv.kind = Stock.Kind.돌; hv.hits = Mathf.RoundToInt(3f + w); hv.perHit = 2; hv.blockAt = p;

            밑동풀(p, w * 0.7f, Random.Range(1, 4));
        }

        // ★★굴은 **바위지대에만** 난다 (2026-08-06). 인과가 있어야 한다 —
        //   허허벌판 한가운데 동굴 입구가 뚫려 있으면 "왜 여기 있나"에 답을 못 한다.
        //   ☆굴이 값을 하는 자리: 야행성 종의 서식지 · 비 피하기 · 광석 · 좁아서 병목이 된다.
        if (굴넣기 && Random.value < 굴확률)
        {
            var 입구 = Scatter(c, spread * 0.6f);
            int 조각수 = 절차.정수(Random.value, 굴조각최소, 굴조각최대, 굴쏠림);
            float 반경 = Mathf.Lerp(14f, 굴반경, Mathf.InverseLerp(굴조각최소, 굴조각최대, 조각수));
            // ★굴은 폐허보다 축이 더 세다 — 4분의 3을 한 줄기로 파고든다.
            //   그래야 「입구 → 가장 깊은 방」이 생기고, 나머지가 막다른 곁가지(광석·둥지)가 된다.
            직소.짓기(holder, 굴주머니(), "굴방", 입구, Random.Range(int.MinValue, int.MaxValue),
                      new 직소.설정 { 조각한도 = 조각수, 반경 = 반경,
                                      깊이한도 = Mathf.Clamp(2 + 조각수, 4, 24),
                                      줄기 = Mathf.Max(3, 조각수 * 3 / 4) });
        }
    }

    [Header("★굴 (바위지대에만)")]
    [Tooltip("바위지대에 굴을 판다")] public bool 굴넣기 = true;
    [Tooltip("바위지대 몇 곳에 굴이 나나")] [Range(0f, 1f)] public float 굴확률 = 0.45f;
    [Tooltip("제일 작은 굴의 조각 수")] [Range(2, 12)] public int 굴조각최소 = 3;
    [Tooltip("제일 큰 굴의 조각 수")] [Range(4, 40)] public int 굴조각최대 = 18;
    [Tooltip("클수록 작은 굴이 흔해진다")] [Range(1f, 4f)] public float 굴쏠림 = 2.4f;
    [Tooltip("굴이 퍼지는 반경 (m)")] [Range(10f, 70f)] public float 굴반경 = 45f;
    [Tooltip("★비우면 상자로 짓는다")] public GameObject[] 굴조각_방, 굴조각_통로, 굴조각_잡동사니;

    List<직소.주머니> 굴주머니()
    {
        if (굴조각_방 == null || 굴조각_방.Length == 0) return 직소상자.굴주머니();
        return new List<직소.주머니>
        {
            new 직소.주머니 { 이름 = "굴방",     조각들 = 굴조각_방 },
            new 직소.주머니 { 이름 = "굴통로",   조각들 = 굴조각_통로 },
            new 직소.주머니 { 이름 = "잡동사니", 조각들 = 굴조각_잡동사니 },
        };
    }

    // ★★폐허는 **조각을 이어 붙여** 짓는다 (2026-08-06 사용자 — 마인크래프트 직소 방식).
    //   전에는 돌기둥을 원으로 둘러 세우기만 했다. 그건 매번 「원」이라 두 번만 봐도 같다.
    //   이제는 방·복도·꺾임이 이어 붙어 **크기도 모양도 매번 달라진다.**
    //   ☆`폐허조각` 이 비어 있으면 상자 조각으로 짓는다 (상자 먼저, 모델 나중).
    // ★★**크기는 고정값이 아니라 범위에서 뽑는다** (2026-08-06 — MC 의 직소 `size` 자리).
    //   폐허마다 조각 수가 3~24 로 갈리고, `절차.작은쪽으로` 가 **작은 폐허를 흔하게,
    //   큰 폐허를 드물게** 만든다. 균등하게 뽑으면 큰 게 자주 나와서 특별하지 않게 된다.
    [Header("★직소 폐허")]
    [Tooltip("끄면 옛 방식(돌기둥 원)으로 돌아간다")] public bool 직소폐허 = true;
    [Tooltip("제일 작은 폐허의 조각 수")] [Range(2, 20)] public int 폐허조각최소 = 3;
    [Tooltip("제일 큰 폐허의 조각 수")] [Range(4, 60)] public int 폐허조각최대 = 24;
    [Tooltip("클수록 작은 폐허가 흔해진다 (1 = 균등)")] [Range(1f, 4f)] public float 폐허쏠림 = 2.2f;
    [Tooltip("폐허가 퍼지는 반경 (m) — 조각 수에 맞춰 늘어난다")] [Range(15f, 90f)] public float 폐허반경 = 55f;
    [Tooltip("★비우면 상자로 짓는다. 진짜 모델이 생기면 여기에 꽂는다")]
    public GameObject[] 폐허조각_방, 폐허조각_복도, 폐허조각_잡동사니;

    List<직소.주머니> 폐허주머니()
    {
        // 프리팹을 하나라도 꽂았으면 그걸 쓴다 — 아니면 상자
        bool 진짜 = (폐허조각_방 != null && 폐허조각_방.Length > 0);
        if (!진짜) return 직소상자.폐허주머니();
        return new List<직소.주머니>
        {
            new 직소.주머니 { 이름 = "폐허터",   조각들 = 폐허조각_방 },
            new 직소.주머니 { 이름 = "돌담길",   조각들 = 폐허조각_복도 },
            new 직소.주머니 { 이름 = "잡동사니", 조각들 = 폐허조각_잡동사니 },
        };
    }

    void Ruin(Vector3 c)
    {
        if (직소폐허)
        {
            // ★크기를 뽑는다 — 작은 게 흔하고 큰 게 드물다
            int 조각수 = 절차.정수(Random.value, 폐허조각최소, 폐허조각최대, 폐허쏠림);
            float 반경 = Mathf.Lerp(20f, 폐허반경, Mathf.InverseLerp(폐허조각최소, 폐허조각최대, 조각수));
            int 깊이 = Mathf.Clamp(2 + 조각수 / 3, 2, 10);
            // ★줄기 = 절반 — 「바깥에서 안쪽으로」 축이 생기고 나머지가 곁가지로 붙는다.
            //   축이 있어야 폐허가 읽힌다 (한 덩어리로 뭉치면 어디가 안쪽인지 모른다).
            int 놓음 = 직소.짓기(holder, 폐허주머니(), "폐허터", c, Random.Range(int.MinValue, int.MaxValue),
                                  new 직소.설정 { 조각한도 = 조각수, 반경 = 반경, 깊이한도 = 깊이,
                                                  줄기 = Mathf.Max(2, 조각수 / 2) });
            if (놓음 > 0) return;
            // 한 조각도 못 놓았으면 옛 방식으로 (조용히 빈 칸이 되는 것보다 낫다)
        }

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

    // ───────────────────────────────── ★물은 「장」이다 (칸이 아니다)
    //
    // ★★온 세계의 물을 **이 함수 하나**가 정한다 (2026-08-06). 땅 그림·칸 종류·잔디·
    //   야생 스폰·나무 흩뿌리기가 **전부 여기를 본다** — 두 곳에서 따로 계산하면
    //   언젠가 갈라져서 물 위에 나무가 서게 된다.
    //
    // ★크기가 저절로 다양해진다: 노이즈가 문턱을 살짝 넘은 자리는 **웅덩이**,
    //   깊이 넘은 자리는 **큰 호수**다. 도장이 아니라서 두 번 같은 모양이 안 나온다.
    [Header("★물 (도장이 아니라 노이즈 장)")]
    [Tooltip("물 무늬 하나의 크기 (m) — 클수록 호수가 크고 드물다")] public static float 물칸 = 260f;
    // ★실측: 문턱 0.54 → 땅의 15.6%가 물 · 0.58 → 8.4% · 0.62 → 4.1%.
    //   0.59 는 약 7% — 목마름이 랜드마크가 될 만큼 드물면서, 걷다 보면 만나는 정도다.
    [Tooltip("이 값을 넘으면 물이다 — 높일수록 물이 줄어든다 (0.59 ≈ 땅의 7%)")]
    public static float 물문턱 = 0.59f;
    [Tooltip("물가가 번지는 폭 — 클수록 얕은 물·모래가 넓다")] public static float 물가폭 = 0.13f;
    [Tooltip("씨앗 (세계마다 다르게)")] public static float 물씨 = 11.7f;

    /// 그 자리가 얼마나 물인가 — **0이면 뭍**, 0보다 크면 물(0.18 미만은 물가 모래, 0.55 넘으면 깊은 물)
    public static float 물기(float x, float z)
    {
        // ★집 둘레는 물이 없다 — 나가자마자 호수면 답답하다 (칸 종류에서 하던 배려를 옮겨 왔다)
        var 집 = WorldGrid.Center;
        float 집d = new Vector2(x - 집.x, z - 집.z).magnitude;
        if (집d < 55f) return 0f;

        float n = 절차.결(x, z, 물칸, 4, 물씨);
        float t = 물문턱;
        if (집d < 110f) t += (1f - (집d - 55f) / 55f) * 0.12f;      // 집 둘레는 문턱을 높여 물을 밀어낸다
        return 절차.넘은만큼(n, t, 물가폭);
    }

    public static bool 물인가(Vector3 p) => 물기(p.x, p.z) > 0.18f;

    /// 그 자리가 어떤 칸인가 — 야생 구성이 이걸 보고 갈린다
    public Land KindAt(Vector3 p)
    {
        // ★물이 칸보다 세다 — 물 위에 숲이라고 답하면 나무가 물에 선다
        if (물인가(p)) return Land.물웅덩이;
        if (kinds == null) return Land.빈들판;
        int x = Mathf.Clamp(Mathf.FloorToInt(p.x / WorldGrid.Tile), 0, WorldGrid.N - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt(p.z / WorldGrid.Tile), 0, WorldGrid.N - 1);
        return kinds[x, z];
    }

    [Header("벌판에 흩뿌리기")]
    // ★「칸 하나에 몇 개」 손잡이 둘은 걷어냈다 (2026-08-06) — 밀도를 이제 노이즈가
    //   정하므로 쓰이지 않는다. 새 손잡이는 아래 「흩뿌리기 — 노이즈 분포」에 있다.

    // ══════════════════════════════════════════ 흩뿌리기 (2026-08-06)
    //
    // ★★★**칸마다 개수를 세지 않는다 — 자리마다 「여기 몇 그루 날까」를 묻는다**
    //   (사용자 "마인크래프트 생성 방식으로 분포해서, 밀집해서 숲처럼 울창한곳도 있을테고,
    //   길가 주변에는 좀 떨어져서 덜있다든가").
    //
    //   전에는 160m 칸 하나에 나무를 **0~3그루 무작위**로 던졌다. 개수가 칸에 묶여 있으니
    //   ①어디를 가도 밀도가 똑같고 ②울창한 데가 생길 수가 없고 ③칸 경계에서 뚝 끊겼다.
    //
    // ★★마인크래프트가 실제로 하는 것과 격자 방식의 **좋은 쪽만** 겹쳤다
    //   (2026-08-06 사용자 "마인크래프트는 함수를 활용해서 배치하는걸로 알고있는데" — 맞다.
    //    MC 는 노이즈로 **바이옴**을 정하고, 밀도는 「청크당 시도 횟수」가 들고 있으며,
    //    자리는 청크 안 균등 난수다. 노이즈가 자리를 정하지 않는다):
    //
    //     ①숲 노이즈      → 여기가 숲인가, 얼마나 진한가        ← MC 의 「바이옴」
    //     ②칸당 그루수     → 진할수록 한 칸에 1→N 그루          ← MC 의 「청크당 개수」
    //     ③칸 안 지터      → 그 안에서 무작위 자리               ← MC 의 「청크 안 균등」
    //     ④빈터 노이즈     → 숲 한복판에도 트인 데를 판다
    //
    //   ★②가 없으면 **칸당 한 그루가 상한**이라 아무리 진해도 울창해지지 않는다.
    //     그게 격자 방식이 인공적으로 보이는 진짜 이유다 (간격이 아니라 상한).
    //   ★④가 「길가 비우기」보다 중요하다. 숲이 균일하게 꽉 차 있으면 길만 트여 있어서
    //     지도가 통조림처럼 보인다. 저절로 생긴 공터가 있어야 들어가 볼 마음이 든다.
    //
    // ★씨앗은 **자리에서 뽑는다** (`WorldGrid.TileSeed`) — 칸을 어떤 순서로 짓든 결과가 같다.
    [Header("흩뿌리기 — 노이즈 분포")]
    [Tooltip("나무 자리 격자 (m)")]
    [Range(2f, 30f)] public float 나무간격 = 8f;
    [Tooltip("숲 한 덩어리의 크기 (m)")]
    [Range(20f, 400f)] public float 숲크기 = 140f;
    [Tooltip("땅의 몇 쯤이 숲인가 — 낮출수록 개활지가 넓다")]
    [Range(0.05f, 1f)] public float 숲비율 = 0.42f;
    [Tooltip("숲 한복판에서 한 칸에 서는 그루 수 — 이게 「울창함」의 상한이다")]
    [Range(1, 10)] public int 칸당최대 = 7;

    // ★★숲의 **심지** — 제일 진한 곳만 나무를 몰아 심어 큰 놈이 못 지나가게 한다 (헌법 7번).
    //   0 이면 예전처럼 고르게 깔린다 (은퇴는 삭제가 아니라 스위치).
    // ★★이건 **효과가 없었다** (2026-08-06 실측: 2.2 로 올려도 티라 통과율 95%→94%).
    //   벌판에 흩뿌리는 나무는 애초에 너무 성겨서 몰아 심어도 벽이 안 된다.
    //   진짜 답은 `Forest()` 의 **심지**였다 (아래). 여기는 **0 으로 꺼 둔다** — 지우지는 않는다.
    [Tooltip("(은퇴) 벌판 나무를 몰아 심기 — 효과가 없어서 0")]
    [Range(0f, 4f)] public float 숲빽빽 = 0f;
    [Tooltip("숲 진하기가 이 값을 넘는 데부터 심지다 — 높일수록 심지가 좁고 드물다")]
    [Range(0.3f, 0.95f)] public float 심지문턱 = 0.72f;
    // ★가장자리는 **얇아야** 대비가 산다. 0.5 로 두면 개활지에도 나무가 흩어져
    //   "어디나 조금씩 있는 벌판"이 된다 — 숲이 도드라지지 않는 진짜 이유였다.
    [Tooltip("숲 가장자리에서 한 칸에 서는 그루 수 — 낮을수록 숲과 벌판이 뚜렷하다")]
    [Range(0f, 1f)] public float 가장자리밀도 = 0.15f;

    [Tooltip("숲 속 빈터 한 덩어리의 크기 (m)")]
    [Range(10f, 200f)] public float 빈터크기 = 35f;
    [Tooltip("숲의 몇 쯤이 빈터인가")]
    [Range(0f, 0.8f)] public float 빈터비율 = 0.22f;

    [Tooltip("돌 자리 격자 (m)")]
    [Range(2f, 30f)] public float 돌간격 = 12f;
    [Tooltip("돌밭 한 덩어리의 크기 (m)")]
    [Range(20f, 400f)] public float 돌밭크기 = 90f;
    [Tooltip("땅의 몇 쯤에 돌이 깔리나")]
    [Range(0.05f, 1f)] public float 돌밭비율 = 0.3f;
    [Tooltip("돌밭 한복판에서 한 칸에 놓이는 돌 수")]
    [Range(1, 6)] public int 돌칸당최대 = 3;

    // ★★길가는 **비운다.** 길 옆까지 나무가 빽빽하면 길이 길로 안 읽힌다 —
    //   숲을 헤치고 난 자국이라는 게 눈에 보여야 한다.
    [Tooltip("길·물에서 이만큼 안쪽은 성글어진다 (m)")]
    [Range(0f, 20f)] public float 길비우기 = 8f;

    /// 그 자리가 길·물에서 얼마나 트여 있나 — 0(길 위) ~ 1(멀다)
    /// ★네 방향만 찍는다. 여덟로 늘려도 눈에 안 보이고 두 배 비싸다.
    float 길에서멂(Vector3 p)
    {
        if (길비우기 <= 0.01f) return 1f;
        int 걸림 = 0;
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * 0.5f;
            if (!GroundPaint.잔디인가(p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 길비우기)) 걸림++;
        }
        return 1f - 걸림 * 0.25f;
    }

    // ★★★**펄린은 0~1 을 고르게 안 쓴다** (2026-08-06 실측 — 사용자 "전혀 숲이랄데가 없는데?").
    //   1440m 를 4m 간격으로 재 봤더니 **면적의 92% 가 0.7 아래**였고 0.9 를 넘는 자리는 0% 였다:
    //       최소 0.004 · 최대 0.928 · 평균 0.465   (0.4~0.6 에 41% 가 몰린 종 모양)
    //
    //   그런데 `문턱 = 1 - 비율` 로 잡고 `InverseLerp(문턱, 1)` 로 진하기를 재고 있었다.
    //   그래서 두 번 손해를 봤다:
    //     ①「숲비율 0.42」라고 적었는데 실제로 문턱을 넘는 면적은 **26%** 뿐이었고
    //     ②넘은 자리의 평균 진하기가 **0.23** 이라 「칸당 4그루」가 실제로는 **1.3그루**가 됐다
    //   → 숲이 있을 수가 없었다. 12m 간격의 성긴 벌판이 나온 이유다.
    //
    // ★고침: **판을 실제로 재서** ①요청한 면적이 나오는 문턱과 ②그 위쪽 실제 상한을 뽑는다.
    //   이러면 「숲비율 0.42」가 진짜로 땅의 42% 가 되고, 진하기도 0~1 을 꽉 쓴다.
    //   짐작한 상수(0.58·1.0)를 실측값으로 바꾸는 것 — 「상수 대신 실측에서 파생」 그대로다.
    struct 노이즈자 { public float 크기, 오프, 문턱, 상한; }

    static float 노이즈(float x, float z, float 크기, float 오프)
        => Mathf.PerlinNoise(x / Mathf.Max(1f, 크기) + 오프, z / Mathf.Max(1f, 크기) + 오프 * 1.7f);

    /// 그 노이즈 판을 훑어 「면적 비율」에 해당하는 문턱과 상한을 잰다.
    /// ★64×64 = 4096 점이면 충분하다 (판을 짓는 동안 딱 한 번 돈다)
    static 노이즈자 재기(float 크기, float 비율, float 오프)
    {
        const int N = 64;
        var 값 = new float[N * N];
        float 칸 = WorldGrid.Size / N;
        for (int i = 0; i < N; i++)
            for (int j = 0; j < N; j++)
                값[i * N + j] = 노이즈((i + 0.5f) * 칸, (j + 0.5f) * 칸, 크기, 오프);
        System.Array.Sort(값);

        비율 = Mathf.Clamp(비율, 0.02f, 0.98f);
        int 자른데 = Mathf.Clamp(Mathf.RoundToInt((1f - 비율) * (값.Length - 1)), 0, 값.Length - 1);
        // 상한은 꼭대기가 아니라 **상위 2% 지점** — 한 점의 극값에 끌려가면 진하기가 또 눌린다
        int 위 = Mathf.Clamp(Mathf.RoundToInt(0.98f * (값.Length - 1)), 자른데 + 1, 값.Length - 1);
        return new 노이즈자 { 크기 = 크기, 오프 = 오프, 문턱 = 값[자른데], 상한 = Mathf.Max(값[위], 값[자른데] + 0.01f) };
    }

    /// 그 자리의 「진하기」 0~1 — 문턱 아래는 0, 상한에서 1.
    static float 진하기(Vector3 p, 노이즈자 자)
    {
        float n = 노이즈(p.x, p.z, 자.크기, 자.오프);
        return n < 자.문턱 ? 0f : Mathf.Clamp01(Mathf.InverseLerp(자.문턱, 자.상한, n));
    }

    /// 칸 하나를 훑으며 자리마다 「여기 몇 그루 날까」를 묻는다
    void 흩뿌리기(int gx, int gz, Land k)
    {
        if (!자잼) 자재기();
        var 중심 = WorldGrid.TileCenter(gx, gz);
        float 반 = WorldGrid.Tile * 0.48f;
        var st = Random.state;

        if (k != Land.숲) 나무뿌리기(중심, 반);          // 숲 칸은 `Forest()` 가 따로 심는다
        돌뿌리기(중심, 반, k == Land.바위지대 ? 2f : 1f);

        Random.state = st;
    }

    // 판을 짓는 동안 딱 한 번 재고 그대로 쓴다
    노이즈자 숲자, 빈터자, 돌자, 돌자_바위;
    bool 자잼;

    void 자재기()
    {
        숲자 = 재기(숲크기, 숲비율, 311.7f);
        빈터자 = 재기(빈터크기, 빈터비율, 77.3f);
        돌자 = 재기(돌밭크기, 돌밭비율, 57.1f);
        돌자_바위 = 재기(돌밭크기, Mathf.Clamp01(돌밭비율 * 2f), 57.1f);
        자잼 = true;
        Debug.Log($"[흩뿌리기] 숲 문턱 {숲자.문턱:0.00}~{숲자.상한:0.00} · 빈터 {빈터자.문턱:0.00}~{빈터자.상한:0.00}" +
                  $" · 돌 {돌자.문턱:0.00}~{돌자.상한:0.00}");
    }

    void 나무뿌리기(Vector3 중심, float 반)
    {
        float 격자 = Mathf.Max(2f, 나무간격);
        int r = Mathf.CeilToInt(반 / 격자);
        for (int ix = -r; ix <= r; ix++)
            for (int iz = -r; iz <= r; iz++)
            {
                int cx = Mathf.FloorToInt((중심.x + ix * 격자) / 격자);
                int cz = Mathf.FloorToInt((중심.z + iz * 격자) / 격자);
                Random.InitState(WorldGrid.TileSeed(0x7ee5, cx, cz));

                var 칸중심 = new Vector3((cx + 0.5f) * 격자, 0f, (cz + 0.5f) * 격자);
                float 숲 = 진하기(칸중심, 숲자);
                if (숲 <= 0f) continue;

                // ★숲 속 빈터 — 진한 곳일수록 크게 판다 (개활지엔 팔 게 없다)
                float 빈터 = 진하기(칸중심, 빈터자);
                숲 *= 1f - 빈터;
                if (숲 <= 0.02f) continue;

                숲 *= 길에서멂(칸중심);
                if (숲 <= 0.02f) continue;

                // ★★여기가 「울창함」이다 — 진할수록 한 칸에 여러 그루.
                //   소수는 확률로 처리해서 가장자리가 계단으로 안 끊기게 한다
                float 몇 = Mathf.Lerp(가장자리밀도, 칸당최대, 숲);

                // ★★★**심지** — 숲의 제일 진한 곳은 **큰 놈이 못 지나가게** 빽빽하다
                //   (2026-08-06 — 헌법 7번 "지형이 무기다, 높낮이가 아니라 폭이다").
                //   ☆재 보니 그냥 나무를 촘촘히 깔면 티라(반지름 1.2)가 96% 지나갔다.
                //     고르게 깔면 나무 사이 간격도 고르게 벌어져서 **틈이 항상 있기** 때문이다.
                //   → 넓게 촘촘히 하는 게 아니라, **좁은 자리에 몰아** 심는다. 그게 심지다.
                //     심지 밖은 그대로 성기니까 숲 전체가 벽이 되지는 않는다.
                float 심지 = 숲빽빽 > 0f ? Mathf.InverseLerp(심지문턱, 1f, 숲) : 0f;
                몇 *= 1f + 심지 * 숲빽빽;

                int n = Mathf.FloorToInt(몇);
                if (Random.value < 몇 - n) n++;

                for (int i = 0; i < n; i++)
                {
                    // 심지에서는 흩어지는 폭을 좁혀 **서로 붙여** 심는다 — 이게 틈을 없앤다
                    float 퍼짐 = 격자 * Mathf.Lerp(0.95f, 0.45f, 심지);
                    var at = 칸중심 + new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f) * 퍼짐;
                    if (Mathf.Abs(at.x - 중심.x) > 반 || Mathf.Abs(at.z - 중심.z) > 반) continue;
                    if (!GroundPaint.잔디인가(at)) continue;
                    // 빽빽한 곳은 어리고 성긴 곳은 굵다 — 서로 빛을 다투는 숲의 모습
                    float h = Random.Range(4.5f, 8.5f) * Mathf.Lerp(1.1f, 0.85f, 숲);   // ★7~13 → 4.5~8.5
                    나무하나(at, h);
                }
            }
    }

    [Tooltip("이 지름(m)보다 작은 돌은 안 막는다 — 넘어 다닌다")]
    [Range(0f, 4f)] public float 막는돌지름 = 1.6f;

    void 돌뿌리기(Vector3 중심, float 반, float 배)
    {
        float 격자 = Mathf.Max(2f, 돌간격);
        int r = Mathf.CeilToInt(반 / 격자);
        for (int ix = -r; ix <= r; ix++)
            for (int iz = -r; iz <= r; iz++)
            {
                int cx = Mathf.FloorToInt((중심.x + ix * 격자) / 격자);
                int cz = Mathf.FloorToInt((중심.z + iz * 격자) / 격자);
                Random.InitState(WorldGrid.TileSeed(0x3c1d, cx, cz));

                var 칸중심 = new Vector3((cx + 0.5f) * 격자, 0f, (cz + 0.5f) * 격자);
                float 진 = 진하기(칸중심, 배 > 1.5f ? 돌자_바위 : 돌자) * 길에서멂(칸중심);
                if (진 <= 0.02f) continue;

                float 몇 = Mathf.Lerp(0.4f, 돌칸당최대, 진);
                int n = Mathf.FloorToInt(몇);
                if (Random.value < 몇 - n) n++;

                for (int i = 0; i < n; i++)
                {
                    var at = 칸중심 + new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f) * 격자 * 0.95f;
                    if (Mathf.Abs(at.x - 중심.x) > 반 || Mathf.Abs(at.z - 중심.z) > 반) continue;
                    if (!GroundPaint.잔디인가(at)) continue;
                    // ★★**작은 돌은 안 막는다** (2026-08-06 사용자 "작은 돌들은 그냥
                    //   지나가지던가 해야 하는데 전부 막혀서 힘들어").
                    //   벌판에 흩뿌린 돌멩이는 0.5~1.8m — 사람이 넘어 다닐 크기인데
                    //   그루마다 장애물을 깔아서 **들판이 온통 지뢰밭**이었다.
                    //   ☆무엇을 막을지는 **그림 크기가 정한다** (「그림 = 판정」).
                    if (Swap(바위프리팹, at, true, -1f)) continue;  // 큰 놈은 막고 작은 놈은 넘어간다
                    float w = Random.Range(0.5f, 1.8f);
                    float h = w * Random.Range(0.5f, 1.0f);
                    Grey.Box(holder, at + Vector3.up * (h * 0.4f),
                             new Vector3(w, h, w * Random.Range(0.7f, 1.3f)), C바위, "돌멩이",
                             w >= 막는돌지름 ? w * 0.4f : 0f, Random.value * 360f);
                }
            }
    }

    /// 나무 한 그루 (줄기 + 잎 + 밑동풀 + 벌목)
    /// ★★나무는 **여기 하나로만** 선다 (2026-08-06). 전에는 심는 자리마다
    ///   `Swap(나무프리팹…)` 을 먼저 부르고 실패하면 회색 상자를 쌓았는데, 그 경로로 선
    ///   프리팹 나무에는 **`TreeFall`·`Harvest` 가 안 붙어서 팰 수가 없었다.**
    ///   문이 하나면 무엇으로 서든 벨 수 있고 길도 막는다.
    void 나무하나(Vector3 p, float h, bool 심지 = false)
    {
        float tr = h * Random.Range(0.04f, 0.06f);
        var 뿌리 = 나무프리팹세우기(p, h);

        if (뿌리 == null)
        {
            float w = h * Random.Range(0.42f, 0.55f);
            var trunk = Grey.Box(holder, p + Vector3.up * (h * 0.3f), new Vector3(tr, h * 0.6f, tr), C줄기, "나무_줄기");
            var leaf = Grey.Box(holder, p + Vector3.up * (h * 0.75f), new Vector3(w, h * 0.5f, w), C잎, "나무_잎");
            leaf.transform.SetParent(trunk.transform, true);   // 줄기를 캐면 잎도 같이 사라진다
            뿌리 = trunk;
        }

        Blocker.Add(p, tr * 0.7f);                             // 줄기 굵기만 막는다

        // ★선 나무를 패면 자원이 아니라 **쓰러진다.** 나무는 통나무를 패야 나온다
        var fall = 뿌리.AddComponent<TreeFall>();
        fall.통나무값 = 심지 ? Mathf.RoundToInt(3f + h * 0.4f) : Mathf.RoundToInt(4f + h * 0.5f);
        fall.선자리 = p;

        var hv = 뿌리.AddComponent<Harvest>();
        hv.kind = Stock.Kind.나무;
        hv.hits = 4; hv.perHit = 0;        // 선 나무를 팬다고 나무가 나오진 않는다
        hv.blockAt = p; hv.장애물치우기 = false;
        hv.쓰러짐 = fall;

        // ★나무가 750그루라 그루당 서너 포기면 2천 개가 넘는다 — 켤 때 그게 곧 렉이다
        if (!심지) 밑동풀(p, tr * 1.6f, Random.Range(0, 3));
    }

    /// 프리팹 나무를 **요청한 키에 맞춰** 세운다. 프리팹이 없으면 null.
    /// ★키는 짐작하지 않고 **렌더러 바운즈로 잰다** (모델을 바꿔도 저절로 따라온다).
    GameObject 나무프리팹세우기(Vector3 p, float h)
    {
        if (나무프리팹 == null || 나무프리팹.Length == 0) return null;
        var pf = 나무프리팹[Random.Range(0, 나무프리팹.Length)];
        if (pf == null) return null;

        var inst = Instantiate(pf, p, Quaternion.Euler(0f, Random.value * 360f, 0f), holder);
        환경손질(inst);

        float 원래키 = 모델키(pf);
        if (원래키 > 0.01f) inst.transform.localScale *= h / 원래키;

        // ★★**밑면을 땅에 맞춘다** (2026-08-07 사용자 "나무가 땅속에 박혀있고").
        //   받아 온 모델은 원점이 **가운데**인 경우가 많다 (Meshy 계열이 그렇다). 그러면
        //   자리에 그냥 놓으면 절반이 땅에 박힌다. 크기를 바꾼 **뒤에** 다시 재서 올린다 —
        //   먼저 재면 배율이 안 반영돼 어긋난다 (`Wildlife.모델몸` 과 같은 수법).
        var rs = inst.GetComponentsInChildren<Renderer>(true);
        if (rs.Length > 0)
        {
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            inst.transform.position += Vector3.up * (p.y - b.min.y);
        }
        return inst;
    }

    static readonly Dictionary<GameObject, float> 키캐시 = new Dictionary<GameObject, float>();

    /// 프리팹의 실제 키(m) — 한 번만 재고 기억한다
    static float 모델키(GameObject pf)
    {
        if (키캐시.TryGetValue(pf, out float v)) return v;
        var rs = pf.GetComponentsInChildren<Renderer>(true);
        float y = 0f;
        if (rs.Length > 0)
        {
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            y = b.size.y;
        }
        키캐시[pf] = y;
        return y;
    }

    /// ★물체 밑동에 잔디 덤불 (2026-08-04 참고 그림 — 들판을 고르게 덮는 게 아니라
    ///   바위·나무 **밑동에 모여 있다**). 이것만으로 물체가 땅에 「심긴」 것처럼 보인다.
    [Header("★풀·관목·꽃 (비우면 상자로 나온다)")]
    [Tooltip("풀포기·관목 모델")] public GameObject[] 풀프리팹;
    [Tooltip("꽃 모델 — 풀 사이에 섞인다")] public GameObject[] 꽃프리팹;
    [Tooltip("밑동에 나는 것 중 꽃이 될 확률")] [Range(0f, 1f)] public float 꽃섞기 = 0.22f;

    void 밑동풀(Vector3 c, float 반경, int 수)
    {
        for (int i = 0; i < 수; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            var p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 반경 * Random.Range(0.55f, 1.15f);

            // ★모델이 꽂혀 있으면 그것으로 심는다 (꽃을 섞는다). 풀·꽃은 길을 막지 않는다
            var 셋 = (꽃프리팹 != null && 꽃프리팹.Length > 0 && Random.value < 꽃섞기) ? 꽃프리팹 : 풀프리팹;
            if (Swap(셋, p, true, 0f)) continue;

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

    /// `blockR` 에 **음수를 주면 모델 크기가 판정을 정한다** — 큰 놈은 막고 작은 놈은 넘어간다.
    /// ★프리팹은 크기가 제각각인데 고정값(1.2m)을 박고 있었다. 그래서 자잘한 돌까지
    ///   벌판을 지뢰밭으로 만들었고, 반대로 그냥 0 으로 두면 **커다란 돌도 통과**해 버린다.
    ///   → 짐작 대신 **렌더러 바운즈를 재서** 정한다 (「그림 = 판정」).
    bool Swap(GameObject[] set, Vector3 pos, bool randomYaw, float blockR)
    {
        if (set == null || set.Length == 0) return false;
        var pf = set[Random.Range(0, set.Length)];
        if (pf == null) return false;
        var rot = randomYaw ? Quaternion.Euler(0f, Random.value * 360f, 0f) : Quaternion.identity;
        var inst = Instantiate(pf, pos, rot, holder);
        환경손질(inst);

        float r = blockR;
        if (blockR < 0f)
        {
            float 폭 = 모델폭(pf);
            r = 폭 >= 막는돌지름 ? 폭 * 0.4f : 0f;
        }
        if (r > 0f) Blocker.Add(pos, r);
        return true;
    }

    static readonly Dictionary<GameObject, float> 폭캐시 = new Dictionary<GameObject, float>();

    /// 프리팹의 실제 가로폭(m) — 한 번만 재고 기억한다
    static float 모델폭(GameObject pf)
    {
        if (폭캐시.TryGetValue(pf, out float v)) return v;
        var rs = pf.GetComponentsInChildren<Renderer>(true);
        float w = 0f;
        if (rs.Length > 0)
        {
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            w = Mathf.Max(b.size.x, b.size.z);
        }
        폭캐시[pf] = w;
        return w;
    }

    [Header("환경 프리팹 손질")]
    // ★★받아 온 모델은 **흰색**으로 들어온다 (2026-08-05 사용자 "색도 나뭇잎 색으로 넣어주라").
    //   `LeafCard` 재질의 `baseColorFactor` 가 (1,1,1) 이고 잎 그림(`leaf_card_soft`)이
    //   무채색이라, 곱하면 그대로 흰 나무가 된다. 여기서 그 자리를 물들인다.
    // ★재질 **에셋을 고치지 않는다** — `MaterialPropertyBlock` 으로 그 인스턴스에만 얹는다.
    //   에셋을 고치면 프로젝트 파일이 바뀌어 모델을 다시 임포트할 때 날아간다.
    [Tooltip("나뭇잎 색 — 받아 온 모델이 흰색이라 여기서 물들인다")]
    public Color 잎색 = new Color(0.34f, 0.53f, 0.25f);
    [Tooltip("잎 색을 포기마다 조금씩 흔든다 (0 이면 전부 같은 색)")]
    [Range(0f, 0.4f)] public float 잎색흔들기 = 0.10f;
    [Tooltip("줄기 색")] public Color 줄기색 = new Color(0.40f, 0.29f, 0.20f);
    // ★★잎을 **단색으로** 칠한다 (2026-08-09 사용자 "그라데이션 느낌 나지않게 단색으로,
    //   빠지는곳없이"). 실측: 나무 5종은 렌더러 1 · 재질 1 이고 잎과 줄기가 텍스처 한 장에
    //   같이 들어 있다 → 이름으로는 못 고른다. 셰이더가 **텍셀 색으로** 가른다.
    [Tooltip("나무를 단색 두 가지(잎·줄기)로 칠한다")] public bool 단색나무 = true;
    // ★색을 그루마다 따로 주면 드로콜이 그루 수만큼 난다 — 몇 벌로 묶어야 한다
    [Tooltip("잎 색을 몇 벌로 나눌까 (많을수록 다채롭고 드로콜이 는다)")] [Range(1, 12)]
    public int 색벌수 = 5;

    static MaterialPropertyBlock 잎블록;

    /// 심은 환경 프리팹 손질 — ①테두리를 안 두른다 ②흰 잎을 물들인다
    void 환경손질(GameObject inst)
    {
        if (inst == null) return;

        // ★표식 하나면 모델 이름이 무엇이든 테두리가 안 붙는다 (`NoOutline` 참고)
        if (inst.GetComponent<NoOutline>() == null) inst.AddComponent<NoOutline>();

        잎블록 ??= new MaterialPropertyBlock();
        float 흔들 = Random.Range(-잎색흔들기, 잎색흔들기);
        var 색 = new Color(Mathf.Clamp01(잎색.r + 흔들 * 0.7f),
                          Mathf.Clamp01(잎색.g + 흔들),
                          Mathf.Clamp01(잎색.b + 흔들 * 0.5f), 1f);

        // ★나무인가 — 프리팹 이름으로 고른다. 바위·조약돌엔 안 건다
        bool 나무다 = inst.name.StartsWith("나무");

        foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
        {
            var m = r.sharedMaterial;
            if (m == null) continue;

            if (단색나무 && 나무다)
            {
                // ★★★색 변주를 `MaterialPropertyBlock` 으로 주면 안 된다 (2026-08-09 사용자
                //   "렉이 너무심해"). 블록을 얹는 순간 **SRP 배처가 깨져 나무 한 그루가
                //   드로콜 하나**가 된다 — 이 씬의 나무는 실측 **27,802그루**다.
                //   → 색을 몇 벌로 묶고 그루마다 그중 하나를 고른다. 재질이 같으면 묶여 그려지고,
                //     눈에는 「그루마다 조금씩 다른 초록」 그대로다.
                r.sharedMaterial = 단색재질(m, Random.Range(0, Mathf.Max(1, 색벌수)));
                continue;
            }

            // 잎인가 — 재질 이름과 오브젝트 이름 둘 다 본다 (모델마다 다르게 부른다)
            if (!m.name.Contains("Leaf") && !m.name.Contains("Canopy")
                && !r.gameObject.name.Contains("Canopy")) continue;

            r.sharedMaterial = 잎재질(m);
            if (!m.HasProperty("baseColorFactor")) continue;

            r.GetPropertyBlock(잎블록);
            잎블록.SetColor("baseColorFactor", 색);
            r.SetPropertyBlock(잎블록);
        }
    }

    // ★원본 재질 × 색 벌 수 만큼만 만든다. 나무 5종 × 5벌 = 재질 25개 —
    //   2만 7천 그루가 이 25개를 나눠 쓰므로 드로콜이 25 언저리로 떨어진다.
    static readonly Dictionary<(Material, int), Material> 단색캐시 = new Dictionary<(Material, int), Material>();

    Material 단색재질(Material 원본, int 벌)
    {
        if (단색캐시.TryGetValue((원본, 벌), out var 있던) && 있던 != null) return 있던;

        var sh = Shader.Find("토이라/단색나무");
        if (sh == null) { Debug.LogWarning("[나무] 토이라/단색나무 셰이더를 못 찾았다"); return 원본; }

        var m = new Material(sh) { name = $"{원본.name}_단색{벌}" };
        // 원본 그림을 그대로 물려준다 — 잎·줄기를 가르는 잣대다
        Texture tex = null;
        if (원본.HasProperty("baseColorTexture")) tex = 원본.GetTexture("baseColorTexture");
        if (tex == null && 원본.HasProperty("_BaseMap")) tex = 원본.GetTexture("_BaseMap");
        if (tex == null && 원본.HasProperty("_MainTex")) tex = 원본.GetTexture("_MainTex");
        if (tex != null) m.SetTexture("_BaseMap", tex);
        // 벌마다 초록을 조금씩 어긋내 둔다 — 벌 하나면 흔들림 0 과 같다
        float 흔들 = 색벌수 <= 1 ? 0f
                   : Mathf.Lerp(-잎색흔들기, 잎색흔들기, 벌 / (float)(색벌수 - 1));
        m.SetColor("_LeafColor", new Color(Mathf.Clamp01(잎색.r + 흔들 * 0.7f),
                                           Mathf.Clamp01(잎색.g + 흔들),
                                           Mathf.Clamp01(잎색.b + 흔들 * 0.5f), 1f));
        m.SetColor("_BarkColor", 줄기색);
        // 원본이 알파로 잘라 쓰던 재질이면 그대로 자른다 (잎 카드)
        float cut = 원본.HasProperty("alphaCutoff") ? 원본.GetFloat("alphaCutoff") : 0f;
        m.SetFloat("_DoClip", cut > 0.001f ? 1f : 0f);
        if (cut > 0.001f) m.SetFloat("_Cutoff", cut);
        m.enableInstancing = true;

        단색캐시[(원본, 벌)] = m;
        return m;
    }

    // ★★★**잎을 「자르기(cutout)」로 바꾼다** (2026-08-06 사용자 "나무 그림자가 잎의
    //   빈공간까지 그림자를 받아버리는 버그").
    //
    //   받아온 잎 재질은 **투명(Transparent)** 이다. 투명 재질은 그림자를 그릴 때
    //   **알파를 안 자른다** — 잎 카드의 네모 판 전체가 그림자가 되어, 잎 사이 빈 곳까지
    //   시커먼 덩어리로 진다. 화면에 보이는 잎 모양과 그림자 모양이 어긋나는 이유다.
    //
    //   → 투명 대신 **알파 컷아웃**으로 돌린다. 그림자 패스가 같은 문턱으로 잘라내므로
    //     **그림자가 잎 모양 그대로** 진다. 덤으로 정렬 문제도 사라진다(불투명이라 뎁스를 쓴다).
    //
    // ★재질을 **원본마다 한 벌만** 만들어 나눠 쓴다. 나무 하나에 한 벌씩 만들면
    //   1만 그루에 재질 1만 개가 생겨 드로콜이 폭발한다. 색 변주는 `MaterialPropertyBlock`
    //   이 맡으므로 재질을 나눠 써도 그루마다 다른 색이 나온다.
    static readonly Dictionary<Material, Material> 잎재질캐시 = new Dictionary<Material, Material>();

    static Material 잎재질(Material 원본)
    {
        if (잎재질캐시.TryGetValue(원본, out var 있던) && 있던 != null) return 있던;

        var m = new Material(원본) { name = 원본.name + "_컷아웃" };
        if (m.HasProperty("alphaCutoff")) m.SetFloat("alphaCutoff", 0.5f);
        if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);       // 0 = 불투명
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", 1f);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", 0f);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 1f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;

        잎재질캐시[원본] = m;
        return m;
    }
}
