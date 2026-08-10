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
    [Tooltip("캘 수 있다 (나무·바위)")] public Color 캘것 = new Color(0.95f, 0.95f, 0.9f);
    [Tooltip("★산 채로 잡을 수 있다 (지쳤거나 기절함)")] public Color 잡을것 = new Color(0.35f, 0.95f, 0.5f);
    [Tooltip("때릴 대상")] public Color 때릴것 = new Color(0.95f, 0.35f, 0.3f);
    [Tooltip("상호작용 (모닥불·무더기)")] public Color 만질것 = new Color(1f, 0.7f, 0.25f);

    [Header("두께")]
    [Tooltip("껍데기를 몇 배로 부풀리나")] [Range(1.01f, 1.2f)] public float 부풀리기 = 1.05f;
    [Tooltip("몇 초마다 대상을 다시 찾나 (0.1 = 초당 10번)")] public float 찾는간격 = 0.08f;

    Hero hero;
    HeroAttack 손;
    HeroCarry 안음;
    Transform 지금대상;
    GameObject 껍데기;
    float 찾을때까지;
    static Material 재질;

    void Awake()
    {
        hero = GetComponent<Hero>();
        손 = GetComponent<HeroAttack>();
        안음 = GetComponent<HeroCarry>();
    }

    void OnDisable() { 치우기(); }

    void LateUpdate()
    {
        찾을때까지 -= Time.deltaTime;
        if (찾을때까지 > 0f) return;
        찾을때까지 = 찾는간격;

        Transform 대상 = null; Color 색 = 캘것;
        if (hero.Alive) 대상 = 찾기(out 색);

        if (대상 != 지금대상) { 치우기(); 지금대상 = 대상; if (대상 != null) 씌우기(대상, 색); }
        else if (껍데기 != null) 색칠(색);
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

        // ⑤ 캘 것 (`Harvest.TryHarvest` 와 같은 잣대)
        float 닿음 = (손 != null ? 손.사거리 : 2.2f) + 0.4f;
        Harvest hb = null; float hd = 닿음 * 닿음;
        for (int i = Harvest.All.Count - 1; i >= 0; i--)
        {
            var h = Harvest.All[i];
            if (h == null) continue;
            var v = h.transform.position - p; v.y = 0f;
            float d2 = v.sqrMagnitude;
            if (d2 > hd) continue;
            if (d2 > 0.01f && Vector3.Dot(v.normalized, look) < 0.2f) continue;
            hd = d2; hb = h;
        }
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
        껍데기.transform.localScale = Vector3.one * 부풀리기;

        재질만들기();

        // 대상의 몸을 그대로 한 겹 복사한다
        foreach (var mf in 대상.GetComponentsInChildren<MeshFilter>(false))
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || !mr.enabled || mf.sharedMesh == null) continue;

            var g = new GameObject("겹");
            g.transform.SetParent(껍데기.transform, false);
            // 대상 기준의 자리를 그대로 물려받는다
            g.transform.localPosition = 대상.InverseTransformPoint(mf.transform.position);
            g.transform.localRotation = Quaternion.Inverse(대상.rotation) * mf.transform.rotation;
            // ★부모가 대상이라 대상 스케일이 다시 곱해진다 — lossyScale 을 그대로 넣으면
            //   껍데기가 스케일 **제곱**이 된다 (스케일 5짜리 나무 = 화면을 덮는 흰 덩어리, 2026-08-11)
            var ls = mf.transform.lossyScale; var ts = 대상.lossyScale;
            g.transform.localScale = new Vector3(ls.x / ts.x, ls.y / ts.y, ls.z / ts.z);

            g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = 재질;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // 뼈가 있는 몸(리깅 모델)은 뼈를 나눠 써야 자세가 따라온다
        foreach (var sm in 대상.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (!sm.enabled || sm.sharedMesh == null) continue;
            var g = new GameObject("겹");
            g.transform.SetParent(껍데기.transform, false);
            var r = g.AddComponent<SkinnedMeshRenderer>();
            r.sharedMesh = sm.sharedMesh;
            r.bones = sm.bones;                 // ★같은 뼈를 쓴다 — 동작까지 따라온다
            r.rootBone = sm.rootBone;
            r.sharedMaterial = 재질;
            r.updateWhenOffscreen = true;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        색칠(색);
    }

    static void 재질만들기()
    {
        if (재질 != null) return;
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        재질 = new Material(sh) { name = "외곽선" };
        // ★앞면을 버린다 — 뒤집힌 껍질이라 **실루엣 바깥에만** 남는다
        재질.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Front);
        재질.renderQueue = 2000;
    }

    MaterialPropertyBlock 값;
    void 색칠(Color c)
    {
        if (껍데기 == null) return;
        값 ??= new MaterialPropertyBlock();
        값.SetColor("_BaseColor", c);
        값.SetColor("_Color", c);
        foreach (var r in 껍데기.GetComponentsInChildren<Renderer>(true)) r.SetPropertyBlock(값);
    }

    void 치우기()
    {
        if (껍데기 != null) Destroy(껍데기);
        껍데기 = null; 지금대상 = null;
    }
}
