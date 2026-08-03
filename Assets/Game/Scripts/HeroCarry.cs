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

    [Header("집")]
    [Tooltip("여기 안에 들여놓으면 목줄을 맨다 (m)")] public float 집반경 = 22f;

    [Header("먹이 (E)")]
    [Tooltip("이 거리 안에 먹이를 준다 (m)")] public float 먹이거리 = 3f;

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
        if (먹임) 먹이주기();

        if (데려가는것 == null)
        {
            if (눌림) 붙잡기();
            hero.MoveMul = Mathf.Max(hero.MoveMul, 1f);
            return;
        }

        // ── 데려가는 중
        if (데려가는것 == null || !데려가는것.Alive) { 놓침("놓쳤다"); return; }

        if (눌림) { 놓기(); return; }

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

        // ── 집에 도착했나
        var c = WorldGrid.Center;
        float 집까지 = Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z),
                                        new Vector3(c.x, 0f, c.z));
        if (집까지 <= 집반경) 도착();
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

    void 놓침(string 말)
    {
        띄움(말);
        놓기();
    }

    /// ★캠프에 닿으면 **매어둔다.** 여기서 끝이 아니라 여기서 시작이다 —
    ///   먹이를 주며 신뢰를 채워야 비로소 내 것이 된다
    void 도착()
    {
        var got = 데려가는것;
        해제();
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
            if (c == null || !c.Alive || c.side != Critter.Side.야생) continue;
            if (!c.묶임 && !c.지침) continue;               // 멀쩡한 놈은 안 받아먹는다
            float d2 = (c.transform.position - transform.position).sqrMagnitude;
            if (d2 > bd) continue;
            bd = d2; best = c;
        }
        if (best == null) return;

        if (!Stock.Take(Stock.Kind.고기, 1)) { 띄움("고기가 없다"); return; }

        best.먹이받음();
        if (best.신뢰 >= 100f)
        {
            best.묶임 = false;
            best.길들여짐(transform);
            띄움($"{best.종.이름}");
        }
        else 띄움($"{best.종.이름}  {Mathf.RoundToInt(best.신뢰)}");
    }

    /// 가까이 있는 길들이는 중인 놈 — 화면에 신뢰를 띄우려고 찾는다
    public Critter 가까운대상()
    {
        Critter best = null; float bd = 먹이거리 * 먹이거리 * 2.2f;
        foreach (var c in Critter.All)
        {
            if (c == null || !c.Alive || c.side != Critter.Side.야생) continue;
            if (!c.묶임 && !c.지침) continue;
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
