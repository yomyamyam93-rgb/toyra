using UnityEngine;

/// 무광화 — 세상의 모든 재질에서 **반짝임을 걷어낸다.**
///
/// ★왜 (2026-08-04 사용자 — "픽셀들이 자글자글 계속 변해서 보기 불편해"):
///   저해상도에서는 **하이라이트 한 점이 픽셀 하나**다. 카메라가 조금만 움직여도 그
///   점이 픽셀을 넘나들며 색이 확확 바뀐다 — 그게 자글거림의 큰 몫이다.
///   재질을 무광으로 만들면 그 점 자체가 사라진다.
///
/// ★스타일 정본과도 같은 말이다: **"표면은 거의 무광"** (docs/스타일_프롬프트.md).
///   그러니 이건 임시방편이 아니라 원래 가야 할 자리다.
///
/// 재질을 공유하므로 한 번만 손보면 같은 재질을 쓰는 것이 전부 따라온다.
[DefaultExecutionOrder(300)]
public class Matte : MonoBehaviour
{
    [Range(0f, 0.4f)] [Tooltip("표면 매끄러움 — 0 에 가까울수록 무광")] public float 매끄러움 = 0.03f;
    [Range(0f, 1f)] [Tooltip("금속성 — 0 이 기본")] public float 금속성 = 0f;
    [Tooltip("몇 초마다 새로 생긴 것을 훑나 (0 이면 처음 한 번만)")] public float 다시훑기 = 2f;

    [Tooltip("자식 수가 안 바뀌어도 이만큼마다 한 번은 훑는다 (깊은 데 조용히 생긴 것 줍기)")]
    public float 강제훑기 = 120f;   // ★실측: 강제 훑기 한 번이 100ms 다 — 드물어야 한다

    float cd, 강제cd;
    int 지난자식수 = -1;

    // ★이미 무광으로 만든 재질은 다시 안 본다. 재질은 **공유**되므로 렌더러가 7만 개라도
    //   실제 재질은 수십 개뿐이다 — 그 수십 개만 기억하면 끝난다.
    readonly System.Collections.Generic.HashSet<Material> 처리됨 = new System.Collections.Generic.HashSet<Material>();
    // ★`r.sharedMaterials` 는 **부를 때마다 배열을 새로 만든다.** 7만 번이면 4MB —
    //   실측 `GC 4.1MB` 의 정체다. 목록을 재사용하는 `GetSharedMaterials` 로 바꾼다.
    static readonly System.Collections.Generic.List<Material> 재질버퍼 = new System.Collections.Generic.List<Material>();

    void Start() { 훑기(); }

    void Update()
    {
        if (다시훑기 <= 0f) return;
        cd -= Time.deltaTime;
        if (cd > 0f) return;
        cd = 다시훑기;

        // ★★★**새로 생긴 게 없으면 훑지 않는다** (2026-08-06 실측 — 주기적 끊김의 정체).
        //   씬 렌더러가 7만 개라, 할 일이 하나도 없어도 훑기 한 번이 **119ms + GC 4.1MB**
        //   였고 그게 `다시훑기 = 2초` 마다 왔다. 판별은 `씬바뀜` 이 거의 공짜로 한다.
        강제cd -= 다시훑기;
        int 셈 = 씬바뀜.자식수합();
        if (셈 == 지난자식수 && 강제cd > 0f) return;
        지난자식수 = 셈;
        if (강제cd <= 0f) 강제cd = 강제훑기;
        훑기();
    }

    /// 씬의 모든 렌더러 재질을 무광으로. 이미 무광이면 건드리지 않는다
    public void 훑기()
    {
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            r.GetSharedMaterials(재질버퍼);
            for (int i = 0; i < 재질버퍼.Count; i++)
            {
                var m = 재질버퍼[i];
                if (m == null) continue;
                if (!처리됨.Add(m)) continue;      // 이미 무광으로 만든 재질은 건너뛴다

                if (m.HasProperty("_Smoothness") && m.GetFloat("_Smoothness") > 매끄러움 + 0.001f)
                    m.SetFloat("_Smoothness", 매끄러움);
                if (m.HasProperty("_Glossiness") && m.GetFloat("_Glossiness") > 매끄러움 + 0.001f)
                    m.SetFloat("_Glossiness", 매끄러움);
                if (m.HasProperty("_Metallic") && m.GetFloat("_Metallic") > 금속성 + 0.001f)
                    m.SetFloat("_Metallic", 금속성);
                // 스펙큘러 하이라이트를 아예 끌 수 있는 재질이면 끈다
                if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
                if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0f);
            }
        }
    }
}
