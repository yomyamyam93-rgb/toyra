using UnityEngine;

/// 주인공의 몸 — **여자와 남자를 갈아 끼운다.**
///
/// ★상자는 지우지 않고 끈다 (「은퇴는 삭제가 아니라 스위치」). 모델이 없거나
///  비워두면 도로 상자가 나온다 — 시스템은 모델 없이도 돌아가야 한다.
///
/// ★모델은 **1유닛 = 1미터**로 들어온다 (실측 1.8000m · 발바닥 y=0 · 스케일 1).
///  크기는 상수로 박지 않고 **`Hero.height` 에 맞춰 재서 맞춘다** — 모델을 다시
///  뽑아도, 키를 바꿔도 그림과 판정이 저절로 같이 간다 (「그림 = 판정」).
///
/// 모션은 유니티에서 붙인다 — 뼈 이름이 휴머노이드 규격이라 클립 하나를
/// 남녀 양쪽에 그대로 쓴다. `Animator.runtimeAnimatorController` 만 꽂으면 된다.
[ExecuteAlways]
[DefaultExecutionOrder(-5)]
public class HeroBody : MonoBehaviour
{
    public enum 성별 { 여자, 남자 }

    [Header("누구로 나갈까")]
    [Tooltip("바꾸면 즉시 갈아 끼워진다 (실행 중에도)")]
    public 성별 지금 = 성별.여자;

    [Header("모델")]
    public GameObject 여자모델;
    public GameObject 남자모델;

    [Header("살 재질 — 비우면 모델에 딸린 것을 쓴다")]
    public Material 재질;

    // ★컨트롤러를 여기서 문다. `HeroAnim` 은 실행 중에만 도는데, 거기서 물리면
    //  에디터에서 애니메이터가 비어 있어 **애니메이션 창에 키프레임이 안 뜬다.**
    [Header("모션 컨트롤러")]
    public RuntimeAnimatorController 여자컨트롤러;
    public RuntimeAnimatorController 남자컨트롤러;

    [Header("모델이 없을 때 쓰는 회색 상자")]
    public Transform 상자;

    /// 붙은 모델의 애니메이터 — 모션을 붙일 때 여기에 컨트롤러를 꽂는다
    public Animator Anim { get; private set; }

    GameObject 현재;
    성별 만든것;
    bool 지었나;

    GameObject 고른모델 => 지금 == 성별.여자 ? 여자모델 : 남자모델;

    void OnEnable() { 다시짓기(); }

    void OnValidate()
    {
        // 인스펙터에서 값이 바뀐 순간에는 오브젝트를 못 만든다 → 다음 틱에 짓는다
        지었나 = false;
    }

    void Update()
    {
        if (!지었나 || 현재 == null || 만든것 != 지금) 다시짓기();
    }

    public void 다시짓기()
    {
        지었나 = true;
        만든것 = 지금;

        if (현재 != null) 지우기(현재);
        현재 = null;
        Anim = null;

        var 원본 = 고른모델;
        if (원본 == null)
        {
            if (상자 != null) 상자.gameObject.SetActive(true);
            return;
        }

        현재 = Instantiate(원본, transform);
        현재.name = "몸_" + 지금;
        현재.transform.localPosition = Vector3.zero;
        현재.transform.localRotation = Quaternion.identity;
        현재.transform.localScale = Vector3.one;
        현재.hideFlags = HideFlags.DontSave;      // 씬 파일에 안 굳는다 (켤 때마다 새로)
        Anim = 현재.GetComponentInChildren<Animator>();

        var ctrl = 지금 == 성별.여자 ? 여자컨트롤러 : 남자컨트롤러;
        if (Anim != null && ctrl != null) Anim.runtimeAnimatorController = ctrl;

        키맞추기();
        if (재질 != null)
            foreach (var r in 현재.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterial = 재질;
        if (상자 != null) 상자.gameObject.SetActive(false);
    }

    /// 모델을 실제로 재서 `Hero.height` 가 되도록 배율을 낸다.
    /// ★배율을 손으로 적지 않는다 — 모델을 다시 뽑아도 저절로 맞는다.
    void 키맞추기()
    {
        if (현재 == null) return;
        var hero = GetComponent<Hero>();
        float 목표 = hero != null ? hero.height : 1.8f;

        현재.transform.localScale = Vector3.one;
        var smr = 현재.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr == null || smr.sharedMesh == null) return;

        float lo = float.MaxValue, hi = float.MinValue;
        var vs = smr.sharedMesh.vertices;
        for (int i = 0; i < vs.Length; i++)
        {
            float y = transform.InverseTransformPoint(smr.transform.TransformPoint(vs[i])).y;
            if (y < lo) lo = y;
            if (y > hi) hi = y;
        }
        float 잰키 = hi - lo;
        if (잰키 > 0.01f) 현재.transform.localScale = Vector3.one * (목표 / 잰키);
    }

    static void 지우기(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }
}
