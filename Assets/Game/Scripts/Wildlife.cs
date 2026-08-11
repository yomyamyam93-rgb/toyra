using UnityEngine;

/// 야생 — **무리 단위로** 플레이어 주변에만 살려 둔다 (스트리밍).
///
/// ★한 마리씩 뿌리지 않는다. 무리(`Pack`)가 영역과 동요를 쥐고 있어야
///   "한 마리가 놀라면 같이 놀란다 · 새끼가 위험하면 어미가 사나워진다"가 성립한다.
///
/// ★어디서 무엇이 나오나는 **칸 종류**가 정한다. 지역마다 구성이 기울어야
///   조합을 바꿀 이유가 생긴다.
public class Wildlife : MonoBehaviour
{
    // ★★★**지어낸 종 이름을 다 지웠다** (2026-08-06 사용자 — "꼭꼬랑 호동, 늑구 이런쪽은
    //   싹다 지우라고했다"). 옛 이름(늑구·호동·꼭꼬·티라)은 앞 프로젝트에서 온 것이고,
    //   **실제로 우리가 가진 모델과 하나도 안 맞았다.** 이름이 둘이면 어느 게 진짜인지
    //   매번 헷갈린다 — 모델 파일 이름을 그대로 쓴다.
    //   ☆수치는 옛것을 가져왔다 (검증된 값이라). 크기·체력은 눈으로 보고 다시 정할 자리다.
    [Header("종 — 크기가 곧 성격이다")]
    public SpeciesDef 다람쥐 = new SpeciesDef
    {
        // ★S급 — 하나는 쉽고 떼는 죽는다 (헌법 5). 사람 3대면 눕는다
        이름 = "다람쥐", 키 = 1.62f, 반지름 = 0.3f, 무게 = 0.6f,
        체력 = 42f, 이속 = 4.6f, 피해 = 3f, 간격 = 1.1f, 사거리 = 0.9f,
        시야 = 20f, 시야각 = 260f, 청각 = 36f,           // 겁쟁이는 뒤도 잘 본다
        겁 = 0.9f, 공격성 = 0.1f, 영역 = 40f,            // 보면 도망간다
        무리최소 = 5, 무리최대 = 10, 새끼비율 = 0f,
        번식 = SpeciesDef.번식식.알, 활동시간 = SpeciesDef.활동.낮
    };

    public SpeciesDef 늑대 = new SpeciesDef
    {
        // ★M급 — 1대1 도 진짜 싸움이다 (7방 때려야 눕고, 13대 맞으면 내가 죽는다)
        이름 = "늑대", 키 = 2.04f, 반지름 = 0.4f, 무게 = 1.3f,
        체력 = 90f, 이속 = 4.0f, 피해 = 8f, 간격 = 0.9f, 사거리 = 1.1f,
        시야 = 22f, 시야각 = 200f, 청각 = 32f,
        겁 = 0.45f, 공격성 = 0.7f, 영역 = 50f,
        무리최소 = 4, 무리최대 = 8, 새끼비율 = 0.25f,   // ★태생 무리 — 새끼를 노리는 사냥
        번식 = SpeciesDef.번식식.태생, 활동시간 = SpeciesDef.활동.언제나
    };

    public SpeciesDef 사슴 = new SpeciesDef
    {
        // ★L급 — 정면으론 버겁다(11방). 무거워서 성체는 못 끌고 온다 → **새끼를 노린다**
        이름 = "사슴", 키 = 3.12f, 반지름 = 0.5f, 무게 = 3.0f,
        체력 = 150f, 이속 = 4.8f, 피해 = 9f, 간격 = 1.4f, 사거리 = 1.3f,
        시야 = 26f, 시야각 = 240f, 청각 = 40f,
        겁 = 0.8f, 공격성 = 0.15f, 영역 = 60f,           // 먼저 안 덤빈다. 잘 도망간다
        무리최소 = 3, 무리최대 = 7, 새끼비율 = 0.25f,
        번식 = SpeciesDef.번식식.태생, 활동시간 = SpeciesDef.활동.낮
    };

    public SpeciesDef 랩터 = new SpeciesDef
    {
        // ★M+ — 가장 사납다. 8대면 사람이 죽는다. 겁이 없어 제일 빨리 길든다
        이름 = "랩터", 키 = 2.6f, 반지름 = 0.45f, 무게 = 1.8f,
        체력 = 120f, 이속 = 4.4f, 피해 = 13f, 간격 = 0.8f, 사거리 = 1.2f,
        시야 = 26f, 시야각 = 190f, 청각 = 34f,
        겁 = 0.25f, 공격성 = 0.85f, 영역 = 60f,
        무리최소 = 3, 무리최대 = 6, 새끼비율 = 0f,       // ★알집 — 한 번에 여럿
        번식 = SpeciesDef.번식식.알, 활동시간 = SpeciesDef.활동.언제나
    };

    public SpeciesDef 티라노 = new SpeciesDef
    {
        // ★★XL 은 **싸울 대상이 아니다** (헌법 5 — "하나가 곧 사건이다. 피하는 게 정답").
        //   체력 170 이면 몽둥이 열두어 대라 「해볼 만한 보스」가 되어 버린다.
        //   격을 벌려 놓아야 **도망칠 이유**가 생기고, 헌법 3 의 *"나중에 상대하게 되는 건
        //   티라가 약해져서가 아니라 내 펫이 컸기 때문"* 이 성립한다.
        //   실측: 몽둥이 31방을 때려야 눕고, 3대 맞으면 내가 죽는다. 못 끌고 오고,
        //   기절시키려면 17대다. **이길 수 있게 만들지 않는다** (헌법 5)
        이름 = "티라노", 키 = 4.8f, 반지름 = 1.2f, 무게 = 9f,
        체력 = 430f, 이속 = 2.6f, 피해 = 34f, 간격 = 2f, 사거리 = 2.2f,
        시야 = 28f, 시야각 = 160f, 청각 = 40f,
        겁 = 0.05f, 공격성 = 0.95f, 영역 = 70f,          // 거의 안 물러선다
        무리최소 = 1, 무리최대 = 1, 새끼비율 = 0f,        // 혼자 다닌다
        번식 = SpeciesDef.번식식.알, 활동시간 = SpeciesDef.활동.언제나
    };

    // ★동행 펫도 여기에 둔다 — 모델을 인스펙터에서 끼울 수 있어야 하기 때문.
    //   내 편은 겁을 안 낸다 (`Critter.판단`) — 주인이 시킨 자리를 지킨다.
    [Header("동행 펫")]
    public SpeciesDef 내펫 = new SpeciesDef
    {
        이름 = "내펫", 키 = 1.9f, 반지름 = 0.55f, 무게 = 2f,
        체력 = 90f, 이속 = 3.4f, 피해 = 10f, 간격 = 1f, 사거리 = 1.3f,
        시야 = 28f, 시야각 = 220f, 청각 = 30f,
        겁 = 0f, 공격성 = 1f, 영역 = 999f,
        무리최소 = 1, 무리최대 = 1
    };

    [Header("얼마나 살려 두나")]
    public int 목표마릿수 = 26;
    [Tooltip("이 거리 밖에서 생긴다 (m) — 눈앞에서 튀어나오면 안 된다")] public float 최소거리 = 45f;
    public float 최대거리 = 85f;
    [Tooltip("이 거리를 넘으면 지운다 (m)")] public float 지우는거리 = 130f;
    public float 채우는간격 = 1.5f;
    [Tooltip("집에서 이만큼 안은 안전하다 (m)")] public float 집안전반경 = 45f;

    // ★★거리로 위험을 정한다 (발하임식). 세상이 약해지는 게 아니라 **내가 더 멀리 간다.**
    [Tooltip("안전반경 밖으로 이만큼 나가면 제일 사납다 (m)")] public float 사나워지는거리 = 520f;
    [Tooltip("클수록 집 가까이가 오래 순하다 (1 = 고르게)")] [Range(0.5f, 3f)] public float 사나움쏠림 = 1.6f;

    WorldGen world;
    DayNight day;
    float cd;

    void Start()
    {
        world = FindFirstObjectByType<WorldGen>();
        day = FindFirstObjectByType<DayNight>();
        if (미리읽기) StartCoroutine(미리읽기속());
    }

    // ★★★**미리 읽어 둔다** (2026-08-11 실측 — 사용자가 준 렉재기 로그가 범인을 짚었다:
    //   `[렉스파이크] 1154ms · 스크립트 95ms · 렌더 0ms · 컬링 0ms · GC 163,717KB`).
    //   계산이 아니라 **디스크에서 읽는 것**이 프레임을 통째로 먹었다. 처음 보는 변형이
    //   나타날 때 그 모델과 텍스처를 그 자리에서 읽기 때문이다 (텍스처 한 장이 메모리에선
    //   수십 MB 다). 평균은 7.4ms(134fps)인데 이 순간만 1초를 멈춘다.
    //   → 시작할 때 **몇 개씩 나눠** 읽어 캐시에 올려 둔다. 게임 중엔 읽을 것이 없어진다.
    //   ☆한 프레임에 몰아 읽으면 그게 또 렉이다 — 프레임당 하나씩만 읽는다.
    [Header("★미리 읽기 (렉 방지)")]
    [Tooltip("시작할 때 펫 모델·동작을 미리 읽어 둔다 — 껐다 켜서 효과를 견줄 수 있다")]
    public bool 미리읽기 = true;
    [Tooltip("한 프레임에 몇 개까지 읽나")] [Range(1, 8)] public int 프레임당 = 1;
    [Tooltip("애니메이터 연결까지 미리 시킨다 — 첫 스폰 11~15ms 가 1ms 로 떨어진다")]
    public bool 예열 = true;

    System.Collections.IEnumerator 미리읽기속()
    {
        var 시계 = System.Diagnostics.Stopwatch.StartNew();
        int 모델수 = 0, 클립수 = 0, 센것 = 0;
        var 동작 = 동작진열.동작들;

        foreach (var v in 변형표)
        {
            // 모델뽑기 가 `변형캐시` 에 담아 둔다 — 같은 앞머리는 한 번만 읽는다
            var 이름들 = new System.Collections.Generic.List<string>();
            if (v.모델.EndsWith("_")) { for (int n = 1; n <= 9; n++) 이름들.Add(v.모델 + n); }
            else 이름들.Add(v.모델);

            foreach (var nm in 이름들)
            {
                var g = Resources.Load<GameObject>("rig/" + nm);
                if (g == null) continue;
                모델수++;
                // ★메시·텍스처를 **실제로 메모리에 올린다** — 참조만 잡으면 아직 안 읽는다
                foreach (var r in g.GetComponentsInChildren<Renderer>(true))
                {
                    var m = r.sharedMaterial;
                    if (m != null && m.mainTexture != null) { var _ = m.mainTexture.width; }
                }
                foreach (var d in 동작)
                    if (Resources.Load<AnimationClip>("rig/" + d + "_" + nm) != null) 클립수++;

                // ★★★**애니메이터 연결까지 미리 시킨다** (2026-08-11 실측 — 종류마다
                //   첫 스폰 11~15ms · 두 번째부터 1ms 로 **12배** 차이가 났다).
                //   모델을 읽어 두는 것만으로는 모자란다: 유니티는 그 모델에 컨트롤러를
                //   **처음 물릴 때** 뼈와 커브를 짝지어 놓는데(바인딩), 그게 첫 스폰의 비용이다.
                //   → 화면 밖에서 한 마리를 만들어 한 틱 돌리고 지운다. 그러면 그 짝짓기가
                //     끝나 있어서 게임 중 첫 등장이 1ms 로 떨어진다.
                if (예열)
                {
                    var 통 = new GameObject("예열_" + nm);
                    통.transform.position = new Vector3(0f, -2000f, 0f);   // 화면 밖(땅 아래)
                    var 몸 = Instantiate(g, 통.transform);
                    몸.name = "Armature";
                    foreach (var r in 몸.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                    var an = 통.AddComponent<Animator>();
                    var 짓 = 통.AddComponent<몸짓>();
                    짓.준비(an, nm);
                    짓.enabled = false;              // Critter 없이 Update 가 돌면 안 된다
                    an.Update(0f);                   // ★여기서 바인딩이 실제로 일어난다
                    Destroy(통);
                }

                if (++센것 % Mathf.Max(1, 프레임당) == 0) yield return null;
            }
            모델뽑기(v.모델);       // 캐시 채우기
        }
        Debug.Log($"[야생] 미리 읽기 끝 — 모델 {모델수}개 · 동작 {클립수}개 · {시계.ElapsedMilliseconds}ms");
    }

    // ★★스스로 시간을 잰다 (2026-08-11) — 스파이크에 「Update 72ms」 라고만 찍히면
    //   **어느 코드인지** 알 수가 없다. 오래 걸린 프레임에 무엇을 했는지 같이 남긴다.
    [Tooltip("Wildlife 가 이보다 오래 걸린 프레임을 콘솔에 남긴다 (ms · 0 이면 안 남김)")]
    public float 스스로재기 = 12f;

    void Update()
    {
        var 시계 = 스스로재기 > 0f ? System.Diagnostics.Stopwatch.StartNew() : null;
        int 지운수기록 = 0, 만든수기록 = 0;
        try { 실제Update(ref 지운수기록, ref 만든수기록); }
        finally
        {
            if (시계 != null)
            {
                시계.Stop();
                double ms = 시계.Elapsed.TotalMilliseconds;
                if (ms >= 스스로재기)
                    Debug.LogFormat("[야생-느림] {0:F0}ms · 지움 {1} · 만듦 {2} · 지금 {3}마리",
                                    ms, 지운수기록, 만든수기록, Critter.All.Count);
            }
        }
    }

    void 실제Update(ref int 지운수기록, ref int 만든수기록)
    {
        var hero = Hero.Me;
        if (hero == null) return;

        // ★★멀어진 놈 치우기도 **한 프레임에 몰아서 하지 않는다** (2026-08-06).
        //   뼈 있는 몸을 여러 마리 한꺼번에 `Destroy` 하면 그 프레임이 통째로 튄다.
        int 지운수 = 0;
        for (int i = Critter.All.Count - 1; i >= 0 && 지운수 < 한프레임치우기; i--)
        {
            var c = Critter.All[i];
            if (c == null || c.side != Critter.Side.야생) continue;
            if (Flat(c.transform.position, hero.transform.position) > 지우는거리)
            { Destroy(c.gameObject); 지운수++; }
        }
        지운수기록 = 지운수;

        cd -= Time.deltaTime;
        if (cd > 0f) return;
        cd = 채우는간격;

        int wild = 0;
        foreach (var c in Critter.All) if (c != null && c.side == Critter.Side.야생) wild++;
        if (wild >= 목표마릿수) return;

        // 많이 모자라면(시작 직후·긴 이동 뒤) 빨리 채운다 — 그래도 한 틱에 한 마리다
        if (wild < 목표마릿수 / 2) cd = 채우는간격 * 0.2f;

        홀로생성(hero.transform.position);
        만든수기록 = 1;
    }

    [Tooltip("한 프레임에 몇 마리까지 치우나")]
    [Range(1, 10)] public int 한프레임치우기 = 2;

    /// ★★★**무리 스폰을 걷어냈다 — 전부 홀로 나온다** (2026-08-07 사용자 "팻스폰 싹 지우고
    ///   다시 만들어, 아크 스폰방식으로. 특정 팻 지정하기 전까지 무리 넣지 마").
    ///
    ///   전에는 `무리최소~최대` 마리가 한 자리에 뭉쳐 나와서 **어딜 가나 덩어리**였다.
    ///   아크의 구조는 「지역 테이블에서 종을 뽑아 → 그 자리에 소수를 놓는다」이고,
    ///   우리는 이미 지역 테이블(`종고르기` 가 칸 종류·거리로 고른다)을 갖고 있다.
    ///   빠져 있던 건 **구성**이다 — 지금은 전 종 1마리 고정.
    ///
    ///   ☆무리를 되살릴 자리: 종을 지정받으면 그 종만 `무리최소~최대` 를 쓰게 한다.
    ///     `Pack` 클래스는 그날을 위해 남겨 둔다 (은퇴는 삭제가 아니라 스위치).
    ///   ☆한 틱에 한 마리라 스폰 스파이크도 구조적으로 사라졌다 (전엔 무리 통째 = 71ms).
    // ★★★**rig 로스터 전체를 쓴다** (2026-08-07 사용자 "리깅 넣은 모델링 팻들을 왜 안 쓰냐").
    //   전에는 종당 변형 1개(늑대_1 하나)만 물려서 어딜 가나 같은 몸이었다.
    //   기본 종에서 뽑힌 뒤 여기서 **모델 변형·희귀종으로 갈라진다** — 키·체력·피해는
    //   키 비례로 따라간다. 키 숫자는 F1(동작진열) 표의 값 그대로다.
    // ★★**「먼곳부터」 — 멀어야 나오는 변형** (2026-08-11 사용자 "초반마을에 다양한 종들이
    //   전부 나오지 않을거같아서"). 전엔 변형 확률이 어디서나 같아서 **마을 코앞에서도
    //   다이어울프·스테고가 나왔다.** 기본종은 이미 거리로 골라지는데(종고르기의 멂)
    //   변형만 거리를 안 봤던 것 — 같은 자(멂재기, 0~1)로 문턱을 건다.
    //   0 = 어디서나 · 0.35 ≈ 집에서 315m · 0.5 ≈ 380m. 걸러진 몫은 기본형이 가져간다.
    static readonly (string 기본종, string 모델, float 키, float 확률, float 먼곳부터, string 이름)[] 변형표 =
    {
        // 다람쥐 계열
        ("다람쥐", "다람쥐_",     1.62f, 0.90f, 0f,    "다람쥐"),
        ("다람쥐", "검치다람쥐",  1.98f, 0.10f, 0.35f, "검치다람쥐"),
        // 늑대 계열
        ("늑대",   "늑대_",       2.04f, 0.88f, 0f,    "늑대"),
        ("늑대",   "다이어울프",  2.28f, 0.12f, 0.40f, "다이어울프"),
        // 사슴 계열 — 초식 대형들이 여기서 갈라진다
        ("사슴",   "사슴_",       3.12f, 0.55f, 0f,    "사슴"),
        ("사슴",   "큰뿔사슴",    3.60f, 0.10f, 0.35f, "큰뿔사슴"),
        ("사슴",   "트리케_",     3.40f, 0.20f, 0.45f, "트리케"),
        ("사슴",   "스테고_",     3.60f, 0.15f, 0.50f, "스테고"),
        // 랩터 계열
        ("랩터",   "랩터_",       2.60f, 0.75f, 0f,    "랩터"),
        ("랩터",   "테러버드_",   2.40f, 0.25f, 0.30f, "테러버드"),
        // 티라노 계열 — 기본종 자체가 먼 데서만 나온다 (종고르기)
        ("티라노", "티라노_",     4.80f, 1.00f, 0f,    "티라노"),
    };

    /// 앞머리가 `_` 로 끝나면 번호 변형이 있다 — rig 폴더에서 있는 번호를 세어 무작위로 뽑는다
    static readonly System.Collections.Generic.Dictionary<string, GameObject[]> 변형캐시 =
        new System.Collections.Generic.Dictionary<string, GameObject[]>();

    static GameObject 모델뽑기(string 앞머리)
    {
        if (!변형캐시.TryGetValue(앞머리, out var 들))
        {
            var 목록 = new System.Collections.Generic.List<GameObject>();
            if (앞머리.EndsWith("_"))
                for (int n = 1; n <= 9; n++)
                {
                    var g = Resources.Load<GameObject>("rig/" + 앞머리 + n);
                    if (g != null) 목록.Add(g);
                }
            else
            {
                var g = Resources.Load<GameObject>("rig/" + 앞머리);
                if (g != null) 목록.Add(g);
            }
            변형캐시[앞머리] = 들 = 목록.ToArray();
        }
        return 들.Length > 0 ? 들[Random.Range(0, 들.Length)] : null;
    }

    [Tooltip("스폰마다 콘솔에 무엇을 넣었는지 찍는다 — 모델이 잘못 들어가면 여기서 바로 보인다")]
    public bool 스폰로그 = true;

    [Tooltip("★태생 종은 새끼를 데리고 나온다 (끄면 예전처럼 성체만 나온다)")]
    public bool 어미새끼 = true;

    [Header("★난이도 — 덩치가 곧 강함")]
    [Tooltip("키 배수에 이 지수로 체력이 붙는다 (1 = 비례 · 2.4 = 부피에 가깝다)")]
    [Range(1f, 3f)] public float 체력지수 = 2.4f;
    [Tooltip("키 배수에 이 지수로 피해가 붙는다 — 체력보다 완만해야 한다")]
    [Range(1f, 2.5f)] public float 피해지수 = 1.6f;

    void 홀로생성(Vector3 heroPos)
    {
        // ★★**화면 밖에서 생긴다** (2026-08-07 사용자 "화면 안에서 생성되는 게 보이지 않게").
        //   줌을 60m 까지 풀어서 옛 상수(45~85m)가 화면 안에 들어와 버렸다 —
        //   상수 대신 **지금 화면 반경에서 파생**시킨다 (「상수 대신 실측에서 파생」).
        float 근 = 최소거리, 원 = 최대거리;
        var cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            float h = cam.orthographicSize, w = h * cam.aspect;
            float 화면반경 = Mathf.Sqrt(h * h + w * w);         // 화면 대각선 절반
            근 = Mathf.Max(최소거리, 화면반경 * 1.12f);
            원 = 근 + 45f;
        }

        SpeciesDef s = null; Vector3 at = heroPos;
        for (int tries = 0; tries < 8 && s == null; tries++)
        {
            float a = Random.value * Mathf.PI * 2f;
            float r = Random.Range(근, 원);
            at = heroPos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
            at.x = Mathf.Clamp(at.x, 20f, WorldGrid.Size - 20f);
            at.z = Mathf.Clamp(at.z, 20f, WorldGrid.Size - 20f);
            s = 종고르기(at);
        }
        if (s == null) return;

        // 변형으로 갈라진다 — 원본 정의는 안 건드린다 (복제)
        // ★가까우면 못 나오는 변형을 먼저 거르고, 남은 것끼리 확률을 다시 나눈다 —
        //   안 거르고 굴리면 걸러진 확률만큼 아무도 안 뽑혀 회색 상자가 나온다
        float 멂변 = 멂재기(at), 합 = 0f;
        foreach (var v in 변형표)
            if (v.기본종 == s.이름 && 멂변 >= v.먼곳부터) 합 += v.확률;
        float 룰렛 = Random.value * Mathf.Max(0.0001f, 합), 누적 = 0f;
        foreach (var (기본, 앞머리, 키, 확률, 먼곳부터, 이름) in 변형표)
        {
            if (기본 != s.이름 || 멂변 < 먼곳부터) continue;
            누적 += 확률;
            if (룰렛 > 누적) continue;

            var 모델 = 모델뽑기(앞머리);
            if (모델 != null)
            {
                var d = s.복제();
                float 배 = 키 / Mathf.Max(0.01f, d.키);
                d.모델 = 모델; d.이름 = 이름;
                d.키 = 키; d.반지름 *= 배; d.사거리 *= 배;
                // ★★★**덩치는 비례가 아니라 제곱으로 세진다** (2026-08-10 사용자 —
                //   "큰 팻이 말도안돼게 3,4방에 뒤지는경우가 많길래").
                //   전에는 체력도 `× 배` 라, 키가 두 배인 놈이 두 배만 버텼다.
                //   실제로 덩치는 **부피**로 커지므로 비례로는 「크다」가 안 읽힌다.
                //   ☆피해는 체력보다 완만하게 — 똑같이 올리면 큰 놈이 한 대에 사람을 죽인다
                d.체력 *= Mathf.Pow(배, 체력지수);
                d.피해 *= Mathf.Pow(배, 피해지수);
                // ★★★**무게도 같이 커진다** (2026-08-10 — 빠져 있었다).
                //   전에는 무게만 기본 종 값 그대로였다. 그래서 **3.4m 트리케를 사슴처럼
                //   끌고 다니고, 2.28m 다이어울프를 품에 안고** 다닐 수 있었다 —
                //   기획의 「몸집이 곧 통행권」·「크기가 방법을 정한다」가 여기서 깨졌다.
                //   ☆세제곱(부피)이 아니라 **키에 비례**로 둔다. 세제곱을 걸면 늑대까지
                //     한 번에 못 잡는 무게가 되어 길들일 수 있는 종이 거의 없어진다.
                d.무게 *= 배;
                s = d;
            }
            break;
        }

        var c = Make(s, at, Critter.Side.야생, null);   // 무리 없음 — 한 마리가 전부다

        // ★★★**새끼가 어미 옆에 붙어 나온다** (기획 8장 2번 — "태생 + 어미+새끼 + 생포".
        //   `새끼비율`·`새끼로()` 는 진작 있었는데 **아무도 안 불렀다** — 여태 성체만 나왔다).
        //
        //   ☆이게 길들이기의 문턱을 정한다: 성체는 먹이값 15 라 일곱 번을 먹여야 하고,
        //     새끼는 34 라 세 번이면 된다. **"새끼를 노린다"는 사냥이 여기서 생긴다.**
        //   ☆무리(`Pack`)를 되살리는 게 아니다 — 어미 하나와 새끼 하나뿐이다.
        if (어미새끼 && s.번식 == SpeciesDef.번식식.태생 && Random.value < s.새끼비율)
        {
            var 애정의 = s.새끼로();
            var 옆 = at + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
            var 애 = Make(애정의, 옆, Critter.Side.야생, null);
            애.새끼로설정(s);                            // 다 크면 어미의 몸이 된다
            if (스폰로그) Debug.Log($"[야생] {애정의.이름} ← 어미 {s.이름} 옆에");
        }

        if (스폰로그)
            Debug.Log(string.Format("[야생] {0} ← 모델 {1} · 키 {2:F2}m · 자리 {3:F0},{4:F0} (화면반경 밖 {5:F0}m)",
                s.이름, s.모델 != null ? s.모델.name : "★상자(모델없음)", s.키, at.x, at.z, 근));
    }

    /// 집에서 얼마나 먼가 0~1 — 기본종(종고르기)과 변형(변형표의 먼곳부터)이 **같은 자**를 쓴다
    float 멂재기(Vector3 p)
    {
        var home = WorldGrid.Center;
        float 멂 = Mathf.Clamp01((new Vector2(p.x - home.x, p.z - home.z).magnitude - 집안전반경)
                                  / Mathf.Max(1f, 사나워지는거리));
        return Mathf.Pow(멂, 사나움쏠림);          // 가까운 쪽이 넓다 — 초반이 답답하지 않게
    }

    /// 그 자리·그 시각에 무엇이 나오나
    SpeciesDef 종고르기(Vector3 p)
    {
        var home = WorldGrid.Center;
        if ((new Vector2(p.x - home.x, p.z - home.z)).sqrMagnitude < 집안전반경 * 집안전반경) return null;

        // ★불 옆에는 생기지 않는다 — 생기자마자 놀라 달아나는 그림은 고장처럼 보인다.
        //   (겁내는 건 `Critter.불에놀람` 이 하고, 여기서는 애초에 안 놓는다)
        if (모닥불.무서운불(p) != null) return null;

        var land = world != null ? world.KindAt(p) : WorldGen.Land.빈들판;

        // ★★★**집에서 멀수록 사납다** (2026-08-06 — 발하임이 바이옴을 세계 중심에서의
        //   거리로 정하는 방식).
        //
        //   헌법 3번: *"성장해도 세상이 안 약해진다. 성장의 보상은 쉬워지는 게 아니라
        //   **저기까지 갈 수 있게 된다**"*. 그런데 지금까지 우리 세계는 **어디나 위험이
        //   똑같았다** — 「저기」가 없으면 그 헌법이 작동할 데가 없다.
        //   ☆세상이 약해지는 게 아니다. **세상은 그대로고 내가 더 멀리 갈 뿐이다.**
        float 멂 = 멂재기(p);

        // ★칸 종류가 후보를 정하고, **집에서의 거리**가 그 안에서 사나운 쪽으로 기울인다
        SpeciesDef s;
        float r = Random.value;
        switch (land)
        {
            case WorldGen.Land.물웅덩이: return null;

            // ★동굴 칸엔 야생을 안 놓는다 (2026-08-11) — 길찾기가 없어서 벽 안에 생기면
            //   비비기만 한다. 동굴 서식 종(야행)을 제대로 만들 때 여기서 연다
            case WorldGen.Land.동굴: return null;

            // 숲 — 다람쥐와 사슴의 자리. 멀어지면 늑대가 섞이고 더 멀면 티라노
            case WorldGen.Land.숲:
                s = r < 0.40f ? 다람쥐
                  : r < 0.70f ? 사슴
                  : r < 0.70f + 멂 * 0.20f ? 늑대 : (멂 > 0.55f ? 티라노 : 다람쥐);
                break;

            // 둥지 — 알을 낳는 종만
            case WorldGen.Land.둥지:
                s = r < 0.6f ? 다람쥐 : (멂 > 0.5f ? 티라노 : 랩터);
                break;

            // 바위지대·폐허 — 사나운 쪽
            case WorldGen.Land.바위지대:
            case WorldGen.Land.폐허:
                s = r < 0.30f + 멂 * 0.35f ? 랩터
                  : r < 0.65f + 멂 * 0.30f ? 늑대 : (멂 > 0.4f ? 티라노 : 다람쥐);
                break;

            // ★테마 권역 — 제 서식지 팻(블록 팻·솜털 팻…)이 로스터에 들어오면 **여기서 갈린다**
            //   (2026-08-11 사용자 "블록방식의 팻들은 블록지역에 떠야하고").
            //   지금은 그 팻들이 아직 없어서 들판과 같은 구성으로 둔다 — 자리만 파 놓는다
            case WorldGen.Land.찰흙:
            case WorldGen.Land.솜털실:
            case WorldGen.Land.블록:
            case WorldGen.Land.유리설원:
                goto default;

            // 빈들판 — 떼가 몰려오는 자리 (헌법 5번의 「수」)
            default:
                s = r < 0.35f ? 사슴
                  : r < 0.65f ? 다람쥐
                  : r < 0.65f + 멂 * 0.25f ? 늑대 : (멂 > 0.6f ? 티라노 : 사슴);
                break;
        }

        // 활동 시간이 안 맞으면 안 나온다 — 밤에만 도는 종이 생기는 자리
        if (day != null && s.활동시간 != SpeciesDef.활동.언제나)
        {
            bool 낮 = day.낮정도 > 0.5f;
            if ((s.활동시간 == SpeciesDef.활동.낮) != 낮) return null;
        }
        return s;
    }

    /// 생물 하나 만들기.
    /// ★자식 0번이 「몸」이어야 한다 — `Critter` 가 그걸 눌렀다 펴서 피격을 보여준다.
    ///   모델이 있으면 모델이 그 자리에 들어가고, 없으면 색칠한 상자가 들어간다.
    public static Critter Make(SpeciesDef s, Vector3 pos, Critter.Side side, Transform owner)
    {
        var go = new GameObject(s.이름);
        go.transform.position = new Vector3(pos.x, 0f, pos.z);

        if (s.모델 != null) 모델몸(go.transform, s);
        else 상자몸(go.transform, s, side);

        var c = go.AddComponent<Critter>();
        c.side = side; c.종 = s; c.owner = owner;

        return c;
    }

    /// 진짜 모델을 「몸」으로 — 키에 맞춰 줄이고 발을 땅에 붙인다
    static void 모델몸(Transform parent, SpeciesDef s)
    {
        var g = Instantiate(s.모델);
        g.name = "몸";
        g.transform.SetParent(parent, false);
        g.transform.localPosition = Vector3.zero;
        g.transform.localRotation = Quaternion.Euler(0f, s.모델회전, 0f);
        g.transform.localScale = Vector3.one;

        var rs = g.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;

        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        float k = 1f;
        if (b.size.y > 0.0001f) { k = s.키 / b.size.y; g.transform.localScale = Vector3.one * k; }

        // ★★크기를 바꾼 「직후」에 bounds 를 다시 읽으면 안 된다 (2026-08-07 —
        //   "다람쥐가 공중에 떠 있고"). `SkinnedMeshRenderer.bounds` 는 스키닝이 한 번
        //   돌아야 갱신되는데 그건 **다음 프레임**이다 — 낡은 값으로 발을 붙이니 떴다.
        //   → 다시 재지 말고 **수학으로 구한다**: 크기를 k배 하면 밑면도 원점 기준 k배다.
        float 밑 = g.transform.position.y + (b.min.y - g.transform.position.y) * k;
        g.transform.position += Vector3.up * (parent.position.y - 밑);

        걷기물리기(parent, g, s);

        // ★★★새 몸이 생겼다고 **아웃라인에 알려 준다** (2026-08-11 실측).
        //   안 알려주면 아웃라인이 씬 전체를 `FindObjectsByType` 로 뒤져서 찾아낸다 —
        //   세계 렌더러가 11만 개라 그게 **86ms · GC 1.7MB** 다. 야생이 2초마다 스폰되니
        //   그 스파이크가 2초마다 왔다 (렉재기 로그로 확인). 알려주면 그 훑기가 아예 없다.
        Outliner.새것(g);
    }

    /// ★★**리깅 모델에 걷기 애니메이션을 물린다** (2026-08-07 사용자 "팻 리깅 다 쳐넣고
    ///   하나도 적용을 안 하네").
    ///
    ///   `Resources/rig/` 의 모델에는 짝이 되는 컨트롤러가 **이미 다 들어 있다**
    ///   (`걷기_늑대_1.controller` 처럼 이름이 짝이다). 그런데 아무도 물려 주지 않아서
    ///   모델만 서 있는 채로 미끄러졌다. 여기서 이름으로 찾아 붙인다.
    ///
    /// ★`AlwaysAnimate` 로 둔다 — 아이소 화면에서는 몸이 화면 밖으로 조금 나가도
    ///   그림자·실루엣이 남으므로, 컬링되어 자세가 굳으면 눈에 띈다.
    /// ★★★**경로가 한 칸 어긋나 있었다** (2026-08-07 — "공중에 떠 있고 모션도 하나도 없는").
    ///
    ///   클립은 `Armature/spine_hip/...` 을 찾는데, glb 로 임포트된 프리팹에는 **Armature
    ///   노드가 없다** (루트가 곧 그 자리다). 그래서 46종 전부 **바인딩 0%** — 애니메이터는
    ///   시간만 흐르고 뼈는 하나도 안 붙었다. "모션 없음" 의 정체가 이것이다.
    ///   ☆실측: `Armature/` 접두를 떼면 46종 모두 100% 붙는다.
    ///   → **모델 루트를 `Armature` 로 개명**하고 애니메이터를 **부모(생물 루트)** 에 단다.
    ///     그러면 클립 경로가 그대로 맞는다. `Critter` 는 자식을 이름이 아니라
    ///     `GetChild(0)` 으로 잡으므로 개명해도 안전하다.
    static void 걷기물리기(Transform parent, GameObject g, SpeciesDef s)
    {
        if (s.모델 == null) return;
        // ★동작은 이제 `몸짓` 이 공용 컨트롤러(_공용동작) + 오버라이드로 전부 맡는다 —
        //   여기서는 뼈 경로를 맞추고(개명) 애니메이터만 세운다
        g.name = "Armature";                            // 클립 경로의 첫 칸이 된다

        var an = parent.GetComponent<Animator>();
        if (an == null) an = parent.gameObject.AddComponent<Animator>();
        an.applyRootMotion = false;                    // 이동은 `Critter.걷기` 가 한다
        an.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // ★★검은 테두리 — 짐승은 **스폰될 때** 붙여야 한다 (씬에 미리 없으니까).
        //   비용이 드로콜 ×2 라 **짐승·캐릭터에만** 붙인다. 나무 2만 그루엔 절대 안 붙인다.
        if (parent.GetComponent<외곽선붙이기>() == null)
            parent.gameObject.AddComponent<외곽선붙이기>();

        // ★상태 따라 여섯 동작(걷기·뛰기·대기·공격·피격·죽음)을 갈아탄다
        var 몸짓기 = parent.GetComponent<몸짓>();
        if (몸짓기 == null) 몸짓기 = parent.gameObject.AddComponent<몸짓>();
        몸짓기.준비(an, s.모델.name);

        // ★★★**크기·바닥은 「애니메이션이 붙은 자세」로 다시 잰다** (2026-08-07 실측 —
        //   "스케일이 하나도 적용 안 됐어, 공중에 떠 있고").
        //   클립이 뼈 위치를 원본 리그 값으로 끌고 가서, 정지 자세로 맞춘 크기가
        //   **재생 첫 프레임에 절반으로 줄고 몸이 떠올랐다** (다람쥐 실측 1.62 → 0.83).
        //   정지 자세 바운즈는 거짓말을 한다 — **스킨 정점을 구워(BakeMesh) 재는 것**만 진실이다.
        an.Update(0f);                                   // 첫 자세를 강제로 적용
        진짜맞춤(parent, g.transform, s, an);
    }

    // ★필드 초기화에서 `new Mesh()` 를 부르면 안 된다 (유니티가 클래스 통째로 죽인다 —
    //   실제로 야생이 0마리가 됐다). 처음 쓸 때 만든다.
    static Mesh 잼틀;

    /// 스킨 정점 기준으로 ①키를 `종.키` 에 맞추고 ②발밑을 땅에 붙인다
    static void 진짜맞춤(Transform parent, Transform g, SpeciesDef s, Animator an)
    {
        if (잼틀 == null) 잼틀 = new Mesh();
        float 최저, 최고;
        void 재기(out float lo, out float hi)
        {
            lo = 9e9f; hi = -9e9f;
            foreach (var smr in g.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.BakeMesh(잼틀, true);
                var vs = 잼틀.vertices;
                for (int i = 0; i < vs.Length; i += 5)
                {
                    float y = smr.transform.TransformPoint(vs[i]).y;
                    if (y < lo) lo = y; if (y > hi) hi = y;
                }
            }
        }

        재기(out 최저, out 최고);
        if (최저 > 8e9f) return;                          // 스킨이 없으면 (상자) 그대로

        float 실키 = 최고 - 최저;
        if (실키 > 0.01f)
        {
            g.localScale *= s.키 / 실키;                  // ①진짜 키를 설정값으로
            an.Update(0f);
            재기(out 최저, out 최고);                      // 크기를 바꿨으니 다시 잰다
        }
        g.position += Vector3.up * (parent.position.y - 최저);   // ②진짜 발밑을 땅에
    }

    /// 모델이 없을 때 — 색칠한 상자 (앞에 머리를 붙여 방향이 읽히게)
    static void 상자몸(Transform parent, SpeciesDef s, Critter.Side side)
    {
        var col = side == Critter.Side.내편
            ? new Color(0.45f, 0.75f, 0.95f)
            : Grey.ColorFor(s.이름);

        float w = s.반지름 * 2f;
        var body = Grey.Box(parent, Vector3.zero, new Vector3(w * 0.8f, s.키, w), col, "몸");
        body.transform.SetParent(parent, false);
        body.transform.localPosition = Vector3.up * (s.키 * 0.5f);

        var head = Grey.Box(body.transform, Vector3.zero, new Vector3(0.6f, 0.6f, 0.5f),
                            new Color(col.r * 0.5f, col.g * 0.5f, col.b * 0.5f), "머리");
        head.transform.SetParent(body.transform, false);
        head.transform.localPosition = new Vector3(0f, 0.35f, 0.6f);
    }

    static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }
}
