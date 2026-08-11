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
    // ★찰흙~유리설원은 **테마 권역** (2026-08-11) — 룰렛이 아니라 `테마깔기` 가 권역으로 얹는다
    // ★동굴은 **독립 랜드마크** (2026-08-11 사용자 확정 — 바위지대 소속이 아니라 어디든 난다)
    public enum Land { 빈들판, 숲, 바위지대, 물웅덩이, 폐허, 둥지, 캠프, 찰흙, 솜털실, 블록, 유리설원, 동굴 }

    [Header("씨앗")]
    [Tooltip("0 = 켤 때마다 새 맵 · 다른 숫자 = 그 숫자의 맵이 항상 똑같이 나온다")]
    public int worldSeed = 0;

    [Header("칸 종류가 뽑히는 비율")]
    // ★`w물` 은 0 이다 — 물은 칸이 아니라 노이즈 장(`물기`)이 정한다 (2026-08-06).
    //   손잡이는 `물칸`·`물문턱`. 여긴 은퇴한 자리라 지우지 않고 0 으로 둔다.
    public float w빈들판 = 4f, w숲 = 2.5f, w바위 = 2f, w물 = 0f, w폐허 = 0.8f, w둥지 = 0.8f;
    // ★0.7 → 1.8 (2026-08-11 사용자 "동굴 크고작게 좀더 많이 스폰되게해주면안돼?").
    //   0.7 이면 81칸 중 대여섯 개라 1440m 맵에서 우연히 만나기 어려웠다
    [Tooltip("★동굴 — 독립 랜드마크. 밖은 막힌 바위, 틈새로 들어가면 덮개가 걷힌다")]
    public float w동굴 = 1.8f;

    // ★★★**테마 권역** (2026-08-11 사용자 "찰흙지역, 솜털실지역, 블록지역, 유리설원 등등
    //   새로운 테마의 지형들을 준비중인데 그런 곳들도 맵생성에서 분포되서 나올 수 있어야").
    //   기존 지역(숲·바위…) **위에 몇 칸짜리 권역으로** 얹힌다 — 칸 단위 룰렛에 섞으면
    //   한 칸씩 흩어져서 테마로 안 읽힌다. 씨앗 칸을 잡고 이웃으로 번져 덩어리가 된다.
    //   ★먼곳부터 = 집에서의 거리 0~1 (맵 반변 720m 기준) — 유리설원은 먼 데만 있다.
    //     변형표(Wildlife)의 「먼곳부터」와 같은 사상: **멀수록 낯설다** (헌법 3).
    //   ★새 테마 추가 = enum 한 자리 + 이 표 한 줄 + 소품 함수 한 개 (+ 서식 줄 하나)
    //   ★바닥·바닥세기: 그 권역의 **땅색** (2026-08-11 사용자 "바닥색은 그대로인데?") —
    //     소품만 바꾸면 바닥이 여전히 풀·흙이라 지역이 안 읽힌다. 셰이더가 이 색으로 물들인다
    // ★★**권역수** — 테마마다 **몇 군데**에 나나 (2026-08-11 사용자 "테마가 너무 적지않나..?").
    //   옛 코드는 테마마다 씨앗을 **딱 하나**만 심어서, 온 세상에 찰흙 지대가 한 군데뿐이었다.
    //   그게 3칸이라 81칸 중 13칸(16%)이고 전부 집에서 멀어 **있어도 평생 못 보는** 상태였다.
    //   → 13칸 → 27칸(33%). 걷다 보면 만나되, 바탕(숲·들판·바위)이 여전히 3분의 2다.
    //   ★유리설원만 한 군데로 남긴다 — 제일 멀고 제일 낯선 데라 희소해야 값을 한다 (헌법 3).
    static readonly (Land 땅, int 권역수, int 칸수, float 먼곳부터, Color 바닥, float 바닥세기)[] 테마표 =
    {
        (Land.찰흙,     2, 4, 0.20f, new Color(0.60f, 0.40f, 0.26f), 0.75f),
        (Land.솜털실,   2, 4, 0.30f, new Color(0.86f, 0.74f, 0.80f), 0.75f),
        (Land.블록,     2, 3, 0.45f, new Color(0.30f, 0.55f, 0.30f), 0.70f),   // 블록 바닥판의 초록
        (Land.유리설원, 1, 5, 0.60f, new Color(0.86f, 0.92f, 0.96f), 0.85f),
    };

    [Header("★교체 자리 — 프리팹을 넣으면 상자 대신 그게 나온다")]
    public GameObject[] 나무프리팹;
    public GameObject[] 바위프리팹;
    public GameObject[] 폐허프리팹;
    public GameObject 둥지프리팹;
    public GameObject 부화터프리팹;
    public GameObject 물프리팹;
    [Header("★테마 권역 프리팹 — 준비 중인 모델이 오면 여기 꽂는다 (비우면 상자)")]
    public GameObject[] 찰흙프리팹;
    public GameObject[] 솜털실프리팹;
    public GameObject[] 블록프리팹;
    public GameObject[] 유리설원프리팹;

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
    // 테마 권역 상자 색 — 소품이 색으로 「여긴 다른 데다」를 말한다
    static readonly Color C찰흙A = new Color(0.72f, 0.45f, 0.28f);
    static readonly Color C찰흙B = new Color(0.58f, 0.34f, 0.22f);
    static readonly Color C솜털A = new Color(0.92f, 0.78f, 0.84f);
    static readonly Color C솜털B = new Color(0.75f, 0.82f, 0.92f);
    static readonly Color[] C블록들 =
    {
        new Color(0.85f, 0.25f, 0.22f), new Color(0.22f, 0.45f, 0.85f),
        new Color(0.95f, 0.80f, 0.20f), new Color(0.25f, 0.70f, 0.35f),
    };
    // 동굴 뚜껑 위의 풀 — 언덕처럼 보이게 한다
    static readonly Color C뚜껑풀A = new Color(0.30f, 0.42f, 0.24f);
    static readonly Color C뚜껑풀B = new Color(0.25f, 0.36f, 0.21f);
    static readonly Color C유리A = new Color(0.82f, 0.92f, 0.97f);
    static readonly Color C유리B = new Color(0.65f, 0.82f, 0.92f);

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
    // ★★22 (2026-08-11) — 굴 수를 늘렸다. **값만 고치고 이 번호를 안 올리면 아무 일도 안 난다**
    //   (실측: 씬 정본 21 == 정본지금 21 이라 111행에서 즉시 return, w동굴 3.2 가 한 번도 안 돌았다)
    const int 정본지금 = 22;
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
        // ★★폐허·굴·둥지를 **끈다** (2026-08-11 사용자 "폐허나 동굴 알둥지 이런것들은 좀 다
        //   빼줄래? 니 멋대로 기획해서 넣은거고, 제대로 만들지도 않은거같은데").
        //   지우지 않고 스위치로 끈다 (은퇴는 삭제가 아니라 스위치) — 제대로 기획하고
        //   만드는 날, 여기 세 값을 되켜면 그대로 돌아온다.
        w폐허 = 0f;
        w둥지 = 0f;
        굴넣기 = false;
        // ★★동굴을 독립 랜드마크로 (정본 20, 2026-08-11) — "사이즈도 다양하게 엄청큰것도
        //   있고 좁은곳도있고". 씬에 저장된 옛 값(최대 18조각·45m)을 넓힌다
        // ★★크기 차이를 더 벌린다 (2026-08-11 사용자 "동굴 크고작게 좀더 많이") —
        //   제일 작은 굴은 곁방 하나짜리, 제일 큰 굴은 한참 헤매는 크기가 되게
        굴조각최소 = 2;
        굴조각최대 = 60;
        // ★★**작은 굴을 훨씬 많이** (2026-08-11 사용자 "작은 굴좀 수좀 많이 늘려줘").
        //   두 손잡이가 같이 한다 — `w동굴` 은 **굴이 몇 개 나나**, `굴쏠림` 은 **그중 작은 게
        //   몇이나**. 하나만 올리면 원하는 그림이 안 된다:
        //     · w동굴만 올리면 → 큰 굴도 같이 늘어 세상이 굴 천지가 된다
        //     · 굴쏠림만 올리면 → 굴 수는 그대로고 큰 굴만 사라진다
        //   ☆9-4 「몇 개가 되나」 — 가중치 합 10.3 → 11.7 이라 굴 비율 17.5% → 27.4%.
        //     81칸에 약 14개 → 약 22개. 대신 쏠림을 올려 **굴 하나가 작아지므로**
        //     굴 하나당 상자 수는 줄어든다. 총량은 개수 증가분만큼 안 는다.
        굴쏠림 = 3.4f;
        굴반경 = 80f;
        // ★★★굴을 **칸 룰렛에서 뺀다** — 이제 `굴흩기` 가 맵 전역에 개수만큼 판다.
        //   룰렛에 두면 ①칸 81개에 갇히고 ②굴 칸이 맨땅이 되고 ③테마가 덮어써서 4개로 줄었다(실측).
        w동굴 = 0f;
        작은굴수 = 26;          // ★사용자 "작은 동굴은 적어도 20개는 있어야"
        큰굴수 = 5;             // ★"큰것들도 좀 있고"
        작은굴규모 = 7;         // ★크기도 좀 갈리게 (2~7). 성격 넷이 따로 흔들어 준다
        큰굴규모최소 = 22;
        굴높이배 = 2.1f;        // ★정본에 넣어 둔다 — 빠져 있으면 씬의 옛 값이 이긴다 (w동굴이 그랬다)
        뚜껑잔디 = 0.55f;       // ★사용자 "그냥 잔디 재질만 좀 입혀줘, 불규칙하게"
        뚜껑잔디얼룩 = 14f;
        뚜껑나무 = 0f;          // ★사용자 "동굴 천장에 나무는 박지 말아줘"
        굴가장자리 = 90f;
        굴집비움 = 150f;
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

        테마깔기(seed);          // ★테마 권역은 맨 마지막에 얹는다 — 밑에 뭐가 깔렸었는지 안 따진다
    }

    /// ★테마 권역 깔기 — 테마마다 씨앗 칸을 잡고 이웃으로 번져 덩어리를 만든다.
    /// 집 옆 여덟 칸·캠프·남의 테마 위에는 안 앉는다. 자리가 안 나오는 맵이면 그냥 없다
    /// (씨앗을 바꾸면 나온다 — 매 맵에 다 있으라는 법은 없다).
    void 테마깔기(int seed)
    {
        int n = WorldGrid.N;
        for (int i = 0; i < 테마표.Length; i++)
        {
            var 테마 = 테마표[i];
            // ★한 테마가 **여러 군데** 난다 — 씨앗마다 난수를 갈라 서로 다른 자리에 앉는다.
            //   `테마가능` 이 남의 테마 칸 위를 막으므로 권역끼리 겹치지 않는다
            for (int g = 0; g < 테마.권역수; g++)
            {
                Random.InitState(seed ^ (0x7e11 + i * 7919 + g * 104729));

                int sx = -1, sz = -1;
                for (int t = 0; t < 60 && sx < 0; t++)
                {
                    int x = Random.Range(0, n), z = Random.Range(0, n);
                    if (테마가능(x, z, 테마.먼곳부터)) { sx = x; sz = z; }
                }
                if (sx < 0) continue;

                kinds[sx, sz] = 테마.땅;
                var 덩어리 = new List<(int x, int z)> { (sx, sz) };
                // ★번질 때는 거리 문턱을 살짝 낮춘다 — 씨앗만 충분히 멀면 가장자리는 걸쳐도 된다
                for (int t = 0; t < 80 && 덩어리.Count < 테마.칸수; t++)
                {
                    var (bx, bz) = 덩어리[Random.Range(0, 덩어리.Count)];
                    int x = bx + Random.Range(-1, 2), z = bz + Random.Range(-1, 2);
                    if ((x == bx && z == bz) || !WorldGrid.InRange(x, z)) continue;
                    if (!테마가능(x, z, 테마.먼곳부터 * 0.75f)) continue;
                    kinds[x, z] = 테마.땅;
                    덩어리.Add((x, z));
                }
            }
        }
    }

    bool 테마가능(int x, int z, float 먼곳부터)
    {
        int home = WorldGrid.Home;
        if (Mathf.Abs(x - home) <= 1 && Mathf.Abs(z - home) <= 1) return false;   // 집 둘레는 트여 있어야 한다
        foreach (var v in 테마표) if (kinds[x, z] == v.땅) return false;           // 남의 테마 위엔 안 앉는다
        var c = WorldGrid.TileCenter(x, z);
        var h = WorldGrid.Center;
        float 멂 = new Vector2(c.x - h.x, c.z - h.z).magnitude / (WorldGrid.Size * 0.5f);
        return 멂 >= 먼곳부터;
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
        float total = w빈들판 + w숲 + w바위 + w물 + w폐허 + w둥지 + w동굴;
        float r = Random.value * total;
        if ((r -= w빈들판) < 0) return Land.빈들판;
        if ((r -= w숲) < 0) return Land.숲;
        if ((r -= w바위) < 0) return Land.바위지대;
        if ((r -= w물) < 0) return Land.물웅덩이;
        if ((r -= w폐허) < 0) return Land.폐허;
        if ((r -= w둥지) < 0) return Land.둥지;
        return Land.동굴;
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
                // ★동굴 칸엔 안 흩뿌린다 (2026-08-11) — 방 한가운데 나무가 선다
                if (k != Land.캠프 && k != Land.물웅덩이 && k != Land.동굴) 흩뿌리기(x, z, k);

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
                    case Land.찰흙: 찰흙터(c); break;
                    case Land.솜털실: 솜털실터(c); break;
                    case Land.블록: 블록터(c); break;
                    case Land.유리설원: 유리설원터(c); break;
                    case Land.동굴: 동굴터(c); break;      // (은퇴) w동굴 = 0 이라 안 온다
                }
            }

        굴흩기(seed);      // ★굴은 칸과 무관하게 맵 전역에 흩는다 — 테마가 덮어쓸 수 없다
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

    /// 테마 권역이 어디이고 바닥을 무슨 색으로 물들이나 — 셰이더가 읽는다 (바위지도와 같은 방식).
    /// 9×9 를 부드럽게 늘려 읽으니 권역 경계가 스르르 넘어간다
    Texture2D 테마지도만들기()
    {
        int n = WorldGrid.N;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            name = "테마지도", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
        };
        var px = new Color32[n * n];
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                var c = new Color(0f, 0f, 0f, 0f);
                foreach (var v in 테마표)
                    if (kinds[x, z] == v.땅) { c = v.바닥; c.a = v.바닥세기; break; }
                px[z * n + x] = c;
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
            m.SetTexture("_ThemeMap", 테마지도만들기());   // ★테마 권역 바닥색 (2026-08-11)
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
            // ★프리팹으로 서든 상자로 서든 **같이 캘 수 있어야 한다** — 나무의 「문 하나」
            //   원칙과 같다 (프리팹 바위만 Harvest 가 안 붙어 아예 못 캤다, 2026-08-11)
            var 세운 = Swap놈(바위프리팹, p, true, -1f);
            if (세운 != null) { 바위채집(세운, p); continue; }
            float w = i == 0 ? Random.Range(3.5f, 5.5f) : Random.Range(1.2f, 2.8f);
            float h = w * Random.Range(0.7f, 1.4f);
            var rock = Grey.Box(holder, p + Vector3.up * (h * 0.42f),
                     new Vector3(w, h, w * Random.Range(0.7f, 1.3f)), C바위, "바위",
                     w >= 막는돌지름 ? w * 0.5f : 0f, Random.value * 360f);
            바위채집(rock, p);

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

    // ★★동굴 — **독립 랜드마크** (2026-08-11 사용자 "밖에서 보면 막힌 지형인데, 입구가
    //   있고, 들어가면 좀보이드처럼 투시되서 보이는 그런 공간").
    //   땅은 평지 그대로 두고 **위에 얹는 높은 구조물**이다 — 높이차 금지(헌법 7)는 땅의
    //   높낮이 얘기라 안 부딪힌다. 덮개는 `굴가림` 이 걷고, 입구 틈새는 입구방이 만든다.
    //   ☆`굴넣기`(바위지대 소속이던 옛 방식)는 꺼진 채 은퇴 — 동굴은 `w동굴` 룰렛으로 나온다.
    [Header("★동굴 — 밖은 막힌 바위, 틈새로 들어간다")]
    [Tooltip("(은퇴) 바위지대에 굴을 판다 — 동굴이 독립 랜드마크가 되면서 껐다")] public bool 굴넣기 = true;
    [Tooltip("(은퇴) 바위지대 몇 곳에 굴이 나나")] [Range(0f, 1f)] public float 굴확률 = 0.45f;
    [Tooltip("제일 작은 굴의 조각 수")] [Range(2, 12)] public int 굴조각최소 = 3;
    [Tooltip("제일 큰 굴의 조각 수")] [Range(4, 40)] public int 굴조각최대 = 18;
    [Tooltip("클수록 작은 굴이 흔해진다")] [Range(1f, 4f)] public float 굴쏠림 = 2.4f;
    [Tooltip("굴이 퍼지는 반경 (m)")] [Range(10f, 70f)] public float 굴반경 = 45f;
    // ★★폭을 손잡이로 뺀다 (2026-08-11 사용자 "그 폭이좀 넓어야할듯한데?") —
    //   숫자를 코드에 박아 두면 눈으로 보고 못 고친다 (규칙 9-4 「화면에 띄워 놓고 정한다」).
    //   ☆반지름이다. 실제 길 너비는 이 값의 두 배 — 2.6 이면 5.2m 짜리 길이다
    [Tooltip("좁은 길의 반지름 (m) — 너비는 이 값의 두 배")] [Range(0.8f, 6f)] public float 굴길좁게 = 2.4f;
    [Tooltip("넓은 길의 반지름 (m)")] [Range(1.5f, 12f)] public float 굴길넓게 = 6.0f;
    [Tooltip("작은 공간의 반지름 (m)")] [Range(2f, 20f)] public float 굴방작게 = 7f;
    [Tooltip("큰 공간의 반지름 (m)")] [Range(4f, 40f)] public float 굴방크게 = 16f;
    [Tooltip("제일 큰 굴의 천장이 작은 굴의 몇 배인가 — 1 이면 다 같은 높이")]
    [Range(1f, 3f)] public float 굴높이배 = 2.1f;
    // ★★맵 전역에 흩는 굴 — **개수를 여기서 바로 정한다** (칸 룰렛 `w동굴` 은 0 으로 은퇴)
    [Tooltip("맵 전역에 흩는 작은 굴의 수")] [Range(0, 80)] public int 작은굴수 = 26;
    [Tooltip("맵 전역에 흩는 큰 굴의 수")] [Range(0, 20)] public int 큰굴수 = 5;
    [Tooltip("작은 굴의 최대 조각 수 — 작을수록 아담하다")] [Range(3, 20)] public int 작은굴규모 = 9;
    [Tooltip("큰 굴의 최소 조각 수")] [Range(10, 50)] public int 큰굴규모최소 = 22;
    [Tooltip("맵 가장자리에서 이만큼 안쪽에만 판다 (m)")] public float 굴가장자리 = 90f;
    [Tooltip("집 둘레 이 반경 안엔 안 판다 (m) — 집 앞은 트여 있어야 한다")] public float 굴집비움 = 150f;
    // ★★뚜껑 위엔 **나무를 안 심는다** (2026-08-11 사용자 "동굴 천장에 나무는 박지 말아줘,
    //   그냥 잔디 재질만 좀 입혀줘, 불규칙하게"). 나무는 굴 뚜껑을 「언덕」이 아니라
    //   「나무 심은 상자」로 읽히게 했다. 잔디만 **얼룩덜룩** 입힌다.
    //   ☆비율을 올리되 노이즈로 뭉치게 한다 — 칸마다 동전 던지면 소금후추가 되고,
    //     노이즈를 쓰면 덮인 데와 드러난 바위가 **덩어리로** 갈린다.
    [Tooltip("뚜껑 위 잔디가 덮인 비율 — 노이즈로 얼룩덜룩하게")] [Range(0f, 1f)] public float 뚜껑잔디 = 0.55f;
    [Tooltip("잔디 얼룩의 크기 (m) — 클수록 넓게 뭉친다")] [Range(3f, 40f)] public float 뚜껑잔디얼룩 = 14f;
    [Tooltip("(은퇴) 뚜껑 위 나무 — 사용자가 빼라고 했다. 0 이면 안 심는다")] [Range(0f, 0.3f)] public float 뚜껑나무 = 0f;
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

    /// ★★★동굴 — **직소 방을 버리고 벌레가 판다** (2026-08-11 사용자 "다 방 같이 되어있는데
    ///   동굴이라면 길의 두께도 다르고, 공간이 생겼을때도 공간의 크기도 다르고할텐데말이지
    ///   랜덤하지도 않고 길도 그냥 다 연결되있고 2D 던전게임도 아니고").
    ///   방·복도 조각을 이어 붙이면 아무리 섞어도 「건물」로 읽힌다. 이제는:
    ///     · 벌레가 노이즈로 **구불구불** 파고든다 — 직선도 직각도 없다
    ///     · **길 두께가 계속 변한다** (1.1~3.4m) — 좁아졌다 넓어졌다
    ///     · 가다가 불쑥 **공간이 열린다** — 원 여러 개를 겹쳐 파서 크기·모양이 매번 다르다
    ///     · **곁가지·막다른 끝** — 전부 이어진 격자가 아니라 나뭇가지다
    ///     · 넓은 데는 **천장도 높다.** 겉의 바위 살이 그 위를 덮는다
    ///   ☆직소 굴 조각(직소상자)은 안 부르게 됐지만 지우지 않는다 (은퇴는 스위치)
    /// ★★★**굴은 칸에 묶이지 않는다 — 맵 전역에 흩는다** (2026-08-11 사용자 "동굴은 그냥
    ///   맵전역에 좀 분포되게해줘,, 여러개 좀많이 왜케 없어").
    ///
    ///   옛 방식은 칸 룰렛(`w동굴`)이었다. 그게 왜 못 늘렸나 — **실측으로 확인한 것**:
    ///     ① 칸이 81개뿐이라 굴 20개면 세상의 4분의 1을 굴 칸으로 써야 한다
    ///     ② 굴 칸엔 흩뿌리기가 안 돈다(323행) → 굴을 늘릴수록 **맨땅이 넓어진다**
    ///     ③ `테마깔기` 가 **나중에** 27칸을 덮어써서 굴을 먹는다 → 실측 **4개**만 남았다
    ///   → 칸과 무관하게 좌표를 찍어 판다. **개수를 숫자로 바로 정하는 게** 유일하게 확실하다.
    ///   ☆이게 원래 적혀 있던 설계다 (17행 "동굴은 독립 랜드마크 — 어디든 난다").
    ///     구현만 룰렛에 묶여 있었다 — 9-2 패턴(자산은 앞서고 배선이 뒤처진다) 그대로.
    ///
    ///   ★9-4 「몇 개가 되나」: 작은 굴 26 × 약 60상자 + 큰 굴 5 × 약 400상자 ≒ 3,500 렌더러.
    ///     전부 정적 상자라 매 프레임 도는 것은 없다. 굴가림은 굴마다 범위 비교 한 번뿐(31번/프레임).
    // ★굴이 파낸 칸 (2m 격자) — 굴을 다 판 뒤 **그 자리의 나무·돌을 치우는** 데 쓴다
    readonly HashSet<(int ix, int iz)> 굴판칸 = new HashSet<(int ix, int iz)>();

    void 굴흩기(int seed)
    {
        Random.InitState(seed ^ 0x9d51);
        굴판칸.Clear();
        var 집 = WorldGrid.Center;
        int 못앉음 = 0;
        for (int i = 0; i < 작은굴수 + 큰굴수; i++)
        {
            bool 큰가 = i >= 작은굴수;
            Vector3 p = Vector3.zero;
            bool 됐나 = false;
            for (int t = 0; t < 24 && !됐나; t++)
            {
                p = new Vector3(Random.Range(굴가장자리, WorldGrid.Size - 굴가장자리), 0f,
                                Random.Range(굴가장자리, WorldGrid.Size - 굴가장자리));
                if (물인가(p)) continue;                                  // 호수 위엔 안 판다
                float dx = p.x - 집.x, dz = p.z - 집.z;
                if (dx * dx + dz * dz < 굴집비움 * 굴집비움) continue;      // 집 둘레는 트여 있어야 한다
                됐나 = true;
            }
            if (!됐나) { 못앉음++; continue; }
            동굴터(p, 큰가 ? 절차.정수(Random.value, 큰굴규모최소, 굴조각최대, 1.2f)
                          : 절차.정수(Random.value, 굴조각최소, 작은굴규모, 1.6f));
        }
        // ★9-4 「없앤 것은 말한다」 — 자리를 못 잡아 조용히 빠진 굴이 있으면 알린다
        if (못앉음 > 0) Debug.Log("[세계] 굴 " + 못앉음 + "개는 자리를 못 잡았다 (물·집 둘레를 피하다가)");
        굴안치우기();
    }

    /// ★★★**굴 안에 박힌 나무·돌을 치운다** (2026-08-11 사용자 "동굴안쪽에,, 이렇게 나무를
    ///   쳐박으면 안돼지"). 옛날엔 굴이 **칸 종류**였고 굴 칸엔 흩뿌리기를 안 돌렸다(323행).
    ///   굴을 칸에서 떼어 맵 전역에 흩으면서 그 보호가 무력해졌다 — 나무가 먼저 심기고
    ///   굴이 나중에 그 밑을 판다. **내가 만든 문제다.**
    ///   ☆굴이 다 파인 **뒤에 한 번만** 훑는다. 굴마다 훑으면 31번 도는 셈이라 비싸다 (9-4 ②).
    void 굴안치우기()
    {
        if (holder == null || 굴판칸.Count == 0) return;
        const float 칸 = 2f;
        var 지울것 = new List<GameObject>();
        for (int i = 0; i < holder.childCount; i++)
        {
            var t = holder.GetChild(i);
            if (t.name == "동굴") continue;                       // 굴 자신은 둔다
            var p = t.position;
            if (굴판칸.Contains((Mathf.RoundToInt(p.x / 칸), Mathf.RoundToInt(p.z / 칸))))
                지울것.Add(t.gameObject);
        }
        foreach (var g in 지울것)
        {
            if (Application.isPlaying) Destroy(g); else DestroyImmediate(g);
        }
        if (지울것.Count > 0) Debug.Log("[세계] 굴 안에 박힌 소품 " + 지울것.Count + "개를 치웠다");
    }

    /// `규모고정` 을 주면 그 크기로 판다 (안 주면 제 손으로 굴린다)
    void 동굴터(Vector3 c, int 규모고정 = -1)
    {
        int 규모 = 규모고정 > 0 ? 규모고정
                 : 절차.정수(Random.value, 굴조각최소, 굴조각최대, 굴쏠림);      // 3~40 · 작은 굴이 흔하다
        var 루트 = new GameObject("동굴");
        루트.transform.SetParent(holder, false);

        const float 칸 = 2f;
        var 천장 = new Dictionary<(int ix, int iz), float>();      // 판 자리 → 천장 높이
        // ★★**큰 굴일수록 천장이 높다** (2026-08-11 사용자 "동굴 높이도, 크기에 따라서 좀
        //   다르게 넣어줄 수 있어? 지금 높이가 작은 사이즈일때로 맞추고").
        //   지금 값을 **작은 굴의 높이**로 삼고, 규모에 따라 위로만 늘린다
        float 규모비0 = Mathf.InverseLerp(굴조각최소, 굴조각최대, 규모);
        float 높이배 = Mathf.Lerp(1f, 굴높이배, 규모비0);

        // ★★★**굴마다 성격이 다르다** (2026-08-11 사용자 "동굴 모양이 다 쳐똑같은데").
        //   크기만 갈리면 전부 **같은 굴의 확대·축소판**이다 — 실측에서도 작은 굴들이
        //   렌더러 185·187·191·192·195, 천장 6.1·6.2·6.2m 로 거의 판박이였다.
        //   갈려야 하는 건 크기가 아니라 **비율**이다. 넷을 굴마다 새로 뽑는다:
        //     · 굽이   — 곧게 뻗는 굴 ↔ 뱀처럼 감기는 굴
        //     · 가지끼 — 외길 굴 ↔ 갈래가 많아 헤매는 굴
        //     · 방끼   — 통로뿐인 굴 ↔ 방이 자꾸 열리는 굴
        //     · 길이배 — 같은 규모라도 짧고 굵은 굴 ↔ 길고 가는 굴
        //   ☆옛 고정값은 각각 1.5 · 0.045 · 0.045 · 1.0 이었다 (그래서 다 같았다).
        float 굽이 = Random.Range(0.55f, 2.7f);
        float 가지끼 = Random.Range(0f, 0.11f);
        float 방끼 = Random.Range(0.008f, 0.10f);
        float 길이배 = Random.Range(0.7f, 1.6f);
        int 가지한계 = Mathf.Max(1, Mathf.RoundToInt(규모 * Random.Range(0.12f, 0.45f)));

        void 새기기(Vector3 p, float r, float 높이)
        {
            int 반 = Mathf.CeilToInt(r / 칸);
            int cx = Mathf.RoundToInt(p.x / 칸), cz = Mathf.RoundToInt(p.z / 칸);
            for (int ix = cx - 반; ix <= cx + 반; ix++)
                for (int iz = cz - 반; iz <= cz + 반; iz++)
                {
                    if (new Vector2(ix * 칸 - p.x, iz * 칸 - p.z).sqrMagnitude > r * r) continue;
                    천장.TryGetValue((ix, iz), out float h);
                    천장[(ix, iz)] = Mathf.Max(h, 높이);
                }
        }

        // 공간 — 원 몇 개를 어긋나게 겹쳐 판다. 개수·반지름이 달라 **같은 방이 두 번 안 나온다**
        void 방파기(Vector3 p)
        {
            // ★★규모가 공간 크기도 정한다 (2026-08-11) — 곁방 하나짜리 작은 굴에 20m 짜리
            //   홀이 있으면 어색하다. 큰 굴일수록 큰 방이 나올 수 있게 위쪽을 연다
            //   ★★★**아래 한계도 같이 줄여야 한다** (2026-08-11 실측). 옛 코드는 하한이
            //     `굴방작게`(7m = 지름 14m)로 **고정**이라, 규모 9짜리 작은 굴에도 14m 홀이
            //     들어갔다 → 굴 하나가 **렌더러 765개**. 방 넓이는 반지름의 제곱으로 든다.
            float 규모비 = Mathf.InverseLerp(굴조각최소, 굴조각최대, 규모);
            float 아래한계 = Mathf.Lerp(3.5f, 굴방작게, 규모비);        // 작은 굴은 방도 아담하다
            float 위한계 = Mathf.Lerp(아래한계 + 2f, 굴방크게, 규모비);
            float 큰r = Random.Range(아래한계, Mathf.Max(아래한계 + 0.5f, 위한계));
            int 원수 = Random.Range(3, 7);
            for (int i = 0; i < 원수; i++)
            {
                var 옆 = p + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * 큰r * 0.55f;
                float r = 큰r * Random.Range(0.45f, 0.9f);
                새기기(옆, r, (2.9f + r * 0.28f) * 높이배);          // 넓으면·큰 굴일수록 천장이 높다
            }
        }

        // ── ① 판다 — 입구에서 안쪽으로
        // ★★★**입구는 카메라 쪽(남서)에 낸다** (2026-08-11 사용자 "동굴 입구는 위쪽 말고
        //   시야에서 보이는 쪽에좀 설치해줄래..?"). 아이소 카메라는 yaw 45°·pitch 40° 로
        //   **남서쪽에서** 내려다본다 — 즉 화면 앞면은 −X·−Z 면이다. 입구가 북동쪽에 나면
        //   언덕 **뒤통수**에 생겨서 화면에서 안 보이고, 돌아 들어가야 한다.
        //   ☆조금은 흔든다(±50°) — 전부 똑같은 자리면 도장 찍은 것처럼 보인다.
        float 카메라쪽 = Mathf.Atan2(-1f, -1f);                    // 남서쪽
        float 입구각 = 카메라쪽 + Random.Range(-0.87f, 0.87f);
        var 입구p = c + new Vector3(Mathf.Cos(입구각), 0f, Mathf.Sin(입구각)) * Mathf.Min(굴반경 * 0.45f, 26f);
        새기기(입구p, Mathf.Max(굴길좁게, 2.2f), 3.2f * 높이배);   // ★입구 자리를 확실히 판다
        var 줄기들 = new Stack<(Vector3 p, float dir, int 걸음)>();
        줄기들.Push((입구p, 입구각 + Mathf.PI, Mathf.RoundToInt((규모 * 3 + 8) * 길이배)));
        float 씨 = Random.value * 512f;
        int 가지 = 0;
        while (줄기들.Count > 0)
        {
            var (p, dir, 걸음) = 줄기들.Pop();
            float t = Random.value * 64f;
            for (int i = 0; i < 걸음; i++)
            {
                t += 1f;
                dir += (Mathf.PerlinNoise(씨, t * 0.13f) - 0.5f) * 굽이;             // 구불구불 (굴마다 다르다)
                p += new Vector3(Mathf.Cos(dir), 0f, Mathf.Sin(dir)) * 1.5f;
                p.x = Mathf.Clamp(p.x, 8f, WorldGrid.Size - 8f);
                p.z = Mathf.Clamp(p.z, 8f, WorldGrid.Size - 8f);
                if ((p - c).magnitude > 굴반경 * 0.62f)                              // 제 칸을 벗어나지 않게
                    dir = Mathf.Atan2(c.z - p.z, c.x - p.x) + Random.Range(-0.6f, 0.6f);
                // ★작은 굴은 **넓어지는 상한도 낮다** — 길 넓이도 폭의 제곱으로 든다
                float 폭상한 = Mathf.Lerp(굴길좁게 + 0.8f, 굴길넓게, 규모비0);
                float 폭 = Mathf.Lerp(굴길좁게, Mathf.Max(굴길좁게 + 0.3f, 폭상한),
                                      Mathf.PerlinNoise(씨 + 31f, t * 0.08f));   // 길 두께가 변한다
                새기기(p, 폭, (2.7f + 폭 * 0.35f) * 높이배);
                if (가지 < 가지한계 && 걸음 - i > 10 && Random.value < 가지끼)       // 곁가지
                { 가지++; 줄기들.Push((p, dir + Random.Range(1.1f, 2.1f) * (Random.value < 0.5f ? 1f : -1f), (걸음 - i) / 2)); }
                if (Random.value < 방끼) 방파기(p);                                  // 가다 불쑥 열리는 공간
            }
            방파기(p);                                                               // 막다른 끝은 공간이다
        }

        // ── ② 살 — 판 자리를 두 칸(4m) 두께의 바위가 감싼다
        var 살 = new HashSet<(int, int)>();
        foreach (var k in 천장.Keys)
            for (int dx = -2; dx <= 2; dx++)
                for (int dz = -2; dz <= 2; dz++)
                {
                    var k2 = (k.ix + dx, k.iz + dz);
                    if (!천장.ContainsKey(k2)) 살.Add(k2);
                }

        // ★★★**입구 통로는 바위를 끝까지 뚫는다** (2026-08-11 사용자 "입구가 없는 동굴도있네").
        //   전에는 입구 자리에서 **9m 만** 텄다. 그런데 벌레가 헤매다 보면 입구 자리가
        //   덩어리 한가운데에 들어앉기도 하고, 그러면 9m 로는 바깥까지 못 닿아
        //   **입구가 바위 속에서 끝나 버린다** — 들어갈 수 없는 굴이 그렇게 생겼다.
        //   → 덩어리를 확실히 벗어날 만큼(굴반경 + 여유) 곧게 뚫는다. 없는 살은 지워도 그만이다.
        var 바깥벡 = new Vector3(Mathf.Cos(입구각), 0f, Mathf.Sin(입구각));
        float 뚫는길이 = 굴반경 + 40f;
        살.RemoveWhere(k =>
        {
            var q = new Vector3(k.Item1 * 칸, 0f, k.Item2 * 칸) - 입구p;
            float 앞 = Vector3.Dot(q, 바깥벡);
            return 앞 > -2f && 앞 < 뚫는길이 && (q - 바깥벡 * 앞).magnitude < 2.8f;
        });

        // 갇힌 빈칸은 돌이 된다 — 밖에서 걸어 들어갈 수 없는 구멍은 지형이 아니라 얼룩이다
        int bxMin = int.MaxValue, bxMax = int.MinValue, bzMin = int.MaxValue, bzMax = int.MinValue;
        foreach (var k in 천장.Keys) { bxMin = Mathf.Min(bxMin, k.ix); bxMax = Mathf.Max(bxMax, k.ix); bzMin = Mathf.Min(bzMin, k.iz); bzMax = Mathf.Max(bzMax, k.iz); }
        bxMin -= 3; bxMax += 3; bzMin -= 3; bzMax += 3;
        int W = bxMax - bxMin + 1, H = bzMax - bzMin + 1;
        var 닿음 = new bool[W, H];
        var 큐 = new Queue<(int, int)>();
        bool 빈칸(int ix, int iz) => !천장.ContainsKey((ix, iz)) && !살.Contains((ix, iz));
        void 씨앗(int ix, int iz)
        {
            if (ix < bxMin || ix > bxMax || iz < bzMin || iz > bzMax) return;
            if (닿음[ix - bxMin, iz - bzMin] || !빈칸(ix, iz)) return;
            닿음[ix - bxMin, iz - bzMin] = true; 큐.Enqueue((ix, iz));
        }
        for (int ix = bxMin; ix <= bxMax; ix++) { 씨앗(ix, bzMin); 씨앗(ix, bzMax); }
        for (int iz = bzMin; iz <= bzMax; iz++) { 씨앗(bxMin, iz); 씨앗(bxMax, iz); }
        while (큐.Count > 0)
        {
            var (qx, qz) = 큐.Dequeue();
            씨앗(qx - 1, qz); 씨앗(qx + 1, qz); 씨앗(qx, qz - 1); 씨앗(qx, qz + 1);
        }
        for (int ix = bxMin; ix <= bxMax; ix++)
            for (int iz = bzMin; iz <= bzMax; iz++)
                if (빈칸(ix, iz) && !닿음[ix - bxMin, iz - bzMin]) 살.Add((ix, iz));

        // ── ③ 짓는다 — 전부 격자 재질 (바닥이고 벽이고 모두 격자형식으로)
        GameObject 격자상자(Vector3 pos, Vector3 size, Color col, string name, float blockR)
        {
            var g = Grey.Box(루트.transform, pos, size, col, name, blockR, 0f);
            g.GetComponent<MeshRenderer>().sharedMaterial = Grey.격자Mat(col);
            return g;
        }

        foreach (var kv in 천장)
        {
            // ★★★굴 바닥은 **땅격자 위에 얹는다** (2026-08-11 사용자 "동굴안에 있을때 발이
            //   땅에 박히는버그가있어"). 옛 코드는 y 를 `0f` 로 박고 바닥 상자를 0.08 에 두어
            //   **윗면이 0.16m** 였는데, 사람은 `땅격자.걷는높이` 를 딛는다 (Hero.cs:207).
            //   → 발이 정확히 16cm 묻혔다.
            //   ☆Hero.cs:205 에 **이미 적힌 사고의 재발**이다 — "`땅격자` 가 그림과 판정의
            //     단 하나뿐인 출처다. 여기서 안 물으면 발이 묻힌다" (2026-08-09).
            //     고치는 방향은 **그림을 출처에 맞추는 것** — 반대로 하면 그 규칙이 또 깨진다.
            //   ☆윗면을 걷는높이에 딱 맞추면 땅과 z파이팅이 난다 → 2cm 만 띄운다.
            //     상자 반높이가 0.08 이므로 중심은 걷는높이 + 0.02 - 0.08.
            굴판칸.Add((kv.Key.ix, kv.Key.iz));      // ★나중에 이 자리의 나무·돌을 치운다
            var p2 = new Vector3(kv.Key.ix * 칸, 땅격자.걷는높이(kv.Key.ix * 칸, kv.Key.iz * 칸), kv.Key.iz * 칸);
            격자상자(p2 + Vector3.up * (0.02f - 0.08f), new Vector3(칸 * 1.03f, 0.16f, 칸 * 1.03f), 직소상자.C굴바닥, "바닥", 0f);
            격자상자(p2 + Vector3.up * (kv.Value + 0.2f), new Vector3(칸 * 1.03f, 0.4f, 칸 * 1.03f), 직소상자.C굴덮개, "덮개", 0f);
            // 높은 공간엔 가끔 돌기둥 — 빈 방이 심심하지 않게, 몸을 숨길 데가 생기게
            if (kv.Value > 4f && Random.value < 0.035f)
                격자상자(p2 + Vector3.up * Random.Range(0.6f, 1f), new Vector3(0.55f, Random.Range(1.1f, 2f), 0.55f), 직소상자.C굴벽, "돌기둥", 0.4f);
        }
        // ★★★**뚜껑 위는 그냥 바위가 아니라 「땅」이다** (2026-08-11 사용자 "동굴 뚜껑
        //   윗부분에는 군데군데 잔디지형처럼 좀 들어가있고 나무도 박혀있었음 좋겠어").
        //   밖에서 보면 **풀이 난 언덕**이라야 「막힌 지형」으로 읽힌다 — 회색 덩어리는 건물이다.
        //   ☆「덮개장식」 아래에 묶는다 — 안에 들어가면 `굴가림` 이 천장과 함께 통째로 숨긴다.
        //   ☆개수를 센다 (지침 9-4 ①): 살 칸의 잔디 25% · 나무 7%.
        //     큰 굴의 살이 1,500칸이면 나무 100그루쯤 — 세계 전체 2만 6천 그루에 견주면 작다.
        var 장식 = new GameObject("덮개장식");
        장식.transform.SetParent(루트.transform, false);
        float 잔디씨 = Random.value * 997f;      // 굴마다 잔디 얼룩 무늬가 다르게

        foreach (var k in 살)
        {
            var p2 = new Vector3(k.Item1 * 칸, 0f, k.Item2 * 칸);
            float h = Random.Range(4.8f, 6.2f) * Mathf.Lerp(1f, 높이배, 0.6f);   // 큰 굴은 언덕도 두툼하다
            격자상자(p2 + Vector3.up * (h * 0.5f - 0.2f), new Vector3(칸 * 1.04f, h, 칸 * 1.04f), 직소상자.C굴덮개, "덮개산", 칸 * 0.52f);

            float 꼭대기 = h - 0.2f;
            // ★★잔디 — **노이즈로 뭉친다** (2026-08-11 사용자 "잔디 재질만 좀 입혀줘, 불규칙하게").
            //   칸마다 `Random.value` 로 동전을 던지면 **소금후추**가 된다 — 한 칸 걸러 한 칸이라
            //   「얼룩」이 아니라 「점무늬」로 읽힌다. 노이즈를 쓰면 덮인 데와 드러난 바위가
            //   **덩어리로** 갈려서 진짜 언덕처럼 보인다.
            float n잔디 = Mathf.PerlinNoise((p2.x + 잔디씨) / 뚜껑잔디얼룩, (p2.z + 잔디씨) / 뚜껑잔디얼룩);
            if (n잔디 < 뚜껑잔디)
            {
                var 풀 = Grey.Box(장식.transform, p2 + Vector3.up * (꼭대기 + 0.12f),
                                  new Vector3(칸 * 1.02f, 0.24f, 칸 * 1.02f),
                                  Random.value < 0.5f ? C뚜껑풀A : C뚜껑풀B, "덮개풀");
                풀.GetComponent<MeshRenderer>().sharedMaterial =
                    Grey.격자Mat(Random.value < 0.5f ? C뚜껑풀A : C뚜껑풀B);
            }
            // 나무 — 언덕 위에 박혀 있다. ★`나무하나` 가 아니라 프리팹만 세운다:
            //   벨 수도 없고(손이 안 닿는다) 길도 막지 않아야 한다 (바위가 이미 막는다)
            if (Random.value < 뚜껑나무)
            {
                var 자리 = p2 + Vector3.up * 꼭대기
                         + new Vector3(Random.Range(-0.6f, 0.6f), 0f, Random.Range(-0.6f, 0.6f));
                var 나 = 나무프리팹세우기(자리, Random.Range(4.5f, 7.5f));
                if (나 != null) 나.transform.SetParent(장식.transform, true);
            }
        }

        var 가림 = 루트.AddComponent<굴가림>();
        가림.입구자리 = 입구p;
        가림.입구방향 = 바깥벡;
    }

    // (2026-08-11) 직소 방을 감싸던 「살채우기」는 웜 방식 동굴터가 제 살을 직접 채우면서 걷어냈다

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

    // ══════════════════════════════════════════ ★테마 권역 소품 (2026-08-11)
    //  전부 색칠한 상자다 (규칙 9-1 「상자 먼저, 모델 나중」) — 진짜 모델이 오면
    //  「테마 권역 프리팹」 칸에 꽂는다. 코드는 안 고쳐도 된다.

    void 찰흙터(Vector3 c)
    {
        // 낮고 퍼진 덩어리 — 뭉쳐 놓은 찰흙 무더기로 읽힌다
        int count = Random.Range(14, 24);
        float spread = Random.Range(35f, 55f);
        for (int i = 0; i < count; i++)
        {
            var p = Scatter(c, spread);
            if (물인가(p)) continue;
            if (Swap(찰흙프리팹, p, true, -1f)) continue;
            float w = Random.Range(1.5f, 4f), h = w * Random.Range(0.35f, 0.6f);
            Grey.Box(holder, p + Vector3.up * (h * 0.45f),
                     new Vector3(w, h, w * Random.Range(0.8f, 1.2f)),
                     Random.value < 0.5f ? C찰흙A : C찰흙B, "찰흙덩이",
                     w >= 2.5f ? w * 0.45f : 0f, Random.value * 360f);
        }
    }

    void 솜털실터(Vector3 c)
    {
        // 파스텔 실뭉치 — 가끔 실타래 기둥이 선다
        int count = Random.Range(16, 26);
        float spread = Random.Range(35f, 55f);
        for (int i = 0; i < count; i++)
        {
            var p = Scatter(c, spread);
            if (물인가(p)) continue;
            if (Swap(솜털실프리팹, p, true, -1f)) continue;
            var 색 = Random.value < 0.5f ? C솜털A : C솜털B;
            if (Random.value < 0.2f)
            {
                float h = Random.Range(4f, 7f);
                Grey.Box(holder, p + Vector3.up * (h * 0.45f), new Vector3(1.1f, h, 1.1f),
                         색, "실기둥", 0.7f, Random.value * 360f);
            }
            else
            {
                float w = Random.Range(1.2f, 2.6f);
                Grey.Box(holder, p + Vector3.up * (w * 0.45f), new Vector3(w, w, w), 색, "실뭉치",
                         w >= 2f ? w * 0.5f : 0f, Random.value * 360f);
            }
        }
    }

    void 블록터(Vector3 c)
    {
        // 원색 블록 — 두세 개씩 쌓인 탑이 블록 장난감으로 읽힌다
        int count = Random.Range(12, 20);
        float spread = Random.Range(30f, 50f);
        for (int i = 0; i < count; i++)
        {
            var p = Scatter(c, spread);
            if (물인가(p)) continue;
            if (Swap(블록프리팹, p, true, -1f)) continue;
            float w = Random.Range(1.4f, 3f);
            int 층 = Random.Range(1, 4);
            float yaw = Random.value * 360f;
            for (int j = 0; j < 층; j++)
            {
                float bw = w * (1f - j * 0.18f);
                Grey.Box(holder, p + Vector3.up * (w * 0.5f + j * w * 0.92f),
                         new Vector3(bw, w * 0.9f, bw),
                         C블록들[Random.Range(0, C블록들.Length)], "블록",
                         j == 0 && w >= 2f ? w * 0.5f : 0f, yaw + j * 14f);
            }
        }
    }

    void 유리설원터(Vector3 c)
    {
        // 희고 시린 밭 — 가늘고 큰 유리 조각이 서 있고 사이에 눈더미가 깔린다
        int count = Random.Range(16, 28);
        float spread = Random.Range(35f, 60f);
        for (int i = 0; i < count; i++)
        {
            var p = Scatter(c, spread);
            if (물인가(p)) continue;
            if (Swap(유리설원프리팹, p, true, -1f)) continue;
            var 색 = Random.value < 0.6f ? C유리A : C유리B;
            if (Random.value < 0.4f)
            {
                float h = Random.Range(3.5f, 8f);
                Grey.Box(holder, p + Vector3.up * (h * 0.42f),
                         new Vector3(Random.Range(0.5f, 1f), h, Random.Range(0.5f, 1f)),
                         색, "유리조각", 0.6f, Random.value * 360f);
            }
            else
            {
                float w = Random.Range(1f, 2.4f), h = w * Random.Range(0.5f, 0.8f);
                Grey.Box(holder, p + Vector3.up * (h * 0.45f), new Vector3(w, h, w), 색, "눈더미",
                         0f, Random.value * 360f);
            }
        }
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
        // ★0.48 → 0.5 (2026-08-11 사용자 "왜케 중간중간 뚝뚝 길게끊어진 구간들이 있지?") —
        //   0.48 로 자르니 칸(160m)마다 못 심는 6.4m 띠가 남아 **모든 칸 경계가 일직선으로
        //   비었다.** 겹침 방지는 이제 「주인 정하기」(아래 뿌리기 함수들)가 한다
        float 반 = WorldGrid.Tile * 0.5f;
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
        int r = Mathf.CeilToInt(반 / 격자) + 1;
        for (int ix = -r; ix <= r; ix++)
            for (int iz = -r; iz <= r; iz++)
            {
                int cx = Mathf.FloorToInt((중심.x + ix * 격자) / 격자);
                int cz = Mathf.FloorToInt((중심.z + iz * 격자) / 격자);

                var 칸중심 = new Vector3((cx + 0.5f) * 격자, 0f, (cz + 0.5f) * 격자);
                // ★★칸 경계의 빈 띠를 없앤다 — **주인 정하기** (2026-08-11). 씨앗칸의 중심이
                //   든 칸이 그 씨앗칸의 주인이다. 반개구간([-반, 반))이라 이웃 칸이 같은
                //   씨앗칸을 또 심는 일도 없다 — 전엔 「나무가 중심에서 76.8m 안」으로 잘라
                //   칸 경계마다 6.4m 빈 띠가 일직선으로 남았다
                if (칸중심.x - 중심.x < -반 || 칸중심.x - 중심.x >= 반
                 || 칸중심.z - 중심.z < -반 || 칸중심.z - 중심.z >= 반) continue;
                Random.InitState(WorldGrid.TileSeed(0x7ee5, cx, cz));

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
                    // ★칸 밖으로 반 발짝 삐져나가는 건 그냥 둔다 — 주인이 하나뿐이라 겹치지 않는다
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
        int r = Mathf.CeilToInt(반 / 격자) + 1;
        for (int ix = -r; ix <= r; ix++)
            for (int iz = -r; iz <= r; iz++)
            {
                int cx = Mathf.FloorToInt((중심.x + ix * 격자) / 격자);
                int cz = Mathf.FloorToInt((중심.z + iz * 격자) / 격자);

                var 칸중심 = new Vector3((cx + 0.5f) * 격자, 0f, (cz + 0.5f) * 격자);
                // ★주인 정하기 — 나무뿌리기와 같은 이유 (칸 경계의 빈 띠 방지, 2026-08-11)
                if (칸중심.x - 중심.x < -반 || 칸중심.x - 중심.x >= 반
                 || 칸중심.z - 중심.z < -반 || 칸중심.z - 중심.z >= 반) continue;
                Random.InitState(WorldGrid.TileSeed(0x3c1d, cx, cz));
                float 진 = 진하기(칸중심, 배 > 1.5f ? 돌자_바위 : 돌자) * 길에서멂(칸중심);
                if (진 <= 0.02f) continue;

                float 몇 = Mathf.Lerp(0.4f, 돌칸당최대, 진);
                int n = Mathf.FloorToInt(몇);
                if (Random.value < 몇 - n) n++;

                for (int i = 0; i < n; i++)
                {
                    var at = 칸중심 + new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f) * 격자 * 0.95f;
                    if (!GroundPaint.잔디인가(at)) continue;
                    // ★★**작은 돌은 안 막는다** (2026-08-06 사용자 "작은 돌들은 그냥
                    //   지나가지던가 해야 하는데 전부 막혀서 힘들어").
                    //   벌판에 흩뿌린 돌멩이는 0.5~1.8m — 사람이 넘어 다닐 크기인데
                    //   그루마다 장애물을 깔아서 **들판이 온통 지뢰밭**이었다.
                    //   ☆무엇을 막을지는 **그림 크기가 정한다** (「그림 = 판정」).
                    if (Swap(바위프리팹, at, true, -1f)) continue;  // 큰 놈은 막고 작은 놈은 넘어간다
                    float w = Random.Range(0.5f, 1.8f);
                    float h = w * Random.Range(0.5f, 1.0f);
                    var 돌알 = Grey.Box(holder, at + Vector3.up * (h * 0.4f),
                             new Vector3(w, h, w * Random.Range(0.7f, 1.3f)), C바위, "돌멩이",
                             w >= 막는돌지름 ? w * 0.4f : 0f, Random.value * 360f);
                    // ★작은 돌멩이는 주울 수 있다 — 새로 깔지 않고 있던 것을 줍이로 (사용자 확정)
                    if (w < 막는돌지름) 땅무더기.줍이(아이템표.찾기("돌"), 1, 돌알);
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

        // ★나무 밑에 나뭇가지가 떨어져 있다 — 주우면 나무 ×1 (줍기 부트스트랩)
        if (!심지 && Random.value < 0.45f)
        {
            var 가지자리 = p + new Vector3(Random.Range(-1.8f, 1.8f), 0f, Random.Range(-1.8f, 1.8f));
            var 막대 = Grey.Box(holder, 가지자리 + Vector3.up * 0.035f,
                     new Vector3(0.08f, 0.06f, Random.Range(0.7f, 1.1f)),
                     new Color(0.40f, 0.29f, 0.17f), "나뭇가지", 0f, Random.value * 360f);
            땅무더기.줍이(아이템표.찾기("나무"), 1, 막대);
        }
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
        => Swap놈(set, pos, randomYaw, blockR) != null;

    /// Swap 과 같되 **세운 놈을 돌려준다** — 채집을 붙이려면 손에 쥐어야 한다
    GameObject Swap놈(GameObject[] set, Vector3 pos, bool randomYaw, float blockR)
    {
        if (set == null || set.Length == 0) return null;
        var pf = set[Random.Range(0, set.Length)];
        if (pf == null) return null;
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
        return inst;
    }

    /// 바위에 채집을 단다 — 크기는 짐작하지 않고 렌더러로 잰다
    static void 바위채집(GameObject g, Vector3 p)
    {
        float w = 2f;
        var rs = g.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            w = Mathf.Max(b.size.x, b.size.z);
        }
        var hv = g.AddComponent<Harvest>();
        // ★perHit = 한 히트에 튀는 돌맹이 수 (돌은 인벤 직행이 아니라 튀어 떨어진다)
        hv.kind = Stock.Kind.돌; hv.hits = Mathf.RoundToInt(3f + w); hv.perHit = 1; hv.blockAt = p;
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
