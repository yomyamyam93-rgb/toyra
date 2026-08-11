using System.Reflection;
using UnityEditor;
using UnityEngine;

/// **맵 보기** — 이번 씨앗의 세계를 **실제로 지어서 위에서 찍어** 보여준다.
///
/// ★게임 안에는 지도가 없다. 좀보이드처럼 **머릿속에 지도가 생기는 게** 재미라서
///   화면에 미니맵을 띄우지 않는다. 하지만 **만드는 사람은 봐야 한다** —
///   폐허가 어디 났는지, 숲이 뭉쳤는지, 길이 어떻게 났는지를 모르면 손볼 수가 없다.
///   → 이건 **개발용 창**이다. 게임 화면은 안 건드린다.
///
/// ★★칸 색표가 아니라 **진짜 그림**이다 (2026-08-06 사용자 — "그냥 칸인데, 나는 실제 맵이
///   어떻게 생성됐는지 보고싶다는건데"). `WorldGen.Generate()` 를 그대로 불러 세계를 짓고,
///   임시 카메라를 하늘에 띄워 한 장 찍은 뒤 도로 치운다.
///   ☆플레이를 켤 필요가 없다. 다 찍고 나면 세계도 지운다 (씬이 안 더러워진다).
///   ☆잔디는 안 찍힌다 — 잔디는 매 프레임 그리는 방식이라 편집 중에는 안 돈다.
public class 맵보기 : EditorWindow
{
    [MenuItem("Tools/토이라기/㉨ 맵 보기", priority = 8)]
    static void 열기() => GetWindow<맵보기>("맵 보기").minSize = new Vector2(560, 700);

    int 씨앗 = 0;
    int 그림크기 = 1024;
    bool 찍고치우기 = true;
    bool 표시달기 = true;
    Texture2D 그림;
    WorldGen.Land[,] 칸;

    static readonly (WorldGen.Land 종류, Color 색, string 이름)[] 표시표 =
    {
        (WorldGen.Land.폐허, new Color(1f, 0.55f, 0.15f), "폐허"),
        (WorldGen.Land.둥지, new Color(1f, 0.9f, 0.2f),  "둥지"),
        (WorldGen.Land.캠프, new Color(1f, 0.25f, 0.25f), "캠프"),
        (WorldGen.Land.물웅덩이, new Color(0.4f, 0.8f, 1f), "물"),
        // 테마 권역 (2026-08-11) — 분포를 걸어가 보지 않고 여기서 확인한다
        (WorldGen.Land.찰흙,     new Color(0.85f, 0.50f, 0.30f), "찰흙"),
        (WorldGen.Land.솜털실,   new Color(0.95f, 0.70f, 0.85f), "솜털실"),
        (WorldGen.Land.블록,     new Color(0.30f, 0.55f, 1f),    "블록"),
        (WorldGen.Land.유리설원, new Color(0.75f, 0.95f, 1f),    "유리설원"),
        (WorldGen.Land.동굴,     new Color(0.65f, 0.58f, 0.52f), "동굴"),
    };

    void OnGUI()
    {
        EditorGUILayout.Space(4);
        using (new EditorGUILayout.HorizontalScope())
        {
            씨앗 = EditorGUILayout.IntField("씨앗", 씨앗);
            if (GUILayout.Button("씬 씨앗", GUILayout.Width(60)))
            {
                var w = FindFirstObjectByType<WorldGen>();
                if (w != null) 씨앗 = w.worldSeed;
            }
            if (GUILayout.Button("다른 씨앗", GUILayout.Width(70))) 씨앗 = Random.Range(1, 999999);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            그림크기 = EditorGUILayout.IntPopup("그림 크기", 그림크기,
                        new[] { "512", "1024", "2048" }, new[] { 512, 1024, 2048 });
            찍고치우기 = EditorGUILayout.ToggleLeft("찍고 치우기", 찍고치우기, GUILayout.Width(100));
        }
        표시달기 = EditorGUILayout.ToggleLeft("폐허·둥지·캠프 자리에 표시 찍기", 표시달기);

        EditorGUILayout.Space(2);
        if (GUILayout.Button("세계 지어서 찍기", GUILayout.Height(28))) 찍기();

        if (그림 == null)
        {
            EditorGUILayout.HelpBox("「세계 지어서 찍기」 를 누르세요.\n" +
                                    "실제로 세계를 짓고 하늘에서 한 장 찍습니다 (플레이 안 켜도 됩니다).",
                                    MessageType.Info);
            return;
        }

        float 폭 = Mathf.Min(position.width - 20f, position.height - 190f);
        var 판 = GUILayoutUtility.GetRect(폭, 폭);
        판.x = (position.width - 폭) * 0.5f;
        GUI.DrawTexture(판, 그림, ScaleMode.StretchToFill, false);

        if (표시달기 && 칸 != null) 표시그리기(판);

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField(
            $"{WorldGrid.Tile * WorldGrid.N:0}m × {WorldGrid.Tile * WorldGrid.N:0}m  ·  " +
            $"칸 {WorldGrid.Tile:0}m × {WorldGrid.N}×{WorldGrid.N}  ·  " +
            $"한 픽셀 = {WorldGrid.Tile * WorldGrid.N / 그림.width:0.0}m", EditorStyles.miniLabel);

        if (칸 != null)
        {
            var sb = new System.Text.StringBuilder();
            for (int x = 0; x < WorldGrid.N; x++)
                for (int z = 0; z < WorldGrid.N; z++)
                    if (칸[x, z] == WorldGen.Land.폐허 || 칸[x, z] == WorldGen.Land.둥지)
                    {
                        var c = WorldGrid.TileCenter(x, z);
                        sb.Append($"{(칸[x, z] == WorldGen.Land.폐허 ? "폐허" : "둥지")}({c.x:0},{c.z:0})  ");
                    }
            EditorGUILayout.LabelField(sb.ToString(), EditorStyles.wordWrappedMiniLabel);
        }
    }

    /// 화면 자리와 세계 좌표를 이어 주는 자 — 표시를 정확히 얹으려면 이게 있어야 한다
    void 표시그리기(Rect 판)
    {
        float 전체 = WorldGrid.Tile * WorldGrid.N;
        var 집 = WorldGrid.Center;
        float 왼 = 집.x - 전체 * 0.5f, 아래 = 집.z - 전체 * 0.5f;

        var 라벨 = new GUIStyle(EditorStyles.miniLabel);
        for (int x = 0; x < WorldGrid.N; x++)
            for (int z = 0; z < WorldGrid.N; z++)
            {
                Color? 색 = null; string 이름 = null;
                foreach (var (종류, c, n) in 표시표)
                    if (칸[x, z] == 종류 && 종류 != WorldGen.Land.물웅덩이) { 색 = c; 이름 = n; }
                if (색 == null) continue;

                var p = WorldGrid.TileCenter(x, z);
                // ★위가 +z 다. 화면 y 는 아래로 커지므로 뒤집는다
                float u = (p.x - 왼) / 전체, v = (p.z - 아래) / 전체;
                var 자리 = new Vector2(판.x + u * 판.width, 판.y + (1f - v) * 판.height);

                EditorGUI.DrawRect(new Rect(자리.x - 4, 자리.y - 4, 8, 8), 색.Value);
                라벨.normal.textColor = 색.Value;
                GUI.Label(new Rect(자리.x + 6, 자리.y - 8, 60, 16), 이름, 라벨);
            }
    }

    // ───────────────────────────────── 찍기

    void 찍기()
    {
        var w = FindFirstObjectByType<WorldGen>();
        if (w == null) { EditorUtility.DisplayDialog("맵 보기", "씬에 WorldGen 이 없습니다.", "확인"); return; }

        // ★★플레이 중엔 **이미 있는 세상을 찍는다** (2026-08-11). 전엔 플레이 중에 눌러도
        //   세상을 새로 지었다 뜯어서(찍고치우기), 찍고 나면 게임이 빈 땅 위에 서 있었다.
        bool 놀이중 = Application.isPlaying;
        int 옛씨앗 = w.worldSeed;
        if (!놀이중) w.worldSeed = 씨앗;

        Camera 눈 = null; RenderTexture rt = null;

        // ★★밤에 찍으면 검게 나온다 (2026-08-11 사용자 "검게 타서 오류난거같아" — 오류가
        //   아니라 밤이었다. 나무는 조명을 안 받는 단색 셰이더라 저 혼자 초록으로 남는다).
        //   → 찍는 동안만 조명을 낮으로 젖혔다가 되돌린다 (동작진열이 밤을 젖히는 것과 같은 이유)
        var 해 = RenderSettings.sun;
        if (해 == null) { var dl = GameObject.Find("Directional Light"); if (dl != null) 해 = dl.GetComponent<Light>(); }
        Quaternion 해회전 = Quaternion.identity; float 해세기 = 0f; Color 해색 = Color.white;
        if (해 != null) { 해회전 = 해.transform.rotation; 해세기 = 해.intensity; 해색 = 해.color; }
        bool 안개 = RenderSettings.fog;
        var 주변모드 = RenderSettings.ambientMode;
        var 하늘색 = RenderSettings.ambientSkyColor;

        try
        {
            if (!놀이중)
            {
                EditorUtility.DisplayProgressBar("맵 보기", "세계를 짓는 중…", 0.2f);
                w.Generate();
            }

            // 칸 종류는 표시를 찍는 데만 쓴다 (그림은 진짜 세계다)
            var 칸밭 = typeof(WorldGen).GetField("kinds", BindingFlags.NonPublic | BindingFlags.Instance);
            칸 = 칸밭 != null ? (WorldGen.Land[,])칸밭.GetValue(w) : null;

            if (해 != null)
            {
                해.transform.rotation = Quaternion.Euler(50f, 215f, 0f);
                해.intensity = 1.4f;
                해.color = new Color(1f, 0.97f, 0.9f);
            }
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.60f, 0.65f);

            EditorUtility.DisplayProgressBar("맵 보기", "하늘에서 찍는 중…", 0.7f);
            float 전체 = WorldGrid.Tile * WorldGrid.N;
            var 집 = WorldGrid.Center;

            var 눈오브 = new GameObject("맵카메라") { hideFlags = HideFlags.HideAndDontSave };
            눈 = 눈오브.AddComponent<Camera>();
            눈.orthographic = true;
            눈.orthographicSize = 전체 * 0.5f;
            눈.transform.position = new Vector3(집.x, 400f, 집.z);
            눈.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 똑바로 내려다본다
            눈.nearClipPlane = 1f; 눈.farClipPlane = 900f;
            눈.clearFlags = CameraClearFlags.SolidColor;
            눈.backgroundColor = new Color(0.08f, 0.09f, 0.10f);
            눈.cullingMask = ~((1 << Outliner.층) | (1 << Outliner.잔디층) | (1 << 5));  // 5 = UI
            눈.allowMSAA = false;

            rt = new RenderTexture(그림크기, 그림크기, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            눈.targetTexture = rt;
            // ★★셰이더가 준비되기를 기다렸다 찍는다 (2026-08-11 사용자 "검게 타서 오류난거같아").
            //   에디터는 셰이더를 뒤에서 천천히 굽는데, 안 구워진 첫 렌더는 **땅이 검게** 나온다.
            //   비동기를 끄면 이 렌더에서 다 구워질 때까지 기다린다. 한 번 구워지면 다음부턴 즉시다.
            bool 옛비동기 = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            눈.Render();
            ShaderUtil.allowAsyncCompilation = 옛비동기;

            var 옛 = RenderTexture.active;
            RenderTexture.active = rt;
            if (그림 != null) DestroyImmediate(그림);
            그림 = new Texture2D(그림크기, 그림크기, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            그림.ReadPixels(new Rect(0, 0, 그림크기, 그림크기), 0, 0);
            그림.Apply();
            RenderTexture.active = 옛;
        }
        finally
        {
            // 조명을 원래대로 — 플레이 중이면 DayNight 가 다음 틱에 어차피 되잡지만, 안 미룬다
            if (해 != null) { 해.transform.rotation = 해회전; 해.intensity = 해세기; 해.color = 해색; }
            RenderSettings.fog = 안개;
            RenderSettings.ambientMode = 주변모드;
            RenderSettings.ambientSkyColor = 하늘색;

            if (눈 != null) { 눈.targetTexture = null; DestroyImmediate(눈.gameObject); }
            if (rt != null) { rt.Release(); DestroyImmediate(rt); }
            if (!놀이중 && 찍고치우기) w.Clear();     // ★플레이 중엔 세상을 안 지운다 — 게임이 그 위에 서 있다
            if (!놀이중) w.worldSeed = 옛씨앗;
            EditorUtility.ClearProgressBar();
        }
        Repaint();
    }

    void OnDisable() { if (그림 != null) DestroyImmediate(그림); }
}
