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
    [Tooltip("클수록 즉시 최고 속도에 붙는다")] public float accel = 18f;

    [Header("몸")]
    [Tooltip("키 (m)")] public float height = 1.8f;
    [Tooltip("장애물에 걸리는 반지름 (m)")] public float radius = 0.35f;

    [Header("지구력")]
    [Tooltip("최대 지구력 (초 단위로 생각하면 편하다)")] public float maxStamina = 100f;
    [Tooltip("달릴 때 1초에 쓰는 양")] public float runCost = 12f;
    [Tooltip("걷거나 설 때 1초에 차는 양")] public float regen = 9f;
    [HideInInspector] public float stamina = 100f;

    /// 지금 보고 있는 방향 (마우스 쪽, 수평)
    public Vector3 LookDir { get; private set; } = Vector3.forward;
    /// 지금 뛰고 있나 — 소리로 야생을 부르는 판정에 쓴다
    public bool Running { get; private set; }

    /// 밖에서 거는 이동 속도 배수 — 휘두르는 동안 발이 느려진다 (`HeroAttack`)
    [HideInInspector] public float MoveMul = 1f;

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
        Running = wantRun && mv.sqrMagnitude > 0.01f && stamina > 1f;
        float spd = Running ? run : (wantSneak ? sneak : walk);

        // ★뛸 때는 마우스가 아니라 **가는 쪽**을 본다 (2026-08-04 사용자).
        //   달리면서 싸울 일이 없으니 시선을 붙들 이유가 없고, 몸과 진행 방향이
        //   어긋나면 옆걸음·뒷걸음 블렌드가 섞여 달리기가 달리기로 안 읽힌다.
        //   (걷기는 그대로 마우스 기준 — 보면서 물러나는 게 전투의 뼈대다)
        var 갈방향 = Quaternion.Euler(0f, cam != null ? cam.yaw : 45f, 0f)
                   * new Vector3(mv.x, 0f, mv.y);
        if (Running && 갈방향.sqrMagnitude > 0.01f) LookDir = 갈방향.normalized;

        // 사람도 16칸으로 본다 (`Critter.Face` 와 같은 규칙 — 버티기 포함)
        FaceQuantized(LookDir);

        stamina += (Running ? -runCost : regen) * Time.deltaTime;
        stamina = Mathf.Clamp(stamina, 0f, maxStamina);
        // 숨이 차면 느려진다 — 화력이 아니라 숨이 전투를 제한한다
        if (stamina < maxStamina * 0.2f) spd *= Mathf.Lerp(0.6f, 1f, stamina / (maxStamina * 0.2f));
        spd *= Mathf.Clamp(MoveMul, 0.05f, 1f);      // 휘두르는 동안 발이 느려진다

        // ── 이동 (화면 기준 — 카메라가 고정이라 항상 같다)
        var want = 갈방향;
        if (want.sqrMagnitude > 1f) want.Normalize();
        want *= spd;

        vel = Vector3.MoveTowards(vel, want, accel * Time.deltaTime);

        var pos = transform.position + vel * Time.deltaTime;
        pos = Blocker.Resolve(pos, radius);
        pos.x = Mathf.Clamp(pos.x, 1f, WorldGrid.Size - 1f);
        pos.z = Mathf.Clamp(pos.z, 1f, WorldGrid.Size - 1f);
        pos.y = 0f;
        transform.position = pos;
    }

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
