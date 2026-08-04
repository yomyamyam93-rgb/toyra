using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// 네발 동작을 **직접 찍는다** — 남의 클립을 옮겨 붙이지 않는다.
///
/// ★왜 이 방식인가 (2026-08-05 사용자 "이식하지말고 … 세세한 계획을 한 뒤 적용").
///   이식은 계속 어그러졌다. 원인을 재 보니 **뼈의 로컬 축이 모델마다 다르다**:
/// <code>
///     다이어울프 spine_hip  로컬Y = 세계(-0.02, 0.20, 0.98)   ← 위축이 앞을 본다
///     큰뿔사슴   spine_back 로컬X = 세계(0.07, -0.99, -0.16)  ← 옆축이 아래를 본다
/// </code>
///   즉 "뼈를 X로 20도" 라는 말이 모델마다 딴 뜻이다. 그래서 각도를 **몸 기준 세계축**으로
///   적고, 구울 때 그 모델의 쉬는 자세를 실측해 로컬로 환산한다:
/// <code>
///     새로컬 = 부모쉬는회전⁻¹ · 회전 · 부모쉬는회전 · 쉬는로컬
/// </code>
///   부모가 돌면 자식이 딸려 오므로 **한 번 적으면 어느 몸에서도 같은 동작**이 된다
///   (`HeroHold` 의 `몸 · Euler · 몸⁻¹` 과 같은 수법).
///
/// ★부호 — 전부 실측에서 나왔다 (모델은 전부 머리 +Z · 위 +Y · 꼬리 -Z):
/// <code>
///     옆(X) +  숙임 · 다리는 뒤로 밀기      -  듦 · 다리는 앞으로 뻗기
///     위(Y) +  오른쪽으로 돌림
///     앞(Z) +  오른쪽이 위로 (= 왼쪽으로 쓰러짐)
/// </code>
///
/// ★회전만 담는다 — 뼈 길이가 모델마다 달라 위치를 미터로 적으면 큰 놈이 찢어진다.
///   단 **뿌리뼈(`spine_hip`)의 위치만 예외**다. 회전으로는 "앞으로 튀어나가기" 를 못 만든다.
///   대신 미터가 아니라 **제 몸길이의 배수**로 적고 구울 때 실측 크기로 환산한다.
public static class 네발동작저작
{
    public const string 저장 = "Assets/Game/Resources/rig";
    public const int 초당 = 30;

    public enum 축 { 옆, 위, 앞 }

    public enum 바닥 { 그대로, 안파묻힘, 딱붙임 }

    /// 구간을 **어떻게** 건너는가. 등속(선형)은 기계처럼 보여 거의 안 쓴다.
    public enum 결
    {
        선형,       // 등속 — 거의 쓰지 않는다
        들어감,     // 천천히 시작해 폭발  (예비 정점 → 본동작)
        나감,       // 빠르게 갔다 감속    (예비·반동)
        부드럽게,   // 가속-감속 S곡선     (정착·이동)
        되튐,       // 지나쳤다 돌아옴     (여운 — 쫀득함의 정체)
    }

    public struct 키
    {
        public float t, 값; public 결 c;
        public 키(float t, float 값, 결 c = 결.부드럽게) { this.t = t; this.값 = 값; this.c = c; }
    }

    /// 한 뼈의 한 축(또는 뿌리 위치 한 성분)에 대한 키 열
    public class 줄
    {
        public string 뼈; public 축 축; public 키[] 키들;
        public float 위상;          // 0~1 — 다리 넷을 같은 키로 쓰되 시간만 어긋나게
        public int 위치성분 = -1;   // 0=x 1=y 2=z 이면 회전이 아니라 뿌리 위치 (몸길이 배수)
    }

    public class 동작
    {
        public string 이름; public float 길이 = 1f; public bool 반복;
        /// 구운 뒤 **실제로 재서** 뿌리 높이를 고친다 (아래 `땅에붙이기`).
        /// 기본은 `안파묻힘` — 아래로만 막으므로 뛰어오르는 건 그대로 산다.
        /// `딱붙임` 은 뜨는 것도 끌어내린다 (죽음처럼 반드시 바닥에 놓여야 하는 것).
        public 바닥 바닥맞춤 = 바닥.안파묻힘;
        public List<줄> 줄들 = new List<줄>();

        public 동작 회전(string 뼈, 축 축, params 키[] 키들)
        { 줄들.Add(new 줄 { 뼈 = 뼈, 축 = 축, 키들 = 키들 }); return this; }

        public 동작 회전(string 뼈, 축 축, float 위상, params 키[] 키들)
        { 줄들.Add(new 줄 { 뼈 = 뼈, 축 = 축, 키들 = 키들, 위상 = 위상 }); return this; }

        /// 뿌리 위치 — **몸길이 배수** (+z 앞 · +y 위)
        public 동작 뿌리(int 성분, params 키[] 키들)
        { 줄들.Add(new 줄 { 뼈 = "spine_hip", 위치성분 = 성분, 키들 = 키들 }); return this; }

        /// 다리 넷을 같은 키로 깔되 **위상만 어긋나게** 한다.
        /// ★대각 짝(FL↔BR)이 유지되므로 모델마다 좌우가 뒤집혀 있어도(실측 확인) 상관없다.
        public 동작 다리(string 다리, float 위상, 키[] 허벅, 키[] 정강)
        {
            줄들.Add(new 줄 { 뼈 = 다리 + "_thigh", 축 = 축.옆, 키들 = 허벅, 위상 = 위상 });
            줄들.Add(new 줄 { 뼈 = 다리 + "_shin", 축 = 축.옆, 키들 = 정강, 위상 = 위상 });
            return this;
        }
    }

    // ───────────────────────────────── 이징

    static float 곡선(float s, 결 c)
    {
        s = Mathf.Clamp01(s);
        switch (c)
        {
            case 결.선형: return s;
            case 결.들어감: return s * s;
            case 결.나감: return 1f - (1f - s) * (1f - s);
            case 결.되튐: { float u = s - 1f; return 1f + 2.70158f * u * u * u + 1.70158f * u * u; }
            default: return s * s * (3f - 2f * s);
        }
    }

    /// 키 열을 진행도 u(0~1)에서 읽는다. **곡선은 도착 키가 갖는다** (그 구간을 어떻게 건넜나).
    static float 값(줄 r, float u)
    {
        float x = r.위상 != 0f ? Mathf.Repeat(u + r.위상, 1f) : u;
        var k = r.키들;
        if (x <= k[0].t) return k[0].값;
        for (int i = 0; i < k.Length - 1; i++)
            if (x <= k[i + 1].t)
            {
                float s = k[i + 1].t > k[i].t ? (x - k[i].t) / (k[i + 1].t - k[i].t) : 1f;
                return Mathf.Lerp(k[i].값, k[i + 1].값, 곡선(s, k[i + 1].c));
            }
        return k[k.Length - 1].값;
    }

    // ───────────────────────────────── 굽기

    /// 그 몸의 **쉬는 자세를 실측해서** 클립을 굽는다.
    public static AnimationClip 굽기(동작 m, GameObject 견본)
    {
        var g = Object.Instantiate(견본);
        g.transform.position = Vector3.zero; g.transform.rotation = Quaternion.identity;
        g.transform.localScale = Vector3.one;

        // ★껍데기를 여기서도 씌운다 — 클립 경로가 `Armature/…` 라 없으면 **조용히 안 걸린다.**
        //   실제로 이것 때문에 `땅붙임` 이 껍데기 있는 브론토에서만 먹었다 (나머지는 보정 0).
        {
            var 뿌리 = g.transform.Find("spine_hip");
            if (뿌리 != null)
            {
                var 껍 = new GameObject("Armature").transform;
                껍.SetParent(g.transform, false);
                뿌리.SetParent(껍, true);
            }
        }

        // 뼈 찾기 — `Armature` 껍데기가 있는 모델도 없는 모델도 있어서 **이름으로** 훑는다
        var 뼈 = new Dictionary<string, Transform>();
        var 길 = new Dictionary<string, string>();
        foreach (var t in g.GetComponentsInChildren<Transform>(true))
        {
            if (뼈.ContainsKey(t.name)) continue;
            뼈[t.name] = t;
            string p = ""; var c = t;
            while (c != null && c != g.transform) { p = c.name + (p == "" ? "" : "/" + p); c = c.parent; }
            // 클립 경로는 언제나 `Armature/…` — 껍데기가 없는 모델은 세울 때 씌워 준다
            길[t.name] = p.StartsWith("Armature/") ? p : "Armature/" + p;
        }

        // 몸길이 — 뿌리 위치를 미터로 환산할 자. **실측**이다
        float 몸길이 = 1f;
        {
            bool 첫 = true; var b = new Bounds();
            foreach (var r in g.GetComponentsInChildren<Renderer>(true))
            { if (첫) { b = r.bounds; 첫 = false; } else b.Encapsulate(r.bounds); }
            if (!첫) 몸길이 = Mathf.Max(0.01f, Mathf.Max(b.size.z, b.size.y));
        }

        // 쉬는 자세 — 이게 이 도구의 심장이다
        var 쉬는로컬 = new Dictionary<string, Quaternion>();
        var 부모세계 = new Dictionary<string, Quaternion>();
        var 쉬는위치 = new Dictionary<string, Vector3>();
        foreach (var kv in 뼈)
        {
            쉬는로컬[kv.Key] = kv.Value.localRotation;
            쉬는위치[kv.Key] = kv.Value.localPosition;
            부모세계[kv.Key] = kv.Value.parent != null ? kv.Value.parent.rotation : Quaternion.identity;
        }

        // 줄을 뼈별로 모은다
        var 뼈별회전 = new Dictionary<string, List<줄>>();
        var 뼈별위치 = new Dictionary<string, List<줄>>();
        foreach (var r in m.줄들)
        {
            if (!뼈.ContainsKey(r.뼈)) continue;
            var d = r.위치성분 >= 0 ? 뼈별위치 : 뼈별회전;
            if (!d.TryGetValue(r.뼈, out var l)) d[r.뼈] = l = new List<줄>();
            l.Add(r);
        }

        var clip = new AnimationClip { name = m.이름, frameRate = 초당 };
        int 프레임 = Mathf.Max(2, Mathf.RoundToInt(m.길이 * 초당) + 1);

        foreach (var kv in 뼈별회전)
        {
            var 이름 = kv.Key;
            var P = 부모세계[이름]; var Pi = Quaternion.Inverse(P); var R = 쉬는로컬[이름];
            var cx = new AnimationCurve(); var cy = new AnimationCurve();
            var cz = new AnimationCurve(); var cw = new AnimationCurve();
            Quaternion 앞것 = R;
            for (int f = 0; f < 프레임; f++)
            {
                float t = f / (float)초당, u = m.길이 > 0f ? Mathf.Clamp01(t / m.길이) : 0f;
                float 옆 = 0f, 위 = 0f, 앞 = 0f;
                foreach (var r in kv.Value)
                {
                    float v = 값(r, u);
                    if (r.축 == 축.옆) 옆 += v; else if (r.축 == 축.위) 위 += v; else 앞 += v;
                }
                var W = Quaternion.Euler(옆, 위, 앞);
                var q = (Pi * W * P) * R;
                if (Quaternion.Dot(q, 앞것) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w); // 뒤집힘 방지
                앞것 = q;
                cx.AddKey(t, q.x); cy.AddKey(t, q.y); cz.AddKey(t, q.z); cw.AddKey(t, q.w);
            }
            string p = 길[이름];
            clip.SetCurve(p, typeof(Transform), "m_LocalRotation.x", cx);
            clip.SetCurve(p, typeof(Transform), "m_LocalRotation.y", cy);
            clip.SetCurve(p, typeof(Transform), "m_LocalRotation.z", cz);
            clip.SetCurve(p, typeof(Transform), "m_LocalRotation.w", cw);
        }

        foreach (var kv in 뼈별위치)
        {
            var 이름 = kv.Key; var 기본 = 쉬는위치[이름];
            var cx = new AnimationCurve(); var cy = new AnimationCurve(); var cz = new AnimationCurve();
            for (int f = 0; f < 프레임; f++)
            {
                float t = f / (float)초당, u = m.길이 > 0f ? Mathf.Clamp01(t / m.길이) : 0f;
                Vector3 d = Vector3.zero;
                foreach (var r in kv.Value) d[r.위치성분] += 값(r, u) * 몸길이;
                cx.AddKey(t, 기본.x + d.x); cy.AddKey(t, 기본.y + d.y); cz.AddKey(t, 기본.z + d.z);
            }
            string p = 길[이름];
            clip.SetCurve(p, typeof(Transform), "m_LocalPosition.x", cx);
            clip.SetCurve(p, typeof(Transform), "m_LocalPosition.y", cy);
            clip.SetCurve(p, typeof(Transform), "m_LocalPosition.z", cz);
        }

        if (m.바닥맞춤 != 바닥.그대로 && 길.ContainsKey("spine_hip"))
            땅에붙이기(clip, g, 길["spine_hip"], 쉬는위치["spine_hip"], m.길이, m.바닥맞춤 == 바닥.딱붙임);

        var s = AnimationUtility.GetAnimationClipSettings(clip);
        s.loopTime = m.반복;
        AnimationUtility.SetAnimationClipSettings(clip, s);

        Object.DestroyImmediate(g);
        return clip;
    }

    /// ★★계산으로는 못 맞춘다 — **구운 걸 재서 고친다** (2026-08-05).
    ///   옆으로 굴러 눕는 높이를 `몸너비÷2 - 골반높이` 로 계산해 넣었는데 넷 다 땅에 파묻혔다
    ///   (늑대 0.25m · 브론토 0.67m). 굴러가는 동안 최저점이 골반이 아니라 **목·꼬리로
    ///   옮겨다니기** 때문이다. 그래서 프레임마다 실제 최저점을 재서 그만큼 올린다.
    ///   보정은 뿌리 높이 하나만 움직이므로 한 번에 정확히 맞는다 (선형 관계).
    static void 땅에붙이기(AnimationClip clip, GameObject g, string 뿌리길, Vector3 쉬는뿌리, float 길이, bool 딱)
    {
        System.Func<float> 바닥 = () =>
        {
            float lo = float.MaxValue;
            foreach (var r in g.GetComponentsInChildren<Renderer>(true))
            {
                var sk = r as SkinnedMeshRenderer; if (sk == null) continue;
                var mesh = new Mesh(); sk.BakeMesh(mesh, true);
                var mtx = sk.transform.localToWorldMatrix;
                foreach (var v in mesh.vertices) { float y = mtx.MultiplyPoint3x4(v).y; if (y < lo) lo = y; }
                Object.DestroyImmediate(mesh);
            }
            return lo;
        };

        clip.SampleAnimation(g, 0f);
        float 쉬는바닥 = 바닥();

        var 기존 = AnimationUtility.GetEditorCurve(clip,
            EditorCurveBinding.FloatCurve(뿌리길, typeof(Transform), "m_LocalPosition.y"));
        var 새 = new AnimationCurve();
        int 프레임 = Mathf.Max(2, Mathf.RoundToInt(길이 * 초당) + 1);
        for (int f = 0; f < 프레임; f++)
        {
            float t = f / (float)초당;
            clip.SampleAnimation(g, t);
            float 밑 = 바닥();
            float y = 기존 != null ? 기존.Evaluate(t) : 쉬는뿌리.y;
            float 보정 = 쉬는바닥 - 밑;
            새.AddKey(t, y + (딱 ? 보정 : Mathf.Max(0f, 보정)));   // 딱이 아니면 **아래로만** 막는다
        }
        if (!딱)   // 아무 프레임도 안 파묻혔으면 굳이 커브를 남기지 않는다
        {
            bool 쓸모 = 기존 != null;
            for (int f = 0; f < 프레임 && !쓸모; f++)
                if (Mathf.Abs(새.Evaluate(f / (float)초당) - 쉬는뿌리.y) > 1e-4f) 쓸모 = true;
            if (!쓸모) return;
        }
        clip.SetCurve(뿌리길, typeof(Transform), "m_LocalPosition.y", 새);
    }

    // ───────────────────────────────── 메뉴

    [MenuItem("Tools/토이라기/㉧ 네발 동작 찍기", priority = 7)]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        { Debug.LogError("[저작] 플레이 중엔 못 굽는다 — 멈추고 다시."); return; }

        var 모델 = new List<GameObject>();
        foreach (var g in AssetDatabase.FindAssets("t:GameObject", new[] { 저장 }))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (!p.EndsWith(".glb")) continue;
            var o = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (o != null && o.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) 모델.Add(o);
        }
        if (모델.Count == 0) { Debug.LogError("[저작] " + 저장 + " 에 리깅된 모델이 없다"); return; }

        int 만든수 = 0;
        foreach (var 몸 in 모델)
        {
            foreach (var m in 네발동작표.만들기(몸.name))
            {
                var clip = 굽기(m, 몸);
                string ap = 저장 + "/" + m.이름 + "_" + 몸.name + ".anim";
                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(ap) != null) AssetDatabase.DeleteAsset(ap);
                AssetDatabase.CreateAsset(clip, ap);

                string cp = 저장 + "/" + m.이름 + "_" + 몸.name + ".controller";
                if (AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(cp) != null)
                    AssetDatabase.DeleteAsset(cp);
                var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(cp);
                var st = ctrl.layers[0].stateMachine.AddState(m.이름);
                st.motion = clip; st.writeDefaultValues = true;
                ctrl.layers[0].stateMachine.defaultState = st;
                EditorUtility.SetDirty(ctrl);
                만든수++;
            }
            Debug.Log("[저작] " + 몸.name + " — 동작 " + 네발동작표.만들기(몸.name).Count + "개");
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[저작] 클립 " + 만든수 + "개 완료");
    }
}
