using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// 캐릭터 — **WASD 로 몸이 가고, 마우스로 몸이 본다.** (좀보이드 조작)
///
/// ★이 분리가 핵심이다. 적을 보면서 뒷걸음질 칠 수 있어야 한다.
///   방향은 **화면 기준**이다 — 카메라가 안 돌아가니(`IsoCam`) 위 키는 언제나 화면 위.
///
/// ★기본이 「걷기」다 (대부분의 게임은 기본이 달리기). 뛰려면 Shift 를 눌러야 하고,
///   뛰면 지구력을 쓴다. 이 하나로 게임의 템포가 정해진다.
///   걷기 1.4m/s = 사람의 실제 걷는 속도. 1440m 맵을 가로지르는 데 17분.
[DefaultExecutionOrder(0)]
public class Hero : MonoBehaviour, IHittable
{
    public static Hero Me { get; private set; }

    [Header("목숨")]
    public float maxHp = 100f;
    [HideInInspector] public float hp;

    public Transform T => transform;
    public float Radius => radius;
    public bool Alive => hp > 0f;

    /// 맞으면 잠깐 몸이 눌린다 — 맞은 걸 알아야 물러날 판단을 한다
    [HideInInspector] public float squash;

    public void TakeDamage(float d)
    {
        if (!Alive) return;
        hp -= d;
        squash = 1f;
        if (hp <= 0f) hp = 0f;
    }

    // ★현실 속도(걷기 1.4m/s)는 게임에서 답답하다 (2026-08-03 실측 — 사용자 "너무 느리다").
    //   카메라가 멀고 주변 시야가 없어서, 화면에서는 실제보다 훨씬 느리게 보인다.
    //   그래서 현실의 약 1.9배로 올렸다 — "빠릿한 걸음" 쯤. 달리기도 같은 비율로.
    [Header("속도 (m/s)")]
    [Tooltip("기본 — 빠릿하게 걷는 속도")] public float walk = 2.6f;
    [Tooltip("Shift — 달리기. 지구력을 쓴다")] public float run = 6f;
    [Tooltip("Ctrl — 살금살금")] public float sneak = 1.4f;
    // ★★★**가속과 감속을 갈랐다** (2026-08-06 사용자 "캐릭터 움직임이 살짝 딜레이되면서
    //   밀리는 느낌... 즉각적으로 반응 좀 하게").
    //   전에는 둘이 같은 `accel = 18` 이었다. 걷기 2.6m/s 에 붙는 데 0.14초, 달리기
    //   6m/s 에는 0.33초 — 그리고 **뗐을 때도 같은 시간만큼 미끄러진다.** 그 미끄러짐이
    //   "밀리는 느낌" 의 정체다.
    //   ☆손맛의 요령: **가속은 빠르게, 감속은 더 빠르게.** 서는 게 늦으면 조작이 무겁게
    //     느껴지고, 서는 게 빠르면 같은 속도인데도 즉각적으로 읽힌다.
    [Tooltip("클수록 즉시 최고 속도에 붙는다")] public float accel = 55f;
    [Tooltip("발을 뗐을 때 멈추는 빠르기 — 가속보다 커야 안 미끄러진다")] public float 감속 = 90f;
    [Tooltip("방향을 꺾을 때 속도가 돌아가는 빠르기 (도/초) — 클수록 즉시 꺾인다")]
    [Range(360f, 3600f)] public float 방향전환 = 2400f;
    [Tooltip("달릴 때 **몸이 도는** 빠르기 (도/초) — 작을수록 크게 돌며 꺾는다")]
    [Range(180f, 1440f)] public float 회전속도 = 620f;

    /// 달리는 동안의 시선 각도 — 목표로 서서히 돌아간다 (뚝 끊기지 않게)
    float 달릴각; bool 달릴각있음;

    [Header("몸")]
    [Tooltip("키 (m)")] public float height = 1.8f;
    [Tooltip("장애물에 걸리는 반지름 (m)")] public float radius = 0.35f;

    [Header("지구력")]
    [Tooltip("최대 지구력 (초 단위로 생각하면 편하다)")] public float maxStamina = 100f;
    [Tooltip("달릴 때 1초에 쓰는 양")] public float runCost = 12f;
    [Tooltip("걷거나 설 때 1초에 차는 양")] public float regen = 9f;
    [Tooltip("숨이 다 찼을 때, 지구력이 이 비율만큼 차기 전에는 다시 못 뛴다")]
    [Range(0.05f, 0.6f)] public float 숨돌리기 = 0.25f;
    [HideInInspector] public float stamina = 100f;
    [HideInInspector] public float 회복정지끝;   // 이 시각까지는 숨이 안 찬다 — 휘두르면 `HeroAttack` 이 민다

    /// 숨이 다 차서 쉬는 중인가 — `숨돌리기` 만큼 찰 때까지 달리기가 안 걸린다
    bool 지쳤다;

    /// 지금 보고 있는 방향 (마우스 쪽, 수평)
    public Vector3 LookDir { get; private set; } = Vector3.forward;
    /// 지금 뛰고 있나 — 소리로 야생을 부르는 판정에 쓴다
    public bool Running { get; private set; }
    /// 지금 웅크리고 있나 (Ctrl) — 걷기 동작을 웅크린 것으로 바꾸는 데 쓴다 (`HeroAnim`)
    public bool Sneaking { get; private set; }

    /// 밖에서 거는 이동 속도 배수 — 휘두르는 동안 발이 느려진다 (`HeroAttack`)
    [HideInInspector] public float MoveMul = 1f;

    // ★★생존이 거는 배수 (`생존` 이 매 프레임 써넣는다). **죽이지 않고 무디게 만든다** —
    //   기획 5-5: *"벌칙은 죽음이 아니라 무뎌짐. 느려지고 힘이 빠진다"*.
    //   ☆`MoveMul` 과 따로 둔다 — 그쪽은 `HeroCarry`·`HeroAttack` 이 매 프레임 덮어써서
    //     같이 쓰면 서로 지워 버린다 (소유권이 갈려 있어야 안 싸운다)
    [HideInInspector] public float 생존이속 = 1f;
    [HideInInspector] public float 생존힘 = 1f;
    [HideInInspector] public float 생존지구력 = 1f;   // 최대 지구력에 곱한다
    /// 앉아 있는 동안은 발이 묶인다 (의자) — 밖에서 건다 (`HeroAnim`)
    [HideInInspector] public bool 묶임;

    /// 지금 실제로 움직이는 속도 (m/s, 수평) — 걷기 동작을 고르는 데 쓴다
    public Vector3 속도 => new Vector3(vel.x, 0f, vel.z);

    Vector3 vel;
    IsoCam cam;

    void Awake() { Me = this; hp = maxHp; }

    void Start()
    {
        stamina = maxStamina;
        cam = FindFirstObjectByType<IsoCam>();
    }

    void Update()
    {
        if (!Alive) return;
        ReadKeys(out Vector2 mv, out bool wantRun, out bool wantSneak);

        // ── 보는 방향 = 마우스
        if (cam != null && cam.MouseGround(transform.position.y, out var g))
        {
            var d = g - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) LookDir = d.normalized;
        }

        // ── 속도 — 지구력이 바닥나면 못 뛴다
        if (묶임) mv = Vector2.zero;                  // 의자에 앉아 있는 동안은 발이 안 나간다
        // ★★★**숨이 차면 한 번 쉬어야 다시 뛴다** (2026-08-06 사용자 "스태미너 없을 때
        //   달리기 누르고 있으니까 부들부들 떨리는 버그").
        //
        //   전에는 조건이 `stamina > 1f` 하나였다. 0 이 되면 걷기로 떨어지고, 회복이 1 을
        //   넘는 **그 프레임에 다시 달리기**가 되고, 곧바로 소모돼 1 밑으로 — 매 프레임
        //   달리기↔걷기가 뒤집혔다. 그런데 이 둘은 **시선 기준이 다르다**(달리면 가는 쪽,
        //   걸으면 마우스 쪽) → 몸이 좌우로 오가며 부들부들 떨렸다. 애니메이션도 같이 튄다.
        //
        //   → 문턱을 둘로 벌린다: **0 이 되면 지친 것**이고, **`숨돌리기` 만큼 찰 때까지는
        //     못 뛴다.** 뒤집힘이 구조적으로 불가능해지고, "숨이 차서 못 뛴다" 로도 읽힌다.
        if (stamina <= 0.5f) 지쳤다 = true;
        else if (지쳤다 && stamina >= maxStamina * 숨돌리기) 지쳤다 = false;

        // 짐이 넘치면 뛰기는커녕 걷지도 못한다 — 지구력만 축내지 않게 여기서 끊는다
        Running = wantRun && mv.sqrMagnitude > 0.01f && !지쳤다 && !인벤.총넘침;
        Sneaking = wantSneak && !Running;             // 뛰면서 웅크릴 수는 없다
        float spd = Running ? run : (wantSneak ? sneak : walk);

        // ★뛸 때는 마우스가 아니라 **가는 쪽**을 본다 (2026-08-04 사용자).
        //   달리면서 싸울 일이 없으니 시선을 붙들 이유가 없고, 몸과 진행 방향이
        //   어긋나면 옆걸음·뒷걸음 블렌드가 섞여 달리기가 달리기로 안 읽힌다.
        //   (걷기는 그대로 마우스 기준 — 보면서 물러나는 게 전투의 뼈대다)
        // ★★★★**클릭한 곳까지 알아서 걸어간다** (2026-08-11 사용자 "클릭해서 누르면
        //   거기까지 알아서 걸어가서, 캐거나 갈무리하게해줘"). 알비온식이다.
        //   ☆**WASD 가 언제나 이긴다** — 한 번이라도 누르면 자동 걸음은 그 자리에서 끊긴다.
        //     두 조작이 싸우면 손이 안 맞으므로, 내 손이 들어오면 무조건 자동이 물러난다.
        //   ☆`mv` 에 얹는다 — 아래 이동 코드(가속·회전·장애물)를 그대로 쓴다. 따로 걷지 않는다.
        if (mv.sqrMagnitude > 0.01f) 걸어가는중 = false;
        else if (걸어가는중)
        {
            var 갈d = 걸어갈곳 - transform.position; 갈d.y = 0f;
            if (갈d.sqrMagnitude < 0.02f) 걸어가는중 = false;
            else
            {
                // 세계 방향 → 화면(WASD) 방향으로 되돌린다 — 아래에서 다시 카메라로 돌리므로
                var 화면 = Quaternion.Euler(0f, -(cam != null ? cam.yaw : 45f), 0f) * 갈d.normalized;
                mv = new Vector2(화면.x, 화면.z);
            }
        }

        var 갈방향 = Quaternion.Euler(0f, cam != null ? cam.yaw : 45f, 0f)
                   * new Vector3(mv.x, 0f, mv.y);
        // ★★★**달릴 때는 방향을 돌려서 바꾼다** (2026-08-06 사용자 "뚝 방향이 바뀌니까
        //   어색한데 회전을 좀 넣어주면 안 돼?").
        //   전에는 누른 방향을 그 프레임에 그대로 시선으로 박았다. 시선은 16칸으로 끊기니까
        //   (`FaceQuantized`) 위→왼쪽이면 **90°가 한 프레임에** 넘어가 뚝 끊겨 보였다.
        //   → 목표 각도로 **정해진 속도로 돌린다.** 16칸 계단을 차례로 밟고 지나가므로
        //     끊김이 「도는 동작」으로 읽힌다. 걷기는 그대로 마우스 기준이라 안 건드린다.
        if (Running && 갈방향.sqrMagnitude > 0.01f)
        {
            float 목표 = Mathf.Atan2(갈방향.x, 갈방향.z) * Mathf.Rad2Deg;
            if (!달릴각있음) { 달릴각 = 목표; 달릴각있음 = true; }
            달릴각 = Mathf.MoveTowardsAngle(달릴각, 목표, 회전속도 * Time.deltaTime);
            LookDir = Quaternion.Euler(0f, 달릴각, 0f) * Vector3.forward;
        }
        else 달릴각있음 = false;

        // 사람도 16칸으로 본다 (`Critter.Face` 와 같은 규칙 — 버티기 포함)
        FaceQuantized(LookDir);

        // ★휘두른 직후엔 숨이 안 찬다 (2026-08-11 "지구력회복도 멈춰야하고") — 연타에 숨값을 매긴다
        stamina += (Running ? -runCost : Time.time < 회복정지끝 ? 0f : regen) * Time.deltaTime;
        // ★피로가 쌓이면 **최대 지구력 자체가 줄어든다** (`생존`) — 덜 뛰게 된다
        stamina = Mathf.Clamp(stamina, 0f, maxStamina * Mathf.Clamp(생존지구력, 0.1f, 1f));
        // 숨이 차면 느려진다 — 화력이 아니라 숨이 전투를 제한한다
        if (stamina < maxStamina * 0.2f) spd *= Mathf.Lerp(0.6f, 1f, stamina / (maxStamina * 0.2f));
        spd *= Mathf.Clamp(MoveMul, 0.05f, 1f);      // 휘두르는 동안 발이 느려진다
        spd *= Mathf.Clamp(생존이속, 0.2f, 1f);        // 굶고 목마르면 느려진다 (`생존`)
        // ★★짐이 무거우면 느려지고, **한도를 넘으면 발이 아예 안 나간다** (`인벤.짐배`).
        //   떨어뜨려 주지 않는다 — 무엇을 버릴지는 내가 고른다
        spd *= 인벤.짐배;

        // ── 이동 (화면 기준 — 카메라가 고정이라 항상 같다)
        var want = 갈방향;
        if (want.sqrMagnitude > 1f) want.Normalize();
        want *= spd;

        // ★★★**방향은 돌리고, 크기만 가속으로 붙인다** (2026-08-06 사용자 "방향전환할 때
        //   살짝 밀리면서 미끄러지는 것 좀 아주 최소화해줘").
        //
        //   `MoveTowards` 만 쓰면 속도가 **직선으로** 목표를 향한다. 그래서 오른쪽으로 뛰다
        //   왼쪽을 누르면 속도가 **0 을 지나가야** 하고, 그 사이 몸은 원래 방향으로 계속
        //   미끄러진다. 값을 아무리 올려도 「지나가는 구간」은 안 없어진다.
        //   → 먼저 **속도 벡터를 목표 방향으로 회전**시킨다(크기는 그대로). 그러면 꺾을 때
        //     느려지지 않고 **바로 그쪽으로 간다.** 크기 변화만 가속·감속이 맡는다.
        if (want.sqrMagnitude > 0.0001f && vel.sqrMagnitude > 0.0001f)
            vel = Vector3.RotateTowards(vel, want,
                                        Mathf.Deg2Rad * 방향전환 * Time.deltaTime, 0f);

        // 가려는 쪽이 없으면(발을 뗐으면) 감속 쪽 값으로 — 미끄러짐이 여기서 사라진다
        float 붙는속 = want.sqrMagnitude > 0.0001f ? accel : Mathf.Max(accel, 감속);
        vel = Vector3.MoveTowards(vel, want, 붙는속 * Time.deltaTime);

        var pos = transform.position + vel * Time.deltaTime;
        pos = Blocker.Resolve(pos, radius);
        pos.x = Mathf.Clamp(pos.x, 1f, WorldGrid.Size - 1f);
        pos.z = Mathf.Clamp(pos.z, 1f, WorldGrid.Size - 1f);
        // ★★칸 높이를 딛는다 (2026-08-09 사용자 "캐릭터 팻들이 모두 땅에 박혀있어").
        //   `땅격자` 가 그림과 판정의 **단 하나뿐인 출처**다 — 여기서 안 물으면 발이 묻힌다.
        pos.y = 땅격자.걷는높이(pos.x, pos.z);
        transform.position = pos;
    }

    // ★★(은퇴 · 2026-08-11) 자동 걸음 — 클릭한 대상까지 알아서 걸어가려던 것.
    //   캐기·갈무리를 **F 상호작용**으로 옮기면서 부르는 데가 없어졌다.
    //   지우지 않고 둔다 (은퇴는 삭제가 아니라 스위치) — 나중에 「클릭해서 그리로 간다」가
    //   필요해지면 `걸어가기(곳)` 한 줄만 부르면 된다. WASD 가 들어오면 스스로 끊긴다.
    [HideInInspector] public Vector3 걸어갈곳;
    [HideInInspector] public bool 걸어가는중;
    public void 걸어가기(Vector3 곳) { 걸어갈곳 = 곳; 걸어가는중 = true; }
    public void 걸음멈춤() { 걸어가는중 = false; }

    /// 바라보는 방향을 16칸 중 하나로 — 결과를 밀지 않고 **목표를 끊는다**
    /// (`Critter.Face` 와 같은 규칙. 이유는 그쪽 주석 참고)
    float 본각도; bool 각도있음;
    void FaceQuantized(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) return;
        float want = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        int n = Critter.방향수;
        if (n < 2) { transform.rotation = Quaternion.Euler(0f, want, 0f); return; }

        float step = 360f / n;
        if (!각도있음) { 본각도 = Mathf.Round(want / step) * step; 각도있음 = true; }
        else if (Mathf.Abs(Mathf.DeltaAngle(본각도, want)) > step * 0.6f)
            본각도 = Mathf.Round(want / step) * step;

        transform.rotation = Quaternion.Euler(0f, 본각도, 0f);
    }

    void ReadKeys(out Vector2 mv, out bool wantRun, out bool wantSneak)
    {
        mv = Vector2.zero; wantRun = false; wantSneak = false;
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k == null) return;
        if (k.wKey.isPressed) mv.y += 1f;
        if (k.sKey.isPressed) mv.y -= 1f;
        if (k.dKey.isPressed) mv.x += 1f;
        if (k.aKey.isPressed) mv.x -= 1f;
        wantRun = k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
        wantSneak = k.leftCtrlKey.isPressed || k.rightCtrlKey.isPressed;
#else
        mv = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        wantRun = Input.GetKey(KeyCode.LeftShift);
        wantSneak = Input.GetKey(KeyCode.LeftControl);
#endif
    }
}
