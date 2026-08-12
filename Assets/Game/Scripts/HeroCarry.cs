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
    [Tooltip("이 무게까지는 끌고 간다 (맨손)")] public float 끄는무게 = 3f;
    [Tooltip("★밧줄이 있으면 끄는 한계가 이 배가 된다 — 묶어서 끈다")]
    [Range(1f, 4f)] public float 밧줄배 = 2.2f;
    [Tooltip("★끌차가 있으면 이 배 — 싣고 끈다. 생포의 마지막 칸")]
    [Range(1f, 8f)] public float 끌차배 = 4f;
    [Tooltip("끌차를 끌 때의 이동 속도 배수 (밧줄보다 느리다)")] [Range(0.2f, 1f)] public float 끌차때 = 0.4f;
    bool 밧줄로, 끌차로;
    Transform 수레;

    // ★★★★**끌기는 「자세 한 벌」이 아니라 순서가 있는 동작이다** (2026-08-12 사용자
    //   "각 단계별로 동작을 계산해서 순서대로 넣어야지").
    //   ☆여태는 각도 한 벌을 통째로 켰다. 그러니 **무기를 든 채** 두 팔이 대칭으로 꺾였고,
    //     몸도 안 돌아 뒤로걷기가 안 나왔다. 순서가 없으니 「집는 동작」도 아니었다.
    [Header("★끌 때 단계 (초) — 순서가 있다")]
    [Tooltip("③ 두 손이 줄을 쥐기 시작하는 시각")] [Range(0f, 0.6f)] public float 쥠시작 = 0.18f;
    [Tooltip("③ 쥐는 데 걸리는 시간")] [Range(0.1f, 1f)] public float 쥠시간 = 0.38f;
    [Tooltip("④ 몸을 젖히기 시작하는 시각 — **쥔 뒤여야 한다**")] [Range(0f, 1f)] public float 젖힘시작 = 0.42f;
    [Tooltip("④ 젖히는 데 걸리는 시간")] [Range(0.1f, 1f)] public float 젖힘시간 = 0.4f;
    float 끌기t;

    [Header("★줄 그리기")]
    // ★★맨손 조항은 **스위치로 남긴다** (2026-08-12 사용자 "밧줄이 아예 안보이고").
    //   맨손은 목덜미를 잡는 것이라 줄이 없는 게 기획엔 맞다. 그런데 이걸 켜 두니
    //   **밧줄을 안 들고 있으면 줄도 고리도 통째로 안 보여서** 만든 걸 확인할 수가 없었다.
    //   → 기본은 꺼 둔다 (맨손이어도 줄이 보인다). 은퇴는 삭제가 아니라 스위치다 (9-3).
    [Tooltip("켜면 밧줄·끌차가 있어야 줄이 보인다 (맨손은 목덜미를 잡는 것)")]
    public bool 맨손엔줄없음 = false;
    [Tooltip("줄이 늘어지는 정도 — 가까울수록 많이 처진다 (거리에 대한 비율)")]
    [Range(0f, 0.5f)] public float 줄처짐 = 0.22f;
    [Tooltip("줄 굵기 (m)")] [Range(0.02f, 0.12f)] public float 줄굵기 = 0.05f;
    // ★★한 번 만들어 껐다 켠다 (9-4) — 매 프레임 하는 일은 자리·각도 얹기뿐이다.
    //   토막 6 + 고리판 8 + 수레 1 = 15개, 그것도 **한 마리 끌 때만** 켜진다
    const int 토막수 = 6;
    const int 고리수 = 2;              // 고리 하나가 판 4개(위·아래·좌·우)로 몸을 두른다
    Transform[] 줄토막, 고리판;

    [Header("데려가기")]
    [Header("★집어들기")]
    [Tooltip("숙여서 집어드는 데 걸리는 시간 (초)")] [Range(0.1f, 1.5f)] public float 드는데 = 0.55f;
    [Tooltip("등에 맨 높이 — 사람 키에 대한 비율")] [Range(0.3f, 1.2f)] public float 등높이 = 0.72f;
    [Tooltip("걸을 때 등에서 흔들리는 각 (°)")] [Range(0f, 20f)] public float 흔들각 = 7f;
    float 줍기t; Vector3 잡은자리; HeroAttack 손; HeroHold 팔;

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
            // ★★★**집어들고 등에 맨다** (2026-08-12 사용자 "숙여서 집어들고 머리위 or
            //   등뒤로 매달아두는거지"). 전에는 몸 앞 90cm 에 **그냥 떠 있었다** —
            //   드는 동작도, 붙어 있는 자리도 없이 공중에 매달린 그림이었다.
            //   ☆①숙인다 ②새끼가 바닥에서 등으로 올라온다 ③등에 매달려 흔들린다
            //   ☆드는 동안은 느리다 — 두 손이 차 있으니 당연하다
            줍기t = Mathf.Min(줍기t + dt, 드는데);
            float 듦 = 드는데 > 0.01f ? Mathf.SmoothStep(0f, 1f, 줍기t / 드는데) : 1f;

            // 몸 자세는 `HeroAttack` 이 쥐고 있다 — 얼마나 숙일지만 넘긴다
            if (손 == null) 손 = GetComponent<HeroAttack>();
            if (팔 == null) 팔 = GetComponent<HeroHold>();
            // ★★온몸이 같이 움직인다 — 다리·골반·척추는 `HeroAttack.줍기굽힘` 이,
            //   어깨·팔·팔뚝·머리는 `HeroHold.줍기` 가 맡는다 (각자 제 뼈만 만진다).
            //   ☆숙임은 처음에 깊고 펴지는데, 팔은 **끝까지 감싸 안은 채**로 남는다 —
            //     다 들고 나서도 새끼를 붙잡고 있어야 하니까
            if (손 != null) 손.줍기굽힘 = 1f - 듦;          // 처음에 깊게 숙였다 펴진다
            if (팔 != null) 팔.줍기 = Mathf.Max(0.55f, 1f - 듦 * 0.45f);

            if (듦 < 1f) hero.MoveMul = 0.25f;              // 드는 동안은 거의 못 간다

            // 바닥(집은 자리) → 등 뒤 위쪽으로 올라온다
            var 바닥 = 잡은자리;
            var 등 = transform.position - transform.forward * 0.32f
                     + Vector3.up * (hero.height * 등높이)
                     + transform.right * 0.12f;            // 살짝 한쪽으로 — 정중앙이면 가려진다
            데려가는것.transform.position = Vector3.Lerp(바닥, 등, 듦);

            // 등에 매달려 걸음마다 흔들린다 — 매달린 것은 흔들려야 매달린 것으로 읽힌다
            float 흔 = 듦 * Mathf.Sin(Time.time * 6.5f) * 흔들각;
            데려가는것.transform.rotation = transform.rotation
                * Quaternion.Euler(28f * 듦 + 흔 * 0.4f, 흔, -14f * 듦 + 흔 * 0.6f);
        }
        else
        {
            // ★★★**끌차에 실으면 수레가 따라오고 그 위에 얹힌다** (2026-08-12).
            //   밧줄은 짐승이 제 발로 끌려오지만, 끌차는 **수레가 짐승을 태우고** 온다 —
            //   그래서 버티지도 않고 더 무거운 놈도 간다. 대신 느리다.
            if (끌차로) hero.MoveMul = 끌차때;

            // ★★★**내 자리를 넘긴다 — 내 「경로」가 아니다** (2026-08-12 사용자 "내 경로가
            //   아니라 내 쪽으로 끌려오게해야할듯하고?").
            //   ☆옛 코드는 「사람 뒤 ○m」라는 **목표점**을 만들어 넘겼다. 그러면 짐승이
            //     내가 지나온 길을 밟는다 — 끌리는 게 아니라 뒤를 따라 걷는 것이다.
            //     서 있을 자리를 게임이 정해 주는 셈이라, 몸을 돌리기만 해도 목표가 튀었다.
            //   ☆이제 그냥 **내 자리**를 넘긴다. 뒤에 설 자리를 정해 줄 필요가 없다 —
            //     **줄 길이가 정한다** (`Critter.끌림`). 줄이 팽팽한 만큼만 이쪽으로 딸려온다.
            데려가는것.끌림(transform.position, dt, out bool 버팀);

            // ── ★네 단계가 순서대로 온다
            끌기t += dt;
            무기등에(true);                                     // ① 두 손을 비운다
            짐승쪽보기();                                       // ② 몸이 돌아간다 → 뒤로걷기가 나온다
            if (팔 == null) 팔 = GetComponent<HeroHold>();
            if (팔 != null)
            {
                팔.끌기 = 단계(끌기t, 쥠시작, 쥠시간);           // ③ 두 손이 줄을 쥔다
                팔.끌기젖힘 = 단계(끌기t, 젖힘시작, 젖힘시간)    // ④ 쥔 뒤에 무게를 싣는다
                            * (버팀 ? 1f : 0.78f);              //    버티는 순간 더 깊이 젖힌다
            }

            줄그리기();
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

        // ★★★★**무거우면 도구가 정한다** (2026-08-12 사용자 "커다란 팻의경우 밧줄이나
        //   끌차 같은 도구가 없으면 그자리에서 테이밍이 가능하게 … 밧줄이 있으면 묶어서
        //   끌고가거나").
        //   ☆그 자리 길들이기는 **이미 된다** — 기절해 쓰러진 놈에게 먹이를 주면 신뢰가
        //     차고 100 이면 내 것이 된다(`먹이주기`). 빠져 있던 건 두 가지였다:
        //     ①밧줄이 있어도 무거운 놈을 못 끌었다 ②거절 메시지가 **대안을 안 알려줬다**.
        //   ★밧줄은 「크기가 방법을 정한다」(5-2)의 그 자리다 — 맨손으로 끄는 한계를 넘긴다.
        float w = best.종.무게;
        bool 밧줄있나 = 인벤.어느통에든도구("밧줄") != null || 인벤.다합쳐개수("밧줄") > 0;
        bool 끌차있나 = 인벤.어느통에든도구("끌기") != null || 인벤.다합쳐개수("끌차") > 0;
        // ★도구가 좋을수록 더 무거운 놈을 데려간다 — 맨손 < 밧줄 < 끌차
        float 끌한계 = 끌차있나 ? 끄는무게 * 끌차배
                     : 밧줄있나 ? 끄는무게 * 밧줄배 : 끄는무게;
        if (w > 끌한계)
        {
            // ☆못 끄는 이유만 말하지 않는다 — **무엇을 할 수 있는지**를 같이 말한다
            띄움(끌차있나 ? $"{best.종.이름} — 끌차로도 못 끈다. 먹여서 길들여라"
                : 밧줄있나 ? $"{best.종.이름} — 밧줄로도 못 끈다. 끌차가 있거나, 먹여서 길들여라"
                           : $"{best.종.이름} — 너무 무겁다. 밧줄·끌차가 있거나, 먹여서 길들여라");
            return;
        }
        // 무엇으로 데려가나 — 안기 < 밧줄 < 끌차
        끌차로 = 끌차있나 && w > 끄는무게 * 밧줄배 * 0.6f;   // 무거운 놈은 실어야 한다
        밧줄로 = !끌차로 && 밧줄있나 && w > 안는무게;

        데려가는것 = best;
        안는중 = w <= 안는무게;
        best.잡힘 = true;
        // ★집어드는 동작의 시작점 — **누워 있던 그 자리**에서 올라와야 「집었다」로 읽힌다
        줍기t = 0f;
        잡은자리 = best.transform.position;
        // ★붙잡았으면 동글뱅이를 끈다 — 이제 「기절해 누워 있는 놈」이 아니라 「내 손 안」이다
        var 표 = best.GetComponent<기절표시>();
        if (표 != null) 표.켜기(false);

        // ★줄로 데려간다면 여기서 **몸에 감는다** (한 번만 재서 몸에 붙인다)
        if (!안는중) { 고리붙이기(best); 끌기t = 0f; }    // 단계는 언제나 ①부터 다시 시작한다

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

    // ★★★**줄과 수레가 실제로 보인다** (2026-08-12 사용자 D·E).
    //   전에는 짐승이 그냥 뒤따라왔다 — **무엇으로 끌고 있는지**가 화면에 없으면
    //   밧줄을 만든 보람이 없고, 끌차는 아예 있는지도 모른다.
    //   ☆규칙 12: 지우면 무슨 일이 일어나는지 못 알아보는 것 — 그러니 넣는다.
    //   ☆9-4: 만들고 부수지 않는다. 한 번 만들어 껐다 켠다. 매 프레임 하는 일은
    //     길이·각도 얹기뿐이다 (상자 하나를 늘렸다 줄인다).
    void 줄그리기()
    {
        if (데려가는것 == null) return;

        if (맨손엔줄없음 && !밧줄로 && !끌차로) { 줄치우기(); return; }

        // 고리는 몸에 **붙어 있다** — 자식이라 몸이 눌리든 돌든 저절로 따라간다
        if (고리판 != null) foreach (var p in 고리판) if (p != null) p.gameObject.SetActive(true);

        // 줄은 사람의 두 손 → **앞 고리**로 간다. 고리가 곧 매듭이다
        var 매듭 = (고리판 != null && 고리판[0] != null)
                 ? 고리판[0].position
                 : 데려가는것.transform.position + Vector3.up * (데려가는것.종.반지름 * 0.6f);
        var 손 = transform.position + transform.forward * 0.34f + Vector3.up * (hero.height * 0.55f);
        줄토막그리기(손, 매듭, 데려가는것.줄길이);

        수레그리기();
    }

    /// ★①몸에 감긴 고리 — **붙잡을 때 한 번** 몸 크기를 재서 **몸의 자식으로** 붙인다.
    ///
    ///   ☆옛 방식은 매 프레임 `짐승 뿌리 + 반지름 어림값` 자리에 그렸다. 그런데 진짜 모델은
    ///     기준점이 발밑이고 메시 크기도 종마다 제각각이라, 고리가 몸을 벗어나 **공중에 떴다**
    ///     (2026-08-12 사용자 "몸에 감긴 밧줄도 몸이 아니라 공중에 떠서 묶여있어").
    ///   ☆자식으로 붙이면 자리·각도는 물론 **기절해 눌린 것까지** 저절로 따라간다.
    ///     같이 눌리는 게 맞다 — 몸에 감긴 줄이니까.
    ///   ☆링 메시가 없으니 **판 넷(위·아래·좌·우)으로 사각 고리**를 만든다. 각진 게 오히려
    ///     장난감답다 (6장 — 색은 크게 나눈 면으로만, 경계는 뚜렷하게).
    ///   ☆`GetComponentsInChildren` 은 **붙잡는 순간 한 번뿐**이다 (9-4)
    void 고리붙이기(Critter c)
    {
        var 몸t = c.몸;
        if (몸t == null) return;
        if (고리판 == null) 고리판 = new Transform[고리수 * 4];

        // 몸의 실제 크기를 잰다 — 어림값이 아니라 **메시가 차지하는 자리**를 쓴다
        var rs = 몸t.GetComponentsInChildren<Renderer>();
        var 스 = 몸t.lossyScale;
        Vector3 중심L, 반L;
        if (rs.Length > 0)
        {
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            중심L = 몸t.InverseTransformPoint(b.center);
            반L = new Vector3(b.extents.x / Mathf.Max(1e-4f, 스.x),
                              b.extents.y / Mathf.Max(1e-4f, 스.y),
                              b.extents.z / Mathf.Max(1e-4f, 스.z));
        }
        else { 중심L = Vector3.zero; 반L = Vector3.one * Mathf.Max(0.12f, c.종.반지름); }

        float 평균 = (스.x + 스.y + 스.z) / 3f;
        float t = 줄굵기 / Mathf.Max(1e-4f, 평균);          // 부모 크기를 상쇄해 굵기를 맞춘다
        float rx = 반L.x * 1.08f, ry = 반L.y * 1.14f;       // 몸보다 아주 조금 크게 — 살을 파고들지 않게

        for (int i = 0; i < 고리수; i++)
        {
            var c0 = new Vector3(중심L.x, 중심L.y, 중심L.z + 반L.z * (i == 0 ? 0.45f : -0.4f));  // 앞뒤로 하나씩
            for (int j = 0; j < 4; j++)
            {
                int k = i * 4 + j;
                if (고리판[k] == null) 고리판[k] = 새줄상자("고리");
                var p = 고리판[k];
                p.SetParent(몸t, false);
                p.localRotation = Quaternion.identity;
                switch (j)
                {
                    case 0: p.localPosition = c0 + Vector3.up * ry;    p.localScale = new Vector3(rx * 2f + t, t, t); break;
                    case 1: p.localPosition = c0 - Vector3.up * ry;    p.localScale = new Vector3(rx * 2f + t, t, t); break;
                    case 2: p.localPosition = c0 - Vector3.right * rx; p.localScale = new Vector3(t, ry * 2f, t); break;
                    default:p.localPosition = c0 + Vector3.right * rx; p.localScale = new Vector3(t, ry * 2f, t); break;
                }
                p.gameObject.SetActive(true);
            }
        }
    }

    /// ★②줄이 처진다 — 토막 여럿을 포물선으로 늘어뜨린다.
    ///   ☆★**그림이 실제 규칙과 같은 자를 쓴다**: 처짐이 `줄길이` 를 기준으로 정해지므로,
    ///     **줄이 팽팽해 보이는 순간이 곧 짐승이 딸려오기 시작하는 순간**이다
    ///     (`Critter.끌림` 이 같은 값으로 끈다). 화면이 규칙을 그대로 비춘다.
    void 줄토막그리기(Vector3 a, Vector3 b, float 줄길이)
    {
        if (줄토막 == null) 줄토막 = new Transform[토막수];
        var v = b - a;
        float 길이 = v.magnitude;
        float 팽 = Mathf.Clamp01(길이 / Mathf.Max(0.1f, 줄길이));
        float 처짐 = (1f - 팽) * 줄처짐 * 길이;

        var 이전 = a;
        for (int i = 0; i < 토막수; i++)
        {
            float t1 = (i + 1f) / 토막수;
            var 다음 = Vector3.Lerp(a, b, t1) - Vector3.up * (처짐 * 4f * t1 * (1f - t1));
            if (줄토막[i] == null) 줄토막[i] = 새줄상자("끌줄");
            var s = 줄토막[i];
            s.gameObject.SetActive(true);
            var d = 다음 - 이전;
            s.position = (이전 + 다음) * 0.5f;
            s.rotation = d.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(d) : Quaternion.identity;
            s.localScale = new Vector3(줄굵기, 줄굵기, Mathf.Max(0.02f, d.magnitude));
            이전 = 다음;
        }
    }

    /// 끌차 — 사람과 짐승 사이에 수레가 놓인다. 짐승은 그 위에 얹혀 온다
    void 수레그리기()
    {
        if (!끌차로) { if (수레 != null) 수레.gameObject.SetActive(false); return; }

        if (수레 == null)
        {
            var g = Grey.Box(null, Vector3.zero, new Vector3(1.3f, 0.22f, 1.9f),
                             new Color(0.46f, 0.33f, 0.20f), "끌차");
            g.GetComponent<MeshRenderer>().sharedMaterial =
                Grey.격자Mat(new Color(0.46f, 0.33f, 0.20f));   // 표면은 격자다 (11-1)
            g.AddComponent<NoOutline>();
            수레 = g.transform;
        }
        수레.gameObject.SetActive(true);
        수레.position = 데려가는것.transform.position + Vector3.up * 0.22f;
        수레.rotation = 데려가는것.transform.rotation;   // ★사람이 아니라 **짐승**을 따른다 (시선이 자유로우니)
        데려가는것.transform.position += Vector3.up * 0.34f;
    }

    /// 순서가 있는 동작 — 제 차례가 와야 오르기 시작하고, 양 끝이 완만하다
    static float 단계(float t, float 시작, float 길이)
        => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 시작) / Mathf.Max(0.01f, 길이)));

    /// ★② **줄을 쥐고 있으면 늘 짐승 쪽을 본다** (2026-08-12 사용자 "줄을 잡고있으면 항상
    ///   짐승쪽을 보고있어야지").
    ///   ☆`Hero` 에 이미 있는 `시선고정` 을 그대로 쓴다 — 채집이 쓰는 것과 같은 자리다.
    ///     새 축을 만들지 않는다.
    ///   ☆★몸이 짐승을 보면 **걷기 블렌드가 저절로 뒤로걷기가 된다** (`HeroAnim` 은 네 방향
    ///     클립을 **시선 기준**으로 섞는다). 뒷걸음질 클립을 따로 만들 필요가 없었다 —
    ///     여태 안 나온 건 몸이 짐승을 안 보고 있어서였다.
    void 짐승쪽보기()
    {
        if (데려가는것 == null) return;
        var 향 = 데려가는것.transform.position - transform.position; 향.y = 0f;
        if (향.sqrMagnitude < 0.01f) return;
        hero.시선고정 = true;
        hero.고정시선 = 향.normalized;
    }

    /// ★① **두 손이 비어야 줄을 잡는다** — 무기를 등으로 넘긴다.
    ///   ☆처음엔 `장비붙이기` 의 뼈 이름을 바꾸는 방식으로 짰는데, **씬에 그 컴포넌트가
    ///     아예 없었다** (2026-08-12 실측). 무기는 `HeroAttack` 이 제 손으로 붙이고 있다.
    ///     조용히 아무 일도 안 하고 있었던 것이다 — 9-2 의 *"부르는 데가 있나"* 그대로다.
    ///   ☆그래서 무기를 쥔 쪽(`HeroAttack`)에 스위치 하나만 켠다. 자리는 거기 인스펙터에서
    ///     눈으로 맞춘다 (`등짐자리`·`등짐기울임`)
    void 무기등에(bool 켬)
    {
        if (손 == null) 손 = GetComponent<HeroAttack>();
        if (손 != null) 손.등에멨나 = 켬;
    }

    Transform 새줄상자(string 이름)
    {
        var g = Grey.Box(null, Vector3.zero, Vector3.one, new Color(0.78f, 0.70f, 0.48f), 이름);
        g.AddComponent<NoOutline>();
        return g.transform;
    }

    void 줄치우기()
    {
        if (줄토막 != null) foreach (var s in 줄토막) if (s != null) s.gameObject.SetActive(false);
        // ★고리는 **떼어낸다** — 짐승 몸의 자식으로 남겨 두면 그 짐승이 사라질 때 같이 없어진다
        if (고리판 != null)
            foreach (var p in 고리판)
                if (p != null) { p.SetParent(null, true); p.gameObject.SetActive(false); }
        if (수레 != null) 수레.gameObject.SetActive(false);
    }

    void 해제()
    {
        줄치우기();
        // ★단계를 **거꾸로 되돌린다** — 무기가 손으로 돌아오고, 시선이 마우스로 풀린다
        무기등에(false);
        if (hero != null) hero.시선고정 = false;
        끌기t = 0f;
        밧줄로 = false; 끌차로 = false;
        if (데려가는것 != null) 데려가는것.잡힘 = false;
        데려가는것 = null;
        안는중 = false;
        줍기t = 0f;
        hero.MoveMul = 1f;
        var atk = GetComponent<HeroAttack>();
        if (atk != null) { atk.enabled = true; atk.줍기굽힘 = 0f; }   // ★숙임이 남으면 계속 굽은 채 걷는다
        var 팔c = GetComponent<HeroHold>();
        if (팔c != null) { 팔c.줍기 = 0f; 팔c.끌기 = 0f; 팔c.끌기젖힘 = 0f; }   // 팔도 풀어 준다
    }

    void 띄움(string s) { 알림 = s; 알림T = 2.5f; }
}
