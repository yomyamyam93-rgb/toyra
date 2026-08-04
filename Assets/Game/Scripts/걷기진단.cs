using System.Text;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// 공격이 걷기를 방해하는 순간을 붙잡는 장치 (2026-08-05 사용자 "몽둥이 휘두르는게
/// 걷기와 무슨 간섭이 있는거같아 … 누르고있다 떼는게 뭐가 문제인거같기도하고").
///
/// ★한 번 찍어서는 못 잡는다 — 눌러서 재보면 늘 정상이었다. 그래서 **누름→뗌 한 번을
///   통째로** 기록한다: 얼마나 눌렀나 · 어떤 상태를 거쳤나 · 그동안 속도와 애니메이션이
///   어떻게 움직였나.
///
/// ★의심 구간은 여기다 (`HeroAttack`):
///     bool 감았나 = 드는자세.지금 > 0.7f;
///     if (감았나) { state = 휘두름; }   // 예비를 건너뛴다
///     else        { state = 예비; }
///   **누른 시간에 따라 경로가 갈린다.** 짧게 누른 것과 오래 누른 것을 나란히 놓으면
///   어느 쪽이 어긋나는지 바로 보인다.
[RequireComponent(typeof(Hero))]
[DefaultExecutionOrder(900)]          // 모두가 값을 다 쓴 **뒤에** 본다
public class 걷기진단 : MonoBehaviour
{
    [Tooltip("기록을 몇 번까지 들고 있나")] public int 최대 = 12;
    [Tooltip("한 프레임에 속도가 이만큼 넘게 변하면 「튐」")] public float 속도튐 = 1.5f;
    [Tooltip("애니메이션이 실시간의 이 배를 넘게 흐르면 「튐」")] public float 흐름튐 = 2.5f;

    Hero hero; HeroAttack 공격; HeroHold 자세; Animator anim;
    readonly System.Collections.Generic.List<string> 기록 = new();

    bool 누름중; float 누른시각; string 거친상태; object 지난상태;
    float 최소속도, 최대속도, 최대흐름, 최소길이;
    float 지난속도, 지난진행;

    static System.Reflection.FieldInfo fState;

    /// 밖에서 꺼내 본다
    public string 보고()
    {
        if (기록.Count == 0) return "아직 휘두른 기록이 없다 (프레임 " + Time.frameCount + ")";
        var sb = new StringBuilder();
        for (int i = 0; i < 기록.Count; i++) sb.AppendLine((i + 1) + ") " + 기록[i]);
        return sb.ToString();
    }
    public void 지우기() { 기록.Clear(); }

    void Awake()
    {
        hero = GetComponent<Hero>(); 공격 = GetComponent<HeroAttack>(); 자세 = GetComponent<HeroHold>();
        if (fState == null && 공격 != null)
            fState = typeof(HeroAttack).GetField("state",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    }

    void LateUpdate()
    {
        if (anim == null || !anim.isActiveAndEnabled) anim = GetComponentInChildren<Animator>();
        if (anim == null || 공격 == null) return;

        float 속도 = hero.속도.magnitude;
        var st = anim.GetCurrentAnimatorStateInfo(0);
        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        // 애니메이션이 실시간 대비 몇 배로 흘렀나 — 1 이 정상
        float 흐름 = st.length > 0.001f ? Mathf.Abs(st.normalizedTime - 지난진행) * st.length / dt : 0f;

        var 상태 = fState != null ? fState.GetValue(공격) : null;

#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        bool 눌림 = m != null && m.leftButton.isPressed;
        bool 뗌 = m != null && m.leftButton.wasReleasedThisFrame;
#else
        bool 눌림 = Input.GetMouseButton(0);
        bool 뗌 = Input.GetMouseButtonUp(0);
#endif
        if (눌림 && !누름중)
        {   // 누르기 시작 — 여기서부터 한 판을 기록한다
            누름중 = true; 누른시각 = Time.time; 거친상태 = "" + 상태;
            최소속도 = 속도; 최대속도 = 속도; 최대흐름 = 1f; 최소길이 = st.length;
        }
        if (누름중 || (상태 != null && "" + 상태 != "쉼"))
        {
            if (상태 != null && !Equals(상태, 지난상태)) 거친상태 += " → " + 상태;
            최소속도 = Mathf.Min(최소속도, 속도); 최대속도 = Mathf.Max(최대속도, 속도);
            if (지난진행 > 0f) 최대흐름 = Mathf.Max(최대흐름, 흐름);
            최소길이 = Mathf.Min(최소길이, st.length);
        }
        if (뗌 && 누름중)
        {
            float 누른시간 = Time.time - 누른시각;
            bool 감았나 = 자세 != null && 자세.지금 > 0.7f;
            if (기록.Count < 최대)
                기록.Add(string.Format(
                    "누름 {0:F2}초 · 감았나={1} · 상태 {2}",
                    누른시간, 감았나 ? "예(예비 건너뜀)" : "아니오(예비 거침)", 거친상태));
            누름중 = false;
        }
        // 휘두름이 끝난 뒤 한 줄 더 — 그 판에서 속도·애니가 어땠나
        if (상태 != null && "" + 상태 == "쉼" && 지난상태 != null && "" + 지난상태 != "쉼" && 기록.Count > 0)
        {
            int i = 기록.Count - 1;
            bool 이상 = (최대속도 - 최소속도) > 속도튐 || 최대흐름 > 흐름튐 || 최소길이 < 0.05f;
            기록[i] += string.Format("\n     속도 {0:F2}~{1:F2} · 애니흐름 최대 {2:F1}배 · 상태길이 최소 {3:F3}s{4}",
                최소속도, 최대속도, 최대흐름, 최소길이, 이상 ? "   ★여기가 튄 판" : "");
            // ★★콘솔에도 남긴다 — 플레이를 멈추면 메모리 기록은 날아가는데
            //   콘솔은 남는다. 증상을 본 뒤 멈춰도 읽을 수 있어야 한다.
            Debug.Log("[걷기진단] " + 기록[i]);
        }

        지난상태 = 상태; 지난속도 = 속도; 지난진행 = st.normalizedTime;
    }
}
