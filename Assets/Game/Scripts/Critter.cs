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
    public enum 상태 { 어슬렁, 경계, 접근, 공격, 도주, 복귀 }

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
    public bool 넘어짐 => downT > 0f;

    // ══════════════════════════════════════════════════════════
    //  생포 — **아이템으로 줍는 게 아니라 집까지 데려간다**
    // ══════════════════════════════════════════════════════════

    /// 지쳤나 — 이 상태여야 붙잡을 수 있다. 넘어져 있거나 체력이 바닥나거나
    public bool 지침 => Alive && side == Side.야생 && (넘어짐 || hp <= 종.체력 * 0.35f);

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

    public void 먹이받음()
    {
        신뢰 = Mathf.Min(100f, 신뢰 + 먹이값);
        squash = 0.8f;
        // 먹으면 기운이 돌아온다 — 굶어 죽는 시계가 되감긴다
        hp = Mathf.Min(종.체력, hp + 종.체력 * 0.35f);
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
        pos.y = 0f;
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

    Transform body;
    Vector3 bodyScale, bodyPos;
    Quaternion bodyRot;
    float downTotal, downSign = 1f;
    IHittable target;
    float findCd, atkCd, squash, wanderCd, stateT;
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
        }
        if (무리 != null && 무리.식구.Count > 0 && 무리.식구[0] == this) 대표 = true;
    }

    // ══════════════════════════════════════════════════════════
    void Update()
    {
        float dt = Time.deltaTime;
        if (대표 && 무리 != null) 무리.식힘(dt);

        // 붙잡혀 있는 동안은 스스로 판단하지 않는다 (사람이 끌고 간다)
        if (잡힘) { Squash(dt); return; }
        // 매여 있는 동안도 마찬가지 — 먹이를 기다린다
        if (묶임) { 묶인채(dt); Squash(dt); return; }

        // ── 막는 타이머가 먼저다. 넘어져 있으면 아무 판단도 안 한다
        if (downT > 0f)
        {
            downT -= dt;
            넘어진몸();
            if (downT <= 0f) 몸복구();
            return;
        }
        if (staggerT > 0f) { staggerT -= dt; Squash(dt); return; }

        atkCd -= dt; stateT += dt;

        findCd -= dt;
        if (findCd <= 0f) { findCd = 0.4f; target = 표적찾기(); }

        판단();
        행동(dt);
        Squash(dt);
    }

    // ── 어느 상태로 갈까
    void 판단()
    {
        float 두려움 = 두려움값();
        bool 적있음 = target != null && target.Alive;
        float d적 = 적있음 ? Flat(target.T.position, transform.position) : 999f;
        float d집 = Flat(집, transform.position);

        // 내 편은 겁내지 않는다 — 주인이 시킨 자리를 지킨다
        if (side == Side.내편)
        {
            지금상태(적있음 ? (d적 <= 때리는거리(target) ? 상태.공격 : 상태.접근)
                          : (owner != null && Flat(owner.position, transform.position) > 4f ? 상태.복귀 : 상태.어슬렁));
            return;
        }

        // ★영역 — 좀비와 갈리는 첫 자리. 벗어나면 포기하고 돌아간다
        if (d집 > 종.영역 && 지금 != 상태.도주)
        {
            지금상태(상태.복귀);
            return;
        }

        // ★두려움이 임계를 넘으면 달아난다
        if (적있음 && 두려움 > 0.62f) { 지금상태(상태.도주); return; }

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
                    지금상태(덤빌까(두려움) ? 상태.접근 : 상태.도주);
                break;

            case 상태.접근:
                if (!적있음) { 지금상태(상태.복귀); break; }
                if (d적 <= 때리는거리(target)) 지금상태(상태.공격);
                break;

            case 상태.공격:
                if (!적있음) { 지금상태(상태.복귀); break; }
                if (d적 > 때리는거리(target) * 1.25f) 지금상태(상태.접근);
                break;
        }
    }

    /// ★두려움 — 좀비에겐 없던 값. 이 하나가 동물을 동물답게 만든다
    float 두려움값()
    {
        float f = 종.겁;

        // 다칠수록 무섭다 (체력이 반 아래로 떨어지면 급히 오른다)
        float hp01 = hp / Mathf.Max(1f, 종.체력);
        f += (1f - hp01) * 0.8f;

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

    void 지금상태(상태 s) { if (지금 == s) return; 지금 = s; stateT = 0f; }

    // ── 상태대로 움직인다
    void 행동(float dt)
    {
        switch (지금)
        {
            case 상태.어슬렁:
                wanderCd -= dt;
                if (wanderCd <= 0f)
                {
                    wanderCd = Random.Range(2.5f, 6f);
                    var c = side == Side.내편 && owner != null ? owner.position : 집;
                    wander = c + new Vector3(Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f));
                }
                if (Flat(wander, transform.position) > 1f) 걷기(wander, dt, 0.45f);
                break;

            case 상태.경계:
                if (target != null) 바라보기(target.T.position, dt);   // 멈춰서 노려본다
                break;

            case 상태.접근:
                if (target != null) 걷기(target.T.position, dt, 1f);
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
                걷기(side == Side.내편 && owner != null ? owner.position : 집, dt, 0.9f);
                break;
        }
    }

    float 때리는거리(IHittable t) => 종.사거리 + 종.반지름 + (t != null ? t.Radius : 0f);

    void 걷기(Vector3 goal, float dt, float 배)
    {
        var d = goal - transform.position; d.y = 0f;
        if (d.sqrMagnitude < 1e-4f) return;
        d.Normalize();

        var pos = transform.position + d * 종.이속 * 배 * dt;
        pos = 서로밀기(pos);
        pos = Blocker.Resolve(pos, 종.반지름);
        pos.x = Mathf.Clamp(pos.x, 1f, WorldGrid.Size - 1f);
        pos.z = Mathf.Clamp(pos.z, 1f, WorldGrid.Size - 1f);
        pos.y = 0f;
        transform.position = pos;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d, Vector3.up), 10f * dt);
    }

    void 바라보기(Vector3 at, float dt)
    {
        var d = at - transform.position; d.y = 0f;
        if (d.sqrMagnitude < 1e-4f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(d.normalized, Vector3.up), 8f * dt);
    }

    Vector3 서로밀기(Vector3 pos)
    {
        for (int i = 0; i < All.Count; i++)
        {
            var o = All[i];
            if (o == this || o == null || !o.Alive) continue;
            var v = pos - o.transform.position; v.y = 0f;
            float need = (종.반지름 + o.종.반지름) * 0.9f;
            float d = v.magnitude;
            if (d < need && d > 1e-3f) pos = o.transform.position + v / d * need;
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

    void 때리기()
    {
        if (atkCd > 0f) return;
        atkCd = 종.간격;
        if (target != null && target.Alive) target.TakeDamage(종.피해);
        squash = 0.7f;
    }

    // ── 맞기
    public void TakeDamage(float d)
    {
        if (!Alive) return;
        hp -= d;
        squash = 1f;
        if (무리 != null) 무리.다쳤다(this, 새끼);
        if (hp <= 0f) Die();
    }

    /// 밀림 — **무게가 클수록 안 밀린다.** 티라를 밀어 넘어뜨릴 수는 없다
    public void Knock(Vector3 dir, float dist, float stagger, float down = 0f)
    {
        if (!Alive) return;
        float 저항 = Mathf.Max(0.2f, 종.무게);
        dist /= 저항; stagger /= 저항;
        down = 종.무게 > 3f ? 0f : down / 저항;      // 무거운 놈은 아예 안 넘어진다

        dir.y = 0f;
        if (dir.sqrMagnitude > 1e-4f && dist > 0.01f)
        {
            var p = Blocker.Resolve(transform.position + dir.normalized * dist, 종.반지름);
            p.y = 0f;
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
        if (side == Side.야생)
            Carcass.남기다(this, Mathf.Max(1, Mathf.RoundToInt(종.체력 / 22f)));
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

    void 몸복구()
    {
        if (body == null) return;
        body.localScale = bodyScale;
        body.localPosition = bodyPos;
        body.localRotation = bodyRot;
    }

    void Squash(float dt)
    {
        if (body == null) return;
        squash = Mathf.Max(0f, squash - dt * 4f);
        float k = 1f + squash * 0.3f;
        body.localScale = new Vector3(bodyScale.x * k, bodyScale.y / k, bodyScale.z * k);
    }

    static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }
}
