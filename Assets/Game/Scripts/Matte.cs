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

    float cd;

    void Start() { 훑기(); }

    void Update()
    {
        if (다시훑기 <= 0f) return;
        cd -= Time.deltaTime;
        if (cd > 0f) return;
        cd = 다시훑기;
        훑기();
    }

    /// 씬의 모든 렌더러 재질을 무광으로. 이미 무광이면 건드리지 않는다
    public void 훑기()
    {
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;

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
