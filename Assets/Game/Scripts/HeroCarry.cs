using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// 붙잡아 데려가기 — **생포는 아이템 줍기가 아니다.**
///
/// ★사용자 확정: *"팻의 생포는 아이템 칸에 넣는 게 아냐. 끌고 가든 묶어서 데려가든,
///   잘 다독여서 따라오게 하든."* → **"잡았다"가 끝이 아니라 "데려왔다"가 끝이다.**
///
/// ★크기가 방법을 정한다:
///   · 가벼운 놈(새끼) → **안고 간다.** 양손이 막혀 **무기를 못 쓴다**
///   · 중간 → **끌고 간다.** 느려지고, 짐승이 발을 뻗대며 버틴다
///   · 무거운 놈 → 아예 못 붙잡는다 (나중에 밧줄·다독임으로 열린다)
///
/// ★뛰면 놓친다. 그래서 돌아오는 길이 조마조마해진다.
[RequireComponent(typeof(Hero))]
public class HeroCarry : MonoBehaviour
{
    [Header("붙잡기")]
    [Tooltip("이 거리 안의 지친 놈을 붙잡는다 (m)")] public float 손닿는거리 = 2.2f;
    [Tooltip("이 무게까지는 안고 간다 (무기 못 씀)")] public float 안는무게 = 0.8f;
    [Tooltip("이 무게까지는 끌고 간다")] public float 끄는무게 = 3f;

    [Header("데려가기")]
    [Tooltip("안고 있을 때 이동 속도 배수")] [Range(0.2f, 1f)] public float 안았을때 = 0.7f;
    [Tooltip("끌고 있을 때 이동 속도 배수")] [Range(0.2f, 1f)] public float 끌때 = 0.55f;
    [Tooltip("끌 때 이 거리를 넘으면 놓친다 (m)")] public float 끊기는거리 = 3.5f;

    // ★★★**「집」이라는 보이지 않는 원을 없앴다** (2026-08-10 사용자 — *"집이라고 바운더리를
    //   가정하지는 않았으면 좋겠어"*).
    //
    //   전에는 맵 정중앙 반경 22m 안에 들어가면 저절로 묶였다. 그건 규칙이지 세상이 아니고,
    //   무엇보다 **보이지 않는 선**이었다.
    //   → 이제 **내가 F 를 눌러 그 자리에 맨다.** 어디든 맬 수 있다.
    //     ☆그런데도 다들 모닥불 옆에 매게 된다 — **불이 야생을 밀어내서 거기가 안전하기
    //       때문**이다(`모닥불.무서운불`). 캠프가 규칙이 아니라 **이득으로** 생긴다.

    [Header("먹이 (E)")]
    [Tooltip("이 거리 안에 먹이를 준다 (m)")] public float 먹이거리 = 3f;
    [Tooltip("구운 고기를 주면 신뢰가 이 배로 오른다")] [Range(1f, 3f)] public float 구운것배 = 1.7f;

    /// 지금 데려가는 중인 놈 (없으면 null)
    public Critter 데려가는것 { get; private set; }
    /// 안고 있나 (아니면 끌고 있다)
    public bool 안는중 { get; private set; }
    /// 방금 뭐라도 됐나 — 화면 표시용
    public string 알림 { get; private set; } = "";
    float 알림T;

    Hero hero;

    void Awake() { hero = GetComponent<Hero>(); }

    void Update()
    {
        float dt = Time.deltaTime;
        if (알림T > 0f) { 알림T -= dt; if (알림T <= 0f) 알림 = ""; }

        bool 눌림, 먹임;
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        눌림 = k != null && k.fKey.wasPressedThisFrame;
        먹임 = k != null && k.eKey.wasPressedThisFrame;
#else
        눌림 = Input.GetKeyDown(KeyCode.F);
        먹임 = Input.GetKeyDown(KeyCode.E);
#endif
        // 모닥불 앞에 서 있으면 F 는 그쪽 것이다 (재료 붓기·땔감 넣기)
        // ★채집(캐기·갈무리) 중에도 F 는 그쪽 것이다 — 나무를 패다가 옆의 짐승을 붙잡으면 안 된다
        if (모닥불.F먹음 || HeroAttack.F먹음) 눌림 = false;

        if (먹임) 먹이주기();

        if (데려가는것 == null)
        {
            if (눌림) 붙잡기();
            hero.MoveMul = Mathf.Max(hero.MoveMul, 1f);
            return;
        }

        // ── 데려가는 중
        if (데려가는것 == null || !데려가는것.Alive) { 놓침("놓쳤다"); return; }

        // ★F 를 다시 누르면 **그 자리에 맨다.** 여기가 끝이 아니라 여기서 시작이다 —
        //   먹이를 주며 신뢰를 채워야 비로소 내 것이 된다
        if (눌림) { 매어두기(); return; }

        // ★뛰면 놓친다
        if (hero.Running) { 놓침("뿌리치고 달아났다"); return; }

        hero.MoveMul = 안는중 ? 안았을때 : 끌때;

        if (안는중)
        {
            // 안고 있으면 몸 앞에 붙어 온다
            var at = transform.position + transform.forward * 0.55f + Vector3.up * 0.9f;
            데려가는것.transform.position = at;
            데려가는것.transform.rotation = transform.rotation;
        }
        else
        {
            // 끌면 뒤따라온다 — 가끔 버틴다
            var 뒤 = transform.position - transform.forward * 1.5f;
            데려가는것.끌림(뒤, dt, out bool 버팀);
            float d = Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(데려가는것.transform.position.x, 0f, 데려가는것.transform.position.z));
            if (d > 끊기는거리) { 놓침("줄이 풀렸다"); return; }
            if (버팀) hero.MoveMul = 0.12f;      // 버티는 동안은 거의 못 간다
        }
    }

    void 붙잡기()
    {
        Critter best = null; float bd = 손닿는거리 * 손닿는거리;
        foreach (var c in Critter.All)
        {
            if (c == null || !c.지침) continue;
            float d2 = (c.transform.position - transform.position).sqrMagnitude;
            if (d2 > bd) continue;
            bd = d2; best = c;
        }
        if (best == null) return;

        float w = best.종.무게;
        if (w > 끄는무게) { 띄움($"{best.종.이름} — 너무 무겁다"); return; }

        데려가는것 = best;
        안는중 = w <= 안는무게;
        best.잡힘 = true;

        // 안고 있으면 무기를 못 쓴다
        var atk = GetComponent<HeroAttack>();
        if (atk != null) atk.enabled = !안는중;
    }

    void 놓기()
    {
        if (데려가는것 != null)
        {
            데려가는것.잡힘 = false;
            데려가는것.transform.position = new Vector3(데려가는것.transform.position.x, 0f,
                                                        데려가는것.transform.position.z);
        }
        해제();
    }

    /// 뿌리치고 달아났다 — **안 묶인다.** 실패는 실패로 남아야 한다
    void 놓침(string 말)
    {
        띄움(말);
        놓기();
    }

    /// ★내가 고른 자리에 **매어둔다.** 어디든 맬 수 있다 — 보이지 않는 「집」은 없다.
    ///   ☆그래도 불 옆에 매게 된다. 야생이 불 안으로 못 들어오기 때문이다.
    void 매어두기()
    {
        var got = 데려가는것;
        놓기();
        if (got == null) return;
        got.묶임 = true;
        띄움($"{got.종.이름}  묶었다");
    }

    /// ★먹이 주기 — 매인 놈에게도, 그 자리에 지쳐 쓰러진 놈에게도 준다.
    ///   현장에서 다독여 길들이는 길이 여기서 열린다 (끌고 오지 않아도 된다).
    void 먹이주기()
    {
        Critter best = null; float bd = 먹이거리 * 먹이거리;
        foreach (var c in Critter.All)
        {
            if (c == null || !c.Alive) continue;
            if (!먹일수있나(c)) continue;
            float d2 = (c.transform.position - transform.position).sqrMagnitude;
            if (d2 > bd) continue;
            bd = d2; best = c;
        }
        if (best == null) return;

        // ★★구운 고기를 먼저 준다 — **더 잘 길든다.**
        //   "오늘 잡아온 고기를 내가 먹나, 펫을 주나" 에 "구워서 주나" 가 한 겹 더 얹힌다 (5-5).
        bool 구움 = Stock.Take(Stock.Kind.구운고기, 1);
        if (!구움 && !Stock.Take(Stock.Kind.고기, 1)) { 띄움("고기가 없다"); return; }

        best.먹이받음(구움 ? 구운것배 : 1f);

        // ★내 펫은 이미 내 것이다 — 먹이는 건 「기르는」 일이지 「길들이는」 일이 아니다
        if (best.side == Critter.Side.내편)
        {
            띄움(best.새끼 ? $"{best.종.이름}  {Mathf.RoundToInt(best.자람 * 100f)}%"
                           : best.종.이름);
            return;
        }

        if (best.신뢰 >= 100f)
        {
            best.묶임 = false;
            best.길들여짐(transform);
            띄움($"{best.종.이름}");
        }
        else 띄움($"{best.종.이름}  {Mathf.RoundToInt(best.신뢰)}");
    }

    /// ★누구에게 먹일 수 있나
    ///   · 야생 — **묶였거나 지친** 놈만. 멀쩡한 놈은 안 받아먹는다
    ///   · 내 펫 — **아직 크는 중이거나 다친** 놈. 길들인 뒤에도 먹여야 큰다 (기획 5-2 2층)
    bool 먹일수있나(Critter c)
    {
        if (c.side == Critter.Side.야생) return c.묶임 || c.지침;
        return c.새끼 || c.hp < c.종.체력 * 0.99f;
    }

    /// 가까이 있는 길들이는 중인 놈 — 화면에 신뢰를 띄우려고 찾는다
    public Critter 가까운대상()
    {
        Critter best = null; float bd = 먹이거리 * 먹이거리 * 2.2f;
        foreach (var c in Critter.All)
        {
            if (c == null || !c.Alive) continue;
            if (!먹일수있나(c)) continue;
            float d2 = (c.transform.position - transform.position).sqrMagnitude;
            if (d2 > bd) continue;
            bd = d2; best = c;
        }
        return best;
    }

    void 해제()
    {
        if (데려가는것 != null) 데려가는것.잡힘 = false;
        데려가는것 = null;
        안는중 = false;
        hero.MoveMul = 1f;
        var atk = GetComponent<HeroAttack>();
        if (atk != null) atk.enabled = true;
    }

    void 띄움(string s) { 알림 = s; 알림T = 2.5f; }
}
