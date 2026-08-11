using System.Collections.Generic;
using UnityEngine;

/// 지금 무엇에 걸리는지 — **대상 하나에만 외곽선을 씌운다.**
///
/// ★왜 필요한가: 이 게임은 F 와 좌클릭이 **앞에 무엇이 있느냐**로 갈리는데(4장),
///   정작 무엇이 잡혔는지 화면에 표시가 없었다. 나무 앞인지 짐승 앞인지 모른 채 누른다.
///   ☆8/7 에 `Outliner`(전체 윤곽선)가 **성능 때문에** 은퇴하면서 이 자리가 통째로 비었다.
///
/// ★★그래서 **씬을 훑지 않는다** — 상호작용 대상은 어차피 한 놈이다.
///   `Outliner` 는 매 프레임 씬 전체를 뒤져서 19.8ms(전체의 97%)를 썼다. 여기서는
///   이미 각 시스템이 쓰는 짧은 목록만 본다.
///
/// ★방식: **껍데기를 살짝 키워 앞면을 버리고 그린다**(뒤집힌 껍질).
///   셰이더가 필요 없고 드로우콜이 하나 는다. `Toyra/Outline` 은 후처리 마스크용이라 여기 못 쓴다.
///
/// ★색이 무엇을 할지 말해 준다 — 글자를 안 쓴다 (11장)
[RequireComponent(typeof(Hero))]
[DefaultExecutionOrder(400)]
public class 대상표시 : MonoBehaviour
{
    [Header("색 — 무엇을 할 수 있나")]
    [Tooltip("캘 수 있다 (나무·바위)")] public Color 캘것 = new Color(0.35f, 0.95f, 0.45f);   // ★초록 (2026-08-11 사용자)
    [Tooltip("★산 채로 잡을 수 있다 (지쳤거나 기절함)")] public Color 잡을것 = new Color(0.35f, 0.95f, 0.5f);
    [Tooltip("때릴 대상")] public Color 때릴것 = new Color(0.95f, 0.35f, 0.3f);
    [Tooltip("상호작용 (모닥불·무더기)")] public Color 만질것 = new Color(1f, 0.7f, 0.25f);

    [Header("두께")]
    [Tooltip("몇 초마다 대상을 다시 찾나 (0.1 = 초당 10번)")] public float 찾는간격 = 0.08f;

    Hero hero;
    HeroAttack 손;
    HeroCarry 안음;
    Transform 지금대상;
    GameObject 껍데기;
    float 찾을때까지;
    static Material 마스크재질, 선재질;

    void Awake()
    {
        hero = GetComponent<Hero>();
        손 = GetComponent<HeroAttack>();
        안음 = GetComponent<HeroCarry>();
    }

    void OnDisable() { 치우기(); }

    // ★스스로 시간을 잰다 (2026-08-11) — 스파이크에 「Late 24ms」 라고만 찍히면
    //   어느 부품인지 알 수가 없다. 빈도가 이 증상과 맞아 제일 유력한 후보다
    [Tooltip("이보다 오래 걸린 프레임을 콘솔에 남긴다 (ms · 0 이면 안 남김)")]
    public float 스스로재기 = 5f;
    // ★★진단 스위치 — 대상이 **바뀔 때만** 한 줄 찍는다 (매 프레임 로그는 그 자체가 렉이다 · 9-4)
    [Tooltip("외곽선이 왜 안 보이는지 콘솔에 남긴다 — 잡고 나면 끈다")]
    public bool 진단 = true;

    void LateUpdate()
    {
        찾을때까지 -= Time.deltaTime;
        if (찾을때까지 > 0f) return;
        찾을때까지 = 찾는간격;

        var 시계 = 스스로재기 > 0f ? System.Diagnostics.Stopwatch.StartNew() : null;
        double 찾은뒤 = 0;

        Transform 대상 = null; Color 색 = 캘것;
        if (hero.Alive) 대상 = 찾기(out 색);
        if (시계 != null) 찾은뒤 = 시계.Elapsed.TotalMilliseconds;

        bool 바뀜 = 대상 != 지금대상;
        if (바뀜) { 치우기(); 지금대상 = 대상; if (대상 != null) 씌우기(대상, 색); }
        else if (껍데기 != null) 색칠(색);

        if (시계 != null)
        {
            시계.Stop();
            double ms = 시계.Elapsed.TotalMilliseconds;
            if (ms >= 스스로재기)
                Debug.LogFormat("[대상표시-느림] {0:F1}ms (찾기 {1:F1} · 씌우기 {2:F1}) · 바뀜={3} · 대상={4}",
                    ms, 찾은뒤, ms - 찾은뒤, 바뀜, 대상 != null ? 대상.name : "없음");
        }
    }

    // ══════════════════════════════════════════════════════════ 무엇이 대상인가

    /// ★각 시스템이 실제로 쓰는 것과 **같은 순서**로 본다.
    ///   순서가 다르면 "표시된 것과 다른 게 잡히는" 최악의 버그가 된다
    Transform 찾기(out Color 색)
    {
        색 = 캘것;
        var p = transform.position;
        var look = hero.LookDir;

        // 데려가는 중이면 손이 차 있다 — 아무것도 안 걸린다
        if (안음 != null && 안음.데려가는것 != null) return null;

        // ① 산 채로 잡을 수 있는 놈 (`HeroCarry.붙잡기` 와 같은 잣대)
        if (안음 != null)
        {
            Critter best = null; float bd = 안음.손닿는거리 * 안음.손닿는거리;
            foreach (var c in Critter.All)
            {
                if (c == null || !c.지침) continue;
                float d2 = (c.transform.position - p).sqrMagnitude;
                if (d2 > bd) continue;
                bd = d2; best = c;
            }
            if (best != null) { 색 = 잡을것; return best.transform; }
        }

        // ② 모닥불 (`모닥불.가까운것` 과 같은 잣대)
        var 불 = 모닥불.가까운것(p, 3f);
        if (불 != null) { 색 = 만질것; return 불.transform; }

        // ③ 땅 무더기
        var 짐 = 땅무더기.가까운것(p, 2.6f);
        if (짐 != null) { 색 = 만질것; return 짐.transform; }

        // ④ 앞쪽의 살아 있는 야생 (`HeroAttack.Sweep` 과 같은 잣대)
        if (손 != null && 손.enabled)
        {
            Critter best = null; float bd = float.MaxValue;
            foreach (var c in Critter.All)
            {
                if (c == null || !c.Alive || c.side != Critter.Side.야생) continue;
                var v = c.transform.position - p; v.y = 0f;
                float d = v.magnitude;
                if (d > 손.사거리 + c.Radius) continue;
                if (d > 0.01f && Vector3.Dot(v / d, look) < 0.35f) continue;   // 앞쪽만
                if (d >= bd) continue;
                bd = d; best = c;
            }
            if (best != null) { 색 = 때릴것; return best.transform; }
        }

        // ⑤ 캘 것 — ★★★★**F 가 부르는 바로 그 함수를 부른다** (2026-08-11 사용자
        //   "여전히.. 외곽 선택되었다고 안뜸").
        //   여기 옛 코드가 **점 하나**로 재는 옛 방식을 그대로 복사해 갖고 있었다 —
        //   반경도, 경계도, 방향무관도 없었다. 그래서 `Harvest.찾기` 를 고쳐도
        //   **표시만 안 떴다.** 두 자가 다르면 언제든 다시 어긋난다.
        //   ☆이 파일의 원칙이 바로 그것이다 (82행 "각 시스템이 실제로 쓰는 것과 같은 순서").
        //     `조준표시` 도 사거리를 베끼지 않고 `HeroAttack.닿는거리()` 를 부른다 — 같은 길이다.
        var hb = Harvest.찾기(p, look, (손 != null ? 손.사거리 : 2.2f) + 0.6f);
        if (hb != null) { 색 = 캘것; return hb.transform; }

        return null;
    }

    // ══════════════════════════════════════════════════════════ 껍데기

    void 씌우기(Transform 대상, Color 색)
    {
        껍데기 = new GameObject("외곽선");
        껍데기.transform.SetParent(대상, false);
        껍데기.transform.localPosition = Vector3.zero;
        껍데기.transform.localRotation = Quaternion.identity;
        껍데기.transform.localScale = Vector3.one;      // 부풀림은 셰이더 노멀 푸시가 맡는다

        재질만들기();
        if (선재질 == null)
        {
            if (진단) Debug.LogWarning("[대상표시-진단] 선재질이 null — 셰이더를 못 찾았다. 껍데기만 빈 채로 남는다");
            return;
        }

        // ★겹은 **마스크·선 두 장씩** — 마스크(큐 2001)가 원본 실루엣을 스텐실에 굽고,
        //   선(큐 2002)은 스텐실 밖에만 그려져 **최외곽만** 남는다 (2026-08-11 사용자
        //   "모델링 안쪽까지 실루엣을 보이게 하면 안 됨"). 큐가 순서를 강제하므로
        //   메시가 여럿이어도 안쪽 이음선이 안 샌다.
        int 만든겹 = 0, 본MF = 0, 거른MF = 0;
        foreach (var mf in 대상.GetComponentsInChildren<MeshFilter>(false))
        {
            본MF++;
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || !mr.enabled || mf.sharedMesh == null) { 거른MF++; continue; }
            겹만들기(mf.transform, 대상, mf.sharedMesh, 마스크재질);
            겹만들기(mf.transform, 대상, mf.sharedMesh, 선재질);
            만든겹 += 2;
        }

        // 뼈가 있는 몸(리깅 모델)은 뼈를 나눠 써야 자세가 따라온다
        int 본SMR = 0;
        foreach (var sm in 대상.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            본SMR++;
            if (!sm.enabled || sm.sharedMesh == null) continue;
            뼈겹만들기(sm, 마스크재질);
            뼈겹만들기(sm, 선재질);
            만든겹 += 2;
        }

        // ★★진단 (2026-08-11) — 「찾기도 되고 껍데기도 만들어졌는데 안 보인다」를 잡는 자리.
        //   겹이 0 이면 그릴 게 아무것도 없다는 뜻이다. 무엇 때문에 걸러졌는지까지 남긴다.
        if (진단)
            Debug.LogFormat("[대상표시-진단] 대상={0} · 겹={1} (MeshFilter 본 {2}/거른 {3} · Skinned 본 {4})"
                          + " · 대상크기={5} · 활성={6}",
                대상.name, 만든겹, 본MF, 거른MF, 본SMR,
                대상.lossyScale.ToString("F2"), 대상.gameObject.activeInHierarchy);

        색칠(색);
    }

    void 겹만들기(Transform 원본, Transform 대상, Mesh mesh, Material mat)
    {
        var g = new GameObject("겹");
        g.transform.SetParent(껍데기.transform, false);
        // 대상 기준의 자리를 그대로 물려받는다
        g.transform.localPosition = 대상.InverseTransformPoint(원본.position);
        g.transform.localRotation = Quaternion.Inverse(대상.rotation) * 원본.rotation;
        // ★부모가 대상이라 대상 스케일이 다시 곱해진다 — lossyScale 을 그대로 넣으면
        //   껍데기가 스케일 **제곱**이 된다 (스케일 5짜리 나무 = 화면을 덮는 흰 덩어리, 2026-08-11)
        var ls = 원본.lossyScale; var ts = 대상.lossyScale;
        g.transform.localScale = new Vector3(ls.x / ts.x, ls.y / ts.y, ls.z / ts.z);

        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = g.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    void 뼈겹만들기(SkinnedMeshRenderer sm, Material mat)
    {
        var g = new GameObject("겹");
        g.transform.SetParent(껍데기.transform, false);
        var r = g.AddComponent<SkinnedMeshRenderer>();
        r.sharedMesh = sm.sharedMesh;
        r.bones = sm.bones;                 // ★같은 뼈를 쓴다 — 동작까지 따라온다
        r.rootBone = sm.rootBone;
        r.sharedMaterial = mat;
        r.updateWhenOffscreen = true;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    static void 재질만들기()
    {
        if (선재질 != null) return;
        var sh = Shader.Find("토이라/대상외곽");
        if (sh == null) { Debug.LogWarning("[대상표시] 토이라/대상외곽 셰이더가 없다 — Assets/Game/Shaders/대상외곽.shader"); return; }
        마스크재질 = new Material(sh) { name = "대상외곽_마스크", renderQueue = 2001 };
        마스크재질.SetFloat("_Mode", 0f);
        마스크재질.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
        마스크재질.SetFloat("_ColorMask", 0f);                       // 색을 안 쓴다 — 스텐실만
        마스크재질.SetFloat("_StencilComp", (float)UnityEngine.Rendering.CompareFunction.Always);
        선재질 = new Material(sh) { name = "대상외곽_선", renderQueue = 2002 };
        선재질.SetFloat("_Mode", 1f);
        선재질.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Front);
        선재질.SetFloat("_ColorMask", 15f);
        선재질.SetFloat("_StencilComp", (float)UnityEngine.Rendering.CompareFunction.NotEqual);
    }

    MaterialPropertyBlock 값;
    void 색칠(Color c)
    {
        if (껍데기 == null || 선재질 == null) return;
        값 ??= new MaterialPropertyBlock();
        값.SetColor("_OutlineColor", c);
        foreach (var r in 껍데기.GetComponentsInChildren<Renderer>(true))
            if (r.sharedMaterial == 선재질) r.SetPropertyBlock(값);
    }

    void 치우기()
    {
        if (껍데기 != null) Destroy(껍데기);
        껍데기 = null; 지금대상 = null;
    }
}
