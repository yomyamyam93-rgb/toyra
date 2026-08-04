using UnityEngine;

/// 무기 드는 자세 — **다리는 걷고 팔만 따로.**
///
/// ★애니메이터 레이어와 아바타 마스크로도 되지만, 지금처럼 **고정 자세 하나**를 얹는
///   거라면 팔 뼈를 직접 잡는 쪽이 훨씬 간단하다. 무엇보다 **실행 중에 각도를 보면서
///   맞출 수 있다** — 자세를 클립으로 구우면 고칠 때마다 다시 구워야 한다.
///
/// ★애니메이터가 뼈를 다 돌린 **뒤에** 잡는다 (`LateUpdate`). 그래서 걷기는 그대로
///   살아 있고 팔만 바뀐다.
///
/// ★★**더하지 말고 갈아끼워야 한다** (2026-08-04 사용자 "이상하게 흔들고").
///   처음엔 애니메이션 회전 위에 각도를 *더했다.* 그러면 걸을 때 팔이 계속 앞뒤로
///   흔들리고 그 위에 자세만 얹혀서 무기가 마구 흔들린다. 실측: 서 있을 땐 0.00°/f
///   인데 걸으면 그대로 팔 스윙이 살아 있었다.
///   → 지금은 **차렷 자세(뼈대 기본값)를 기준으로 정한 각도**로 덮어쓴다. 팔이 멈춘다.
///
/// ★기준은 몸통이다. 어깨뼈의 부모(가슴)가 걸으면서 기우는 건 그대로 따라간다 —
///   팔만 허공에 못박히면 그게 더 이상하다.
///
/// ★각도는 **세상 기준**이라 읽기 쉽다 — X 를 음수로 주면 팔이 뒤로 올라간다.
[DefaultExecutionOrder(200)]     // Animator 다음
public class HeroHold : MonoBehaviour
{
    [Header("드는 자세 (세상 기준 각도)")]
    [Tooltip("어깨")] public Vector3 어깨 = new Vector3(-20f, 0f, 0f);
    [Tooltip("윗팔 — 음수로 주면 뒤로 올라간다")] public Vector3 팔 = new Vector3(-115f, 0f, -20f);
    [Tooltip("팔뚝 — 접는 정도")] public Vector3 팔뚝 = new Vector3(-55f, 0f, 0f);

    // ★★내려치는 자세가 있어야 「휘두른다」가 보인다 (2026-08-04 사용자 "휘두르질 않네").
    //   전엔 든 자세 하나뿐이라, 휘두를 때 팔은 그대로 두고 몽둥이만 세상 기준 yaw 로
    //   돌렸다. 그런데 든 자세에서는 몽둥이가 거의 **수직**이라, 수직 막대를 수직축으로
    //   돌려 봐야 실루엣이 안 바뀐다 — 눈에는 아무 일도 안 일어난 것이다.
    //   → 휘두름은 **팔이 위에서 앞아래로 내려오는 것**이어야 한다.
    [Header("내려친 자세 (휘두름의 끝)")]
    public Vector3 칠때어깨 = new Vector3(12f, 0f, 0f);
    public Vector3 칠때팔 = new Vector3(30f, 0f, -12f);
    public Vector3 칠때팔뚝 = new Vector3(-12f, 0f, 0f);

    [Header("왼팔도 같이 (두 손으로 들 때)")]
    public bool 두손 = true;
    public Vector3 왼팔 = new Vector3(-95f, 0f, 25f);
    public Vector3 왼팔뚝 = new Vector3(-70f, 0f, 0f);

    [Header("느낌")]
    [Tooltip("드는 데 걸리는 빠르기 (클수록 빨리 든다)")] public float 따라붙기 = 8f;
    [Tooltip("어깨→팔→팔뚝이 늦게 따라오는 정도 (0 이면 셋이 동시에 꺾인다)")]
    [Range(0f, 0.5f)] public float 시차 = 0.18f;

    /// 0 = 평소 · 1 = 다 든 자세. 밖에서 넣는다
    [HideInInspector] public float 목표 = 0f;
    /// 0 = 든 자세 · 1 = 다 내려친 자세. 밖에서 넣는다
    [HideInInspector] public float 침 = 0f;

    /// 지금 얼마나 들었나 (0~1) — 밖에서 "다 감았나" 를 물어본다
    public float 지금 { get; private set; }
    Transform 어깨뼈, 팔뼈, 팔뚝뼈, 왼팔뼈, 왼팔뚝뼈;
    Quaternion 어깨기본, 팔기본, 팔뚝기본, 왼팔기본, 왼팔뚝기본;

    /// ★`Awake` 에서 잡는다 — 애니메이터가 아직 한 번도 안 돌아서 **뼈대 기본 자세**가 남아 있다.
    ///   `Start` 나 첫 `LateUpdate` 에서 잡으면 이미 걷는 도중 자세가 기준이 되어 어긋난다.
    void Awake()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            switch (t.name)
            {
                case "RightShoulder": 어깨뼈 = t; 어깨기본 = t.localRotation; break;
                case "RightArm":      팔뼈 = t; 팔기본 = t.localRotation; break;
                case "RightForeArm":  팔뚝뼈 = t; 팔뚝기본 = t.localRotation; break;
                case "LeftArm":       왼팔뼈 = t; 왼팔기본 = t.localRotation; break;
                case "LeftForeArm":   왼팔뚝뼈 = t; 왼팔뚝기본 = t.localRotation; break;
            }
        }
    }

    void LateUpdate()
    {
        지금 = Mathf.Lerp(지금, Mathf.Clamp01(목표), 1f - Mathf.Exp(-따라붙기 * Time.deltaTime));
        if (지금 < 0.001f) return;

        // ★★들리는 게 아니라 **꺾이는** 것처럼 보였던 이유 둘 (2026-08-04 사용자):
        //   ①직선으로 섞었다 — 시작하자마자 최고 속도로 움직이고 도착하는 순간 뚝 선다.
        //     SmoothStep 을 씌워 양 끝을 완만하게 한다.
        //   ②어깨·팔·팔뚝이 **같은 순간에 같은 비율**로 돌았다. 관절이 셋인데 하나처럼
        //     꺾인다. 어깨가 먼저 서고 팔뚝이 늦게 따라오면(오버랩) 팔이 휘어 들린다.
        float w = Mathf.SmoothStep(0f, 1f, 지금);
        float s = Mathf.Clamp01(침);

        // ★부모부터 순서대로 — 어깨를 잡은 뒤라야 팔의 기준이 제대로 선다
        잡기(어깨뼈, 어깨기본, Vector3.Lerp(어깨, 칠때어깨, s), 늦게(w, 0f));
        잡기(팔뼈, 팔기본, Vector3.Lerp(팔, 칠때팔, s), 늦게(w, 시차 * 0.5f));
        잡기(팔뚝뼈, 팔뚝기본, Vector3.Lerp(팔뚝, 칠때팔뚝, s), 늦게(w, 시차));

        if (두손)
        {
            잡기(왼팔뼈, 왼팔기본, 왼팔, 늦게(w, 시차 * 0.5f));
            잡기(왼팔뚝뼈, 왼팔뚝기본, 왼팔뚝, 늦게(w, 시차));
        }
    }

    /// 늦게 따라오기 — 앞의 것이 먼저 서고 뒤의 것이 지연만큼 뒤처진다
    static float 늦게(float w, float 지연)
        => 지연 <= 0f ? w : Mathf.Clamp01((w - 지연) / (1f - 지연));

    /// **기본 자세 + 각도**로 덮어쓴다. 애니메이션이 흔들던 것을 지운다
    static void 잡기(Transform t, Quaternion 기본, Vector3 각도, float 무게)
    {
        if (t == null) return;
        var 부모 = t.parent != null ? t.parent.rotation : Quaternion.identity;
        var 원하는 = Quaternion.Euler(각도) * (부모 * 기본);
        t.rotation = Quaternion.Slerp(t.rotation, 원하는, 무게);
    }
}
