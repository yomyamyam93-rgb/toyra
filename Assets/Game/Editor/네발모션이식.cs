using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// 네발 클립을 **다른 몸에 옮겨 굽는다** (리타기팅).
///
/// ★★왜 필요한가 (2026-08-05 사용자 "근데 왜이렇게 어그러지냐?"):
///   뼈 **이름**이 같으면 클립이 걸리기는 한다. 그런데 회전 커브는 그 뼈의 **로컬 회전을
///   통째로 덮어쓴다.** 몸 생김새가 다르면 뼈가 향한 방향(기본 자세)도 다르므로,
///   브론토의 회전값을 다람쥐 뼈에 그대로 넣으면 뼈마다 엉뚱한 쪽으로 꺾여 몸이 터진다.
///
/// ★맞는 방법: 「브론토가 **제 기본자세에서** 얼마나 돌았나」만 떼어내 우리 몸의
///   기본자세에 얹는다.
/// <code>
///     새 회전 = 우리기본 × (브론토기본⁻¹ × 브론토키값)
/// </code>
///   유니티의 휴머노이드가 사람 뼈대에 자동으로 해 주는 일을, 네발은 우리가 해야 한다
///   (제네릭 리그에는 자동 리타기팅이 없다).
///
/// ★그래도 **모션은 한 번만 만든다.** 옮겨 굽는 건 자동이라, 종이 늘어도 사람 일은 안 는다.
public static class 네발모션이식
{
    const string 원본리그 = "Assets/Game/Resources/rig/dino_animated.glb";
    const string 원본클립 = "Assets/Game/Resources/rig/네발_걷기_회전만.anim";
    const string 저장 = "Assets/Game/Resources/rig";

    // ★★★종마다 **성질**을 준다 (2026-08-05 사용자 "각각 팻마다 형태가 조금씩 다른데
    //   모션을 조금씩 바꿔줄 수가 있나? … 늑대는 앙 물듯이, 트리케는 아래에서 위로 올려치는").
    //
    //   모션은 **한 번만 만든다.** 옮겨 구울 때 뼈 무리별로 **세기**만 다르게 곱한다.
    //     · 1.0 = 원본 그대로
    //     · 0.5 = 절반만 (덜 쳐박는다)
    //     · **음수 = 반대 방향** ← 트리케의 「아래에서 위로 올려치기」가 이걸로 나온다.
    //       클립을 새로 만들 필요가 없다. 숙이는 동작을 뒤집으면 드는 동작이다.
    //
    //   ★세기는 「기본 자세에서 얼마나 벗어나나」에 걸린다. 그래서 축을 몰라도 되고,
    //     리그마다 뼈 축이 달라도 안전하다 (축을 짐작해 오프셋을 주면 반드시 틀린다).
    struct 성질 { public float 목, 머리, 다리, 꼬리, 몸통; }
    static readonly Dictionary<string, 성질> 종별 = new()
    {
        // 목을 덜 숙인다 — 사용자 "과하게 머리를 앞으로 쳐박으면서"
        { "dino_animated",      new 성질 { 목 = 0.55f, 머리 = 0.8f, 다리 = 1f, 꼬리 = 1f, 몸통 = 1f } },
        { "dino_deepBlue_anim", new 성질 { 목 = 0.55f, 머리 = 0.8f, 다리 = 1f, 꼬리 = 1f, 몸통 = 1f } },
        { "dino_galaxy_anim",   new 성질 { 목 = 0.55f, 머리 = 0.8f, 다리 = 1f, 꼬리 = 1f, 몸통 = 1f } },
        { "dino_green_anim",    new 성질 { 목 = 0.55f, 머리 = 0.8f, 다리 = 1f, 꼬리 = 1f, 몸통 = 1f } },
        { "dino_red_anim",      new 성질 { 목 = 0.55f, 머리 = 0.8f, 다리 = 1f, 꼬리 = 1f, 몸통 = 1f } },
        // 늑대 — 목은 거의 안 쓰고 **머리(턱)만** 앙 문다
        { "다이어울프",          new 성질 { 목 = 0.30f, 머리 = 1.35f, 다리 = 1f, 꼬리 = 1.1f, 몸통 = 0.9f } },
        // 트리케 — 목을 **거꾸로**. 숙이는 대신 뿔을 위로 쳐올린다
        { "트리케라톱스",        new 성질 { 목 = -0.85f, 머리 = -0.6f, 다리 = 1f, 꼬리 = 0.8f, 몸통 = 1f } },
        // 스테고 — 목이 짧고 무겁다. 거의 안 숙이고 꼬리를 쓴다
        { "스테고",             new 성질 { 목 = 0.35f, 머리 = 0.5f, 다리 = 1f, 꼬리 = 1.3f, 몸통 = 1f } },
        // 사슴 — 뿔이 무거워 목이 덜 꺾인다
        { "큰뿔사슴",           new 성질 { 목 = 0.6f, 머리 = 0.8f, 다리 = 1f, 꼬리 = 1f, 몸통 = 1f } },
        // 다람쥐 — 작고 촐싹댄다. 머리를 잘 놀린다
        { "검치다람쥐",         new 성질 { 목 = 0.7f, 머리 = 1.2f, 다리 = 1f, 꼬리 = 1.2f, 몸통 = 1f } },
    };
    static readonly 성질 기본성질 = new 성질 { 목 = 1f, 머리 = 1f, 다리 = 1f, 꼬리 = 1f, 몸통 = 1f };

    static float 세기(성질 s, string 뼈)
    {
        if (뼈 == "neck1" || 뼈.StartsWith("neck")) return s.목;
        if (뼈 == "head") return s.머리;
        if (뼈.StartsWith("leg")) return s.다리;
        if (뼈.StartsWith("tail")) return s.꼬리;
        return s.몸통;                     // spine_*
    }

    [MenuItem("Tools/토이라기/㉦ 네발 모션 옮겨 굽기", priority = 6)]
    public static void Run()
    {
        var 원본자세 = 기본자세(원본리그);
        if (원본자세 == null) { Debug.LogError("[이식] 원본 리그를 못 읽었다: " + 원본리그); return; }

        // ★이 도구는 이제 **예비**다 (2026-08-05 사용자 "이식하지말고 … 키프레임 찍어서").
        //   정식 경로는 `네발동작저작` — 동작을 **몸 기준 각도**로 직접 적는다. 이식은 원본
        //   클립의 쉬는 자세를 벗겨내도 뼈의 **로컬 축**이 모델마다 달라서(실측 확인) 한계가 있었다.
        //   결과물 이름을 `이식걷기_` 로 따로 두어 저작한 클립을 덮지 않게 한다.
        var 동작들 = new List<(string 이름, AnimationClip 클립)>();
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(원본클립);
            if (c == null) { Debug.LogError("[이식] 원본 클립이 없다"); return; }
            동작들.Add(("이식걷기", c));
        }

        int 만든수 = 0;
        foreach (var g in AssetDatabase.FindAssets("t:GameObject", new[] { 저장 }))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (p == 원본리그) continue;
            var 이름 = System.IO.Path.GetFileNameWithoutExtension(p);
            var 자세 = 기본자세(p);
            if (자세 == null) continue;
            var s = 종별.TryGetValue(이름, out var v) ? v : 기본성질;

            foreach (var (동작, 원클립) in 동작들)
            {
                var 새클립 = 옮기기(원클립, 원본자세, 자세, s);
                새클립.name = 동작 + "_" + 이름;
                string 경로 = 저장 + "/" + 동작 + "_" + 이름 + ".anim";
                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(경로) != null) AssetDatabase.DeleteAsset(경로);
                AssetDatabase.CreateAsset(새클립, 경로);

                string cp = 저장 + "/" + 동작 + "_" + 이름 + ".controller";
                if (AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(cp) != null)
                    AssetDatabase.DeleteAsset(cp);
                var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(cp);
                var st = ctrl.layers[0].stateMachine.AddState(동작);
                st.motion = 새클립; st.writeDefaultValues = true;
                ctrl.layers[0].stateMachine.defaultState = st;
                EditorUtility.SetDirty(ctrl);
                만든수++;
            }
            Debug.Log("[이식] " + 이름 + " — 동작 " + 동작들.Count + "개 옮겨 구움");
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[이식] 클립 " + 만든수 + "개 완료");
    }

    /// 그 모델의 **기본 자세** — 뼈 경로 → 로컬 회전.
    /// ★프리팹 그대로가 곧 기본 자세다 (애니메이터가 한 번도 안 돌았으므로).
    static Dictionary<string, Quaternion> 기본자세(string 경로)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(경로);
        if (go == null) return null;
        var 표 = new Dictionary<string, Quaternion>();
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            if (t == go.transform) continue;
            표[t.name] = t.localRotation;      // 뼈 이름은 유일하므로 이름으로 잡는다
        }
        return 표;
    }

    static AnimationClip 옮기기(AnimationClip 원본, Dictionary<string, Quaternion> 원본자세,
                                 Dictionary<string, Quaternion> 대상자세, 성질 성)
    {
        var 새 = new AnimationClip { name = 원본.name, frameRate = 원본.frameRate };
        // 경로별로 x·y·z·w 네 커브를 같이 다뤄야 한다 (사원수는 성분 하나씩 못 만진다)
        var 경로들 = new HashSet<string>();
        foreach (var b in AnimationUtility.GetCurveBindings(원본))
            if (b.propertyName.StartsWith("m_LocalRotation")) 경로들.Add(b.path);

        foreach (var 경로 in 경로들)
        {
            var 뼈 = 경로.Substring(경로.LastIndexOf('/') + 1);
            if (!원본자세.TryGetValue(뼈, out var 원기본) || !대상자세.TryGetValue(뼈, out var 새기본))
                continue;                                   // 그 몸에 없는 뼈는 건너뛴다
            var 원역 = Quaternion.Inverse(원기본);
            float 배 = 세기(성, 뼈);

            var cx = 커브(원본, 경로, "x"); var cy = 커브(원본, 경로, "y");
            var cz = 커브(원본, 경로, "z"); var cw = 커브(원본, 경로, "w");
            if (cx == null || cy == null || cz == null || cw == null) continue;

            var nx = new AnimationCurve(); var ny = new AnimationCurve();
            var nz = new AnimationCurve(); var nw = new AnimationCurve();
            for (int i = 0; i < cx.length; i++)
            {
                float t = cx[i].time;
                var q = new Quaternion(cx.Evaluate(t), cy.Evaluate(t), cz.Evaluate(t), cw.Evaluate(t));
                // ★기본자세에서 벗어난 몫만 떼어내고, 거기에 **세기**를 곱한 뒤 우리 기본자세에 얹는다.
                //   Slerp 의 t 가 음수면 반대로 돈다 — 트리케의 「올려치기」가 여기서 나온다.
                var 흔들림 = Quaternion.SlerpUnclamped(Quaternion.identity, 원역 * q, 배);
                var 새q = 새기본 * 흔들림;
                nx.AddKey(t, 새q.x); ny.AddKey(t, 새q.y); nz.AddKey(t, 새q.z); nw.AddKey(t, 새q.w);
            }
            놓기(새, 경로, "x", nx); 놓기(새, 경로, "y", ny);
            놓기(새, 경로, "z", nz); 놓기(새, 경로, "w", nw);
        }
        var s = AnimationUtility.GetAnimationClipSettings(원본);
        s.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(새, s);
        return 새;
    }

    static AnimationCurve 커브(AnimationClip c, string 경로, string 축)
        => AnimationUtility.GetEditorCurve(c,
            EditorCurveBinding.FloatCurve(경로, typeof(Transform), "m_LocalRotation." + 축));

    static void 놓기(AnimationClip c, string 경로, string 축, AnimationCurve cur)
        => AnimationUtility.SetEditorCurve(c,
            EditorCurveBinding.FloatCurve(경로, typeof(Transform), "m_LocalRotation." + 축), cur);
}
