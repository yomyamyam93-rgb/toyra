using System.Collections.Generic;
using UnityEngine;

/// 짐승의 동작 고르기 — **저작해 둔 동작을 상태에 물린다.**
///
/// ★★★여태 **「걷기」 하나만 물려 있었다** (2026-08-10 사용자 — *"팻에 왜 동작이 안박혀있지,
///   걷기나 등등 맞으면 피격되는거까지 다 수정했었는데"*).
///
///   `Resources/rig/` 에는 모델마다 **여섯 벌**이 이미 다 들어 있다 —
///   `걷기 · 뛰기 · 대기 · 공격 · 피격 · 죽음` (57종 × 6 = 343개).
///   그런데 `Wildlife.동작물리기` 가 `걷기_` 만 불렀다. 그래서 짐승들이
///   **서 있어도 걷고, 때려도 걷고, 맞아도 걸었다.** 저작은 멀쩡했고 연결이 없었다.
///
/// ★여기 구조의 특징: 동작이 **한 컨트롤러 안의 상태 여럿**이 아니라 **컨트롤러가 여섯 개**다.
///   그래서 「고른다」는 건 곧 `runtimeAnimatorController` 를 갈아 끼우는 일이다.
///   → **바뀔 때만** 갈아 끼운다. 매 프레임 갈면 애니가 계속 처음으로 되감긴다.
///
/// ★이건 **행동이 아니라 그림이다** (10-4-1) — 무엇을 할지는 `Critter` 가 이미 정했고,
///   여기서는 그 상태를 비추기만 한다. 판단을 새로 하지 않는다.
[DefaultExecutionOrder(20)]          // `Critter` 가 상태를 정한 뒤에 본다
[RequireComponent(typeof(Critter))]
public class 펫동작 : MonoBehaviour
{
    [Tooltip("이 속도를 넘으면 뛰기 (m/s)")] public float 뛰기문턱 = 3.4f;
    [Tooltip("이 속도 아래면 대기 (m/s)")] public float 멈춤문턱 = 0.3f;
    [Tooltip("속도를 얼마나 부드럽게 재나 (클수록 즉각적)")] public float 속도추종 = 10f;

    Critter 놈;
    Animator 애니;
    string 모델이름, 지금동작;
    Vector3 지난자리;
    float 속력;

    /// 어떤 모델의 동작을 쓸지 — `Wildlife` 가 몸을 만들고 나서 알려 준다
    public void 물릴모델(Animator a, string 모델) { 애니 = a; 모델이름 = 모델; }

    void Awake() { 놈 = GetComponent<Critter>(); }
    void Start() { 지난자리 = transform.position; }

    void Update()
    {
        if (애니 == null || 놈 == null || string.IsNullOrEmpty(모델이름)) return;

        // ── 실제로 얼마나 빨리 움직이나 (Critter 는 속도를 안 들고 다닌다 — 자리로 잰다)
        float dt = Time.deltaTime;
        if (dt > 0f)
        {
            var d = transform.position - 지난자리; d.y = 0f;
            float 잰것 = d.magnitude / dt;
            속력 = Mathf.Lerp(속력, 잰것, 1f - Mathf.Exp(-속도추종 * dt));   // 한 프레임 튐에 안 흔들리게
            지난자리 = transform.position;
        }

        갈아끼우기(고르기());
    }

    /// ★순서가 곧 우선순위다 — 맞는 게 때리는 것보다 위다 (맞으면 하던 걸 놓친다)
    string 고르기()
    {
        // ★★★**기절은 저작 클립이 아니라 절차 모션이다** (2026-08-12 사용자
        //   "끌고가는데 팻이 바둥바둥 움직이는 걷는모션이 들어가있네").
        //   ☆옛 코드는 기절에 `피격` 을, 붙잡히면 `대기` 를 물렸다. 그런데 `그로기몸` 이
        //     몸통을 90° 엎어 놓은 위에서 **뼈는 제 클립대로 계속 걸어다녔다** —
        //     엎어진 몸 안에서 다리가 움직이니 「바둥바둥」이 된다.
        //   ☆**빈 칸을 주면 애니메이터를 끈다** → 뼈가 마지막 자세로 굳고, 흔들흔들은
        //     `그로기몸` 이 낸다. 죽음(굳는 클립)과도 갈린다 — 이쪽은 몸통이 계속 움직인다.
        //   ☆붙잡혀 끌려가는 동안도 마찬가지다 (`Critter` 가 기절을 안 풀어 준다).
        // ★★**붙잡혀 있으면 뼈를 굳힌다** (2026-08-13 사용자 "머리위에서 걷는 모션나오는거 팻").
        //   ☆옛 코드는 붙잡히면 `대기` 를 물렸다. 그런데 대기 클립도 제자리에서 다리·고개를
        //     움직이는 저작 모션이라, **머리 위에 얹힌 채 걷는** 그림이 됐다.
        //   ☆안겨 가는 놈은 제 힘으로 아무것도 못 한다 — 기절과 같이 묶는다
        if (놈.기절중 || 놈.잡힘) return "";
        if (놈.묶임) return "대기";                 // 매여 있으면 제자리에서 기다린다
        if (놈.넘어짐 || 놈.맞는중) return "피격";
        if (놈.때리는중) return "공격";
        if (속력 > 뛰기문턱) return "뛰기";
        if (속력 > 멈춤문턱) return "걷기";
        return "대기";
    }

    void 갈아끼우기(string 동작)
    {
        if (동작 == 지금동작) return;

        // ★빈 칸 = 뼈를 멈춘다. 마지막 자세로 굳고 몸통만 절차 모션이 흔든다
        //   ☆끄는 게 곧 싸다 — 애니메이터 업데이트가 통째로 빠진다 (9-4)
        if (string.IsNullOrEmpty(동작)) { 애니.enabled = false; 지금동작 = 동작; return; }

        if (!애니.enabled) 애니.enabled = true;     // ★먼저 켠다 — 컨트롤러가 없어도 굳은 채 안 남게
        var ctrl = 찾기(동작, 모델이름);
        if (ctrl == null) return;                   // 그 동작이 없으면 하던 걸 그대로 둔다
        애니.runtimeAnimatorController = ctrl;
        지금동작 = 동작;
    }

    // 같은 것을 수백 번 찾게 되므로 한 번 찾은 건 들고 있는다
    static readonly Dictionary<string, RuntimeAnimatorController> 캐시 =
        new Dictionary<string, RuntimeAnimatorController>();

    static RuntimeAnimatorController 찾기(string 동작, string 모델)
    {
        string key = 동작 + "_" + 모델;
        if (캐시.TryGetValue(key, out var c)) return c;
        c = Resources.Load<RuntimeAnimatorController>("rig/" + key);
        캐시[key] = c;                                // 없으면 null 을 담아 둔다 (또 안 찾게)
        return c;
    }
}
