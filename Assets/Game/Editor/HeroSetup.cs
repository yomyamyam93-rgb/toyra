using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// 캐릭터 넣기 — 뼈대와 애니메이션이 든 모델을 캐릭터 자리에 물리고,
/// **동작을 섞어 주는 블렌드 트리**를 만든다.
///
/// ★파일마다 메시가 통째로 들어 있지만(각 10MB) **쓰는 건 클립뿐**이다.
///   몸은 하나만 쓰고 나머지 파일에서는 클립만 꺼내 온다 — 뼈 이름과 계층이 같아서
///   그대로 물린다.
///
/// ★「정지」 동작이 없어서 걷기의 첫 프레임을 굳혀 만든다. Meshy 에서 Idle 을
///   받으면 그걸로 바꾸면 된다.
public static class HeroSetup
{
    const string 폴더 = "Assets/Game/Models/hero";
    // ★원본을 쓴다. 폴리곤을 줄였더니 메시가 망가졌다 (2026-08-04) —
    //   반짝임의 원인은 폴리곤이 아니라 **테두리가 모델 안쪽까지 그어지던 것**이었다.
    //   엉뚱한 데를 건드린 것이므로 되돌린다.
    const string 몸파일 = 폴더 + "/Walking.glb";
    const string 저장 = "Assets/Game/Animations";

    /// 모델이 향한 쪽 보정 (°) — 마우스 쪽을 등지면 180 으로 바꾼다
    public static float 모델회전 = 180f;

    /// 살색 재질 — glb 에 재질이 안 딸려 오면 분홍색(재질 없음)으로 뜬다
    static void 재질입히기(GameObject go)
    {
        const string path = "Assets/Game/Models/hero/살.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(sh) { name = "살" };
            var c = new Color(0.85f, 0.78f, 0.68f);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(m, path);
        }

        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var arr = r.sharedMaterials;
            bool 비었나 = arr.Length == 0;
            for (int i = 0; i < arr.Length; i++)
                if (arr[i] == null || arr[i].shader == null || arr[i].shader.name.Contains("Hidden")) 비었나 = true;
            if (!비었나) continue;

            var 새 = new Material[Mathf.Max(1, arr.Length)];
            for (int i = 0; i < 새.Length; i++) 새[i] = m;
            r.sharedMaterials = 새;
        }
    }

    [MenuItem("Tools/토이라기/㉤ 캐릭터 넣기", priority = 4)]
    public static void Run()
    {
        var 몸 = AssetDatabase.LoadAssetAtPath<GameObject>(몸파일);
        if (몸 == null) { Debug.LogError($"[캐릭터] {몸파일} 이 없다"); return; }

        if (!AssetDatabase.IsValidFolder(저장)) AssetDatabase.CreateFolder("Assets/Game", "Animations");

        // ── 클립 모으기 (제자리걸음으로 바꿔서)
        var 걷기 = 제자리로("걷기", 클립찾기("Walking.glb"));
        var 뒤로 = 제자리로("뒤로걷기", 클립찾기("Walk_Backward.glb"));
        var 달리기 = 제자리로("달리기", 클립찾기("Running.glb"));
        var 빠른걷기 = 제자리로("빠른걷기", 클립찾기("Quick_Walk.glb"));
        if (걷기 == null) { Debug.LogError("[캐릭터] Walking 클립을 못 찾았다"); return; }

        var 정지 = 정지만들기(걷기);

        // ── 블렌드 트리
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(저장 + "/캐릭터.controller");
        ctrl.AddParameter("앞뒤", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("좌우", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("빠르기", AnimatorControllerParameterType.Float);

        var sm = ctrl.layers[0].stateMachine;

        // ── 옆걸음은 앞걷기에서 만들어 낸다 (아래 「옆걸음만들기」 참고)
        var 옆오른 = 옆걸음만들기("옆걸음_오른쪽", 클립찾기("Walking.glb"), 90f);
        var 옆왼 = 옆걸음만들기("옆걸음_왼쪽", 클립찾기("Walking.glb"), -90f);

        // ── 걷기 섞기 (앞뒤 × 좌우) — 네 방향이 유기적으로 이어진다
        var 걷기트리 = new BlendTree
        {
            name = "걷기섞기",
            blendType = BlendTreeType.SimpleDirectional2D,
            blendParameter = "좌우",
            blendParameterY = "앞뒤",
            useAutomaticThresholds = false
        };
        AssetDatabase.AddObjectToAsset(걷기트리, ctrl);
        걷기트리.AddChild(걷기, new Vector2(0f, 1f));                       // 앞
        걷기트리.AddChild(뒤로 != null ? 뒤로 : 걷기, new Vector2(0f, -1f)); // 뒤
        걷기트리.AddChild(옆오른 != null ? 옆오른 : 걷기, new Vector2(1f, 0f));  // 오른쪽
        걷기트리.AddChild(옆왼 != null ? 옆왼 : 걷기, new Vector2(-1f, 0f));     // 왼쪽

        // ── 속도 섞기 (빠르기) — 정지 ↔ 걷기 ↔ 달리기
        //   ★같은 트리를 두 번 자식으로 넣으면 안 된다 (2026-08-04 — 그래서 안 돌았다)
        var 속도트리 = new BlendTree
        {
            name = "속도섞기",
            blendType = BlendTreeType.Simple1D,
            blendParameter = "빠르기",
            useAutomaticThresholds = false
        };
        AssetDatabase.AddObjectToAsset(속도트리, ctrl);
        속도트리.AddChild(정지, 0f);
        속도트리.AddChild(걷기트리, 1f);
        속도트리.AddChild(달리기 != null ? (Motion)달리기 : 걷기트리, 2f);

        var st = sm.AddState("이동", new Vector3(300, 0, 0));
        st.motion = 속도트리;
        st.writeDefaultValues = true;
        sm.defaultState = st;

        EditorUtility.SetDirty(ctrl);

        // ── 씬의 캐릭터에 물리기
        var hero = Object.FindFirstObjectByType<Hero>();
        if (hero == null) { Debug.LogError("[캐릭터] 씬에 Hero 가 없다 — ① 씬 짓기 를 먼저"); return; }

        var 옛몸 = hero.transform.Find("몸");
        if (옛몸 != null) 옛몸.gameObject.SetActive(false);      // 상자는 지우지 않고 끈다
        var 코 = hero.transform.Find("코");
        if (코 != null) 코.gameObject.SetActive(false);
        var 있던것 = hero.transform.Find("사람");
        if (있던것 != null) Undo.DestroyObjectImmediate(있던것.gameObject);

        var go = (GameObject)PrefabUtility.InstantiatePrefab(몸, hero.transform);
        go.name = "사람";
        go.transform.localPosition = Vector3.zero;
        // ★모델이 향한 쪽이 유니티 기준(+Z)과 다르면 여기서 돌린다
        go.transform.localRotation = Quaternion.Euler(0f, 모델회전, 0f);
        키맞춤(go, hero.height);

        재질입히기(go);

        var anim = go.GetComponent<Animator>();
        if (anim == null) anim = go.AddComponent<Animator>();

        // ★★아바타가 없으면 애니메이터가 클립을 **한 프레임도 못 돌린다** (2026-08-04).
        //   glTF 로 들여온 모델에는 아바타가 안 딸려 오므로 여기서 만들어 붙인다.
        if (anim.avatar == null)
        {
            var av = AvatarBuilder.BuildGenericAvatar(go, "");
            av.name = "캐릭터아바타";
            AssetDatabase.CreateAsset(av, 저장 + "/캐릭터아바타.asset");
            anim.avatar = av;
        }
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;                             // 이동은 코드가 한다
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        if (hero.GetComponent<HeroAnim>() == null) Undo.AddComponent<HeroAnim>(hero.gameObject);
        if (hero.GetComponent<HeroHold>() == null) Undo.AddComponent<HeroHold>(hero.gameObject);

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(hero.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(hero.gameObject.scene);
        Debug.Log("[캐릭터] 완성 — 걷기·뒤로걷기·달리기가 섞입니다. 플레이해 보세요.");
    }

    static AnimationClip 클립찾기(string 파일)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(폴더 + "/" + 파일))
            if (o is AnimationClip ac && !ac.name.StartsWith("__")) return ac;
        return null;
    }

    /// ★제자리걸음으로 바꾼다 (2026-08-04 사용자 "반대로 걸으면 순간이동하는 버그").
    ///   받아온 클립은 **실제로 앞으로 나아가는** 동작이라, 이동은 코드가 하는데
    ///   애니메이션까지 몸을 밀어 버린다 → 한 바퀴 돌 때마다 원위치로 튕겨서 순간이동처럼 보인다.
    ///   → 뿌리 뼈(Hips)의 **앞뒤·좌우 이동 커브만 지운다.** 위아래 출렁임은 남긴다.
    static AnimationClip 제자리로(string 이름, AnimationClip 원본)
    {
        if (원본 == null) return null;
        string path = 저장 + "/" + 이름 + ".anim";
        var 있던 = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (있던 != null) return 있던;

        var c = new AnimationClip { name = 이름, frameRate = 원본.frameRate };
        foreach (var b in AnimationUtility.GetCurveBindings(원본))
        {
            bool 뿌리 = b.path.EndsWith("Hips") || b.path.EndsWith("Root") || b.path == "Armature";
            bool 수평이동 = b.propertyName == "m_LocalPosition.x" || b.propertyName == "m_LocalPosition.z";
            if (뿌리 && 수평이동) continue;                     // 이 커브만 버린다

            var curve = AnimationUtility.GetEditorCurve(원본, b);
            if (curve != null) AnimationUtility.SetEditorCurve(c, b, curve);
        }

        var s = AnimationUtility.GetAnimationClipSettings(원본);
        s.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(c, s);
        AssetDatabase.CreateAsset(c, path);
        return c;
    }

    // ══════════════════════════════════════════════════════════
    //  ★옆걸음을 **앞걷기에서 만들어 낸다** (2026-08-04 사용자 "지금 있는 모션 참고해서")
    //
    //  받은 동작에 옆걸음이 없다. 그런데 옆걸음은 결국
    //    **골반을 옆으로 돌려 다리가 옆으로 딛고, 상체는 앞을 보는 것**이다.
    //  그러니 앞걷기의 골반 회전에 90도를 더하고, 척추에서 그만큼 되돌리면 된다.
    //  (다리는 골반의 자식이라 저절로 따라 돌아간다)
    //
    //  ★척추 두 마디에 나눠서 되돌린다 — 한 마디에 몰면 허리가 꺾인 것처럼 보인다.
    // ══════════════════════════════════════════════════════════
    // ★★척추를 되돌리는 방식은 버렸다 (2026-08-04 사용자 "고개를 심각하게 뒤로 젖힌 게 이상하네").
    //   뼈의 회전은 **그 뼈의 부모 기준**이라, 척추처럼 축이 누워 있는 뼈에 세로축(Y) 회전을
    //   그대로 곱하면 **고개가 뒤로 젖혀진다.** 축이 달라서 생기는 일이라 각도로는 못 고친다.
    //
    //   → 훨씬 단순하고 안전한 길: **넓적다리만 돌린다.** 다리는 골반 바로 아래라 축이
    //     서 있고, 몸통·머리는 아예 안 건드리니 젖혀질 일이 없다.
    //     골반은 앞을 보고 다리만 옆으로 딛는다 — 그게 옆걸음이다.
    const string 왼다리 = "Armature/Hips/LeftUpLeg";
    const string 오른다리 = "Armature/Hips/RightUpLeg";

    static AnimationClip 옆걸음만들기(string 이름, AnimationClip 원본, float 각도)
    {
        if (원본 == null) return null;
        string path = 저장 + "/" + 이름 + ".anim";
        var 있던 = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (있던 != null) return 있던;

        var c = new AnimationClip { name = 이름, frameRate = 원본.frameRate };

        // 회전이 아닌 커브는 그대로 옮긴다 (단 뿌리 수평이동은 버린다 — 제자리걸음)
        foreach (var b in AnimationUtility.GetCurveBindings(원본))
        {
            if (b.propertyName.StartsWith("m_LocalRotation")) continue;
            bool 뿌리 = b.path.EndsWith("Hips") || b.path.EndsWith("Root");
            if (뿌리 && (b.propertyName == "m_LocalPosition.x" || b.propertyName == "m_LocalPosition.z")) continue;
            var cur = AnimationUtility.GetEditorCurve(원본, b);
            if (cur != null) AnimationUtility.SetEditorCurve(c, b, cur);
        }

        // 회전 커브는 뼈마다 통째로 다뤄야 한다 (x·y·z·w 를 같이 봐야 하므로)
        foreach (var 경로 in 회전경로들(원본))
        {
            // ★넓적다리는 **흔들리는 방향만** 돌린다 (2026-08-04 사용자 "다리와 발이
            //   전체적으로 걷는 방향쪽으로 이동되어 있어, 몸 전체가 기울어진 것처럼 보여").
            //   통째로 돌리면 **기본 자세까지** 옆으로 끌려가 다리가 몸 밖으로 뻗는다.
            //   기본 자세는 그대로 두고 「앞뒤로 흔들리던 것」을 「좌우로 흔들리게」만 바꾼다.
            bool 다리 = 경로 == 왼다리 || 경로 == 오른다리;
            float 흔들각 = 다리 ? 각도 : 0f;

            // 보폭은 다리 사슬 전체에서 줄인다
            float 폭 = 경로.Contains("UpLeg") || 경로.Contains("Leg") || 경로.Contains("Foot")
                     ? 보폭 : 1f;
            회전옮기기(원본, c, 경로, 흔들각, 폭);
        }

        var s = AnimationUtility.GetAnimationClipSettings(원본);
        s.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(c, s);
        AssetDatabase.CreateAsset(c, path);
        return c;
    }

    static List<string> 회전경로들(AnimationClip 원본)
    {
        var 목록 = new List<string>();
        foreach (var b in AnimationUtility.GetCurveBindings(원본))
            if (b.propertyName == "m_LocalRotation.x" && !목록.Contains(b.path)) 목록.Add(b.path);
        return 목록;
    }

    /// 옆걸음 보폭 (1 = 앞걷기와 같음 · 낮출수록 다리를 덜 벌린다)
    public static float 보폭 = 0.55f;

    /// 한 뼈의 회전 커브를 옮긴다.
    /// `흔들각` — 흔들리는 **방향**을 이만큼 돌린다 (기본 자세는 안 건드린다)
    /// `폭` — 흔들리는 **크기**를 이만큼으로 줄인다 (1 = 그대로)
    static void 회전옮기기(AnimationClip 원본, AnimationClip 새것, string 경로, float 흔들각, float 폭 = 1f)
    {
        var cx = 커브(원본, 경로, "m_LocalRotation.x");
        var cy = 커브(원본, 경로, "m_LocalRotation.y");
        var cz = 커브(원본, 경로, "m_LocalRotation.z");
        var cw = 커브(원본, 경로, "m_LocalRotation.w");
        if (cx == null || cy == null || cz == null || cw == null) return;

        var nx = new AnimationCurve(); var ny = new AnimationCurve();
        var nz = new AnimationCurve(); var nw = new AnimationCurve();

        // ★기준 자세 = 첫 프레임. 여기서 **얼마나 벗어났는가**가 「흔들림」이다.
        //   기준은 그대로 두고 흔들림만 손대야 다리가 몸 밖으로 끌려가지 않는다.
        var 기준 = new Quaternion(cx.Evaluate(0f), cy.Evaluate(0f), cz.Evaluate(0f), cw.Evaluate(0f));
        var 기준역 = Quaternion.Inverse(기준);

        // 흔들리는 방향을 돌릴 회전 — 세로축을 **이 뼈 기준**으로 옮겨서 쓴다
        // (뼈마다 축이 누워 있어서, 그냥 Y축으로 돌리면 엉뚱하게 꺾인다)
        var 축 = 기준역 * Vector3.up;
        var 방향돌림 = Quaternion.AngleAxis(흔들각, 축);
        var 방향돌림역 = Quaternion.Inverse(방향돌림);

        for (int i = 0; i < cx.length; i++)
        {
            float t = cx[i].time;
            var q = new Quaternion(cx.Evaluate(t), cy.Evaluate(t), cz.Evaluate(t), cw.Evaluate(t));

            var 흔들림 = 기준역 * q;                                   // 기준에서 벗어난 만큼
            if (Mathf.Abs(흔들각) > 0.01f)
                흔들림 = 방향돌림 * 흔들림 * 방향돌림역;                // 흔들리는 **방향**만 회전
            if (폭 < 0.999f)
                흔들림 = Quaternion.Slerp(Quaternion.identity, 흔들림, 폭);   // 흔들리는 **크기**를 줄임

            q = 기준 * 흔들림;
            nx.AddKey(t, q.x); ny.AddKey(t, q.y); nz.AddKey(t, q.z); nw.AddKey(t, q.w);
        }

        AnimationUtility.SetEditorCurve(새것, 묶음(경로, "m_LocalRotation.x"), nx);
        AnimationUtility.SetEditorCurve(새것, 묶음(경로, "m_LocalRotation.y"), ny);
        AnimationUtility.SetEditorCurve(새것, 묶음(경로, "m_LocalRotation.z"), nz);
        AnimationUtility.SetEditorCurve(새것, 묶음(경로, "m_LocalRotation.w"), nw);
    }

    static EditorCurveBinding 묶음(string 경로, string 속성)
        => EditorCurveBinding.FloatCurve(경로, typeof(Transform), 속성);

    static AnimationCurve 커브(AnimationClip c, string 경로, string 속성)
        => AnimationUtility.GetEditorCurve(c, 묶음(경로, 속성));

    /// 「정지」 동작 만들기 — 걷기의 첫 프레임을 굳힌다
    static AnimationClip 정지만들기(AnimationClip 원본)
    {
        const string path = 저장 + "/정지.anim";
        var 있던 = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (있던 != null) return 있던;

        var c = new AnimationClip { name = "정지", legacy = false };
        foreach (var b in AnimationUtility.GetCurveBindings(원본))
        {
            var curve = AnimationUtility.GetEditorCurve(원본, b);
            if (curve == null || curve.length == 0) continue;
            float v = curve.Evaluate(0f);
            AnimationUtility.SetEditorCurve(c, b, AnimationCurve.Constant(0f, 1f, v));
        }
        var s = new AnimationClipSettings { loopTime = true };
        AnimationUtility.SetAnimationClipSettings(c, s);
        AssetDatabase.CreateAsset(c, path);
        return c;
    }

    /// 모델의 실제 높이를 재서 사람 키에 맞춘다
    static void 키맞춤(GameObject go, float 키)
    {
        go.transform.localScale = Vector3.one;
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        if (b.size.y > 0.0001f) go.transform.localScale = Vector3.one * (키 / b.size.y);

        b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        go.transform.position += Vector3.up * (go.transform.parent.position.y - b.min.y);
    }
}
