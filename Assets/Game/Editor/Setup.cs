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

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[씬] 완성 — 플레이를 누르세요. 씬은 Ctrl+S 로 저장.");
    }

    /// 캐릭터 상자 — 나중에 이 자식들을 지우고 진짜 모델을 넣으면 된다
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
