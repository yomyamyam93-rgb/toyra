using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// 사람의 손 — **좀보이드식 3막 공격.**
///
/// ★클릭하는 순간 피해가 들어가면 손맛이 없다. 실제 몸이 하는 일을 따라가야 한다:
///   **예비(뒤로 감음) → 휘두름(이때만 판정) → 여운(되돌아옴).**
///   판정은 무기가 **쓸고 지나간 각도**로 나간다 — 그림과 판정이 같아야 읽힌다.
///
/// ★한 번 휘두를 때 한 놈을 두 번 때리지 않는다. 쓸면서 지나간 놈만 한 번씩.
///
/// ★밀기(우클릭)는 좀보이드 기본기다. 피해는 거의 없고 **넘어뜨려 공간을 만든다** —
///   떼에 둘러싸였을 때 빠져나가는 유일한 수단이자, 산 채로 잡을 때 쓰는 손이기도 하다.
///
/// ★휘두르는 동안 발이 느려진다. 안 그러면 "달리며 계속 휘두르기"가 정답이 된다.
[RequireComponent(typeof(Hero))]
public class HeroAttack : MonoBehaviour
{
    enum State { 쉼, 예비, 휘두름, 여운 }

    [Header("때리기 — 3막")]
    [Tooltip("예비 동작 (초) — 뒤로 감는 시간")] public float 예비 = 0.14f;
    [Tooltip("휘두르는 시간 (초) — 이 동안만 판정이 나간다")] public float 휘두름 = 0.16f;
    [Tooltip("여운 (초) — 되돌아오는 시간. 이 동안 다음 공격 못 함")] public float 여운 = 0.22f;

    [Header("위력")]
    public float 피해 = 14f;
    [Tooltip("닿는 거리 (m)")] public float 사거리 = 2.2f;
    [Tooltip("쓸고 지나가는 각도 (°)")] public float 각도 = 110f;
    [Tooltip("맞으면 이만큼 밀린다 (m)")] public float 넉백 = 0.6f;
    [Tooltip("맞으면 이만큼 비틀거린다 (초)")] public float 비틀 = 0.35f;
    [Tooltip("한 번 휘두를 때 쓰는 지구력")] public float 지구력소모 = 6f;

    [Header("밀기 (우클릭)")]
    public float 밀기피해 = 2f;
    public float 밀기사거리 = 1.9f;
    public float 밀기각도 = 90f;
    [Tooltip("밀려나는 거리 (m)")] public float 밀기넉백 = 2.2f;
    [Tooltip("넘어져 있는 시간 (초)")] public float 넘어짐 = 1.4f;
    public float 밀기소모 = 9f;
    public float 밀기쿨 = 0.7f;

    [Header("느낌")]
    [Tooltip("휘두르는 동안 이동 속도 배수")] [Range(0.1f, 1f)] public float 휘두를때이속 = 0.35f;
    [Tooltip("맞히는 순간 아주 짧게 멈춘다 (초) — 0 이면 안 씀")] public float 히트스톱 = 0.045f;

    Hero hero;
    Transform 무기, 몸;
    State state = State.쉼;
    float t, cd, 밀기cd, sweptFrom;
    readonly List<Critter> 맞은것 = new List<Critter>();
    float stopUntil;

    void Awake()
    {
        hero = GetComponent<Hero>();
        몸 = transform.Find("몸");
        MakeWeapon();
    }

    /// 손에 든 것 — 지금은 상자. 캐릭터를 리깅하면 이 자리를 진짜 무기가 대신한다
    void MakeWeapon()
    {
        무기 = transform.Find("무기");
        if (무기 != null) return;
        var g = Grey.Box(transform, Vector3.zero, new Vector3(0.09f, 0.09f, 1.0f),
                         new Color(0.62f, 0.5f, 0.35f), "무기");
        무기 = g.transform;
        무기.SetParent(transform, false);
        무기.localPosition = new Vector3(0.32f, 1.15f, 0.35f);
        무기.localRotation = Quaternion.identity;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        cd -= dt; 밀기cd -= dt;

        if (Time.unscaledTime < stopUntil) return;   // 히트스톱

        if (!hero.Alive) { hero.MoveMul = 1f; return; }

        ReadClick(out bool 좌, out bool 우);

        // ── 밀기 (우클릭) — 언제든 끼어들 수 있다. 이게 탈출 수단이라서
        if (우 && 밀기cd <= 0f && hero.stamina >= 밀기소모)
        {
            밀기cd = 밀기쿨;
            hero.stamina -= 밀기소모;
            Shove();
        }

        // ── 상태 진행
        switch (state)
        {
            case State.쉼:
                hero.MoveMul = 1f;
                if (좌 && cd <= 0f && hero.stamina >= 지구력소모)
                {
                    state = State.예비; t = 0f;
                    hero.stamina -= 지구력소모;
                    맞은것.Clear();
                    캤나 = false;              // 이번 휘두름에서 아직 안 캤다
                }
                break;

            case State.예비:
                hero.MoveMul = 휘두를때이속;
                t += dt;
                Pose(-1f, Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, 예비)));
                if (t >= 예비) { state = State.휘두름; t = 0f; sweptFrom = -각도 * 0.5f; }
                break;

            case State.휘두름:
                hero.MoveMul = 휘두를때이속;
                t += dt;
                {
                    float u = Mathf.Clamp01(t / Mathf.Max(0.01f, 휘두름));
                    float now = Mathf.Lerp(-각도 * 0.5f, 각도 * 0.5f, u);
                    Sweep(sweptFrom, now);       // 지난 프레임부터 지금까지 쓸고 간 구간
                    sweptFrom = now;
                    Pose(1f, u);
                }
                if (t >= 휘두름) { state = State.여운; t = 0f; }
                break;

            case State.여운:
                hero.MoveMul = Mathf.Lerp(휘두를때이속, 1f, t / Mathf.Max(0.01f, 여운));
                t += dt;
                Pose(1f, 1f - Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, 여운)));
                if (t >= 여운) { state = State.쉼; cd = 0f; Pose(0f, 0f); }
                break;
        }
    }

    /// 무기와 몸의 자세 — 예비엔 뒤로 감고, 휘두르면 앞으로 지나간다
    /// dir: -1 = 감음 · +1 = 휘두름 / k: 0~1 진행도
    void Pose(float dir, float k)
    {
        if (무기 == null) return;
        float yaw = dir < 0f ? Mathf.Lerp(0f, -각도 * 0.55f, k)
                             : Mathf.Lerp(-각도 * 0.5f, 각도 * 0.5f, k);
        무기.localRotation = Quaternion.Euler(dir < 0f ? -20f * k : Mathf.Lerp(-20f, 25f, k), yaw, 0f);

        // 몸도 같이 쓴다 — 팔만 움직이면 나무토막처럼 보인다
        if (몸 != null)
            몸.localRotation = Quaternion.Euler(dir < 0f ? -6f * k : Mathf.Lerp(-6f, 9f, k),
                                                yaw * 0.25f, 0f);
    }

    /// a→b 각도 구간을 쓸면서, 그 안에 든 놈을 한 번씩 맞힌다
    void Sweep(float a, float b)
    {
        float lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
        var p = transform.position;
        var look = hero.LookDir;
        bool hit = false;

        for (int i = Critter.All.Count - 1; i >= 0; i--)
        {
            var c = Critter.All[i];
            if (c == null || !c.Alive || c.side != Critter.Side.야생) continue;
            if (맞은것.Contains(c)) continue;

            var v = c.transform.position - p; v.y = 0f;
            float d = v.magnitude;
            if (d > 사거리 + c.Radius) continue;

            // 내 시선을 0° 로 본 상대 각도
            float ang = Vector3.SignedAngle(look, d > 0.01f ? v / d : look, Vector3.up);
            if (ang < lo - 6f || ang > hi + 6f) continue;

            맞은것.Add(c);
            c.TakeDamage(피해);
            if (c.Alive) c.Knock(v, 넉백, 비틀);
            hit = true;
        }

        if (hit && 히트스톱 > 0f) stopUntil = Time.unscaledTime + 히트스톱;
    }

    /// 밀기 — 넘어뜨려 공간을 만든다. 피해는 거의 없다
    void Shove()
    {
        var p = transform.position;
        var look = hero.LookDir;
        float cos = Mathf.Cos(Mathf.Deg2Rad * 밀기각도 * 0.5f);

        for (int i = Critter.All.Count - 1; i >= 0; i--)
        {
            var c = Critter.All[i];
            if (c == null || !c.Alive || c.side != Critter.Side.야생) continue;
            var v = c.transform.position - p; v.y = 0f;
            float d = v.magnitude;
            if (d > 밀기사거리 + c.Radius) continue;
            if (d > 0.01f && Vector3.Dot(v / d, look) < cos) continue;

            c.TakeDamage(밀기피해);
            if (c.Alive) c.Knock(v, 밀기넉백, 0.3f, 넘어짐);
        }

        // 미는 몸짓 — 무기를 앞으로 쭉
        if (무기 != null) 무기.localRotation = Quaternion.Euler(0f, 0f, 0f);
    }

    void ReadClick(out bool 좌, out bool 우)
    {
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        좌 = m != null && m.leftButton.isPressed;
        우 = m != null && m.rightButton.wasPressedThisFrame;
#else
        좌 = Input.GetMouseButton(0);
        우 = Input.GetMouseButtonDown(1);
#endif
    }

    /// 캐기는 휘두름이 끝나는 순간 한 번 — 공격과 같은 동작을 쓴다
    void LateUpdate()
    {
        if (state != State.휘두름 || t < 휘두름 * 0.5f || 캤나) return;
        캤나 = true;
        if (맞은것.Count == 0) Harvest.TryHarvest(transform.position, hero.LookDir, 사거리 + 0.4f);
    }
    bool 캤나;
    void OnDisable() { 캤나 = false; }
}
