using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// 리깅된 공룡을 캐릭터 주변에 늘어놓는다 — **움직임을 눈으로 보려고** 만든 것.
///
/// ★`Resources/rig` 의 모델에는 뼈대와 클립이 들어 있다. 그런데 glTF 로 들여온 것은
///   **아바타도 컨트롤러도 안 딸려 온다** — 그대로 두면 한 프레임도 안 움직인다
///   (캐릭터에서 겪은 것과 같다. `HeroSetup` 의 아바타 주석 참고).
///   그래서 모델마다 ①제네릭 아바타를 만들고 ②클립 하나를 무한 재생하는
///   컨트롤러를 만들어 붙인다.
///
/// ★클립이 둘인 모델은 **둘 다 보여준다** — 하나는 대개 걷기, 하나는 다른 동작이라
///   무엇이 들어 있는지 눈으로 확인해야 로스터를 정할 수 있다.
public static class DinoRigShowcase
{
    const string 폴더 = "Assets/Game/Resources/rig";
    const string 저장 = "Assets/Game/Animations/rig";
    const string 그릇이름 = "리깅_진열";

    [MenuItem("Tools/토이라기/㉥ 리깅 공룡 뿌리기", priority = 5)]
    public static void Run()
    {
        var hero = Object.FindFirstObjectByType<Hero>();
        if (hero == null) { Debug.LogError("[리깅] 씬에 Hero 가 없다"); return; }

        if (!AssetDatabase.IsValidFolder(저장))
            AssetDatabase.CreateFolder("Assets/Game/Animations", "rig");

        var 옛것 = GameObject.Find(그릇이름);
        if (옛것 != null) Undo.DestroyObjectImmediate(옛것);
        var 그릇 = new GameObject(그릇이름);
        Undo.RegisterCreatedObjectUndo(그릇, "리깅 진열");

        // 캐릭터 앞쪽에 줄줄이 — 카메라가 보는 쪽으로 펼친다
        var 가운데 = hero.transform.position;
        var 목록 = new List<(GameObject 원본, AnimationClip 클립)>();
        foreach (var g in AssetDatabase.FindAssets("t:GameObject", new[] { 폴더 }))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go == null) continue;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                if (o is AnimationClip ac && !ac.name.StartsWith("__")) 목록.Add((go, ac));
        }
        if (목록.Count == 0) { Debug.LogError($"[리깅] {폴더} 에 클립이 든 모델이 없다"); return; }

        int 한줄 = Mathf.CeilToInt(Mathf.Sqrt(목록.Count));
        int 만든수 = 0;
        for (int i = 0; i < 목록.Count; i++)
        {
            var (원본, 클립) = 목록[i];
            int cx = i % 한줄, cz = i / 한줄;
            var at = 가운데 + new Vector3((cx - (한줄 - 1) * 0.5f) * 간격,
                                          0f,
                                          (cz - (한줄 - 1) * 0.5f) * 간격 + 앞으로);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(원본, 그릇.transform);
            go.name = 원본.name + "·" + 클립.name;
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // 카메라 쪽을 본다
            키맞춤(go, 키찾기(원본.name));

            var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
            var av = AvatarBuilder.BuildGenericAvatar(go, "");
            av.name = go.name + "_아바타";
            AssetDatabase.CreateAsset(av, 저장 + "/" + 안전이름(go.name) + ".asset");
            anim.avatar = av;
            anim.runtimeAnimatorController = 컨트롤러(클립);
            anim.applyRootMotion = false;      // 제자리에서 보여준다
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // ★옆에 **사람 키 기둥**을 세운다 (CLAUDE.md: "사람 1.8m 기둥 옆에 세워 놓고
            //   눈으로 보고 정한다"). 미터 숫자로는 크기가 안 읽히고, 옆에 사람만 한 게
            //   서 있어야 "이건 너무 크다/작다"가 한눈에 온다.
            var 기둥 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            기둥.name = "사람키기둥";
            기둥.transform.SetParent(그릇.transform, false);
            기둥.transform.position = at + new Vector3(2.2f, 사람키 * 0.5f, 0f);
            기둥.transform.localScale = new Vector3(0.35f, 사람키, 0.35f);
            Object.DestroyImmediate(기둥.GetComponent<Collider>());

            만든수++;
        }

        AssetDatabase.SaveAssets();
        EditorSceneMark(hero);
        Debug.Log($"[리깅] {만든수}마리 세웠다 — 플레이하면 움직입니다. 지우려면 「{그릇이름}」 오브젝트를 지우세요.");
        Selection.activeGameObject = 그릇;
    }

    /// 한 줄에 몇 미터 간격으로 세우나 — 브론토가 8.6m 라 넉넉해야 겹치지 않는다
    const float 간격 = 13f;
    /// 캐릭터에서 얼마나 앞에 세우나
    const float 앞으로 = 6f;
    // ★★종마다 키가 다르다 (2026-08-05 사용자). **사람 2.16m 기준으로 환산한 값**이다 —
    //   「사람보다 몇 배」로 말한 것을 여기서 미터로 바꿔 놓는다. 새 종을 넣을 때도
    //   미터를 짐작하지 말고 **사람 기준 몇 배인지**를 정하고 여기서 곱해라.
    //     다람쥐 = 사람 허리       → 2.16 × 0.5
    //     랩터·사슴 = 지금 그대로  → 2.0m (진열에서 보고 좋다고 한 크기)
    //     늑대 = 랩터보다 조금     → 1.7m
    //     티라노 = 사람 ×3         → 6.48m
    //     브론토 = 사람 ×4         → 8.64m
    const float 사람키 = 2.16f;
    static readonly (string 낱말, float 키)[] 키표 =
    {
        ("squirrel",  사람키 * 0.5f),    // 허리춤
        ("raptor",    2.0f),
        ("deer",      2.0f),
        ("wolf",      1.7f),
        ("tyranno",   사람키 * 3f),
        // ★목 긴 네발 무리 — 브론토로 보이는 것들. 어느 파일이 브론토인지 확정되면 정리한다
        ("animated",  사람키 * 4f),
        ("deepBlue",  사람키 * 4f),
        ("galaxy",    사람키 * 4f),
        ("green",     사람키 * 4f),
        ("red",       사람키 * 4f),
    };

    static float 키찾기(string 이름)
    {
        foreach (var (낱말, 키) in 키표)
            if (이름.Contains(낱말)) return 키;
        return 2f;                       // 표에 없으면 사람 어깨쯤
    }

    static readonly Dictionary<AnimationClip, AnimatorController> 캐시 = new();

    /// 클립 하나를 무한 반복하는 컨트롤러
    static AnimatorController 컨트롤러(AnimationClip 클립)
    {
        if (캐시.TryGetValue(클립, out var 있던) && 있던 != null) return 있던;
        string path = 저장 + "/" + 안전이름(클립.name) + ".controller";
        var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (c == null)
        {
            c = AnimatorController.CreateAnimatorControllerAtPath(path);
            var st = c.layers[0].stateMachine.AddState("재생");
            st.motion = 클립;
            st.writeDefaultValues = true;
            c.layers[0].stateMachine.defaultState = st;
            EditorUtility.SetDirty(c);
        }
        캐시[클립] = c;
        return c;
    }

    /// ★구운 메시의 월드 높이로 잰다 — 바운즈는 파일마다 부풀림이 달라 못 믿는다
    ///   (`HeroSetup.키맞춤` 에서 두 번 데인 그것)
    static void 키맞춤(GameObject go, float 키)
    {
        go.transform.localScale = Vector3.one;
        if (!구운높이(go, out float 바닥, out float 머리)) return;
        float h = 머리 - 바닥;
        if (h > 0.0001f) go.transform.localScale = Vector3.one * (키 / h);
        if (구운높이(go, out 바닥, out _))
            go.transform.position += Vector3.up * -바닥;
    }

    static bool 구운높이(GameObject go, out float 바닥, out float 머리)
    {
        바닥 = float.MaxValue; 머리 = float.MinValue; bool 있음 = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            Mesh m = null; bool 구움 = false;
            if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
            { m = new Mesh(); smr.BakeMesh(m, true); 구움 = true; }
            else m = r.GetComponent<MeshFilter>()?.sharedMesh;
            if (m == null) continue;
            var mtx = r.transform.localToWorldMatrix;
            foreach (var v in m.vertices)
            {
                float y = mtx.MultiplyPoint3x4(v).y;
                if (y < 바닥) 바닥 = y;
                if (y > 머리) 머리 = y;
                있음 = true;
            }
            if (구움) Object.DestroyImmediate(m);
        }
        return 있음;
    }

    static string 안전이름(string s)
    {
        foreach (var bad in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(bad, '_');
        return s.Replace('|', '_').Replace('·', '_');
    }

    static void EditorSceneMark(Hero hero)
    {
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(hero.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(hero.gameObject.scene);
    }
}
