using UnityEngine;

/// 뼈에 무기·장비 모델을 붙이고 **그 자리를 눈으로 보며 맞추는** 자리 (2026-08-06).
///
/// ★왜 (사용자 "모델링에 붙은 무기나 장비 위치 조절 좀 가능하게 해줄래?"):
///   전에는 절차로 만든 상자 몽둥이 하나만 `HeroAttack.쥔자리`로 조절할 수 있었고,
///   **모델을 붙이면 손을 벗어나도 당겨 넣을 방법이 없었다.**
///
/// ★쓰는 법: `붙임` 을 한 줄 추가하고 → 모델과 뼈 이름을 넣고 → **플레이한 채로**
///   위치·회전·크기를 인스펙터에서 만진다. 매 프레임 다시 얹으므로 즉시 보인다.
///   맞춘 값은 그대로 씬에 저장된다.
///
/// ★★실행 순서를 `HeroAttack`(300)보다 늦게 둔다 — 애니메이터·`HeroHold`·무기 자세가
///   전부 끝난 **뒤에** 얹어야 한 프레임 늦게 따라오는 일이 없다.
///
/// ★뼈 밑에 자식으로 매달되, **오프셋은 매 프레임 다시 쓴다.** 애니메이션 클립이 그 경로를
///   건드리면 클립이 이기는데(CLAUDE.md 「애니메이터가 소유한 속성은 못 이긴다」), 매 프레임
///   덮어쓰면 그 싸움에서 이긴다.
[DefaultExecutionOrder(320)]
public class 장비붙이기 : MonoBehaviour
{
    [System.Serializable]
    public class 붙임
    {
        [Tooltip("무엇인지 알아보게 쓰는 이름 (동작에 영향 없음)")]
        public string 이름 = "무기";
        [Tooltip("붙일 모델 — 비우면 아무 일도 안 한다")]
        public GameObject 모델;
        [Tooltip("어느 뼈에 붙이나 (예: RightHand · LeftHand · Spine · Head)")]
        public string 뼈 = "RightHand";
        [Tooltip("뼈 기준 위치 (m)")]
        public Vector3 위치;
        [Tooltip("뼈 기준 회전 (°)")]
        public Vector3 회전;
        [Tooltip("크기 배수")]
        public Vector3 크기 = Vector3.one;
        [Tooltip("끄면 숨긴다 (모델을 지우지 않는다)")]
        public bool 켬 = true;

        [HideInInspector] public Transform 붙은것;   // 실제로 만들어진 인스턴스
        [HideInInspector] public Transform 붙은뼈;
    }

    [Tooltip("붙일 것들 — 플레이 중에 값을 만지면 바로 보인다")]
    public 붙임[] 목록 = new 붙임[0];

    [Tooltip("몸을 바꿨을 때(남↔여) 다시 붙게 한다")]
    public bool 뼈를매프레임확인 = true;

    void LateUpdate()
    {
        if (목록 == null) return;
        for (int i = 0; i < 목록.Length; i++)
        {
            var b = 목록[i];
            if (b == null || b.모델 == null) continue;

            // ★뼈를 놓쳤으면 다시 찾는다 — 몸이 갈리면(남↔여) 옛 뼈가 꺼진 몸의 것이 된다
            if (b.붙은뼈 == null || !b.붙은뼈.gameObject.activeInHierarchy || 뼈를매프레임확인)
            {
                var 뼈 = 뼈찾기(b.뼈);
                if (뼈 != b.붙은뼈)
                {
                    b.붙은뼈 = 뼈;
                    if (b.붙은것 != null && 뼈 != null) b.붙은것.SetParent(뼈, false);
                }
            }
            if (b.붙은뼈 == null) continue;

            if (b.붙은것 == null)
            {
                var g = Instantiate(b.모델, b.붙은뼈);
                g.name = string.IsNullOrEmpty(b.이름) ? b.모델.name : b.이름;
                b.붙은것 = g.transform;
            }

            // 매 프레임 다시 얹는다 — 인스펙터를 만지면 즉시 보이고, 클립과 다퉈도 이긴다
            b.붙은것.localPosition = b.위치;
            b.붙은것.localRotation = Quaternion.Euler(b.회전);
            b.붙은것.localScale = b.크기;
            if (b.붙은것.gameObject.activeSelf != b.켬) b.붙은것.gameObject.SetActive(b.켬);
        }
    }

    Transform 뼈찾기(string 이름)
    {
        if (string.IsNullOrEmpty(이름)) return null;
        // 켜진 몸에서만 찾는다 (꺼진 몸의 뼈를 쥐면 안 보이는 데 붙는다)
        foreach (var t in GetComponentsInChildren<Transform>(false))
            if (t.name == 이름) return t;
        return null;
    }

#if UNITY_EDITOR
    /// 씬 뷰에서 붙은 자리를 점으로 보여 준다 — 손 안에 있나 눈으로 확인하는 용
    void OnDrawGizmosSelected()
    {
        if (목록 == null) return;
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        foreach (var b in 목록)
        {
            if (b == null || b.붙은것 == null) continue;
            Gizmos.DrawWireSphere(b.붙은것.position, 0.03f);
            Gizmos.DrawLine(b.붙은것.position, b.붙은것.position + b.붙은것.forward * 0.15f);
        }
    }
#endif
}
