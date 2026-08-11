using System.Collections.Generic;
using UnityEngine;

/// 맞을 수 있는 것 — 생물과 사람이 같은 규칙으로 표적이 된다
public interface IHittable
{
    Transform T { get; }
    float Radius { get; }
    bool Alive { get; }
    void TakeDamage(float d);
}

/// 생물 — **동물의 뇌.** 야생과 내 펫이 같은 부품을 쓴다 (편만 다르다).
///
/// ★2026-08-03 전면 재작성. 그 전에는 「제일 가까운 적에게 직진해서 때린다」가 전부인
///   **좀비 뇌**였고, 거기에는 도주·영역·겁·무리 반응을 깃발로 붙일 수가 없었다
///   (사용자: "이전 시스템으로 이거 못 만들어").
///
/// ★상태 일곱 + 타이머 둘.
///   기절·넘어짐을 상태로 만들지 않고 **타이머**로 둔 게 요점이다 — 그렇게 안 하면
///   모든 상태가 "기절했나?"를 물어야 하고 전이가 폭발한다.
///
///   어슬렁 → 경계 → 접근 → 공격
///                ↘ 도주 → 복귀 → 어슬렁
///
/// ★행동은 **두려움**과 **공격성** 두 값이 정한다. 좀비는 이 둘이 각각 0과 1로 고정된
///   특수한 경우일 뿐이다.
public class Critter : MonoBehaviour, IHittable
{
    public enum Side { 야생, 내편 }
    // ★불놀람·불도망은 **사람을 무서워하는 도주와 다른 길이다.** `도망끔` 스위치가
    //   도주를 통째로 껐어도 불은 무서워한다 — 끈 것은 「사람이 무섭다」지 「불이 무섭다」가 아니다.
    public enum 상태 { 어슬렁, 경계, 접근, 공격, 도주, 복귀, 불놀람, 불도망 }

    [Header("편")]
    public Side side = Side.야생;

    [Header("종")]
    public SpeciesDef 종 = new SpeciesDef();
    [Tooltip("새끼인가 — 잘 놀라고, 잘 길들고, 어미가 지킨다")] public bool 새끼;

    [Header("내 편일 때")]
    public Transform owner;

    [HideInInspector] public float hp;
    [HideInInspector] public Pack 무리;

    public static readonly List<Critter> All = new List<Critter>();

    public Transform T => transform;
    public float Radius => 종.반지름;
    public bool Alive => hp > 0f;
    public 상태 지금 { get; private set; } = 상태.어슬렁;

    // ★★★**행동은 사용자 확인 없이 넣지 않는다** (2026-08-06 사용자 —
    //   "포기하게하는 그런행동, 도망가게하는 행동들은 나한테 컨펌받고 진행해줘.
    //    니가 좆대로 행동 넣으면 버그 존나 많고 이상했거든").
    //   → 아래 「몸집 통행권으로 포기」는 **꺼 둔 채로** 들어와 있다. 확인을 받으면 켠다.
    //   ☆`Blocker.들어가나`·`갈수있나` 는 **묻기만 하는 도구**라 행동이 아니다 — 그건 켜 둔다.
    public static bool 몸집통행권 = false;

    /// 다칠수록 겁이 오르는 것 — **꺼 둔다** (2026-08-06 사용자 "개피되면 도망가는것도 빼줘")
    public static bool 다치면도망 = false;

    /// ★★★**도망을 통째로 끈다** (2026-08-07 사용자 "도망가는거 없애달라니까").
    ///
    ///   종의 `겁` 을 0 으로 만드는 것만으로는 **안 막힌다.** 두려움이 다른 데서 올라오기
    ///   때문이다 — `무리.남은비율` 이 떨어지면 +0.5, `동요` 로 +0.25, 새끼는 +0.3.
    ///   즉 **때려서 무리가 줄면 두려움이 0.62 를 넘어** 달아났다. 잡으려고 몰아붙이는 것이
    ///   곧 도망 조건이었으니, 데이터로는 절대 못 끈다.
    ///   → 여기서 두려움을 0 으로 잠그고 도주 상태로 가는 길을 다 막는다.
    ///   ☆기능은 남긴다 (은퇴는 삭제가 아니라 스위치) — 되살리려면 이걸 false 로.
    public static bool 도망끔 = true;

    // ★몸집 통행권 (헌법 7번) — 이 반지름보다 큰 몸만 좁은 데를 못 지난다.
    //   작은 놈까지 걸면 다람쥐가 나무 뒤에서 헤매느라 아무 데도 못 간다.
    const float 좁은문턱 = 0.5f;
    /// 몇 초를 못 비집으면 포기하나 — 0 이면 곧바로 포기해서 홱 돌아서는 게 눈에 띈다
    const float 포기까지 = 1.1f;
    float 못가는중;
    public bool 넘어짐 => downT > 0f;

    // ══════════════════════════════════════════════════════════
    //  생포 — **아이템으로 줍는 게 아니라 집까지 데려간다**
    // ══════════════════════════════════════════════════════════

    // ══════════════════════════════════════════════════════════
    //  ★★기절 — **뾰족한 것으로는 기절시킬 수 없다** (2026-08-10 사용자 —
    //  *"뾰족한 무기가 아니라, 둔기같은 뭉뚱한 무기로만 때렸을때 기절하게 해줘,
    //    말이안돼니까"*). 창으로 찔러 기절시키는 건 말이 안 된다.
    //
    //  ☆체력과 **따로 도는 수치**다. 그래서 「죽이지 않고 제압한다」가 가능해진다 —
    //    기획 5-1 의 *"어미를 기절시켜 꺼낸다(죽이면 안 된다)"* 가 여기서 열린다.
    //  ☆큰 놈일수록 한계가 높다. 티라를 기절시키려면 몽둥이로 열일곱 대는 쳐야 한다.
    // ══════════════════════════════════════════════════════════

    /// 쌓인 기절값 — 둔기로 맞을 때만 오른다
    [HideInInspector] public float 기절치;
    /// 이만큼 쌓이면 엎어진다 — 무겁고 튼튼할수록 높다
    public float 기절한계 => 30f + 종.무게 * 22f + 종.체력 * 0.25f;
    /// 지금 기절해 엎어져 있나
    public bool 기절중 { get; private set; }

    [Header("기절")]
    [Tooltip("안 맞고 있으면 기절값이 1초에 이만큼 식는다")] public float 기절식음 = 7f;
    [Tooltip("엎어진 동안 기절값이 1초에 이만큼 빠진다 — 0 이 되면 깬다")] public float 기절빠짐 = 13f;

    [Tooltip("무게가 밀림을 얼마나 막나 — 클수록 큰 놈이 꿈쩍 안 한다")]
    [Range(1f, 2.5f)] public float 밀림지수 = 1.4f;

    [Tooltip("★버티기 — 무게가 이 값이면 맞아도 반드시 안 움츠러든다 (확률 = 무게 ÷ 이 값)")]
    public float 버팀무게 = 6f;

    float 기절든지;                 // 엎어진 지 얼마나 됐나 (엎어지는 동작에 쓴다)

    /// ★둔기로 맞았다 — 기절값이 쌓인다. 뾰족한 무기는 이걸 안 부른다
    public void 기절값먹임(float 값)
    {
        if (값 <= 0f || !Alive || 기절중) return;      // 이미 엎어진 놈에겐 더 안 쌓는다
        기절치 += 값;
        if (기절치 < 기절한계) return;
        기절치 = 기절한계;
        기절중 = true;
        기절든지 = 0f;
        target = null;
        지금상태(상태.어슬렁);                          // 하던 걸 다 놓는다
    }

    /// 지쳤나 — 이 상태여야 붙잡을 수 있다. 넘어져 있거나 **기절했거나** 체력이 바닥나거나
    public bool 지침 => Alive && side == Side.야생 && (넘어짐 || 기절중 || hp <= 종.체력 * 0.35f);

    /// 지금 사람에게 붙잡혀 있나
    [HideInInspector] public bool 잡힘;

    /// ★목줄에 매여 있나 — 캠프에 데려다 놓으면 이 상태가 된다. 아직 내 것이 아니다
    [HideInInspector] public bool 묶임;

    /// ★신뢰 (0~100). **먹이를 주며 채워야 내 것이 된다.**
    ///   사용자 확정: "그냥 캠프로 데려갔는데 내 게 되는 것도 말이 안 되잖아 —
    ///   목줄을 채고 먹이를 주고 해야 수치가 차면서 내 게 되겠지."
    [HideInInspector] public float 신뢰;

    /// 먹이 한 번의 값 — 새끼가 훨씬 잘 길든다. 겁 많은 종은 더디다
    public float 먹이값 => (새끼 ? 34f : 15f) * Mathf.Lerp(1.2f, 0.6f, 종.겁);

    /// `배` — 구운 고기는 더 값을 한다. 불을 땔 이유가 하나 더 생긴다 (기획 5-5)
    public void 먹이받음(float 배 = 1f)
    {
        신뢰 = Mathf.Min(100f, 신뢰 + 먹이값 * 배);
        squash = 0.8f;
        // 먹으면 기운이 돌아온다 — 굶어 죽는 시계가 되감긴다
        hp = Mathf.Min(종.체력, hp + 종.체력 * 0.35f);
        자라기(배);
    }

    // ══════════════════════════════════════════════════════════
    //  ★★성장 — 기획 5-2 의 2층. **먹이면 실제로 몸이 커진다**
    //
    //  *"먹이면 큰다. 안 주면 멈춘다(죽지 않는다)"* — 이게 이 게임의 「깊은 시스템 하나」다.
    //  ☆길들이기의 끝이 아니라 시작이다: 신뢰 100 으로 내 것이 된 다음에도 계속 먹여야 큰다.
    //  ☆리깅을 안 건드린다 — 몸 전체의 크기만 키운다 (6장: 펫은 리깅 안 함).
    // ══════════════════════════════════════════════════════════

    /// 0 = 갓 잡아온 새끼 · 1 = 다 컸다
    [HideInInspector] public float 자람;
    /// 다 크면 이 몸이 된다 (새끼일 때만 들어 있다)
    SpeciesDef 어른;
    /// 갓 잡아왔을 때의 몸 — 자람 0 쪽의 끝점이다
    SpeciesDef 새끼적;
    float 처음키;
    Vector3 첫몸크기, 첫몸자리;
    bool 몸기억함;

    [Tooltip("먹이 한 번에 얼마나 자라나 (0.2 = 다섯 번이면 성체)")]
    public float 한입성장 = 0.2f;

    /// 새끼로 만든다 — `어른정의` 는 다 컸을 때의 몸이다
    public void 새끼로설정(SpeciesDef 어른정의)
    {
        새끼 = true;
        어른 = 어른정의;
        새끼적 = 종.복제();          // 자람 0 쪽의 끝점을 붙들어 둔다
        자람 = 0f;
    }

    void 자라기(float 배)
    {
        if (!새끼 || 어른 == null || 새끼적 == null) return;

        자람 = Mathf.Clamp01(자람 + 한입성장 * 배);

        // ★새끼 몸 → 어른 몸으로. **두 끝점 사이를 `자람` 으로 섞는다** —
        //   현재값에서 조금씩 다가가면 영영 어른 값에 못 닿는다 (지수 접근)
        float hp01 = hp / Mathf.Max(1f, 종.체력);
        종.키      = Mathf.Lerp(새끼적.키,     어른.키,     자람);
        종.반지름  = Mathf.Lerp(새끼적.반지름, 어른.반지름, 자람);
        종.무게    = Mathf.Lerp(새끼적.무게,   어른.무게,   자람);
        종.체력    = Mathf.Lerp(새끼적.체력,   어른.체력,   자람);
        종.피해    = Mathf.Lerp(새끼적.피해,   어른.피해,   자람);
        종.사거리  = Mathf.Lerp(새끼적.사거리, 어른.사거리, 자람);
        종.이속    = Mathf.Lerp(새끼적.이속,   어른.이속,   자람);
        hp = 종.체력 * hp01;

        몸키우기();

        if (자람 >= 1f)
        {
            // ★다 컸다 — 이제 새끼가 아니다. 잘 길들던 이점도 같이 사라진다
            새끼 = false;
            종.이름 = 어른.이름;
            gameObject.name = 어른.이름;
        }
    }

    /// 몸을 실제로 키운다 — **기준 크기 자체를 바꾼다.**
    /// (눌림·놀람 동작이 `bodyScale` 을 기준으로 쓰므로, 여기서 기준을 갱신해야 안 싸운다)
    void 몸키우기()
    {
        if (body == null || !몸기억함 || 처음키 <= 0.0001f) return;
        float r = 종.키 / 처음키;
        bodyScale = 첫몸크기 * r;
        bodyPos = new Vector3(첫몸자리.x, 첫몸자리.y * r, 첫몸자리.z);
        몸복구();
    }

    /// 굶주림 정도 (0 = 멀쩡 · 1 = 곧 죽는다) — 화면에 띄운다
    public float 굶주림 => 묶임 ? 1f - Mathf.Clamp01(hp / Mathf.Max(1f, 종.체력)) : 0f;

    /// ★묶인 채 안 먹이면 **굶어 죽는다** (2026-08-03 사용자 — "줄을 풀고 달아나는 게
    ///   아니라 먹이를 안 주면 굶어 뒤져야지"). 매인 짐승이 스스로 풀고 가는 건 어설프고,
    ///   무엇보다 **방치에 대가가 없어진다.** 잡아 왔으면 책임이 따라야 한다.
    void 묶인채(float dt)
    {
        hp -= 종.체력 / Mathf.Max(1f, 종.굶는시간) * dt;
        if (hp <= 0f) { hp = 0f; Die(); return; }

        if (body == null) return;
        float 기운 = Mathf.Clamp01(hp / Mathf.Max(1f, 종.체력));
        // 몸부림 — 기운이 있을 때만. 굶을수록 잦아들다 축 처진다
        float w = Mathf.Sin(Time.time * 4.2f + GetInstanceID()) * 기운 * (1f - 신뢰 / 100f);
        body.localRotation = bodyRot * Quaternion.Euler((1f - 기운) * 18f, w * 14f, w * 5f);
    }

    /// 붙잡힌 채 끌려간다 — 사람이 매 프레임 부른다
    public void 끌림(Vector3 목표, float dt, out bool 버팀)
    {
        버팀 = false;
        var d = 목표 - transform.position; d.y = 0f;
        float dist = d.magnitude;
        if (dist < 0.05f) return;

        // 가끔 발을 뻗대고 버틴다 — 신뢰가 없으니 순순히 안 온다
        버팀 = Mathf.PerlinNoise(GetInstanceID() * 0.01f, Time.time * 0.7f) > 0.72f;
        if (버팀) return;

        var pos = transform.position + d / dist * 종.이속 * 0.9f * dt;
        pos = Blocker.Resolve(pos, 종.반지름);
        pos.y = 땅격자.걷는높이(pos.x, pos.z);
        transform.position = pos;
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(d / dist, Vector3.up), 8f * dt);
    }

    /// 집에 도착 — 내 편이 된다
    public void 길들여짐(Transform 주인)
    {
        side = Side.내편;
        owner = 주인;
        잡힘 = false;
        무리 = null;
        hp = Mathf.Max(hp, 종.체력 * 0.5f);
        지금상태(상태.복귀);
        // 내 편은 겁을 안 낸다
        종.겁 = 0f; 종.공격성 = 1f; 종.영역 = 9999f;
        var b = transform.childCount > 0 ? transform.GetChild(0) : null;
        if (b != null) 몸복구();
    }

    // ── 타이머 (상태가 아니라 「막는 것」)
    float staggerT, downT;
    Vector3 밀림; float 밀림남은;      // 맞아서 뒤로 밀리는 중 (크기·무게로 보정된 거리)
    float 버팀시각 = -99f;             // 이번 타를 버틴 시각 — 바로 뒤따르는 `Knock` 도 같이 무시한다

    // ★★공격 커밋 (2026-08-07 사용자 "공격모션이 들어갔으면 끝까지 모션하도록, 허공에
    //   때리더라도 · 앞으로 조금 돌진하면서"). 전에는 휘두르다가도 상대가 반 발짝 벗어나면
    //   곧장 추격으로 끊겨서 **공격이 보이질 않았다.** 시작하면 무르지 않는다 —
    //   그 대신 빗나갈 수 있다. 이 「커밋 + 빗나감」이 좀보이드 전투 읽기의 반쪽이다.
    float 휘두름t = -1f;                 // 0 이상 = 휘두르는 중
    float 마지막교전 = -99f;             // 공격 상태에 마지막으로 있던 시각 (선딜 재걸림 방지)
    Vector3 휘두름방향;
    bool 휘두름타격;
    // ★★휘두름 시간은 상수가 아니라 **그 모델의 공격 클립 길이**다 (2026-08-07 "공격을
    //   하고 한번 뚝 끊기는 듯한 느낌" — 클립 1.00초를 0.55초 커밋으로 자르니 모션이
    //   중간에 잘려 대기 자세로 스냅됐다. 컨트롤러 갈아타기엔 블렌드가 없다).
    float 휘두름시간 = 0.55f;            // 때리기 순간 클립 길이로 갱신된다
    float 타격시점 = 0.32f;              // 휘두름시간 × 0.58 — 이빨이 앞을 지나는 순간
    // ★후딜 1.2초 (2026-08-07 사용자 "공격 후에 1~1.5초는 서 있는 모션 후에 뛰어와야지").
    //   이 동안 대기 모션으로 서 있는다 — 「물었다 → 숨 고르고 → 다시」 의 박자.
    const float 후딜 = 1.2f;

    /// 몸짓이 읽는다 — **실제로 휘두르는 동안만** 공격 모션 (후딜엔 대기로 서 있는다)
    public bool 휘두르는중 => 휘두름t >= 0f && 휘두름t < 휘두름시간;

    void 휘두름진행(float dt)
    {
        float 전 = 휘두름t;
        휘두름t += dt;

        if (전 < 휘두름시간)
        {
            // 살짝 파고든다 — 타격 시점까지만 세게, 그 뒤는 여운
            float 밀도 = 휘두름t < 타격시점 ? 0.55f : 0.15f;
            var pos = transform.position + 휘두름방향 * 종.이속 * 밀도 * dt;
            pos = Blocker.Resolve(pos, 종.반지름); pos.y = 땅격자.걷는높이(pos.x, pos.z);
            transform.position = pos;
            Face(휘두름방향);

            // 타격 순간 — 아직 닿으면 맞고, 벗어났으면 **허공을 벤다** (그게 컨트롤이다)
            if (!휘두름타격 && 전 < 타격시점 && 휘두름t >= 타격시점)
            {
                휘두름타격 = true;
                // ★상대의 **실루엣(뼈 점들)** 에 무는 것 — 사람이든 짐승이든 몸 어디든 닿으면 맞는다
                // ★★여유를 0.45 → 0.1 로 줄였다 (2026-08-09 사용자 "실제와 다르게 멀리서 맞추고 떄려").
                //   `종.사거리` 자체가 이미 「몸 표면에서 이만큼 안이면 때린다」는 여유인데,
                //   거기에 반지름과 0.45 를 또 얹어 늑대가 중심에서 2.05m 밖에서 물었다
                //   (늑대 입은 중심에서 0.6m 남짓이다).
                if (target != null && target.Alive
                    && 실루엣판정.닿나(target.T, transform.position, 종.사거리 + 종.반지름, 0.1f))
                    target.TakeDamage(종.피해);
            }
        }
        // 후딜 — 제자리에 선다 (이동도 회전도 없음)
        if (휘두름t >= 휘두름시간 + 후딜) 휘두름t = -1f;
    }

    Transform body;
    Vector3 bodyScale, bodyPos;
    Quaternion bodyRot;
    float downTotal, downSign = 1f;
    IHittable target;
    float findCd, atkCd, squash, wanderCd, stateT;

    // ── 불 공포 (`불에놀람` 참고)
    float 불겁냄쿨, 놀람길이, 안전거리;
    int 놀람종류;
    Vector3 불자리;
    bool 따라가는중;
    Vector3 wander, 집;
    bool 대표;                     // 무리에서 한 마리만 무리 냉각을 돌린다

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); if (무리 != null) 무리.나감(this); }

    void Start()
    {
        hp = 종.체력;
        집 = 무리 != null ? 무리.집 : transform.position;
        findCd = Random.value * 0.5f;
        body = transform.childCount > 0 ? transform.GetChild(0) : null;
        if (body != null)
        {
            bodyScale = body.localScale;
            bodyPos = body.localPosition;
            bodyRot = body.localRotation;

            // ★성장의 기준자 — 이 몸이 「처음 크기」다 (`몸키우기` 가 여기에 배를 곱한다)
            첫몸크기 = bodyScale; 첫몸자리 = bodyPos;
            처음키 = 종.키; 몸기억함 = true;
        }
        if (무리 != null && 무리.식구.Count > 0 && 무리.식구[0] == this) 대표 = true;
    }

    // ══════════════════════════════════════════════════════════
    void Update()
    {
        float dt = Time.deltaTime;
        // ★뿌리 y 는 살아 있는 동안 언제나 0 — 어떤 경로로든 떠오르면 여기서 끌어내린다
        { var 자리 = transform.position; float g = 땅격자.걷는높이(자리.x, 자리.z);
          if (자리.y != g) { 자리.y = g; transform.position = 자리; } }
        if (대표 && 무리 != null) 무리.식힘(dt);

        // ★맞아서 뒤로 밀리는 중 — 처음엔 훅, 끝으로 갈수록 잦아든다 (버티며 멎는 느낌)
        if (밀림남은 > 0f)
        {
            float 총 = Mathf.Max(0.01f, 몸짓.피격길이 * 0.60f);
            float 남음 = 밀림남은 / 총;                 // 1 → 0
            transform.position += 밀림 * (남음 * 남음 * dt / 총 * 2f);
            밀림남은 -= dt;
        }

        // 붙잡혀 있는 동안은 스스로 판단하지 않는다 (사람이 끌고 간다)
        if (잡힘) { Squash(dt); return; }
        // 매여 있는 동안도 마찬가지 — 먹이를 기다린다
        if (묶임) { 묶인채(dt); Squash(dt); return; }

        // ★★기절이 제일 먼저다 — 엎어져 있는 동안은 아무것도 못 한다.
        //   ☆**기절값이 완전히 바닥날 때까지** 엎어져 있는다 (사용자 확정)
        if (기절중)
        {
            기절든지 += dt;
            기절치 -= 기절빠짐 * dt;
            그로기몸();
            if (기절치 <= 0f) { 기절치 = 0f; 기절중 = false; 몸복구(); }
            return;
        }
        if (기절치 > 0f) 기절치 = Mathf.Max(0f, 기절치 - 기절식음 * dt);   // 안 맞으면 식는다

        // ── 막는 타이머가 먼저다. 넘어져 있으면 아무 판단도 안 한다
        if (downT > 0f)
        {
            downT -= dt;
            넘어진몸();
            if (downT <= 0f) 몸복구();
            return;
        }
        if (staggerT > 0f) { staggerT -= dt; Squash(dt); return; }

        // ★휘두르는 중엔 딴생각 안 한다 — 끝까지 모션 (허공이라도)
        if (휘두름t >= 0f) { 휘두름진행(dt); Squash(dt); return; }

        // ★★**서 있을 때도 비켜선다** (2026-08-05). `걷기` 안의 밀어내기는 움직일 때만 도는데,
        //   자리에 다 온 펫은 거기서 멈춘다(`걷기` 의 이른 반환). 그 뒤에 주인이 걸어 들어오면
        //   **파묻힌 채 그대로** 있었다 — 겹침이 안 풀리던 나머지 반쪽이 이것이다.
        // ★구간 표시 — 야생이 무거운 몫을 **짐작 말고 재기** 위한 것.
        //   `BeginSample` 은 릴리스 빌드에서 저절로 빠지므로 두고 가도 값이 안 든다.
        using (new 잼("Critter.비켜서기")) 비켜서기();

        atkCd -= dt; stateT += dt;
        if (휘두름T > 0f) 휘두름T -= dt;

        findCd -= dt;
        if (findCd <= 0f)
        {
            findCd = 0.4f;
            using (new 잼("Critter.표적찾기")) target = 표적찾기();
        }

        using (new 잼("Critter.판단")) 판단();
        using (new 잼("Critter.행동")) 행동(dt);
        using (new 잼("Critter.Squash")) Squash(dt);
    }

    /// 프로파일러 구간을 `using` 한 줄로 여닫는 자
    struct 잼 : System.IDisposable
    {
        public 잼(string 이름) { UnityEngine.Profiling.Profiler.BeginSample(이름); }
        public void Dispose() { UnityEngine.Profiling.Profiler.EndSample(); }
    }

    // ── 어느 상태로 갈까
    void 판단()
    {
        float 두려움 = 두려움값();
        bool 적있음 = target != null && target.Alive;
        float d적 = 적있음 ? Flat(target.T.position, transform.position) : 999f;
        float d집 = Flat(집, transform.position);

        // ══════════════════════════════════════════════════════════
        //  ★★★불 — **어떤 판단보다 먼저다.** 사람을 쫓던 중이라도 불 앞에선 다 잊는다
        // ══════════════════════════════════════════════════════════
        if (불겁냄쿨 > 0f) 불겁냄쿨 -= Time.deltaTime;

        if (지금 == 상태.불놀람)
        {
            // 놀란 시간이 다 차면 등을 돌린다 (개체마다 2~3.2초로 다르다)
            if (stateT >= 놀람길이) { 몸복구(); 지금상태(상태.불도망); }
            return;
        }
        if (지금 == 상태.불도망)
        {
            // 충분히 멀어졌거나 오래 달렸으면 그만둔다.
            // ★쿨을 두는 이유: 경계선에서 놀람↔도망을 오가며 제자리 떠는 걸 막는다
            if (Flat(불자리, transform.position) > 안전거리 || stateT > 4f)
            {
                불겁냄쿨 = 6f;
                지금상태(상태.복귀);
            }
            return;
        }

        // 야생만 무서워한다. 내 펫은 제 주인이 피운 불이다
        if (side == Side.야생 && 불겁냄쿨 <= 0f)
        {
            var 불 = 모닥불.무서운불(transform.position);
            if (불 != null) { 불에놀람(불); return; }
        }

        // 내 편은 겁내지 않는다 — 주인이 시킨 자리를 지킨다
        if (side == Side.내편)
        {
            if (적있음) { 지금상태(d적 <= 때리는거리(target) * 1.45f ? 상태.공격 : 상태.접근); return; }   // 돌진이 틈을 메운다

            // ★★따라가기와 어슬렁이 겹치면 안 된다 (2026-08-04 사용자 지적).
            //   전에는 "4m 밖이면 따라가고 안이면 어슬렁" 이었는데, 어슬렁이 7m 밖 지점을
            //   고르는 바람에 **멀어지려는 행동과 붙으려는 행동이 매 순간 교대**했다.
            //   방향이 계속 뒤집혀 헤드뱅잉이 된 정체가 이것이다.
            //
            //   → **내 편은 어슬렁대지 않는다.** 따라가거나 서 있거나 둘 중 하나다.
            //     문턱도 벌린다 (멀어지면 4.5m 부터 따라가고, 2m 안에 들면 선다).
            float d주인 = owner != null ? Flat(내자리, transform.position) : 0f;
            if (따라가는중) { if (d주인 < 1.2f) 따라가는중 = false; }
            else if (d주인 > 4.5f) 따라가는중 = true;

            지금상태(따라가는중 ? 상태.복귀 : 상태.어슬렁);
            return;
        }

        // ★영역 — 좀비와 갈리는 첫 자리. 벗어나면 포기하고 돌아간다
        if (d집 > 종.영역 && 지금 != 상태.도주)
        {
            지금상태(상태.복귀);
            return;
        }

        // ★★★**몸집이 곧 통행권이다** (2026-08-06 — 헌법 7번).
        //   *"큰 놈은 빽빽한 숲·좁은 바위틈·굴에 못 들어온다."*
        //   쫓아가려는 자리가 내 몸으로는 못 비집는 곳이면 **가장자리에서 포기하고 돌아간다.**
        //   ☆막아서 세우지 않는다 — 나무에 부딪혀 멈춰 있으면 고장처럼 보인다.
        //     서성이다 돌아가는 게 동물답고, 위 「영역」 규칙과 결이 같다.
        //   ★이게 헌법 5번과 한 몸이다: **XL 을 못 이기니 도망쳐야 하고, 도망칠 곳이 「좁은 데」다.**
        if (몸집통행권 && 적있음 && 종.반지름 > 좁은문턱 && d적 < 종.시야
            && (지금 == 상태.접근 || 지금 == 상태.경계 || 지금 == 상태.공격))
        {
            if (!Blocker.갈수있나(transform.position, target.T.position, 종.반지름 * 0.9f))
            {
                못가는중 += Time.deltaTime;
                if (못가는중 > 포기까지) { 못가는중 = 0f; 지금상태(상태.복귀); return; }
            }
            else 못가는중 = 0f;
        }
        else 못가는중 = 0f;

        // ★두려움이 임계를 넘으면 달아난다
        // ★도망이 꺼져 있으면 이 길로 아예 안 간다 (`도망끔` 참고)
        if (!도망끔 && 적있음 && 두려움 > 0.62f) { 지금상태(상태.도주); return; }

        switch (지금)
        {
            case 상태.도주:
                // 충분히 멀어졌거나 무서움이 가시면 돌아간다
                if (!적있음 || d적 > 종.시야 * 1.3f || 두려움 < 0.35f) 지금상태(상태.복귀);
                break;

            case 상태.복귀:
                if (d집 < 4f) 지금상태(상태.어슬렁);
                else if (적있음 && d적 < 종.사거리 + 2f) 지금상태(상태.공격);   // 코앞이면 어쩔 수 없이 싸운다
                break;

            case 상태.어슬렁:
                if (적있음) 지금상태(상태.경계);
                break;

            case 상태.경계:
                // 노려보는 시간 — 겁 많은 종일수록 길다. 이때 플레이어는 물러날 수 있다
                if (!적있음) { 지금상태(상태.어슬렁); break; }
                if (stateT > Mathf.Lerp(0.2f, 1.4f, 종.겁))
                    지금상태((도망끔 || 덤빌까(두려움)) ? 상태.접근 : 상태.도주);
                break;

            case 상태.접근:
                if (!적있음) { 지금상태(상태.복귀); break; }
                if (d적 <= 때리는거리(target) * 1.45f) 지금상태(상태.공격);   // ★반 발짝 모자라도 공격을 시작한다 — 돌진이 메운다 (계속 쫓아붙는 것 방지)
                break;

            case 상태.공격:
                if (!적있음) { 지금상태(상태.복귀); break; }
                if (d적 > 때리는거리(target) * 2.2f) 지금상태(상태.접근);   // ★넉넉해야 공격 자세를 유지한다 — 좁으면 쫓기↔공격이 매 프레임 뒤집힌다
                break;
        }
    }

    /// ★두려움 — 좀비에겐 없던 값. 이 하나가 동물을 동물답게 만든다
    float 두려움값()
    {
        if (도망끔) return 0f;          // ★도망 스위치가 꺼져 있으면 아무도 안 무서워한다
        float f = 종.겁;

        // ★★★**다쳤다고 도망가지 않는다** (2026-08-06 사용자 — "개피되면 도망가는것도 빼줘").
        //   전에는 체력이 깎일수록 두려움이 0.8까지 올라서, **때리면 때릴수록 달아났다.**
        //   ☆이게 왜 나쁜가: 다 잡아 놓은 놈이 도망가면 **쫓아다니는 시간**만 늘고,
        //     생포(체력 20% 아래로 몰아 먹이를 던진다 — 기획 5-2)와 정면으로 부딪힌다.
        //     몰아붙여야 잡히는데 몰아붙일수록 달아나면 그 설계가 성립하지 않는다.
        //   ★기능은 남겨 둔다 (은퇴는 삭제가 아니라 스위치). 되살리려면 이 스위치를 켠다.
        if (다치면도망)
        {
            float hp01 = hp / Mathf.Max(1f, 종.체력);
            f += (1f - hp01) * 0.8f;
        }

        // 무리가 무너지면 무섭다
        if (무리 != null)
        {
            f += (1f - 무리.남은비율) * 0.5f;
            f += 무리.동요 * 0.25f;
            // ★새끼가 위험하면 어미는 오히려 안 무섭다 — 사나워진다
            if (무리.새끼위험 && !새끼) f -= 0.55f;
        }

        // 새끼는 원래 잘 놀란다
        if (새끼) f += 0.3f;

        // 공격성이 높으면 웬만해선 안 물러선다
        f -= 종.공격성 * 0.45f;

        return Mathf.Clamp01(f);
    }

    bool 덤빌까(float 두려움) => 종.공격성 > 두려움 * 0.9f;

    void 지금상태(상태 s)
    {
        if (지금 == s) return;
        지금 = s; stateT = 0f;

        // ★★**도착하자마자 못 때린다** (2026-08-07 사용자 "바로 달려와서 개패는 게 아니라
        //   딜레이가 있어야"). 공격 자세를 잡는 선딜 — 이 반 박자가 플레이어에게
        //   물러날 틈을 준다. 좀보이드의 전투 읽기가 이 틈에서 나온다.
        //   ☆단 **재진입엔 다시 안 건다** — 백스텝 상대로 공격↔접근이 오가며 선딜이
        //     계속 리셋되면 영원히 못 휘두른다 (2026-08-07 실제로 그랬다).
        if (s == 상태.공격 && Time.time - 마지막교전 > 2f) atkCd = Mathf.Max(atkCd, 0.45f);
        if (s == 상태.공격) 마지막교전 = Time.time;
    }

    // ── 상태대로 움직인다
    void 행동(float dt)
    {
        switch (지금)
        {
            case 상태.어슬렁:
                // ★내 편은 어슬렁대지 않는다 — 서서 기다린다 (위 「겹치면 안 된다」 참고)
                if (side == Side.내편) break;

                wanderCd -= dt;
                if (wanderCd <= 0f)
                {
                    wanderCd = Random.Range(2.5f, 6f);
                    wander = 집 + new Vector3(Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f));
                }
                if (Flat(wander, transform.position) > 1f) 걷기(wander, dt, 0.45f);
                break;

            case 상태.경계:
                if (target != null) 바라보기(target.T.position, dt);   // 멈춰서 노려본다
                break;

            case 상태.접근:
                if (target != null)
                {
                    걷기(target.T.position, dt, 1f);
                    // ★★뒤로 빼는 상대에게도 휘두른다 (2026-08-07 사용자 "계속 뒤로 빼니까
                    //   공격 끝까지 안 하고 따라온다"). 상태가 공격↔접근을 오가며 선딜이
                    //   계속 리셋되던 구멍 — **쫓는 중에도 쿨이 차 있고 닿을 만하면 커밋한다.**
                    //   돌진이 반 발짝을 메우고, 그래도 모자라면 허공을 벤다. 그게 맞다.
                    if (atkCd <= 0f && target.Alive
                        && Flat(target.T.position, transform.position) <= 때리는거리(target) * 1.45f)
                        때리기();
                }
                break;

            case 상태.공격:
                if (target != null) { 바라보기(target.T.position, dt); 때리기(); }
                break;

            case 상태.도주:
                if (target != null)
                {
                    var away = transform.position + (transform.position - target.T.position).normalized * 10f;
                    걷기(away, dt, 1.15f);        // 도망은 조금 더 빠르다
                }
                break;

            case 상태.복귀:
                걷기(side == Side.내편 && owner != null ? 내자리 : 집, dt, 0.9f);
                break;

            case 상태.불놀람:
                바라보기(불자리, dt);      // 무서운 것을 본다 — 등을 돌리는 건 그 다음이다
                놀람동작(dt);
                break;

            case 상태.불도망:
                // ★**걷지 않는다. 달린다** (사용자 — "걸어서 멀어지는게 아니라 달려서")
                var 반대 = transform.position + (transform.position - 불자리).normalized * 14f;
                걷기(반대, dt, 1.9f);
                break;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  ★불에 놀란다 — 2026-08-10 사용자
    //  *"바운더리에 왔을때... 한 2~3초정도 두려워하는 놀라는 모습을 하다가,
    //    반대로 도망가는 모습이었음 좋겠어... 달려서 좀 도망가게끔"*
    //  *"비슷하게 말고 서로 조금씩 달랐음하고"*
    //
    //  ☆왜 밀어내지 않는가: 장애물처럼 막으면 「보이지 않는 벽」이 하나 더 생길 뿐이고,
    //    벽에 비벼대는 짐승은 고장처럼 보인다. **무서운 건 몸으로 드러나야 무서운 것이다.**
    //  ☆리깅이 없어도 된다 (6장) — 몸통을 젖히고·세우고·흔드는 것으로 다 된다.
    // ══════════════════════════════════════════════════════════

    void 불에놀람(모닥불 불)
    {
        불자리 = 불.transform.position;
        안전거리 = 불.겁내는거리 * 2.2f;

        // ★개체마다 다르게 — 몸짓 종류도, 놀라는 길이도 제 번호에서 뽑는다.
        //   같은 놈은 언제나 같은 버릇이라 "쟤는 저렇게 놀란다" 가 생긴다
        놀람종류 = Mathf.Abs(GetInstanceID()) % 4;
        놀람길이 = 2f + 절차.값(GetInstanceID(), 0, 7) * 1.2f;      // 2.0 ~ 3.2초

        target = null;                 // 쫓던 것도 잊는다
        지금상태(상태.불놀람);
    }

    /// 놀란 몸짓 — 넷 중 하나. **앞이 세고 뒤로 갈수록 잦아든다** (몸의 3막: 본동작 → 여운)
    void 놀람동작(float dt)
    {
        if (body == null) return;

        float u = Mathf.Clamp01(stateT / Mathf.Max(0.01f, 놀람길이));
        float 세기 = Mathf.Lerp(1f, 0.25f, u);
        float t = Time.time + GetInstanceID() * 0.017f;
        float k = 종.키;

        Vector3 회전 = Vector3.zero, 밀림 = Vector3.zero, 크기 = Vector3.one;

        switch (놀람종류)
        {
            case 0:     // 움찔 — 상체를 홱 젖히고 뒷걸음질하듯 잘게 떤다
                회전.x = -26f * 세기 + Mathf.Sin(t * 26f) * 4f * 세기;
                밀림.z = -0.16f * k * 세기;
                밀림.y = 0.03f * k * 세기;
                break;

            case 1:     // 곧추섬 — 몸을 세우고 굳는다. 숨만 떤다
                크기 = new Vector3(1f - 0.09f * 세기, 1f + 0.17f * 세기, 1f - 0.09f * 세기);
                회전.x = -9f * 세기;
                밀림.y = 0.06f * k * 세기 + Mathf.Sin(t * 19f) * 0.012f * k * 세기;
                break;

            case 2:     // 어쩔 줄 몰라 좌우로 — 갈 데를 못 정한다
                회전.y = Mathf.Sin(t * 11f) * 34f * 세기;
                회전.z = Mathf.Sin(t * 11f) * 7f * 세기;
                밀림.x = Mathf.Sin(t * 11f) * 0.09f * k * 세기;
                break;

            default:    // 고개를 세차게 젓는다
                회전.y = Mathf.Sin(t * 24f) * 19f * 세기;
                회전.x = -12f * 세기 + Mathf.Abs(Mathf.Sin(t * 12f)) * 10f * 세기;
                밀림.y = Mathf.Abs(Mathf.Sin(t * 12f)) * 0.05f * k * 세기;
                break;
        }

        body.localRotation = bodyRot * Quaternion.Euler(회전);
        body.localPosition = bodyPos + 밀림;
        body.localScale = new Vector3(bodyScale.x * 크기.x, bodyScale.y * 크기.y, bodyScale.z * 크기.z);
    }

    float 때리는거리(IHittable t) => 종.사거리 + 종.반지름 + (t != null ? t.Radius : 0f);

    void 걷기(Vector3 goal, float dt, float 배)
    {
        var d = goal - transform.position; d.y = 0f;
        // ★목표에 거의 닿았으면 아예 안 움직인다 — 안 그러면 목표 언저리에서 잘게 떤다
        if (d.sqrMagnitude < 0.16f) return;
        d.Normalize();

        var pos = transform.position + d * 종.이속 * 배 * dt;
        pos = 서로밀기(pos);
        pos = Blocker.Resolve(pos, 종.반지름);
        pos.x = Mathf.Clamp(pos.x, 1f, WorldGrid.Size - 1f);
        pos.z = Mathf.Clamp(pos.z, 1f, WorldGrid.Size - 1f);
        pos.y = 땅격자.걷는높이(pos.x, pos.z);
        transform.position = pos;
        Face(d);
    }

    void 바라보기(Vector3 at, float dt)
    {
        Face(at - transform.position);
    }

    /// 서 있든 걷든, 남과 겹친 만큼은 늘 비켜선다
    void 비켜서기()
    {
        if (!Alive) return;
        var p = 서로밀기(transform.position);
        if ((p - transform.position).sqrMagnitude < 1e-6f) return;
        p = Blocker.Resolve(p, 종.반지름);
        p.y = 땅격자.걷는높이(p.x, p.z);
        transform.position = p;
    }

    /// ★서로 밀어내되 **부드럽게** (2026-08-04 사용자 — "비비면서 두두두두 떨리지 않게").
    ///   한 프레임에 완전히 밀어내면 두 마리가 서로를 튕겨내며 진동한다.
    ///   ①조금씩만 밀고 ②아주 살짝 겹친 건 그냥 둔다 — 그러면 스르르 자리를 잡는다.
    Vector3 서로밀기(Vector3 pos)
    {
        const float 여유 = 0.06f;   // 이만큼 겹친 건 못 본 척한다
        const float 세기 = 0.35f;   // 한 프레임에 이 비율만큼만 민다

        for (int i = 0; i < All.Count; i++)
        {
            var o = All[i];
            if (o == this || o == null || !o.Alive) continue;
            var v = pos - o.transform.position; v.y = 0f;
            float need = (종.반지름 + o.종.반지름) * 0.9f;
            float d = v.magnitude;
            if (d >= need - 여유 || d <= 1e-3f) continue;

            var 목표 = o.transform.position + v / d * need;
            pos = Vector3.Lerp(pos, 목표, 세기);
        }

        // ★★★**캐릭터도 밀어낸다** (2026-08-05 사용자 — "팻이랑 캐릭터랑 계속 겹치는
        //   문제가있어 … 꼬리에 겹쳐서 라인이 이상하게 그려진다거나").
        //   `All` 은 **생물 목록**이라 사람이 들어 있지 않다. 그래서 펫끼리는 서로 비켜서는데
        //   **주인 몸에는 그대로 파고들어** 서 있었다. 외곽선이 겹쳐 이상하게 보인 건 그
        //   증상일 뿐이고, 원인은 둘이 같은 자리에 서 있는 것이었다.
        //
        // ★미는 건 **펫 쪽만** 한다. 사람을 같이 밀면 걷는 손맛에 손이 얹히는 셈이라
        //   조작이 미끄러워진다 — 비켜서는 건 짐승의 일이다.
        var 주인 = Hero.Me;
        if (주인 != null)
        {
            var v = pos - 주인.transform.position; v.y = 0f;
            float need = (종.반지름 + 주인.Radius) * 0.9f;
            float d = v.magnitude;
            if (d < need - 여유 && d > 1e-3f)
                pos = Vector3.Lerp(pos, 주인.transform.position + v / d * need, 세기);
        }
        return pos;
    }

    // ── 감각: 앞쪽 시야각 안만 본다 (좀비와 달리 뒤통수엔 눈이 없다)
    IHittable 표적찾기()
    {
        IHittable best = null;
        float bd = 종.시야 * 종.시야;
        float cos = Mathf.Cos(Mathf.Deg2Rad * 종.시야각 * 0.5f);

        for (int i = 0; i < All.Count; i++)
        {
            var o = All[i];
            if (o == null || o == this || !o.Alive || o.side == side) continue;
            if (!보이나(o.transform.position, cos, ref bd)) continue;
            best = o;
        }

        if (side == Side.야생)
        {
            var h = Hero.Me;
            if (h != null && h.Alive)
            {
                // 뛰는 사람은 소리로 알아챈다 — 시야 밖이어도
                float d2 = (h.transform.position - transform.position).sqrMagnitude;
                bool 소리 = h.Running && d2 < 종.청각 * 종.청각;
                if (소리 && d2 < bd) { bd = d2; best = h; }
                else if (보이나(h.transform.position, cos, ref bd)) best = h;
            }
        }
        return best;
    }

    bool 보이나(Vector3 p, float cos, ref float bd)
    {
        var v = p - transform.position; v.y = 0f;
        float d2 = v.sqrMagnitude;
        if (d2 > bd) return false;
        if (d2 > 0.01f && Vector3.Dot(v.normalized, transform.forward) < cos) return false;
        bd = d2;
        return true;
    }

    // ★동작을 고르라고 밖에 알려 주는 것들 (`펫동작`) — 판단은 안 한다, 상태만 비춘다
    /// 맞아서 비틀거리는 중인가
    public bool 맞는중 => staggerT > 0f;
    /// 방금 휘둘렀나 — 공격 동작이 끝까지 보이도록 잠깐 켜 둔다
    public bool 때리는중 => 휘두름T > 0f;
    float 휘두름T;

    void 때리기()
    {
        if (atkCd > 0f) return;
        atkCd = 종.간격;
        // 즉발 피해가 아니라 **휘두름을 시작**한다 — 피해는 타격 시점에 (`휘두름진행`)
        var d = target != null ? target.T.position - transform.position : transform.forward;
        d.y = 0f;
        휘두름방향 = d.sqrMagnitude > 1e-4f ? d.normalized : transform.forward;
        var 몸짓기 = GetComponent<몸짓>();
        휘두름시간 = 몸짓기 != null ? Mathf.Clamp(몸짓기.공격클립길이(), 0.3f, 1.6f) : 0.55f;
        타격시점 = 휘두름시간 * 0.58f;
        휘두름t = 0f; 휘두름타격 = false;
        휘두름T = 휘두름시간;                          // `때리는중` 표시용 (`펫동작` 이 본다)
        squash = 0.7f;
    }

    // ── 맞기
    public void TakeDamage(float d) => TakeDamage(d, false);

    /// ★★★**버티기 — 맞아도 확률로 움츠러들지 않는다** (2026-08-11 사용자 "급을 나누지 말고
    ///   확률로 보정해서 맞아도 움츠러들지 않게끔").
    ///   전엔 맞으면 반드시 ①0.4초 경직 ②하던 공격 취소 ③뒤로 밀림이 걸렸다. 내 공격
    ///   사이클(0.87초)이 상대의 「경직 회복 + 다시 무는 시간」(약 1초)보다 짧아서,
    ///   **연타만 하면 상대가 영원히 한 대도 못 때렸다** — 그게 "전투가 쉽다"의 정체.
    ///   → 무게에 비례한 확률로 **피해만 받고 하던 일을 계속한다.** 물던 놈은 마저 문다.
    ///     다람쥐 0.1 · 늑대 0.22 · 랩터 0.3 · 사슴 0.5 · 티라 1.0 (급으로 자르지 않고 무게로 잇는다)
    ///   ☆버티면 뒤따르는 넉백(`Knock`)도 같이 무시한다 — 안 움츠러드는데 밀리면 반쪽이다.
    ///   ☆**밀기(우클릭)는 안 굴린다** — 밀기는 둘러싸였을 때의 탈출 수단이라 안 통하는
    ///     수가 있으면 안 된다 (기절값도 그대로 쌓인다 — 생포 루트는 안 건드린다).
    public void TakeDamage(float d, bool 버틸수있나)
    {
        if (!Alive) return;
        hp -= d;

        if (버틸수있나 && Random.value < Mathf.Clamp01(종.무게 / Mathf.Max(0.1f, 버팀무게)))
        {
            버팀시각 = Time.time;
            if (무리 != null) 무리.다쳤다(this, 새끼);
            if (hp <= 0f) Die();
            return;
        }
        // ★눌림(squash)은 안 건다 — 맞는 동작은 저작된 `피격` 클립의 몫이다

        // ★★**맞으면 맞는 게 보여야 전투다** (2026-08-07 사용자 — 좀보이드 참고).
        //   ①피격 모션 (저작된 「피격」 클립을 잠깐 튼다) ②짧은 경직 — 이 0.22초가
        //   「때리면 상대가 멈칫한다」는 컨트롤의 최소 단위다. 없으면 서로 딜만 교환한다.
        // ★경직은 피격 모션의 **버팀 구간까지**만 (클립 0.90초의 45%). 회복 구간에는
        //   다시 움직일 수 있어야 "아파하다 정신 차린다" 로 읽힌다 (2026-08-09).
        staggerT = Mathf.Max(staggerT, 몸짓.피격길이 * 0.45f);

        // ★★★**맞으면 실제로 뒤로 밀린다** (2026-08-09 사용자 "뒤로도 거리가 조금
        //   밀려나게, 크기에 따라 보정되게"). 클립이 그리는 뒷걸음질과 짝이다 —
        //   그림만 밀리고 자리가 그대로면 미끄러지는 것처럼 보인다.
        //   ☆거리는 **몸 크기(키)에 비례**하고 **무게로 나눈다** — 큰 놈은 보폭이 크고,
        //     무거운 놈은 덜 밀린다. 티라를 몽둥이로 밀어낼 수는 없다.
        //   ☆0.22 → 0.07 (2026-08-09 사용자 "밀려나는게 너무 심하고"). 클립도 같이 줄였다 —
        //     둘이 더해지던 걸 못 봤다.
        밀림 = -transform.forward * (종.키 * 0.07f / Mathf.Max(0.2f, 종.무게));
        밀림남은 = 몸짓.피격길이 * 0.60f;      // 클립의 충격+버팀 구간 동안만 밀린다
        휘두름t = -1f;                                  // 맞으면 휘두르던 것도 끊긴다
        var 몸짓기 = GetComponent<몸짓>();
        if (몸짓기 != null) 몸짓기.맞았다();

        if (무리 != null) 무리.다쳤다(this, 새끼);
        if (hp <= 0f) Die();
    }

    /// 밀림 — **무게가 클수록 안 밀린다.** 티라를 밀어 넘어뜨릴 수는 없다
    public void Knock(Vector3 dir, float dist, float stagger, float down = 0f)
    {
        if (!Alive) return;
        if (Time.time - 버팀시각 < 0.05f) return;   // 이번 타를 버텼다 — 밀리지도 비틀거리지도 않는다

        // ★★★**큰 놈이 너무 밀렸다** (2026-08-10 사용자 — "큰데도 밀려나는게 심한것들도 많고").
        //
        //   전에는 `저항 = 무게` 를 그냥 나눴다. 두 가지가 잘못이었다:
        //    ① 무게가 1 보다 작은 놈(다람쥐 0.72)은 저항이 1 미만이라 **오히려 더 날아갔다**
        //    ② 무게에 비례만 해서, 세 배 무거운 놈이 3분의 1 밀리는 데 그쳤다 —
        //       덩치가 세 배면 꿈쩍도 안 해야 「크다」로 읽힌다
        //   → **1 아래로는 안 내려가게 막고, 무게에 지수를 준다.**
        //     실측(넉백 0.6m): 다람쥐 0.60 · 늑대 0.47 · 사슴 0.19 · 트리케 0.13 · 티라노 0.03
        float 저항 = Mathf.Max(1f, Mathf.Pow(Mathf.Max(0.2f, 종.무게), 밀림지수));
        dist /= 저항; stagger /= 저항;
        down = 종.무게 > 3f ? 0f : down / 저항;      // 무거운 놈은 아예 안 넘어진다

        dir.y = 0f;
        if (dir.sqrMagnitude > 1e-4f && dist > 0.01f)
        {
            var p = Blocker.Resolve(transform.position + dir.normalized * dist, 종.반지름);
            p.y = 땅격자.걷는높이(p.x, p.z);
            transform.position = p;
        }
        staggerT = Mathf.Max(staggerT, stagger);
        if (down > downT)
        {
            downT = down;
            downTotal = down;
            // 밀린 쪽으로 자빠진다 — 왼쪽에서 맞으면 오른쪽으로 넘어간다
            downSign = Vector3.Dot(transform.right, dir) >= 0f ? 1f : -1f;
        }
        squash = 1f;
    }

    /// ★죽는다고 고기가 생기지 않는다 — **사체가 남고, 갈무리해야 고기가 나온다.**
    ///   (2026-08-03 사용자: "모든 행동에는 인과관계와 행동이 있어야 한다")
    void Die()
    {
        if (무리 != null) 무리.죽었다(새끼);

        // ★야생은 그 자리에서 **사체로 전환**한다 (2026-08-07). `Destroy` 를 먼저 하면
        //   저작된 `죽음` 모션(`몸짓`)이 돌 틈이 없다 — 죽는 모션이 눕히고, 누운 채 굳고,
        //   그 몸이 곧 사체다. 이 컴포넌트만 끄면 뇌는 멈추고 몸은 남는다.
        if (side == Side.야생)
        {
            // ★죽는 순간의 임시 자세(넘어짐 기울기·눌림·공중)를 먼저 걷어낸다 —
            //   안 걷어내면 그 자세 위에 죽음 클립이 얹혀 시체가 떠서 굳는다 (사슴 실사례)
            downT = 0f; staggerT = 0f; squash = 0f;
            if (body != null) 몸복구();
            var 자리 = transform.position; 자리.y = 땅격자.걷는높이(자리.x, 자리.z); transform.position = 자리;

            // ★★★**죽으면 튕겨나간다** (2026-08-09 사용자 "그자리에서 쓱 눕는게 아니라
            //   튕겨나가서 퍽"). 클립이 그리는 포물선과 짝이다 — 그림만 날고 자리가
            //   그대로면 제자리에서 허우적대는 걸로 보인다.
            //   ☆거리는 피격의 세 배. 크기에 비례하고 무게로 나눈다 (티라는 거의 안 날아간다).
            //   ☆클립의 착지(55%)까지만 밀린다 — 땅에 부딪힌 뒤엔 안 미끄러진다.
            //   ★이 컴포넌트는 곧 `enabled = false` 라 `Update` 가 안 돈다 →
            //     **사체(`Carcass`)에게 맡긴다.** 걔는 계속 살아 있다.
            var 튕김 = -transform.forward * (종.키 * 0.21f / Mathf.Max(0.2f, 종.무게));

            Carcass.전환(this, Mathf.Max(1, Mathf.RoundToInt(종.체력 / 22f)));
            var car = GetComponent<Carcass>();
            if (car != null) car.튕겨내기(튕김, 1.4f * 0.55f);
            enabled = false;                    // OnDisable 이 All 에서도 빼 준다 (표적에서 사라짐)
            return;
        }
        Destroy(gameObject);
    }

    /// ★넘어짐 — **납작하게 누르지 않는다.** 옆으로 자빠져서 바둥거리다 일어난다
    /// (2026-08-03 사용자: "짜부되는 건 좀 아닌 거 같고, 뒤집어지거나 옆으로 눕게 해서
    ///  바둥바둥하는 게 맞다"). 눌리는 건 만화 표현이지 넘어진 게 아니다.
    ///
    ///   자빠짐(0.18초) → 바둥바둥 → 일어남(0.35초)
    void 넘어진몸()
    {
        if (body == null) return;
        body.localScale = bodyScale;                 // 눌림은 쓰지 않는다

        const float 자빠짐 = 0.18f, 일어남 = 0.35f;
        float 누움;                                   // 0 = 서 있음 · 1 = 완전히 누움
        float 지난 = downTotal - downT;

        if (지난 < 자빠짐) 누움 = Mathf.SmoothStep(0f, 1f, 지난 / 자빠짐);
        else if (downT < 일어남) 누움 = Mathf.SmoothStep(0f, 1f, downT / 일어남);
        else 누움 = 1f;

        // 바둥바둥 — 누워 있는 동안만. 다리를 못 젓는 대신 몸을 흔든다
        float 바둥 = 누움 > 0.7f ? Mathf.Sin(Time.time * 13f + GetInstanceID()) * 11f * 누움 : 0f;
        float 들썩 = 누움 > 0.7f ? Mathf.Abs(Mathf.Sin(Time.time * 6.5f)) * 0.06f * 종.키 : 0f;

        body.localRotation = bodyRot * Quaternion.Euler(바둥 * 0.5f, 0f, (86f * 누움 + 바둥) * downSign);
        // 옆으로 누우면 무게중심이 내려간다
        body.localPosition = bodyPos - Vector3.up * (bodyPos.y * 0.55f * 누움) + Vector3.up * 들썩;
    }

    /// ★그로기 — **엎어져서 낑낑댄다** (2026-08-10 사용자).
    ///
    ///   ☆넘어짐(`넘어진몸`)과 갈라 놓는다: 넘어짐은 **옆으로** 자빠져 바둥거리고,
    ///     기절은 **앞으로** 엎어져 코를 박고 들썩인다. 둘이 같으면 무엇에 당했는지 못 읽는다.
    ///   ☆낑낑대는 건 큰 몸짓이 아니라 **잔 떨림**이다 — 기운이 빠져 못 일어나는 것이라
    ///     크게 움직이면 오히려 멀쩡해 보인다.
    void 그로기몸()
    {
        if (body == null) return;
        body.localScale = bodyScale;

        const float 엎어지는데 = 0.28f;
        float 엎 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(기절든지 / 엎어지는데));

        // 깨어날 때가 가까우면 들썩임이 커진다 — 곧 일어난다는 예고
        float 깰때쯤 = 1f - Mathf.Clamp01(기절치 / Mathf.Max(1f, 기절한계));
        float 낑 = Mathf.Sin(Time.time * (6.5f + 깰때쯤 * 5f) + GetInstanceID());
        float 폭 = (0.35f + 깰때쯤 * 0.65f) * 엎;

        // 앞으로 코를 박는다 (x 축으로 78°) + 잔 떨림
        body.localRotation = bodyRot * Quaternion.Euler(78f * 엎 + 낑 * 3.2f * 폭,
                                                        낑 * 5f * 폭,
                                                        낑 * 3f * 폭);
        // 엎어지면 무게중심이 내려가고, 숨 쉬듯 조금 들썩인다
        float 들썩 = Mathf.Abs(Mathf.Sin(Time.time * 3.4f)) * 0.035f * 종.키 * 엎;
        body.localPosition = bodyPos - Vector3.up * (bodyPos.y * 0.62f * 엎) + Vector3.up * 들썩;
    }

    void 몸복구()
    {
        if (body == null) return;
        body.localScale = bodyScale;
        body.localPosition = bodyPos;
        body.localRotation = bodyRot;
    }

    /// ★★★**스케일 눌림은 안 쓴다** (2026-08-09 사용자 "팻 쳐맞는 모션 만들고 왜 이상한걸
    ///   쓰냐"). 맞는 동작은 저작된 **`피격` 클립**이 그린다 (`몸짓.맞았다()`).
    ///   그 위에 몸을 가로 1.3배·세로 0.77배로 눌렀다 펴는 걸 겹치니 꿈틀거렸다.
    ///   ☆같은 이유로 2026-08-03 에 넘어짐의 눌림도 걷어냈다 — *"짜부되는 건 좀 아닌 거 같고"*.
    ///   ☆몸 크기는 건드리지 않고 원래 값으로 유지만 한다.
    void Squash(float dt)
    {
        if (body == null) return;
        // 놀란 몸짓이 몸을 쥐고 있는 동안은 되돌림이 덮어쓰지 않는다 (`놀람동작` 참고)
        if (지금 == 상태.불놀람) return;
        squash = 0f;
        if (body.localScale != bodyScale) body.localScale = bodyScale;
    }

    // ══════════════════════════════════════════════════════════
    //  ★방향은 16칸으로 — **결과를 밀지 말고 목표를 끊는다** (2026-08-04)
    //
    //  처음엔 다 돌아간 각도를 `PixelSnapper` 가 매 프레임 격자로 밀었다. 그런데 펫은
    //  목표를 향해 **조금씩** 도는 방식이라, 밀어 놓으면 다음 프레임에 다시 목표 쪽으로
    //  조금 돌고, 또 밀리고… **진자처럼 흔들렸다** (사용자 "엄청나게 헤드뱅잉").
    //
    //  → 애초에 **바라볼 각도 자체를 16칸 중 하나로 고르고 즉시 그쪽을 본다.**
    //    싸울 상대가 없으니 흔들릴 일이 없고, 옛 도트 게임의 뚝뚝 끊기는 방향 전환이 된다.
    //  → 경계에서 두 칸을 오가지 않게 **버티기**를 둔다 (한 칸의 60% 넘게 벗어나야 넘어간다).
    // ══════════════════════════════════════════════════════════
    // ★★따라다니는 펫은 **저마다 자기 자리**가 있어야 한다 (2026-08-04 사용자
    //   "비비면서 존나 떤다"). 전에는 셋 다 **주인의 똑같은 한 점**을 목표로 삼았다:
    //   서로 겹치면 밀려나고, 밀려나면 또 그 점으로 가고 — 영원히 떤다.
    //   밀어내는 세기를 줄여도 원인이 그대로라 안 없어진다.
    //   → 주인 뒤쪽으로 부챗살 자리를 하나씩 나눠 준다. 자기 자리에 서면 더 안 움직인다.
    static int 자리번호;
    int 내번호 = -1;

    Vector3 내자리
    {
        get
        {
            if (owner == null) return transform.position;
            if (내번호 < 0) 내번호 = 자리번호++;

            // 주인 뒤쪽 반원에 부챗살로 — 번호가 커질수록 옆·뒤로
            float a = (내번호 % 6) * 52f - 130f;
            float r = 2.0f + (내번호 / 6) * 1.5f + 종.반지름 * 2f;
            var dir = Quaternion.Euler(0f, owner.eulerAngles.y + a, 0f) * Vector3.back;
            return owner.position + dir * r;
        }
    }

    // ★16칸 끊기는 **픽셀 화면과 함께 은퇴** (2026-08-07 사용자 "뚝 도는 게 아니라
    //   자연스럽게 회전해서 전환"). 픽셀이 있을 땐 끊는 게 그림체였지만, 걷어낸 지금은
    //   스냅이 그냥 버그로 보인다. → **정해진 속도로 돌아간다.** 큰 몸일수록 천천히 돈다
    //   (다람쥐는 홱, 티라노는 무겁게 — 몸무게가 회전에서 읽힌다).
    public static int 방향수 = 16;   // (은퇴 — 픽셀 화면을 되살리면 다시 쓴다)
    float 본각도; bool 각도있음;

    void Face(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) return;
        float want = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        if (!각도있음) { 본각도 = want; 각도있음 = true; }
        else
        {
            // 키 1m(다람쥐) ≈ 720°/s · 키 5m(티라노) ≈ 240°/s
            float 빠르기 = Mathf.Lerp(720f, 240f, Mathf.InverseLerp(1f, 5f, 종.키));
            본각도 = Mathf.MoveTowardsAngle(본각도, want, 빠르기 * Time.deltaTime);
        }
        transform.rotation = Quaternion.Euler(0f, 본각도, 0f);
    }

    static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }
}
