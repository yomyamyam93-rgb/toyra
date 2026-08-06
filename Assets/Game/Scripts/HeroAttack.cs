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
/// ★**누르면 들고, 떼면 휘두른다** (2026-08-04). 누르는 시간이 곧 예비 동작이다.
///
/// ★실행 순서 300 — 애니메이터(0)·`HeroHold`(200) 가 팔을 다 돌린 **뒤에** 몽둥이를 쥐여 놓는다.
[RequireComponent(typeof(Hero))]
[DefaultExecutionOrder(300)]
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
    // ★★110° → 70° (2026-08-04). 팔이 **머리 위에서 앞으로 내려찍는** 동작이 되면서
    //   판정도 앞쪽으로 좁혀야 그림과 맞는다. 110° 는 옆으로 쓸던 시절의 값이라,
    //   시작 순간 **왼쪽 55°에 있는 놈**부터 맞아서 "앞을 안 때린다" 로 읽혔다.
    [Tooltip("쓸고 지나가는 각도 (°) — 앞을 내려찍는 동작이라 좁다")] public float 각도 = 70f;
    [Tooltip("손목이 젖혔다 눕는 정도 (°) — 몽둥이 끝의 궤적")] public float 손목 = 22f;
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

    // ★★몽둥이는 **팔에 맞춰 잡는다** (2026-08-04 사용자 "몽둥이만 한 번 잘 잡아들 수 있게").
    //   전엔 손뼈 밑에 각도 0 · 위치 0 으로 매달아서, 막대 **한가운데**가 손에 박히고
    //   방향은 뼈의 축(리그마다 다르다)이 정했다 — 반쪽이 손등 뒤로 삐져나왔다.
    //   이제 **팔뚝→손 방향**을 축으로 삼는다. 뼈 이름·축이 어떻든 팔이 가리키는 쪽이라
    //   리그에 안 휘둘리고, 팔을 들면 몽둥이도 따라 들린다.
    [Header("몽둥이 (한손)")]
    [Tooltip("길이 (m)")] public float 길이 = 0.8f;
    [Tooltip("굵기 (m)")] public float 굵기 = 0.06f;
    // ★★쥐는 자리를 **3축으로** 연다 (2026-08-05 사용자 "몽둥이도 손에 제대로 안들려있네,
    //   이거는 내가 위치조절이 되나?"). 전엔 「앞으로 얼마나」 한 축뿐이라, 막대가 손바닥을
    //   벗어나 떠 있어도 당겨 넣을 방법이 없었다.
    //   ★기준은 **막대 자신의 축**이다: z = 막대가 뻗는 쪽 · x = 손등 좌우 · y = 위아래.
    //     z 를 줄이면 손잡이 끝을 쥐고, 늘리면 가운데를 쥔다.
    [Tooltip("손에서 얼마나 떨어져 쥐나 (m) — z=막대 방향 · x=좌우 · y=위아래")]
    public Vector3 쥔자리 = new Vector3(0f, 0f, 0.3f);
    [Tooltip("팔 방향에서 더 기울이는 각도 (°) — x 를 음수로 주면 끝이 위로 선다")]
    public Vector3 기울임 = new Vector3(-25f, 0f, 0f);

    // ★★**모델을 끼울 수 있게 한다** (2026-08-06 사용자 "모델링에 붙은 무기나 장비 위치
    //   조절 좀 가능하게 해줄래?"). 전에는 무기가 **회색 상자 고정**이라, 모델을 넣어도
    //   `localScale = (굵기,굵기,길이)` 가 그 모델을 납작하게 눌러 버렸다.
    //   ☆구조: **겉(`무기`)은 코드가** 손에 맞춰 놓고, **안쪽 모델만 오프셋으로** 맞춘다.
    //     소유가 갈려 있어야 둘이 안 싸운다 (CLAUDE.md 「소유권」).
    //   ☆값은 **플레이 중에 만져도 즉시 보인다** — 매 프레임 다시 얹는다.
    [Header("무기 모델 (비우면 회색 상자)")]
    [Tooltip("손에 들 모델 — 넣으면 상자 대신 이게 나온다")] public GameObject 무기모델;
    [Tooltip("모델만 더 밀어 넣기 (m) — 손 안으로 당길 때")] public Vector3 모델위치;
    [Tooltip("모델만 더 돌리기 (°) — 칼날 방향 맞출 때")] public Vector3 모델회전;
    [Tooltip("모델 크기 배수")] public float 모델크기 = 1f;

    [Header("느낌")]
    [Tooltip("맞히는 순간 아주 짧게 멈춘다 (초) — 0 이면 안 씀")] public float 히트스톱 = 0.045f;

    Hero hero;
    HeroHold 드는자세;
    Transform 무기, 몸, 손뼈, 팔뚝뼈, 모델인스턴스;
    float 스윙yaw, 스윙pitch;
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

    /// 손에 든 것 — 지금은 막대 하나.
    /// ★뼈 밑에 매달지 않는다. 자세는 매 프레임 `무기자세()` 가 손 위치에 맞춰 놓는다 —
    ///   뼈에 붙이면 뼈의 축과 크기(비균등일 수 있다)에 막대가 휘둘린다.
    void MakeWeapon()
    {
        무기 = transform.Find("무기");
        손뼈 = 뼈찾기("hand", "wrist");
        팔뚝뼈 = 뼈찾기("forearm", "lowerarm");
        if (무기 != null) return;

        var g = Grey.Box(transform, Vector3.zero, new Vector3(굵기, 굵기, 길이),
                         new Color(0.62f, 0.5f, 0.35f), "무기");
        무기 = g.transform;
    }

    /// 무기 모델 손질 — 모델이 꽂혀 있으면 상자를 감추고 모델을 얹는다.
    /// ★매 프레임 부른다. 인스펙터 값을 만지면 **플레이 중에도 즉시** 보이게 하려는 것이고,
    ///   비용은 트랜스폼 세 줄이라 사실상 0 이다.
    void 모델손질()
    {
        if (무기 == null) return;

        if (무기모델 == null)
        {
            if (모델인스턴스 != null) { Destroy(모델인스턴스.gameObject); 모델인스턴스 = null; }
            var mr0 = 무기.GetComponent<MeshRenderer>();
            if (mr0 != null && !mr0.enabled) mr0.enabled = true;      // 상자를 되살린다
            return;
        }

        if (모델인스턴스 == null || 모델인스턴스.gameObject == null)
        {
            var inst = Instantiate(무기모델, 무기);
            inst.name = "무기모델";
            모델인스턴스 = inst.transform;
            // ★상자는 **지우지 않고 감춘다** — 길이·굵기가 판정(`쓸고 지나간 각도`)의 기준이다
            var mr = 무기.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
        }

        // 겉(무기)이 손에 맞춰 놓이고, 안쪽 모델은 여기서만 움직인다.
        // ★겉의 크기가 (굵기,굵기,길이)로 눌려 있으므로 그걸 되돌려 곱한다 —
        //   안 그러면 모델이 납작해진다 (그게 옛 버그였다).
        모델인스턴스.localPosition = new Vector3(모델위치.x / Mathf.Max(1e-4f, 굵기),
                                             모델위치.y / Mathf.Max(1e-4f, 굵기),
                                             모델위치.z / Mathf.Max(1e-4f, 길이));
        모델인스턴스.localRotation = Quaternion.Euler(모델회전);
        모델인스턴스.localScale = new Vector3(모델크기 / Mathf.Max(1e-4f, 굵기),
                                          모델크기 / Mathf.Max(1e-4f, 굵기),
                                          모델크기 / Mathf.Max(1e-4f, 길이));
    }

    /// 이름에 낱말이 든 **오른쪽** 뼈를 찾는다 (없으면 null)
    Transform 뼈찾기(params string[] 낱말)
    {
        Transform 후보 = null;
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            var n = t.name.ToLower();
            bool 맞나 = false;
            foreach (var w in 낱말) if (n.Contains(w)) { 맞나 = true; break; }
            if (!맞나) continue;
            if (n.Contains("left") || n.EndsWith(".l") || n.Contains("_l")) continue;
            후보 = t;
            if (n.Contains("right") || n.EndsWith(".r") || n.Contains("_r")) break;
        }
        return 후보;
    }

    /// 몽둥이를 손에 쥐여 놓는다 — **팔뚝에서 손으로 뻗는 방향**이 막대의 축이다.
    /// 애니메이터와 `HeroHold` 가 팔을 다 돌린 뒤에 부른다 (실행 순서 300).
    void 무기자세()
    {
        if (무기 == null) return;

        Vector3 축 = 손뼈 != null && 팔뚝뼈 != null ? 손뼈.position - 팔뚝뼈.position : transform.forward;
        if (축.sqrMagnitude < 1e-6f) 축 = transform.forward;

        // ★★`LookRotation(축, Vector3.up)` 을 쓰면 안 된다 (2026-08-04 사용자 "무기가
        //   팔랑거리기도 하고"). 팔을 들면 축이 **거의 수직**이 되는데, 그때 위쪽 기준이
        //   축과 나란해져 회전이 정의되지 않는다 — 매 프레임 롤이 홱홱 뒤집히고,
        //   그 롤 위에 얹힌 `기울임` 이 같이 돌아 몽둥이가 팔랑거렸다.
        //   FromToRotation 은 **몸이 선 자세에서 최소로 비틀어** 축에 맞추므로 뒤집힘이 없다.
        var 기본 = Quaternion.FromToRotation(transform.forward, 축.normalized) * transform.rotation
                 * Quaternion.Euler(기울임);
        // 휘두름 — 가로로 쓸고(세상 기준 yaw) 앞뒤로 눕는다(막대 자기 축 기준 pitch)
        var 회전 = Quaternion.AngleAxis(스윙yaw, Vector3.up) * 기본 * Quaternion.Euler(스윙pitch, 0f, 0f);

        var 손 = 손뼈 != null ? 손뼈.position
                             : transform.TransformPoint(new Vector3(0.3f, 1.15f, 0.15f));
        무기.SetPositionAndRotation(손 + 회전 * 쥔자리, 회전);
        무기.localScale = new Vector3(굵기, 굵기, 길이);
        모델손질();          // 모델이 꽂혀 있으면 그 안쪽 자리를 맞춘다 (플레이 중 조절 가능)
    }

    void Update()
    {
        float dt = Time.deltaTime;
        cd -= dt; 밀기cd -= dt;

        if (Time.unscaledTime < stopUntil) return;   // 히트스톱

        if (!hero.Alive) { hero.MoveMul = 1f; return; }

        ReadClick(out bool 좌, out bool 우, out bool 뗌);

        // ★★들었다가 **놓는 순간** 휘두른다 (2026-08-04 사용자 "마우스 떼면 휘두르는 것까지").
        //   누르는 동안은 팔이 올라가 있고(예비), 떼면 내려친다. 누르는 시간이 곧
        //   예비 동작이라 「얼마나 감았나」가 손에 남는다 — 연타로는 안 되는 무게가 생긴다.
        //   ※휘두르는 동안은 팔이 **내려가야** 한다. 그 내려감 자체가 내려치는 동작이다.
        if (드는자세 == null) 드는자세 = GetComponent<HeroHold>();
        if (드는자세 != null)
        {
            // 드는 것 — 누르는 동안과 예비·휘두름. 여운에 풀리면서 평소 걷기로 돌아간다
            드는자세.목표 = (좌 || state == State.예비 || state == State.휘두름) ? 1f : 0f;
            // 내려치는 것 — 휘두르는 동안 위 → 앞아래로. **가속**해야 내려치는 힘이 보인다
            float u = state == State.휘두름 ? Mathf.Clamp01(t / Mathf.Max(0.01f, 휘두름)) : 0f;
            드는자세.침 = state == State.휘두름 ? u * u
                        : state == State.여운 ? 1f : 0f;
        }

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
                if (뗌 && cd <= 0f && hero.stamina >= 지구력소모)
                {
                    hero.stamina -= 지구력소모;
                    맞은것.Clear();
                    캤나 = false;              // 이번 휘두름에서 아직 안 캤다
                    t = 0f;
                    // ★이미 팔이 올라가 있으면 예비를 건너뛴다 — 감아 둔 사람에게
                    //   또 0.14초를 기다리게 하면 "떼면 나간다" 가 아니라 "떼면 늦게 나간다" 가 된다.
                    //   딸깍(누르자마자 떼기)은 아직 안 올라갔으니 예비를 거친다.
                    bool 감았나 = 드는자세 != null && 드는자세.지금 > 0.7f;
                    if (감았나) { state = State.휘두름; sweptFrom = -각도 * 0.5f; }
                    else state = State.예비;
                }
                break;

            // ★휘두르는 동안 발을 묶지 않는다 (2026-08-04 사용자 "느려지지도 말고,
            //   그냥 정상적인 걷기이면서 동작만"). 예전엔 세 상태가 모두 이속을 35% 로
            //   깎았는데, 좌클릭을 **누르고 있으면 이 세 상태가 계속 돌아** 드는 내내
            //   35% 였다. 그러면 실제 속도가 2.6 → 0.91m/s 로 떨어지고 `HeroAnim` 의
            //   빠르기가 0.35 가 되어 **정지와 걷기를 섞은 어정쩡한 반걸음**이 나온다.
            //   "손을 들면 걸음걸이가 이상해진다" 의 정체가 이것이었다 — 팔 자세(HeroHold)
            //   는 다리를 건드린 적이 없다.
            case State.예비:
                t += dt;
                Pose(-1f, Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, 예비)));
                if (t >= 예비) { state = State.휘두름; t = 0f; sweptFrom = -각도 * 0.5f; }
                break;

            case State.휘두름:
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
        // ★자세를 여기서 쓰지 않고 값만 남긴다 — 실제로 놓는 것은 `무기자세()` 다.
        //   팔이 다 돌아간 뒤에 놓아야 손을 안 벗어난다
        // ★dir 0 = 원위치. 이걸 안 갈라내면 휘두름 시작값(-55°)이 그대로 남아,
        //   평소에도 몽둥이가 옆으로 55° 틀어진 채 걸어다닌다
        if (dir == 0f)
        {
            스윙yaw = 0f; 스윙pitch = 0f;
            if (몸 != null) 몸.localRotation = Quaternion.identity;
            return;
        }
        // ★★★몽둥이를 **따로** 휘두르지 않는다 (2026-08-04 사용자 "때리는 모션이 맞긴 한데
        //   정확히 앞부분을 때리질 않네, 몽둥이 궤적이 좀..").
        //
        //   전엔 팔이 안 움직여서 **몽둥이만** 세상 기준 yaw 로 ±55° 옆으로 쓸었다.
        //   그런데 이제 `HeroHold` 가 팔을 오른쪽 머리 위 → 앞아래로 실제로 내려친다.
        //   그 위에 옆쓸기까지 얹으니 **두 동작이 겹쳐** 궤적이 옆으로 밀리고 앞을 못 때렸다.
        //   → 몽둥이는 이제 **손을 따라가기만** 한다. 휘두르는 것은 팔의 몫이다.
        //   ★남긴 pitch 는 손목이다 — 감을 때 끝을 세우고 칠 때 눕힌다. 옆으로는 안 민다.
        스윙yaw = 0f;
        스윙pitch = dir < 0f ? -손목 * k : Mathf.Lerp(-손목, 손목 * 0.6f, k);

        // 몸은 `HeroHold` 가 골반·척추로 돌린다 (여기서 또 돌리면 두 번 돈다)
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
        스윙yaw = 0f; 스윙pitch = 0f;
    }

    void ReadClick(out bool 좌, out bool 우, out bool 뗌)
    {
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        좌 = m != null && m.leftButton.isPressed;
        우 = m != null && m.rightButton.wasPressedThisFrame;
        뗌 = m != null && m.leftButton.wasReleasedThisFrame;
#else
        좌 = Input.GetMouseButton(0);
        우 = Input.GetMouseButtonDown(1);
        뗌 = Input.GetMouseButtonUp(0);
#endif
    }

    /// 캐기는 휘두름이 끝나는 순간 한 번 — 공격과 같은 동작을 쓴다
    void LateUpdate()
    {
        무기자세();
        if (state != State.휘두름 || t < 휘두름 * 0.5f || 캤나) return;
        캤나 = true;
        if (맞은것.Count == 0) Harvest.TryHarvest(transform.position, hero.LookDir, 사거리 + 0.4f);
    }
    bool 캤나;
    void OnDisable() { 캤나 = false; }
}
