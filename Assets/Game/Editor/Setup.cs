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
        if (cam.GetComponent<PixelScreen>() == null) Undo.AddComponent<PixelScreen>(cam.gameObject);
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
        if (world.GetComponent<ToyShowcase>() == null) Undo.AddComponent<ToyShowcase>(world.gameObject);
        if (world.GetComponent<Matte>() == null) Undo.AddComponent<Matte>(world.gameObject);
        if (world.GetComponent<Outliner>() == null) Undo.AddComponent<Outliner>(world.gameObject);
        if (world.GetComponent<PixelSnapper>() == null) Undo.AddComponent<PixelSnapper>(world.gameObject);
        if (world.GetComponent<GrassField>() == null) Undo.AddComponent<GrassField>(world.gameObject);

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
        sun.intensity = 1.35f;
        sun.color = new Color(0.85f, 0.88f, 1f);
        // ★부드러운 그림자는 저해상도에서 가장자리가 프레임마다 흔들려 지글거린다.
        //   픽셀 화면에서는 딱딱한 그림자가 맞다
        sun.shadows = LightShadows.Hard;

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
        ("꼭꼬", "꼭꼬"),        // 내펫은 모델을 안 물린다 (랍또 폐기 — 2026-08-04 사용자)
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

    /// 캐릭터 상자 — 캐릭터 모델은 아직 없다 (2026-08-04 사용자가 리깅 작업을 물렀다)
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
