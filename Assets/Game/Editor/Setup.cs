using UnityEditor;
using UnityEngine;

/// 씬 짓기 — 한 번 누르면 놀 수 있는 상태가 된다.
/// 씬에는 오브젝트 셋뿐이다: 카메라 · 캐릭터 · 세계. 나머지는 실행할 때 만들어진다.
public static class Setup
{
    [MenuItem("Tools/토이라기/① 씬 짓기", priority = 0)]
    public static void BuildScene()
    {
        // ── 카메라 (고정 아이소메트릭)
        var cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            Undo.RegisterCreatedObjectUndo(go, "씬 짓기");
        }
        if (cam.GetComponent<IsoCam>() == null) Undo.AddComponent<IsoCam>(cam.gameObject);
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.05f, 0.06f);

        // ── 캐릭터
        var hero = Object.FindFirstObjectByType<Hero>();
        if (hero == null)
        {
            var go = new GameObject("캐릭터");
            Undo.RegisterCreatedObjectUndo(go, "씬 짓기");
            hero = go.AddComponent<Hero>();
            MakeBody(go.transform);
        }
        if (hero.GetComponent<HeroAttack>() == null) Undo.AddComponent<HeroAttack>(hero.gameObject);
        if (hero.GetComponent<HeroCarry>() == null) Undo.AddComponent<HeroCarry>(hero.gameObject);
        WireHeroBody(hero);
        hero.transform.position = WorldGrid.Center;

        // ── 세계
        var world = Object.FindFirstObjectByType<WorldGen>();
        if (world == null)
        {
            var go = new GameObject("세계");
            Undo.RegisterCreatedObjectUndo(go, "씬 짓기");
            world = go.AddComponent<WorldGen>();
            go.AddComponent<Boot>();
            go.AddComponent<VisionCone>();
            go.AddComponent<Wildlife>();
            go.AddComponent<HUD>();
            go.AddComponent<DayNight>();
        }
        else
        {
            if (world.GetComponent<Wildlife>() == null) Undo.AddComponent<Wildlife>(world.gameObject);
            if (world.GetComponent<HUD>() == null) Undo.AddComponent<HUD>(world.gameObject);
            if (world.GetComponent<DayNight>() == null) Undo.AddComponent<DayNight>(world.gameObject);
        }

        // ── 빛 — 어두운 톤. 밝으면 시야 시스템이 뜻을 잃는다
        var sun = Object.FindFirstObjectByType<Light>();
        if (sun == null)
        {
            var go = new GameObject("햇빛");
            Undo.RegisterCreatedObjectUndo(go, "씬 짓기");
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
        }
        sun.transform.rotation = Quaternion.Euler(52f, 30f, 0f);
        sun.intensity = 0.75f;
        sun.color = new Color(0.85f, 0.88f, 1f);
        sun.shadows = LightShadows.Soft;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.22f);
        RenderSettings.fog = false;

        모델끼우기(world.GetComponent<Wildlife>());

        // ★씬을 여기서 바로 저장한다. 사람이 Ctrl+S 를 잊으면 오늘 작업이 통째로 날아간다
        //   (2026-08-03 실제로 그랬다). 「세상은 코드가 만든다」는 원칙 덕에 잃은 건
        //   모델 연결뿐이었고, 그것도 이제 이 함수가 도로 끼운다.
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        Debug.Log("[씬] 완성 · 저장까지 끝 — 플레이를 누르세요.");
    }

    /// 종마다 모델을 파일 이름으로 찾아 끼운다 — 씬이 날아가도 버튼 하나로 되돌아온다
    static readonly (string 종, string 파일)[] 모델표 =
    {
        ("늑구", "chibi_wolf"), ("호동", "chibi_tiger"), ("티라", "chibi_tyranno"),
        ("꼭꼬", "꼭꼬"),        ("내펫", "랍또"),
    };

    static void 모델끼우기(Wildlife wl)
    {
        if (wl == null) return;
        var so = new SerializedObject(wl);
        foreach (var (종, 파일) in 모델표)
        {
            var p = so.FindProperty(종);
            if (p == null) continue;
            var slot = p.FindPropertyRelative("모델");
            if (slot == null || slot.objectReferenceValue != null) continue;   // 이미 있으면 둔다
            var m = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Game/Models/{파일}.glb");
            if (m != null) slot.objectReferenceValue = m;
        }
        so.ApplyModifiedProperties();
    }

    /// 진짜 모델(여자·남자)을 몸 자리에 물린다 — 상자는 지우지 않고 끈다.
    /// 모델 파일이 없으면 아무것도 안 하고 상자가 그대로 나온다.
    static void WireHeroBody(Hero hero)
    {
        var hb = hero.GetComponent<HeroBody>();
        if (hb == null) hb = Undo.AddComponent<HeroBody>(hero.gameObject);

        if (hb.여자모델 == null)
            hb.여자모델 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Models/hero_woman.fbx");
        if (hb.남자모델 == null)
            hb.남자모델 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Models/hero_man.fbx");
        if (hb.재질 == null) hb.재질 = 살재질();
        if (hb.여자컨트롤러 == null)
            hb.여자컨트롤러 = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Game/Animations/영웅_woman.controller");
        if (hb.남자컨트롤러 == null)
            hb.남자컨트롤러 = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Game/Animations/영웅_man.controller");

        if (hero.GetComponent<HeroAnim>() == null) Undo.AddComponent<HeroAnim>(hero.gameObject);

        foreach (var t in hero.GetComponentsInChildren<Transform>(true))
        {
            if (t == hero.transform) continue;
            if (t.name == "몸" && hb.상자 == null) hb.상자 = t;
            // 방향 표시용 코 — 진짜 모델이 있으면 필요 없다
            if (t.name == "코") t.gameObject.SetActive(hb.여자모델 == null && hb.남자모델 == null);
        }
        hb.다시짓기();
        EditorUtility.SetDirty(hb);
    }

    /// 캐릭터 살 재질 — 흰색. 없으면 만든다.
    /// (톤은 색과 빛으로 잡는다 — 모델은 안 건드린다)
    static Material 살재질()
    {
        const string path = "Assets/Game/Models/살_흰색.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;

        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        m = new Material(sh) { name = "살_흰색" };
        m.color = Color.white;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.12f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.12f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    /// 캐릭터 상자 — 진짜 모델이 없을 때만 보인다 (`HeroBody` 가 끄고 켠다)
    static void MakeBody(Transform parent)
    {
        var body = Grey.Box(parent, Vector3.up * 0.9f, new Vector3(0.55f, 1.8f, 0.38f),
                            new Color(0.85f, 0.80f, 0.65f), "몸");
        body.transform.SetParent(parent, false);
        body.transform.localPosition = Vector3.up * 0.9f;

        // 앞쪽 표식 — 어디를 보는지 알아야 한다
        var nose = Grey.Box(parent, Vector3.zero, new Vector3(0.22f, 0.22f, 0.3f),
                            new Color(0.95f, 0.45f, 0.25f), "코");
        nose.transform.SetParent(parent, false);
        nose.transform.localPosition = new Vector3(0f, 1.55f, 0.28f);
    }
}
