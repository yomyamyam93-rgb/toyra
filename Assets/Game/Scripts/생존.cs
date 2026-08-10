using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// 생존 — **배고픔 · 목마름 · 피로.** 기획 5-5.
///
/// ★★셋 다 「그래서 내일 또 나간다」에 답하는 것만 넣었다:
///   · **배고픔** — 고기 한 덩이를 **내가 먹나 펫을 주나.** 이 게임의 매일의 판단이다
///   · **목마름** — 물웅덩이가 **지도에서 뜻을 갖는다.** 멀리 나가려면 물자리를 알아야 한다
///   · **피로** — **모닥불로 돌아올 이유.** 불 옆에서만 풀린다
///
/// ★★★**벌칙은 죽음이 아니라 무뎌짐이다** (5-5). 느려지고 힘이 빠진다.
///   굶어 죽지 않는다 — 체력이 아주 천천히 깎이되 **바닥에서 멈춘다.**
///   ☆죽음으로 벌하면 「관리 게임」이 된다. 무뎌지게 하면 **판단**이 된다:
///     지금 사냥을 접고 물을 찾을 것인가, 조금 더 버틸 것인가.
///
/// ★게이지를 상시로 띄우지 않는다 (5-5 · 11장). 평소엔 화면에 아무것도 없다가
///   문턱을 넘으면 그때 뜬다 — `HUD` 가 그린다.
[RequireComponent(typeof(Hero))]
[DefaultExecutionOrder(10)]        // `모닥불`(-5)·`HeroCarry`(0) 가 F 를 먼저 본다
public class 생존 : MonoBehaviour
{
    [Header("차오르는 속도 (하루 = 1200초)")]
    [Tooltip("배고픔이 0→1 되는 데 걸리는 시간 (초). 1600 = 하루 반 조금 못 됨")]
    public float 배고픈데 = 1600f;
    [Tooltip("목마름이 0→1 되는 데 걸리는 시간 (초) — 배고픔보다 빨라야 물이 뜻을 갖는다")]
    public float 목마른데 = 950f;
    [Tooltip("피로가 0→1 되는 데 걸리는 시간 (초)")]
    public float 지치는데 = 2400f;
    [Tooltip("달릴 때 피로가 몇 배로 쌓이나")] public float 달리면 = 3.5f;

    [Header("푸는 법")]
    [Tooltip("물 한 모금이 목마름을 이만큼 내린다")] public float 한모금 = 0.55f;
    [Tooltip("타는 불 옆에서 쉴 때 피로가 0→1 만큼 풀리는 시간 (초)")] public float 쉬는데 = 150f;
    [Tooltip("불에서 이 안이면 쉰 것으로 친다 (m)")] public float 불곁 = 4.5f;
    [Tooltip("이 속도보다 느려야 「쉬는 중」이다 (m/s)")] public float 쉬는속도 = 0.4f;

    [Header("무뎌짐 — 죽이지 않는다")]
    [Tooltip("배고픔이 이걸 넘으면 힘이 빠지기 시작한다")] [Range(0.2f, 0.9f)] public float 배고픔문턱 = 0.55f;
    [Tooltip("목마름이 이걸 넘으면 숨이 안 돌아온다")] [Range(0.2f, 0.9f)] public float 목마름문턱 = 0.5f;
    [Tooltip("가장 나쁠 때 이속이 이 배까지 떨어진다")] [Range(0.4f, 1f)] public float 최저이속 = 0.62f;
    [Tooltip("가장 나쁠 때 힘이 이 배까지 떨어진다")] [Range(0.3f, 1f)] public float 최저힘 = 0.55f;
    [Tooltip("피로가 가득할 때 최대 지구력이 이 배가 된다")] [Range(0.3f, 1f)] public float 최저지구력 = 0.45f;
    [Tooltip("한계까지 가면 체력이 1초에 이만큼 깎인다 (바닥에서 멈춘다)")] public float 한계피해 = 0.35f;
    [Tooltip("체력이 이 비율 아래로는 굶어서 안 내려간다 — 굶어 죽지 않는다")]
    [Range(0.05f, 0.5f)] public float 굶어도남는것 = 0.15f;

    /// 0 = 멀쩡 · 1 = 한계
    public float 배고픔 { get; private set; }
    public float 목마름 { get; private set; }
    public float 피로 { get; private set; }

    /// 방금 벌어진 일 — 화면에 잠깐 띄운다
    public string 알림 { get; private set; } = "";
    float 알림T;

    /// 지금 물가에 서 있나 (화면에 마실 수 있다고 알려 주려고)
    public bool 물가 { get; private set; }
    /// 지금 불 곁에서 쉬는 중인가
    public bool 쉬는중 { get; private set; }

    Hero hero;
    WorldGen 월드;

    void Awake() { hero = GetComponent<Hero>(); }
    void Start() { 월드 = FindFirstObjectByType<WorldGen>(); }

    void Update()
    {
        float dt = Time.deltaTime;
        if (알림T > 0f) { 알림T -= dt; if (알림T <= 0f) 알림 = ""; }
        if (!hero.Alive) return;

        // ── 차오른다
        배고픔 = Mathf.Clamp01(배고픔 + dt / Mathf.Max(1f, 배고픈데));
        목마름 = Mathf.Clamp01(목마름 + dt / Mathf.Max(1f, 목마른데));

        float 피로속 = dt / Mathf.Max(1f, 지치는데) * (hero.Running ? 달리면 : 1f);

        // ── 불 곁에서 쉬면 피로가 풀린다 — **모닥불로 돌아올 이유**
        var 불 = 모닥불.가까운것(transform.position, 불곁);
        쉬는중 = 불 != null && 불.탄다 && hero.속도.magnitude < 쉬는속도;
        if (쉬는중) 피로 = Mathf.Max(0f, 피로 - dt / Mathf.Max(1f, 쉬는데));
        else 피로 = Mathf.Clamp01(피로 + 피로속);

        // ── 물가인가 (F 로 마신다)
        물가 = 월드 != null && 월드.KindAt(transform.position) == WorldGen.Land.물웅덩이;
        if (물가 && 목마름 > 0.02f && F눌림()) 마시기();

        무뎌짐(dt);
    }

    bool F눌림()
    {
        // 모닥불이나 짐승이 그 프레임의 F 를 이미 먹었으면 넘긴다
        if (모닥불.F먹음) return false;
        var 안음 = GetComponent<HeroCarry>();
        if (안음 != null && 안음.데려가는것 != null) return false;
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && k.fKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.F);
#endif
    }

    void 마시기()
    {
        목마름 = Mathf.Max(0f, 목마름 - 한모금);
        띄움("물");
    }

    /// ★고기를 먹었다 — `제작창` 이 부른다. 구운 것이 더 든든하다
    public void 먹었다(float 채움)
    {
        배고픔 = Mathf.Max(0f, 배고픔 - 채움);
    }

    /// ★★느려지고 힘이 빠진다. **죽지는 않는다**
    void 무뎌짐(float dt)
    {
        float 배 = Mathf.InverseLerp(배고픔문턱, 1f, 배고픔);      // 0~1
        float 물 = Mathf.InverseLerp(목마름문턱, 1f, 목마름);

        float 나쁨 = Mathf.Max(배, 물);
        hero.생존이속 = Mathf.Lerp(1f, 최저이속, 나쁨);
        hero.생존힘 = Mathf.Lerp(1f, 최저힘, 배);
        hero.생존지구력 = Mathf.Lerp(1f, 최저지구력, 피로);

        // 목마르면 숨이 잘 안 돌아온다 — 지구력 회복이 준다
        // (`Hero` 가 `생존지구력` 으로 상한을 깎으므로 여기선 회복만 손댄다)
        if (물 > 0f) hero.stamina = Mathf.Max(0f, hero.stamina - 물 * hero.regen * 0.55f * dt);

        // ── 한계까지 갔을 때만 체력이 깎인다. **바닥에서 멈춘다 — 굶어 죽지 않는다**
        if (배고픔 < 0.98f && 목마름 < 0.98f) return;
        float 바닥 = hero.maxHp * 굶어도남는것;
        if (hero.hp <= 바닥) return;
        hero.hp = Mathf.Max(바닥, hero.hp - 한계피해 * dt);
    }

    void 띄움(string s) { 알림 = s; 알림T = 1.6f; }
}
