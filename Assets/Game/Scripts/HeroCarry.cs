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
    // ★★**팔이 닿는 만큼만 잡는다** (2026-08-13 사용자 "팻에게 가서, 적당한 위치에서 드는
    //   모션이 아니라 그냥 제자리에서 모션이 나오고").
    //   ☆2.2m 는 **두 걸음 거리**다. 그만큼 떨어져서도 잡히니 「멀리서 허공에 팔만 뻗는」
    //     그림이 됐다. 숙여서 손이 실제로 닿는 거리는 그보다 훨씬 짧다.
    //   ☆줄이면 **가까이 가야 잡힌다** — 사람을 저절로 움직이게 하지 않는다 (그건 행동이라
    //     따로 여쭈어야 한다). 규칙으로 막지 않고 거리로 정해지게 한다.
    //   ☆★1.25m 로 줄였더니 **아예 못 잡았다** (2026-08-13 사용자 "F키도 안먹혀").
    //     사슴 몸 반지름이 0.5m 라 몸이 겹칠 만큼 붙어야 닿는 거리였다. 몸 반지름을 더해
    //     **짐승 크기에 맞게** 잰다 — 큰 놈은 멀리서도, 작은 놈은 가까이 가야 잡힌다.
    [Tooltip("이 거리 안의 지친 놈을 붙잡는다 (m) — 여기에 짐승 몸 반지름이 더해진다")]
    public float 손닿는거리 = 1.5f;
    // ★★★**큰 놈은 못 끌고 간다 — 새끼만 데려온다** (2026-08-13 사용자 "그 사슴같은 큰것들은
    //   끌고가지 못하게하자 새끼는 들고튈수있어도").
    //   ☆정본에 이미 그렇게 적혀 있었다 — 사슴 정의가 *"무거워서 성체는 못 끌고 온다 →
    //     새끼를 노린다"* 다. 그런데 사슴 무게 3.0 에 맨손 한계가 3 이라 **간신히 끌렸다.**
    //   ☆이제 무게로 갈린다: 늑대(1.3)는 맨손, 사슴 성체(3.0)는 **끌차라야** 온다.
    //     새끼는 무게가 4분의 1 이라(`새끼로()`) 안고 튈 수 있다.
    [Tooltip("이 무게까지는 안고 간다 (무기 못 씀)")] public float 안는무게 = 0.8f;
    [Tooltip("이 무게까지는 끌고 간다 (맨손) — 늑대까지")] public float 끄는무게 = 1.6f;
    [Tooltip("★밧줄이 있으면 끄는 한계가 이 배 — 묶어서 끈다. 사슴 성체는 아직 안 된다")]
    [Range(1f, 4f)] public float 밧줄배 = 1.8f;
    [Tooltip("★끌차가 있으면 이 배 — 싣고 끈다. 사슴 성체가 여기서 열린다")]
    [Range(1f, 8f)] public float 끌차배 = 5f;
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
    [Tooltip("고리 크기 — 몸통 반지름에 곱한다. 살을 파고들면 키운다")]
    [Range(0.5f, 2.5f)] public float 고리크기 = 1.15f;
    [Tooltip("두 고리를 몸통 앞뒤로 얼마나 벌리나 (반지름 배수)")]
    [Range(0f, 2f)] public float 고리앞뒤 = 0.8f;
    // ★★한 번 만들어 껐다 켠다 (9-4) — 매 프레임 하는 일은 자리·각도 얹기뿐이다.
    //   토막 6 + 고리판 8 + 수레 1 = 15개, 그것도 **한 마리 끌 때만** 켜진다
    const int 토막수 = 6;
    // ★★★**고리는 뼈에 감고, 크기는 몸통 반지름으로 잰다** (2026-08-13 사용자 "밧줄이 몸통에
    //   정활히 둘러지지 않던데? 네모낳게 생기고 범위도 엄청 넓고 붕떠있더라고").
    //   ☆옛 방식은 **렌더러 bounds** 로 크기를 쟀다. 그런데 그건 사슴의 **뿔·다리·꼬리까지**
    //     다 감싼다 — 실측으로 다람쥐 bounds 가 3.2m 였고 그 대부분이 꼬리였다.
    //     그래서 고리가 몸통이 아니라 **몸 전체를 두르는 크기**가 됐다.
    //   ☆→ 자리는 **뼈**(`spine_back` 등), 크기는 **`종.반지름`**. 둘 다 몸통의 자다.
    //   ☆판 4개(사각)를 **8개(팔각)** 로 늘려 둥글게 보이게 한다 — 링 메시가 없으니
    //     면을 늘리는 것이 유일한 길이고, 8면이면 밧줄로 읽힌다.
    const int 고리수 = 2;              // 고리 하나가 판 8개로 몸을 두른다
    const int 판수 = 8;
    Transform[] 줄토막, 고리판;

    [Header("데려가기")]
    // ★★★★**집어들기는 네 순간이다** (2026-08-13 사용자 "자세한 동작들 한순간씩 팔다리 허벅지
    //   정강이 손머리 목 등등 지정한 뒤 행동을 만들어야할듯해 / 몸을 숙여 손을 뻗고, 들어서
    //   머리위로 올리고 들고 그상태로 또 걷기되고").
    //   ☆옛 코드는 **한 값(`듦`)으로 전부**를 했다 — 숙임·팔·짐승 자리가 같은 곡선을 탔다.
    //     그러니 「숙였다 든다」가 아니라 「숙인 채 짐승이 등으로 미끄러진다」가 됐다.
    //   ☆네 순간을 따로 센다 (발차기의 `킥표` 와 같은 방식):
    //       ①숙임  — 무릎·허리를 굽히고 두 팔을 앞아래로 뻗는다. 짐승은 **바닥에 그대로**
    //       ②잡음  — 짐승이 손에 붙는다
    //       ③올림  — 몸을 펴며 두 팔로 **머리 위로** 올린다
    //       ④유지  — 머리 위에 얹은 채 걷는다
    [Header("★집어들기 — 네 순간 (초)")]
    [Tooltip("① 숙여서 두 손을 뻗는 데")] [Range(0.1f, 1f)] public float 숙임시간 = 0.34f;
    [Tooltip("② 잡아서 손에 붙는 데")] [Range(0.05f, 0.6f)] public float 잡음시간 = 0.14f;
    [Tooltip("③ 머리 위로 올리는 데")] [Range(0.1f, 1.2f)] public float 올림시간 = 0.42f;
    [Tooltip("④ 머리 위 높이 — 사람 키에 대한 비율")] [Range(0.7f, 1.4f)] public float 머리위높이 = 1.04f;
    [Tooltip("숙였을 때 손이 닿는 앞거리 (m)")] [Range(0.2f, 1f)] public float 손닿는앞 = 0.52f;
    [Tooltip("걸을 때 머리 위에서 흔들리는 각 (°) — ★기절한 놈은 축 늘어져 있다")]
    [Range(0f, 12f)] public float 흔들각 = 2f;
    float 줍기t; Vector3 잡은자리; HeroAttack 손; HeroHold 팔;

    [Tooltip("안고 있을 때 이동 속도 배수")] [Range(0.2f, 1f)] public float 안았을때 = 0.7f;
    [Tooltip("끌고 있을 때 이동 속도 배수")] [Range(0.2f, 1f)] public float 끌때 = 0.55f;
    [Tooltip("끌 때 이 거리를 넘으면 놓친다 (m) — ★`Critter.줄길이` 보다 넉넉해야 한다")]
    public float 끊기는거리 = 6.2f;

    // ★★★**「집」이라는 보이지 않는 원을 없앴다** (2026-08-10 사용자 — *"집이라고 바운더리를
    //   가정하지는 않았으면 좋겠어"*).
    //
    //   전에는 맵 정중앙 반경 22m 안에 들어가면 저절로 묶였다. 그건 규칙이지 세상이 아니고,
    //   무엇보다 **보이지 않는 선**이었다.
    //   → 이제 **내가 F 를 눌러 그 자리에 맨다.** 어디든 맬 수 있다.
    //     ☆그런데도 다들 모닥불 옆에 매게 된다 — **불이 야생을 밀어내서 거기가 안전하기
    //       때문**이다(`모닥불.무서운불`). 캠프가 규칙이 아니라 **이득으로** 생긴다.

    [Header("먹이 (E) — ★은퇴")]
    // ★★**E 를 껐다** (2026-08-13 사용자 "E 는 왜들어가냐,, 그냥 탭에서 넣어주는걸로만
    //   가능하게해줘.. 그냥 넣어두면 알아서 먹게").
    //   ☆먹이는 길이 셋이나 되어 어느 게 맞는지 헷갈렸다. 이제 **Tab 에서 먹이통에 넣는
    //     하나**로 모았고, 먹는 것은 짐승이 알아서 한다 (`Critter.통에서먹기`).
    //   ☆지우지 않고 스위치로 끈다 (9-3). 아래 `먹이주기()` 도 그대로 남는다.
    [Tooltip("★은퇴 — 켜면 옛날처럼 E 로 즉시 먹인다. 지금은 Tab 의 먹이통이 그 일을 한다")]
    public bool 먹이키씀 = false;
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

        if (먹임 && 먹이키씀) 먹이주기();      // ★은퇴 — Tab 의 먹이통이 이 일을 한다

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
            // ── ★네 순간을 따로 센다 (위 헤더 주석 참고)
            줍기t += dt;
            float t1 = Mathf.Max(0.01f, 숙임시간);
            float t2 = t1 + Mathf.Max(0.01f, 잡음시간);
            float t3 = t2 + Mathf.Max(0.01f, 올림시간);

            float 숙 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(줍기t / t1));                  // ① 숙인다
            float 잡 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((줍기t - t1) / 잡음시간));      // ② 손에 붙는다
            float 올 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((줍기t - t2) / 올림시간));      // ③ 머리 위로

            if (손 == null) 손 = GetComponent<HeroAttack>();
            if (팔 == null) 팔 = GetComponent<HeroHold>();

            // ★★★**클립이 있으면 클립이 그린다** — 진행도만 넘긴다 (2026-08-13).
            //   ☆짐승 자리(바닥 → 손 → 머리 위)는 **아래 코드가 계속 쥔다.** 클립은 사람 뼈만
            //     그리므로, 둘이 어긋나지 않으려면 **같은 진행도**를 써야 한다.
            //   ☆클립이 비어 있으면 `HeroAttack` 이 절차 쪽으로 흘려보낸다
            if (손 != null) 손.들기진행 = Mathf.Clamp01(줍기t / Mathf.Max(0.01f, t3));

            // ★★★**클립이 그리면 절차 자세를 밀지 않는다** (2026-08-13 사용자 "손도 내가
            //   모션넣은거랑 다른 동작이 되는데?").
            //   ☆클립과 `HeroHold` 가 **같은 팔 뼈를 놓고 싸우고 있었다.** 애니메이션 창에서
            //     고친 자세가 절차 자세에 덮여 딴 동작으로 보였다.
            //   ☆클립이 있으면 진행도만 넘기고 팔·다리는 통째로 클립에 맡긴다.
            bool 클립이그린다 = 손 != null && 손.들기클립 != null;
            if (!클립이그린다)
            {
                // ★★온몸이 같이 움직인다 — 다리·골반·척추는 `HeroAttack.줍기굽힘` 이,
                //   어깨·팔·팔뚝·머리는 `HeroHold` 가 맡는다 (각자 제 뼈만 만진다).
                //   ☆숙임은 **올릴 때 펴진다** — 이게 「들어올린다」로 읽히는 핵심이다.
                if (손 != null) 손.줍기굽힘 = 숙 * (1f - 올);
                if (팔 != null)
                {
                    팔.줍기 = 숙 * (1f - 올);        // 앞아래로 뻗은 자세는 올리면서 사라지고
                    팔.들올림 = 올;                   // 머리 위로 받치는 자세가 대신 들어온다
                }
            }
            else if (팔 != null) { 팔.줍기 = 0f; 팔.들올림 = 0f; 손.줍기굽힘 = 0f; }

            if (올 < 0.98f) hero.MoveMul = 0.25f;   // ★다 올릴 때까지는 거의 못 간다. 올린 뒤엔 걷는다

            // ★★★**자리는 `LateUpdate` 에서 잡는다** (2026-08-13 사용자 "손과 손사이가 아니라
            //   앞쪽으로 가있어서 정확하지가않아").
            //   ☆여기(`Update`)에서 손뼈를 읽으면 **한 프레임 늦은 자리**다 — 애니메이터와
            //     `HeroAttack`(실행 순서 300)이 **그 뒤에** 뼈를 움직이기 때문이다.
            //     그래서 짐승이 늘 손보다 앞·뒤로 어긋나 있었다.
            //   ☆값만 기억해 두고, 뼈가 다 움직인 뒤에 얹는다 (아래 `LateUpdate`).
            안은숙 = 숙; 안은잡 = 잡; 안은올 = 올;
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
        // ★★거리에 **짐승 몸 반지름을 더한다** — 큰 놈은 몸이 커서 중심이 멀다.
        //   반지름을 안 더하면 큰 놈일수록 잡기 어려워진다 (사슴이 그래서 안 잡혔다)
        Critter best = null; float bd = float.MaxValue;
        foreach (var c in Critter.All)
        {
            if (c == null || !c.지침) continue;
            float 닿 = 손닿는거리 + c.종.반지름;
            float d2 = (c.transform.position - transform.position).sqrMagnitude;
            if (d2 > 닿 * 닿 || d2 >= bd) continue;
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

        // ★★★**붙잡으면 짐승 애니메이터를 끈다** (2026-08-13 사용자 "아직도 머리에든채로
        //   걸으면 팻이 걷는모션이 나옴").
        //   ☆`펫동작` 이 그 일을 하게 해 뒀는데 **그 컴포넌트가 짐승에 안 붙어 있었다**
        //     (실측). 9-2 그대로다 — 고쳐 놨지만 부르는 데가 없었다.
        //   ☆들려 가는 놈이 제 발로 걷는 건 어떤 경우에도 틀렸으니, 여기서 직접 끈다.
        짐승애니 = best.GetComponentInChildren<Animator>();
        if (짐승애니 != null) 짐승애니.enabled = false;

        // ★★**몸통 중심을 한 번 잰다** (사용자 "팻의 몸통부분을 잘 들어야하고").
        //   ☆`몸.position` 은 기준점일 뿐이다 — 실측으로 **메시 중심이 뿌리보다 82cm 뒤**였다.
        //     그래서 몸통이 아니라 엉뚱한 데가 손에 왔다. 렌더러가 차지한 자리를 쓴다.
        //   ☆붙잡을 때 한 번만 잰다 (매 프레임 렌더러를 훑으면 비싸다 — 9-4)
        // ★★★**허리뼈를 잡는다** (2026-08-13 사용자 "리깅된 뼈위치를 활용해서 좀 붙혀봐" ·
        //   "팻의 몸통이, 허리뼈? 그게 손과 손 사이에 있도록").
        //   ☆실측으로 짐승 뼈 이름을 알아냈다: `spine_hip`(허리) · `spine_back` · `spine_shldr`
        //     · `neck1` · `head` · `tail1~2` · 다리 8개.
        //   ☆메시 중심(bounds)보다 **허리뼈**가 정확하다 — 꼬리나 다리가 뻗은 만큼 중심이
        //     끌려가지 않는다. 다람쥐 꼬리처럼 큰 것이 있으면 특히 어긋난다.
        //   ☆뼈가 없는 종은 메시 중심으로 물러선다 (모델마다 뼈 이름이 다를 수 있다)
        짐승허리 = null; 몸중심로컬 = Vector3.zero;
        var 몸t0 = best.몸;
        if (몸t0 != null)
        {
            // ★`붙일뼈` 에 적은 순서대로 찾는다 — 앞에 적은 것이 이긴다
            var 뼈들 = 몸t0.GetComponentsInChildren<Transform>(true);
            if (붙일뼈 != null)
                foreach (var 이름 in 붙일뼈)
                {
                    if (string.IsNullOrEmpty(이름)) continue;
                    foreach (var t in 뼈들) if (t.name == 이름) { 짐승허리 = t; break; }
                    if (짐승허리 != null) break;
                }
            if (짐승허리 == null)
            {
                var rs = 몸t0.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    var bb = rs[0].bounds;
                    foreach (var r in rs) bb.Encapsulate(r.bounds);
                    몸중심로컬 = best.transform.InverseTransformPoint(bb.center);
                }
            }
        }

        // ★안고 있으면 못 때린다 — 다만 **컴포넌트를 끄지는 않는다.**
        //   숙이는 자세(다리·골반·척추)가 그 안에 있어서, 끄면 몸을 안 숙인다 (2026-08-13)
        var atk = GetComponent<HeroAttack>();
        if (atk != null) atk.두손막힘 = 안는중;
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

        // ★고리를 몸통 뼈에 맞춰 얹는다 (world 좌표 — 뼈 스케일이 끼어들지 않는다)
        if (고리판 == null || 고리판.Length != 고리수 * 판수) 고리판 = new Transform[고리수 * 판수];
        고리그리기(데려가는것);

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
    /// 붙잡을 때 뼈만 찾아 둔다 (실제 배치는 매 프레임 `고리그리기`)
    void 고리붙이기(Critter c) => 감을뼈 = 뼈찾기(c, 붙일뼈) ?? c.몸;
    Transform 감을뼈;

    /// ★★★**고리를 world 좌표로 직접 얹는다** (2026-08-13 사용자 "밧줄 모양봐봐 몸에 감긴게
    ///   아니야 저건 공중에 떠있어").
    ///   ☆뼈의 **자식**으로 붙였더니 그 뼈의 스케일이 곱해졌다. 모델마다 뼈 스케일이 제각각
    ///     이라 사슴에서는 고리가 **몸보다 커졌다.** 상쇄식을 넣어도 종마다 또 어긋난다.
    ///   ☆→ 부모를 두지 않고 **뼈의 자리·축을 읽어 world 로 얹는다.** 스케일이 끼어들 자리가
    ///     없으니 어느 종이든 같은 굵기·같은 크기로 나온다 (줄·수레와 같은 방식).
    void 고리그리기(Critter c)
    {
        if (감을뼈 == null || 고리판 == null) return;
        float r = Mathf.Max(0.10f, c.종.반지름) * 고리크기;
        var 축 = 감을뼈.forward;                                   // 몸통이 뻗은 방향
        if (축.sqrMagnitude < 1e-6f) 축 = c.transform.forward;
        축.Normalize();
        var 기준밖 = Vector3.Cross(축, Vector3.up).sqrMagnitude > 1e-4f
                   ? Vector3.Normalize(Vector3.Cross(축, Vector3.up)) : Vector3.right;
        float 면 = r * 2f * Mathf.Tan(Mathf.PI / 판수) + 줄굵기;    // 팔각형 한 변

        for (int i = 0; i < 고리수; i++)
        {
            var 가운데 = 감을뼈.position + 축 * (r * 고리앞뒤 * (i == 0 ? 1f : -1f));
            for (int j = 0; j < 판수; j++)
            {
                int k = i * 판수 + j;
                if (고리판[k] == null) 고리판[k] = 새줄상자("고리");
                var p = 고리판[k];
                if (p.parent != null) p.SetParent(null, true);
                float a = j * 360f / 판수;
                var 밖 = Quaternion.AngleAxis(a, 축) * 기준밖;      // 몸통 축 둘레로 돈다
                var 접선 = Vector3.Cross(축, 밖);                   // 판이 길게 뻗을 방향
                p.position = 가운데 + 밖 * r;
                p.rotation = Quaternion.LookRotation(접선, 밖);
                p.localScale = new Vector3(줄굵기, 줄굵기, 면);
                p.gameObject.SetActive(true);
            }
        }
    }

    /// 이름 목록에서 먼저 찾히는 뼈 (없으면 null)
    static Transform 뼈찾기(Critter c, string[] 이름들)
    {
        if (c.몸 == null || 이름들 == null) return null;
        var 뼈들 = c.몸.GetComponentsInChildren<Transform>(true);
        foreach (var 이름 in 이름들)
        {
            if (string.IsNullOrEmpty(이름)) continue;
            foreach (var t in 뼈들) if (t.name == 이름) return t;
        }
        return null;
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

    float 안은숙, 안은잡, 안은올;
    Vector3 몸중심로컬;      // 뿌리 → 몸통 메시 중심 (허리뼈가 없는 종일 때만 쓴다)
    Transform 짐승허리;      // ★`spine_hip` — 이게 손 사이에 온다
    Animator 짐승애니;        // 들려 있는 동안 꺼 둔다 — 제 발로 걸으면 안 된다
    // ★★**어느 뼈를 손 사이에 맞출지, 얼마나 눕힐지 — 인스펙터에서 고른다** (2026-08-13 사용자
    //   "허리뼈말고 다른 뼈로해야할듯한데 더 머리쪽에 있는뼈로, 그리고 방향도 흠..").
    //   ☆짐승 뼈 사슬(실측): `spine_hip` → `spine_back` → `spine_shldr` → `neck1` → `head`.
    //     뒤로 갈수록 머리쪽이다. 앞에 적은 것부터 찾으니 **순서만 바꾸면** 기준이 바뀐다.
    //   ☆제가 값을 짐작하는 것보다 눈으로 돌려 보는 쪽이 빠르다 (9번 4항)
    [Tooltip("손 사이에 맞출 짐승 뼈 — 앞에 적은 것부터 찾는다. 뒤로 갈수록 머리쪽")]
    public string[] 붙일뼈 = { "spine_shldr", "spine_back", "spine_hip", "Hips" };
    [Tooltip("들었을 때 짐승을 얼마나 돌리나 (도) — 눈으로 보고 맞춘다")]
    public Vector3 들때기울임 = new Vector3(0f, 0f, -90f);
    [Tooltip("머리뼈에서 얼마나 위에 얹나 (m) — 지금은 안 쓴다 (손 사이를 쓴다)")]
    [Range(0f, 0.8f)] public float 머리위틈 = 0.26f;
    [Tooltip("마지막으로 미세 조정 (m) — 눈으로 보고 맞춘다")]
    public Vector3 손사이보정 = Vector3.zero;

    /// ★★뼈가 다 움직인 뒤에 짐승을 얹는다 — 그래야 **정말로 손과 손 사이**에 놓인다.
    ///   ☆실행 순서: 애니메이터(0) → `HeroHold`(200) → `HeroAttack`(300) → 여기.
    ///     `Update` 에서 얹으면 한 프레임 늦은 손을 쓰게 되어 늘 어긋난다.
    void LateUpdate()
    {
        if (데려가는것 == null || !안는중) return;

        손뼈찾기();
        var 바닥 = 잡은자리;
        var 손앞 = 손사이 ?? (transform.position + transform.forward * 손닿는앞
                              + Vector3.up * (hero.height * 0.26f));
        // ★★★**짐승의 몸통(허리)이 두 손 사이에 온다** (2026-08-13 사용자 "팻의 몸통이,
        //   허리뼈? 그게 손과 손 사이에 있도록하면될듯한데").
        //   ☆한때 머리뼈 기준으로 옮겼었다 — 손 사이에 놓으니 짐승이 앞으로 튀어나왔기 때문.
        //     그런데 그건 **몸통 보정이 없어서**였다. 기준점(발밑)을 손 사이에 놓으니 몸통은
        //     82cm 뒤에 남았던 것이다.
        //   ☆이제 아래에서 **몸통 중심**을 목표에 맞추므로, 손 사이를 목표로 삼으면
        //     **몸통이 정확히 손 사이**에 온다. 손이 어디로 가든 따라간다.
        //   ☆`머리위틈` 은 은퇴시키지 않고 남긴다 — 머리 위로 얹고 싶어지면 위 두 줄만 바꾼다
        var 머리위 = (손사이 ?? (transform.position + Vector3.up * (hero.height * 머리위높이)
                                + transform.forward * 0.04f));
        var 목표 = 안은올 > 0.001f
            ? Vector3.Lerp(손앞, 머리위, 안은올)
            : Vector3.Lerp(바닥, 손앞, 안은잡);

        // ★기절한 놈은 축 늘어져 있다 — 흔들지 않는다. 올라가면서 가로로 눕는다.
        //   ★★**회전을 먼저 준다** — 아래에서 몸이 어디로 갔는지 재려면 이미 돌아 있어야 한다
        float 흔 = 안은올 * Mathf.Sin(Time.time * 3.2f) * 흔들각;
        데려가는것.transform.rotation = transform.rotation
            * Quaternion.Euler(들때기울임 * 안은올 + new Vector3(흔 * 0.3f, 흔, 흔 * 0.5f));

        // ★★★**「몸통 중심」이 목표에 오게 한다** (2026-08-13 사용자 "팻의 몸통부분을 잘
        //   들어야하고").
        //   ☆짐승의 기준점은 발밑이고, 실측으로 **메시 중심이 뿌리보다 82cm 뒤**였다.
        //     기준점을 목표에 놓으면 몸통이 아니라 엉뚱한 데가 손에 온다.
        //   ☆붙잡을 때 재 둔 `몸중심로컬` 을 **지금 회전에 맞춰 돌려** 그만큼 되돌린다.
        //     회전이 바뀌어도(가로로 눕든) 저절로 맞는다 — 짐작한 보정값이 아니다.
        // ★허리뼈가 있으면 그것을, 없으면 메시 중심을 목표에 맞춘다.
        //   ☆뼈는 **이미 회전이 반영된 world 자리**라 따로 돌릴 필요가 없다
        데려가는것.transform.position = 목표;
        var 밀 = 짐승허리 != null
            ? 짐승허리.position - 데려가는것.transform.position
            : 데려가는것.transform.TransformVector(몸중심로컬);
        데려가는것.transform.position = 목표 - 밀 * 안은올 + 손사이보정 * 안은올;
    }

    // ★두 손뼈를 찾아 그 중간점을 쓴다 — 팔 자세가 바뀌어도 짐승이 저절로 손에 붙는다.
    //   ☆한 번만 찾는다 (매 프레임 계층을 훑으면 비싸다 — 9-4)
    Transform 왼손뼈, 오른손뼈, 머리뼈;
    Vector3? 손사이 => (왼손뼈 != null && 오른손뼈 != null)
        ? (Vector3?)((왼손뼈.position + 오른손뼈.position) * 0.5f) : null;

    void 손뼈찾기()
    {
        if (왼손뼈 != null && 오른손뼈 != null && 머리뼈 != null
            && 왼손뼈.gameObject.activeInHierarchy && 머리뼈.gameObject.activeInHierarchy) return;
        왼손뼈 = 오른손뼈 = 머리뼈 = null;
        foreach (var t in GetComponentsInChildren<Transform>(false))   // 켜진 몸에서만
        {
            if (t.name == "LeftHand") 왼손뼈 = t;
            else if (t.name == "RightHand") 오른손뼈 = t;
            else if (t.name == "Head") 머리뼈 = t;
            if (왼손뼈 != null && 오른손뼈 != null && 머리뼈 != null) return;
        }
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
        // ★애니메이터는 **`Critter` 가 쥔다** — 기절해 있으면 계속 멈춰 있고 깨면 켜진다.
        //   여기서 켜 버리면 기절한 놈이 한 프레임 서 있게 된다 (2026-08-13)
        짐승애니 = null;
        몸중심로컬 = Vector3.zero; 짐승허리 = null; 감을뼈 = null;

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
        // ★숙임이 남으면 계속 굽은 채 걷는다 · 들기진행을 음수로 되돌려야 클립이 멈춘다
        if (atk != null) { atk.두손막힘 = false; atk.줍기굽힘 = 0f; atk.들기진행 = -1f; }
        var 팔c = GetComponent<HeroHold>();
        if (팔c != null) { 팔c.줍기 = 0f; 팔c.끌기 = 0f; 팔c.끌기젖힘 = 0f; 팔c.들올림 = 0f; }   // 팔도 풀어 준다
    }

    void 띄움(string s) { 알림 = s; 알림T = 2.5f; }
}
