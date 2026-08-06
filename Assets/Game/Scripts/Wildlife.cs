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
        이름 = "다람쥐", 키 = 0.9f, 반지름 = 0.3f, 무게 = 0.4f,
        체력 = 14f, 이속 = 4.2f, 피해 = 2f, 간격 = 1.1f, 사거리 = 0.9f,
        시야 = 20f, 시야각 = 260f, 청각 = 36f,           // 겁쟁이는 뒤도 잘 본다
        겁 = 0.9f, 공격성 = 0.1f, 영역 = 40f,            // 보면 도망간다
        무리최소 = 5, 무리최대 = 10, 새끼비율 = 0f,
        번식 = SpeciesDef.번식식.알, 활동시간 = SpeciesDef.활동.낮
    };

    public SpeciesDef 늑대 = new SpeciesDef
    {
        이름 = "늑대", 키 = 1.2f, 반지름 = 0.4f, 무게 = 0.7f,
        체력 = 24f, 이속 = 3.6f, 피해 = 4f, 간격 = 0.9f, 사거리 = 1.1f,
        시야 = 22f, 시야각 = 200f, 청각 = 32f,
        겁 = 0.45f, 공격성 = 0.7f, 영역 = 50f,
        무리최소 = 4, 무리최대 = 8, 새끼비율 = 0.25f,   // ★태생 무리 — 새끼를 노리는 사냥
        번식 = SpeciesDef.번식식.태생, 활동시간 = SpeciesDef.활동.언제나
    };

    public SpeciesDef 사슴 = new SpeciesDef
    {
        이름 = "사슴", 키 = 1.8f, 반지름 = 0.5f, 무게 = 1.6f,
        체력 = 40f, 이속 = 4.6f, 피해 = 5f, 간격 = 1.4f, 사거리 = 1.3f,
        시야 = 26f, 시야각 = 240f, 청각 = 40f,
        겁 = 0.8f, 공격성 = 0.15f, 영역 = 60f,           // 먼저 안 덤빈다. 잘 도망간다
        무리최소 = 3, 무리최대 = 7, 새끼비율 = 0.25f,
        번식 = SpeciesDef.번식식.태생, 활동시간 = SpeciesDef.활동.낮
    };

    public SpeciesDef 랩터 = new SpeciesDef
    {
        이름 = "랩터", 키 = 1.6f, 반지름 = 0.45f, 무게 = 1.1f,
        체력 = 34f, 이속 = 4.4f, 피해 = 7f, 간격 = 0.8f, 사거리 = 1.2f,
        시야 = 26f, 시야각 = 190f, 청각 = 34f,
        겁 = 0.25f, 공격성 = 0.85f, 영역 = 60f,
        무리최소 = 3, 무리최대 = 6, 새끼비율 = 0f,       // ★알집 — 한 번에 여럿
        번식 = SpeciesDef.번식식.알, 활동시간 = SpeciesDef.활동.언제나
    };

    public SpeciesDef 티라노 = new SpeciesDef
    {
        이름 = "티라노", 키 = 4.5f, 반지름 = 1.2f, 무게 = 8f,
        체력 = 170f, 이속 = 2.1f, 피해 = 22f, 간격 = 2f, 사거리 = 2.2f,
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
    }

    void Update()
    {
        var hero = Hero.Me;
        if (hero == null) return;

        for (int i = Critter.All.Count - 1; i >= 0; i--)
        {
            var c = Critter.All[i];
            if (c == null || c.side != Critter.Side.야생) continue;
            if (Flat(c.transform.position, hero.transform.position) > 지우는거리) Destroy(c.gameObject);
        }

        cd -= Time.deltaTime;
        if (cd > 0f) return;
        cd = 채우는간격;

        int wild = 0;
        foreach (var c in Critter.All) if (c != null && c.side == Critter.Side.야생) wild++;
        if (wild >= 목표마릿수) return;

        무리생성(hero.transform.position);
    }

    void 무리생성(Vector3 heroPos)
    {
        SpeciesDef s = null; Vector3 at = heroPos;
        for (int tries = 0; tries < 8 && s == null; tries++)
        {
            float a = Random.value * Mathf.PI * 2f;
            float r = Random.Range(최소거리, 최대거리);
            at = heroPos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
            at.x = Mathf.Clamp(at.x, 20f, WorldGrid.Size - 20f);
            at.z = Mathf.Clamp(at.z, 20f, WorldGrid.Size - 20f);
            s = 종고르기(at);
        }
        if (s == null) return;

        var pack = new Pack(s, at);
        int n = Random.Range(s.무리최소, s.무리최대 + 1);
        int 새끼수 = s.번식 == SpeciesDef.번식식.태생
                   ? Mathf.FloorToInt(n * s.새끼비율) : 0;
        pack.처음마릿수 = n;

        for (int i = 0; i < n; i++)
        {
            var p = at + new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));
            bool 새끼 = i < 새끼수;
            // ★새끼는 무리 한가운데 — 어미들이 둘러싼 안쪽이라 골라 때리기 어렵다
            if (새끼) p = at + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
            var c = Make(새끼 ? s.새끼로() : s, p, Critter.Side.야생, null);
            c.새끼 = 새끼;
            pack.들어옴(c);
        }
    }

    /// 그 자리·그 시각에 무엇이 나오나
    SpeciesDef 종고르기(Vector3 p)
    {
        var home = WorldGrid.Center;
        if ((new Vector2(p.x - home.x, p.z - home.z)).sqrMagnitude < 집안전반경 * 집안전반경) return null;

        var land = world != null ? world.KindAt(p) : WorldGen.Land.빈들판;

        // ★★★**집에서 멀수록 사납다** (2026-08-06 — 발하임이 바이옴을 세계 중심에서의
        //   거리로 정하는 방식).
        //
        //   헌법 3번: *"성장해도 세상이 안 약해진다. 성장의 보상은 쉬워지는 게 아니라
        //   **저기까지 갈 수 있게 된다**"*. 그런데 지금까지 우리 세계는 **어디나 위험이
        //   똑같았다** — 「저기」가 없으면 그 헌법이 작동할 데가 없다.
        //   ☆세상이 약해지는 게 아니다. **세상은 그대로고 내가 더 멀리 갈 뿐이다.**
        float 멂 = Mathf.Clamp01((new Vector2(p.x - home.x, p.z - home.z).magnitude - 집안전반경)
                                  / Mathf.Max(1f, 사나워지는거리));
        멂 = Mathf.Pow(멂, 사나움쏠림);          // 가까운 쪽이 넓다 — 초반이 답답하지 않게

        // ★칸 종류가 후보를 정하고, **집에서의 거리**가 그 안에서 사나운 쪽으로 기울인다
        SpeciesDef s;
        float r = Random.value;
        switch (land)
        {
            case WorldGen.Land.물웅덩이: return null;

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
        if (b.size.y > 0.0001f) g.transform.localScale = Vector3.one * (s.키 / b.size.y);

        // 크기를 바꿨으니 다시 재서 발을 땅에 붙인다
        b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        g.transform.position += Vector3.up * (parent.position.y - b.min.y);
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
