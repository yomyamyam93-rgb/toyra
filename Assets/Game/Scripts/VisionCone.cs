using UnityEngine;
using UnityEngine.Rendering.Universal;

/// 시야 — **보는 방향만 보이고 나머지는 어둡다.**
///
/// ★긴장은 「안 보이는 것」에서 온다. 사방이 다 보이면 떼가 몰려와도 놀랄 일이 없다.
/// ★보는 방향은 `Hero.LookDir`(마우스)다 — 뒷걸음질 치면서 앞을 계속 볼 수 있다.
/// ★가리기(바위 뒤가 안 보이는 것)는 아직 없다. 각도와 거리만 본다.
[DefaultExecutionOrder(100)]
public class VisionCone : MonoBehaviour
{
    // ★낮엔 사방이 보여야 한다 (2026-08-03 사용자 — "기존이 낮이었어? 밤인 줄 알았는데").
    //   낮에도 부채꼴 밖을 88% 어둡게 하고 있어서 대낮이 밤처럼 보였다.
    //   좀보이드도 낮엔 주변이 다 보이고, 시야가 뜻을 갖는 건 **밤과 실내**다.
    //   → 낮 어둠은 옅게(0.38), 밤에 짙어진다(`DayNight.밤어둠`).
    [Header("시야")]
    [Tooltip("보이는 거리 (m) — 낮 기준")] public float distance = 45f;
    [Tooltip("부채꼴 반각 (°) — 55 면 110° 만큼 보인다")] public float halfAngle = 55f;
    [Tooltip("부채꼴 가장자리가 흐려지는 폭 (°) — 넓을수록 부드럽다")] public float edgeSoft = 18f;
    [Tooltip("거리 끝이 흐려지는 폭 (m)")] public float distSoft = 10f;

    [Header("코앞 — 등 뒤라도 아는 범위")]
    public float nearRadius = 4.5f;
    public float nearSoft = 3f;

    [Header("어둠")]
    [Range(0f, 1f)] [Tooltip("낮에 부채꼴 밖이 얼마나 어두운가 (1 이면 칠흑)")] public float darkness = 0.38f;
    [Range(0f, 0.6f)] [Tooltip("밝은 안쪽도 멀수록 조금씩 어두워지는 정도")] public float falloff = 0.25f;

    Hero hero;
    Camera cam;
    Material mat;
    DayNight day;

    void Start()
    {
        hero = FindFirstObjectByType<Hero>();
        day = FindFirstObjectByType<DayNight>();
        cam = Camera.main;
        if (cam == null || hero == null) { enabled = false; return; }

        var data = cam.GetUniversalAdditionalCameraData();
        if (data != null) data.requiresDepthTexture = true;   // 이 셰이더는 깊이를 읽는다

        MakeOverlay();
    }

    void MakeOverlay()
    {
        var sh = Shader.Find("Toyra/Vision");
        if (sh == null) { Debug.LogError("[시야] Vision.shader 를 못 찾았다"); enabled = false; return; }
        mat = new Material(sh);

        var go = new GameObject("시야_덮개");
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, cam.nearClipPlane + 0.05f);

        var m = new Mesh { name = "시야_덮개" };
        m.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f), new Vector3(0.5f,  0.5f, 0f)
        };
        m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        // 바운즈를 크게 — 카메라 밖으로 판정돼 안 그려지는 일이 없게
        m.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);

        go.AddComponent<MeshFilter>().sharedMesh = m;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
    }

    void LateUpdate()
    {
        if (hero == null || mat == null) return;

        var p = hero.transform.position + Vector3.up * (hero.height * 0.8f);
        var d = hero.LookDir;

        // 밤이면 보이는 거리가 줄고 어둠이 짙어진다 — 밤을 「다른 규칙의 시간」으로 만드는 자리
        float dist = day != null ? distance * day.시야배 : distance;
        float dark = day != null ? day.어둠(darkness) : darkness;

        Shader.SetGlobalVector("_VisionPos", p);
        Shader.SetGlobalVector("_VisionDir", new Vector4(d.x, 0f, d.z, 0f));
        Shader.SetGlobalVector("_VisionAngle", new Vector4(halfAngle, Mathf.Max(0.5f, edgeSoft), 0f, 0f));
        Shader.SetGlobalVector("_VisionDist",
            new Vector4(dist, Mathf.Max(0.5f, distSoft), nearRadius, Mathf.Max(0.5f, nearSoft)));
        Shader.SetGlobalVector("_VisionDark", new Vector4(dark, falloff, 0f, 0f));
    }
}
