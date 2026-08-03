using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// 걷기·대기 클립을 **손으로 고칠 수 있는 .anim 파일**로 만든다.
///
/// ★왜 코드로 만드나: 첫 판을 손으로 찍는 건 오래 걸린다. 한 번 뽑아 놓고
///  **애니메이션 창에서 키프레임을 끌어서** 다듬는 게 빠르다.
///  ★다시 누르면 덮어쓴다 — 손으로 고친 뒤에는 누르지 말 것.
///
/// ★축은 실측했다 (2026-08-04): 뼈 로컬 **X = 앞뒤 스윙**, +X 가 뒤 / −X 가 앞.
///  Z 는 좌우 벌림, Y 는 거의 무영향. 짐작하면 반대로 걷는다.
///
/// ★남녀 기본 자세가 최대 14.7° 다르다 → 클립을 각각 굽는다.
///  대신 「여자 → 남자 옮기기」 메뉴로 한쪽만 다듬으면 된다.
public static class MakeMotion
{
    const string DIR = "Assets/Game/Animations";
    const float 걷기길이 = 1.0f;      // 두 걸음 한 바퀴
    const float 대기길이 = 2.4f;

    // 뼈 경로 (모델 루트 기준)
    const string HIPS = "Armature/Hips";
    const string SPINE = HIPS + "/Spine";
    const string CHEST = SPINE + "/Chest";
    const string HEAD = CHEST + "/Neck/Head";
    static string 팔(string s) => CHEST + "/Shoulder." + s + "/UpperArm." + s;
    static string 아래팔(string s) => 팔(s) + "/LowerArm." + s;
    static string 다리(string s) => HIPS + "/UpperLeg." + s;
    static string 종아리(string s) => 다리(s) + "/LowerLeg." + s;
    static string 발(string s) => 종아리(s) + "/Foot." + s;

    /// 시각별 회전 「덧값」(도). 기본 자세에 더해서 굽는다.
    struct 곡선 { public string 경로; public float[] x, y, z; }

    static readonly float[] 때 = { 0f, 0.25f, 0.5f, 0.75f, 1.0f };

    static List<곡선> 걷기덧값()
    {
        // 0 = 왼발 착지(앞) · 0.25 = 왼발 통과 · 0.5 = 오른발 착지 · 0.75 = 오른발 통과
        return new List<곡선>
        {
            new 곡선 { 경로 = 다리("L"),   x = new[] { -22f,   4f,  18f, -12f, -22f } },
            new 곡선 { 경로 = 종아리("L"), x = new[] {   6f,   6f,  26f,  48f,   6f } },
            new 곡선 { 경로 = 발("L"),     x = new[] {   8f,  -4f, -12f,   6f,   8f } },

            new 곡선 { 경로 = 다리("R"),   x = new[] {  18f, -12f, -22f,   4f,  18f } },
            new 곡선 { 경로 = 종아리("R"), x = new[] {  26f,  48f,   6f,   6f,  26f } },
            new 곡선 { 경로 = 발("R"),     x = new[] { -12f,   6f,   8f,  -4f, -12f } },

            // 팔은 다리와 반대로 흔든다
            new 곡선 { 경로 = 팔("L"),     x = new[] {  16f,   6f, -16f,  -6f,  16f } },
            new 곡선 { 경로 = 아래팔("L"), x = new[] {  14f,  18f,  22f,  18f,  14f } },
            new 곡선 { 경로 = 팔("R"),     x = new[] { -16f,  -6f,  16f,   6f, -16f } },
            new 곡선 { 경로 = 아래팔("R"), x = new[] {  22f,  18f,  14f,  18f,  22f } },

            // 몸통 비틀기 — 이게 없으면 인형이 미끄러지는 것처럼 보인다
            new 곡선 { 경로 = CHEST, y = new[] {   5f,   0f,  -5f,   0f,   5f } },
            new 곡선 { 경로 = HIPS,  y = new[] {  -4f,   0f,   4f,   0f,  -4f },
                                     z = new[] {   2f,   0f,  -2f,   0f,   2f } },
            new 곡선 { 경로 = HEAD,  y = new[] {  -2f,   0f,   2f,   0f,  -2f } },
        };
    }

    /// 골반 위아래 — 착지에서 내려앉고 통과에서 뜬다 (m)
    static readonly float[] 걷기골반 = { -0.018f, 0.012f, -0.018f, 0.012f, -0.018f };

    static readonly float[] 대기때 = { 0f, 1.2f, 2.4f };

    static List<곡선> 대기덧값()
    {
        return new List<곡선>
        {
            new 곡선 { 경로 = CHEST, x = new[] { 0f, -2.0f, 0f } },
            new 곡선 { 경로 = HEAD,  x = new[] { 0f,  1.2f, 0f } },
            new 곡선 { 경로 = 팔("L"), x = new[] { 0f, -1.5f, 0f } },
            new 곡선 { 경로 = 팔("R"), x = new[] { 0f, -1.5f, 0f } },
        };
    }

    static readonly float[] 대기골반 = { 0f, 0.008f, 0f };

    // ──────────────────────────────────────────────────────────────────
    [MenuItem("Tools/토이라/모션 만들기 (걷기·대기)")]
    public static void 만들기()
    {
        if (!AssetDatabase.IsValidFolder(DIR))
            AssetDatabase.CreateFolder("Assets/Game", "Animations");

        foreach (var 성 in new[] { "woman", "man" })
        {
            var 모델 = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Game/Models/hero_{성}.fbx");
            if (모델 == null) { Debug.LogWarning($"hero_{성}.fbx 가 없다"); continue; }

            var 기본 = 기본자세읽기(모델);
            굽기($"{DIR}/걷기_{성}.anim", 걷기길이, 때, 걷기덧값(), 걷기골반, 기본, true);
            굽기($"{DIR}/대기_{성}.anim", 대기길이, 대기때, 대기덧값(), 대기골반, 기본, true);
            컨트롤러($"{DIR}/영웅_{성}.controller", 성);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("모션 만들기 끝 — Assets/Game/Animations 에 클립 4개 + 컨트롤러 2개");
    }

    /// 모델의 뼈 기본 자세(로컬 회전·위치)와 **부모 기준 「위쪽」 방향**을 읽는다.
    ///
    /// ★위쪽을 y 로 가정하면 안 된다. 블렌더 아마추어는 90° 돌아서 들어와서
    ///  Hips 의 로컬 위치가 (0, 0, 0.916) 이다 — 위가 **로컬 z** 다.
    ///  (골반 흔들림을 y 에 넣었다가 아무 일도 안 일어났다)
    static Dictionary<string, (Quaternion rot, Vector3 pos, Vector3 up)> 기본자세읽기(GameObject 모델)
    {
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(모델);
        var map = new Dictionary<string, (Quaternion, Vector3, Vector3)>();
        foreach (var t in inst.GetComponentsInChildren<Transform>(true))
        {
            if (t == inst.transform) continue;
            string path = "";
            var cur = t;
            while (cur != inst.transform) { path = cur.name + (path == "" ? "" : "/" + path); cur = cur.parent; }
            var up = t.parent != null ? t.parent.InverseTransformDirection(Vector3.up) : Vector3.up;
            map[path] = (t.localRotation, t.localPosition, up.normalized);
        }
        Object.DestroyImmediate(inst);
        return map;
    }

    /// 각도가 360 을 넘나들며 튀지 않게 — 앞 키에 제일 가까운 값으로 편다
    static float 펴기(float 앞, float 값)
    {
        while (값 - 앞 > 180f) 값 -= 360f;
        while (앞 - 값 > 180f) 값 += 360f;
        return 값;
    }

    static void 굽기(string 경로, float 길이, float[] 시각, List<곡선> 덧값,
                     float[] 골반높이,
                     Dictionary<string, (Quaternion rot, Vector3 pos, Vector3 up)> 기본,
                     bool 반복)
    {
        // ★이름을 반드시 넣는다. 안 넣고 CopySerialized 로 덮으면 **클립 이름이 빈칸이 되어**
        //   애니메이션 창 목록에서 고를 수가 없다 (한 번 그랬다).
        var clip = new AnimationClip
        {
            frameRate = 30f,
            name = System.IO.Path.GetFileNameWithoutExtension(경로),
        };

        foreach (var c in 덧값)
        {
            if (!기본.TryGetValue(c.경로, out var rest)) { Debug.LogWarning("뼈 없음: " + c.경로); continue; }
            var cx = new AnimationCurve();
            var cy = new AnimationCurve();
            var cz = new AnimationCurve();
            float px = 0, py = 0, pz = 0;
            for (int i = 0; i < 시각.Length; i++)
            {
                float dx = c.x != null ? c.x[i] : 0f;
                float dy = c.y != null ? c.y[i] : 0f;
                float dz = c.z != null ? c.z[i] : 0f;
                var e = (rest.rot * Quaternion.Euler(dx, dy, dz)).eulerAngles;
                float ex = i == 0 ? e.x : 펴기(px, e.x);
                float ey = i == 0 ? e.y : 펴기(py, e.y);
                float ez = i == 0 ? e.z : 펴기(pz, e.z);
                px = ex; py = ey; pz = ez;
                float t = 시각[i] / 시각[시각.Length - 1] * 길이;
                cx.AddKey(t, ex); cy.AddKey(t, ey); cz.AddKey(t, ez);
            }
            매끄럽게(cx); 매끄럽게(cy); 매끄럽게(cz);
            clip.SetCurve(c.경로, typeof(Transform), "localEulerAnglesRaw.x", cx);
            clip.SetCurve(c.경로, typeof(Transform), "localEulerAnglesRaw.y", cy);
            clip.SetCurve(c.경로, typeof(Transform), "localEulerAnglesRaw.z", cz);
        }

        // 골반 위아래 — 「위쪽」은 실측한 축을 쓴다 (로컬 y 가 아니다)
        if (골반높이 != null && 기본.TryGetValue(HIPS, out var hr))
        {
            var cx = new AnimationCurve();
            var cy = new AnimationCurve();
            var cz = new AnimationCurve();
            for (int i = 0; i < 시각.Length; i++)
            {
                float t = 시각[i] / 시각[시각.Length - 1] * 길이;
                var p = hr.pos + hr.up * 골반높이[i];
                cx.AddKey(t, p.x); cy.AddKey(t, p.y); cz.AddKey(t, p.z);
            }
            매끄럽게(cx); 매끄럽게(cy); 매끄럽게(cz);
            clip.SetCurve(HIPS, typeof(Transform), "m_LocalPosition.x", cx);
            clip.SetCurve(HIPS, typeof(Transform), "m_LocalPosition.y", cy);
            clip.SetCurve(HIPS, typeof(Transform), "m_LocalPosition.z", cz);
        }

        var s = AnimationUtility.GetAnimationClipSettings(clip);
        s.loopTime = 반복;
        AnimationUtility.SetAnimationClipSettings(clip, s);

        var old = AssetDatabase.LoadAssetAtPath<AnimationClip>(경로);
        if (old != null)
        {
            EditorUtility.CopySerialized(clip, old);
            old.name = clip.name;              // CopySerialized 가 이름도 덮는다
            EditorUtility.SetDirty(old);
        }
        else AssetDatabase.CreateAsset(clip, 경로);
    }

    static AnimationCurve 상수(float v, float 길이)
    {
        var c = new AnimationCurve();
        c.AddKey(0f, v); c.AddKey(길이, v);
        return c;
    }

    static void 매끄럽게(AnimationCurve c)
    {
        for (int i = 0; i < c.length; i++)
            c.SmoothTangents(i, 0f);
    }

    // ──────────────────────────────────────────────────────────────────
    static void 컨트롤러(string 경로, string 성)
    {
        var 대기 = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{DIR}/대기_{성}.anim");
        var 걷기 = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{DIR}/걷기_{성}.anim");
        var 뒤로 = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{DIR}/뒤로걷기_{성}.anim");
        if (대기 == null || 걷기 == null) return;

        // ★컨트롤러 파일을 지우고 새로 만들면 안 된다 — 그걸 물고 있던 오브젝트들의
        //   연결이 전부 끊긴다 (편집판·캐릭터의 애니메이터가 「없음」 이 됐다).
        //   있으면 **안을 비우고 다시 채운다.**
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(경로);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(경로);
        else
        {
            var sm0 = ctrl.layers[0].stateMachine;
            foreach (var st0 in sm0.states) sm0.RemoveState(st0.state);
            foreach (var sub in sm0.stateMachines) sm0.RemoveStateMachine(sub.stateMachine);
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(경로))
                if (sub is BlendTree) Object.DestroyImmediate(sub, true);
        }
        bool 있나 = false;
        foreach (var p in ctrl.parameters) if (p.name == "속도") 있나 = true;
        if (!있나) ctrl.AddParameter("속도", AnimatorControllerParameterType.Float);

        var tree = new BlendTree { name = "이동", blendParameter = "속도",
                                   blendType = BlendTreeType.Simple1D,
                                   useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(tree, ctrl);
        // ★「속도」는 **부호가 있다** — 뒤로 가면 음수. 뒷걸음질이 이 게임의 조작 핵심이라
        //   (마우스를 보면서 물러난다) 앞뒤를 한 축에 놓는다.
        // ★걸음 속도는 「걷기 2.6m/s」에 맞춰 시간배속으로 맞춘다 — 발이 미끄러지지 않게.
        //   달리기 클립이 생기면 ±6.0 칸을 그걸로 바꾸면 된다.
        if (뒤로 != null)
        {
            tree.AddChild(뒤로, -6.0f);
            tree.AddChild(뒤로, -2.6f);
        }
        tree.AddChild(대기, 0f);
        tree.AddChild(걷기, 2.6f);
        tree.AddChild(걷기, 6.0f);

        // ★배속을 손으로 적지 않는다 — 클립이 한 바퀴에 실제로 나아가는 거리를 재서 나눈다.
        //   그래야 보폭을 고쳐도 발이 계속 안 미끄러진다 (「그림 = 판정」).
        var hero = Object.FindFirstObjectByType<Hero>();
        float 모델배율 = (hero != null ? hero.height : 1.8f) / 1.8f;
        float 앞거리 = 한바퀴거리($"{DIR}/걷기_{성}.anim", 성, 모델배율);
        float 뒤거리 = 뒤로 != null ? 한바퀴거리($"{DIR}/뒤로걷기_{성}.anim", 성, 모델배율) : 앞거리;

        var ch = tree.children;
        for (int i = 0; i < ch.Length; i++)
        {
            float t = ch[i].threshold;
            float d = t < 0f ? 뒤거리 : 앞거리;
            ch[i].timeScale = (Mathf.Abs(t) < 0.01f || d < 0.01f) ? 1f : Mathf.Abs(t) / d;
        }
        tree.children = ch;

        var st = ctrl.layers[0].stateMachine.AddState("이동");
        st.motion = tree;
        ctrl.layers[0].stateMachine.defaultState = st;
        EditorUtility.SetDirty(ctrl);
    }

    // ──────────────────────────────────────────────────────────────────
    /// 클립의 회전 「덧값」을 배수로 키우거나 줄인다 — 보폭·팔 흔들림 크기 조절.
    ///
    /// ★기본 자세는 그대로 두고 **거기서 벗어난 만큼**만 곱한다. 그래서 손으로 만든
    ///  클립에도 쓸 수 있다 (뒤로걷기처럼).
    public static void 스윙조절(string 클립경로, string 성, float 배수)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(클립경로);
        if (clip == null) return;
        var 기본 = 기본자세읽기(AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/Game/Models/hero_{성}.fbx"));

        var 묶음 = new Dictionary<string, AnimationCurve[]>();
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            if (b.type != typeof(Transform) || !b.propertyName.StartsWith("localEulerAnglesRaw.")) continue;
            if (!묶음.TryGetValue(b.path, out var arr)) 묶음[b.path] = arr = new AnimationCurve[3];
            int k = b.propertyName.EndsWith(".x") ? 0 : b.propertyName.EndsWith(".y") ? 1 : 2;
            arr[k] = AnimationUtility.GetEditorCurve(clip, b);
        }

        foreach (var kv in 묶음)
        {
            if (!기본.TryGetValue(kv.Key, out var rest)) continue;
            var a = kv.Value;
            if (a[0] == null || a[1] == null || a[2] == null) continue;
            var nx = new AnimationCurve();
            var ny = new AnimationCurve();
            var nz = new AnimationCurve();
            float px = 0, py = 0, pz = 0;
            for (int i = 0; i < a[0].length; i++)
            {
                float t = a[0].keys[i].time;
                var abs = Quaternion.Euler(a[0].Evaluate(t), a[1].Evaluate(t), a[2].Evaluate(t));
                var 덧 = Quaternion.Inverse(rest.rot) * abs;
                덧 = Quaternion.SlerpUnclamped(Quaternion.identity, 덧, 배수);   // 덧값만 키운다
                var e = (rest.rot * 덧).eulerAngles;
                float ex = i == 0 ? e.x : 펴기(px, e.x);
                float ey = i == 0 ? e.y : 펴기(py, e.y);
                float ez = i == 0 ? e.z : 펴기(pz, e.z);
                px = ex; py = ey; pz = ez;
                nx.AddKey(t, ex); ny.AddKey(t, ey); nz.AddKey(t, ez);
            }
            매끄럽게(nx); 매끄럽게(ny); 매끄럽게(nz);
            clip.SetCurve(kv.Key, typeof(Transform), "localEulerAnglesRaw.x", nx);
            clip.SetCurve(kv.Key, typeof(Transform), "localEulerAnglesRaw.y", ny);
            clip.SetCurve(kv.Key, typeof(Transform), "localEulerAnglesRaw.z", nz);
        }
        EditorUtility.SetDirty(clip);
    }

    /// 클립 한 바퀴에 나아가는 거리(m)를 **잰다** — 배속을 짐작하지 않으려고.
    public static float 한바퀴거리(string 클립경로, string 성, float 모델배율)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(클립경로);
        var 모델 = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Game/Models/hero_{성}.fbx");
        if (clip == null || 모델 == null) return 0f;
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(모델);
        inst.transform.position = Vector3.zero;
        inst.transform.rotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one * 모델배율;
        var fl = inst.transform.Find($"Armature/Hips/UpperLeg.L/LowerLeg.L/Foot.L");
        var fr = inst.transform.Find($"Armature/Hips/UpperLeg.R/LowerLeg.R/Foot.R");
        float sep = 0f;
        for (int i = 0; i <= 60; i++)
        {
            clip.SampleAnimation(inst, i / 60f * clip.length);
            sep = Mathf.Max(sep, Mathf.Abs(fl.position.z - fr.position.z));
        }
        Object.DestroyImmediate(inst);
        return sep * 2f;                 // 한 바퀴 = 두 걸음
    }

    // ──────────────────────────────────────────────────────────────────
    /// 키프레임을 고칠 **편집 전용 인형**을 씬에 세운다.
    ///
    /// ★게임 캐릭터(`캐릭터/몸_여자`)는 `HeroBody` 가 매번 새로 만드는 임시 오브젝트라
    ///  애니메이션 창에서 손대기 나쁘다. 그래서 손댈 놈을 따로 세운다.
    ///  다 고쳤으면 지우면 된다 — 클립(.anim)은 파일이라 그대로 남는다.
    [MenuItem("Tools/토이라/모션 편집판 세우기")]
    public static void 편집판()
    {
        var old = GameObject.Find("모션편집판");
        if (old != null) Object.DestroyImmediate(old);

        var root = new GameObject("모션편집판");
        Undo.RegisterCreatedObjectUndo(root, "모션 편집판");
        int i = 0;
        foreach (var 성 in new[] { "woman", "man" })
        {
            var 모델 = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Game/Models/hero_{성}.fbx");
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{DIR}/영웅_{성}.controller");
            if (모델 == null) continue;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(모델);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "편집_" + 성;
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(i++ * 1.4f, 0f, 0f);

            var a = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
            a.runtimeAnimatorController = ctrl;
            a.applyRootMotion = false;

            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Models/살_흰색.mat");
            if (mat != null)
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mat;
        }
        Selection.activeGameObject = root.transform.childCount > 0
            ? root.transform.GetChild(0).gameObject : root;
        SceneView.FrameLastActiveSceneView();
        Debug.Log("모션편집판 세움 — 「편집_woman」 을 고른 채 Window → Animation 을 열면 "
                + "걷기_woman / 대기_woman 클립이 뜬다. 다 고치면 「모션편집판」 을 지우면 된다.");
    }

    // ──────────────────────────────────────────────────────────────────
    [MenuItem("Tools/토이라/모션 옮기기 (여자 → 남자)")]
    public static void 옮기기()
    {
        var wm = 기본자세읽기(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Models/hero_woman.fbx"));
        var mm = 기본자세읽기(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Models/hero_man.fbx"));

        foreach (var 이름 in new[] { "걷기", "대기", "뒤로걷기" })
        {
            var src = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{DIR}/{이름}_woman.anim");
            if (src == null) continue;
            var dst = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{DIR}/{이름}_man.anim");
            if (dst == null)
            {
                // 남자 쪽이 아직 없으면 빈 클립을 만들어 둔다 (손으로 만든 클립도 옮겨진다)
                var 빈것 = new AnimationClip { frameRate = src.frameRate, name = $"{이름}_man" };
                AssetDatabase.CreateAsset(빈것, $"{DIR}/{이름}_man.anim");
                dst = 빈것;
            }

            var 새클립 = new AnimationClip { frameRate = src.frameRate, name = dst.name };
            // 뼈마다 x/y/z 곡선을 모아서 「여자 기본자세 기준 덧값」을 뽑고 남자 기본자세에 다시 얹는다
            var 묶음 = new Dictionary<string, AnimationCurve[]>();
            foreach (var b in AnimationUtility.GetCurveBindings(src))
            {
                if (b.type != typeof(Transform)) continue;
                if (b.propertyName.StartsWith("localEulerAnglesRaw."))
                {
                    if (!묶음.TryGetValue(b.path, out var arr))
                        묶음[b.path] = arr = new AnimationCurve[3];
                    int k = b.propertyName.EndsWith(".x") ? 0 : b.propertyName.EndsWith(".y") ? 1 : 2;
                    arr[k] = AnimationUtility.GetEditorCurve(src, b);
                }
                else if (b.propertyName.StartsWith("m_LocalPosition."))
                {
                    // 위치는 남자 기본 위치에 「차이」를 얹는다
                    var cur = AnimationUtility.GetEditorCurve(src, b);
                    if (!wm.TryGetValue(b.path, out var wr) || !mm.TryGetValue(b.path, out var mr)) continue;
                    int k = b.propertyName.EndsWith(".x") ? 0 : b.propertyName.EndsWith(".y") ? 1 : 2;
                    float wbase = k == 0 ? wr.pos.x : k == 1 ? wr.pos.y : wr.pos.z;
                    float mbase = k == 0 ? mr.pos.x : k == 1 ? mr.pos.y : mr.pos.z;
                    var nc = new AnimationCurve();
                    foreach (var key in cur.keys) nc.AddKey(key.time, mbase + (key.value - wbase));
                    매끄럽게(nc);
                    새클립.SetCurve(b.path, typeof(Transform), b.propertyName, nc);
                }
            }

            foreach (var kv in 묶음)
            {
                if (!wm.TryGetValue(kv.Key, out var wr) || !mm.TryGetValue(kv.Key, out var mr)) continue;
                var a = kv.Value;
                if (a[0] == null || a[1] == null || a[2] == null) continue;
                var nx = new AnimationCurve();
                var ny = new AnimationCurve();
                var nz = new AnimationCurve();
                float px = 0, py = 0, pz = 0;
                for (int i = 0; i < a[0].length; i++)
                {
                    float t = a[0].keys[i].time;
                    var abs = Quaternion.Euler(a[0].Evaluate(t), a[1].Evaluate(t), a[2].Evaluate(t));
                    var 덧 = Quaternion.Inverse(wr.rot) * abs;        // 여자 기본 대비 덧값
                    var e = (mr.rot * 덧).eulerAngles;                // 남자 기본에 얹기
                    float ex = i == 0 ? e.x : 펴기(px, e.x);
                    float ey = i == 0 ? e.y : 펴기(py, e.y);
                    float ez = i == 0 ? e.z : 펴기(pz, e.z);
                    px = ex; py = ey; pz = ez;
                    nx.AddKey(t, ex); ny.AddKey(t, ey); nz.AddKey(t, ez);
                }
                매끄럽게(nx); 매끄럽게(ny); 매끄럽게(nz);
                새클립.SetCurve(kv.Key, typeof(Transform), "localEulerAnglesRaw.x", nx);
                새클립.SetCurve(kv.Key, typeof(Transform), "localEulerAnglesRaw.y", ny);
                새클립.SetCurve(kv.Key, typeof(Transform), "localEulerAnglesRaw.z", nz);
            }

            var s = AnimationUtility.GetAnimationClipSettings(src);
            AnimationUtility.SetAnimationClipSettings(새클립, s);
            string 클립이름 = dst.name;
            EditorUtility.CopySerialized(새클립, dst);
            dst.name = 클립이름;                // CopySerialized 가 이름도 덮는다
            EditorUtility.SetDirty(dst);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("여자 클립을 남자에게 옮겼다 (기본 자세 차이를 보정해서)");
    }
}
