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
    //   → 낮 어둠은 0, 밤에만 짙어진다(`DayNight.밤어둠`).
    [Header("시야")]
    [Tooltip("보이는 거리 (m) — 낮 기준")] public float distance = 45f;
    [Tooltip("부채꼴 반각 (°) — 55 면 110° 만큼 보인다")] public float halfAngle = 55f;
    [Tooltip("부채꼴 가장자리가 흐려지는 폭 (°) — 넓을수록 부드럽다")] public float edgeSoft = 18f;
    [Tooltip("거리 끝이 흐려지는 폭 (m)")] public float distSoft = 10f;

    [Header("코앞 — 등 뒤라도 아는 범위")]
    public float nearRadius = 4.5f;
    public float nearSoft = 3f;

    [Header("어둠")]
    // ★★낮에는 시야가 **아예 없다** (2026-08-04 사용자 — "낮에는 시야같은 게 아예 안 뜨게,
    //   밤에만 시야 적용"). 전엔 낮에도 옅은 어둠(0.06)과 거리 감쇠(0.08)가 남아 있어서
    //   대낮인데도 화면 가장자리가 침침했다. 이제 낮 값은 **0 이 아니라 아예 없다** —
    //   인스펙터 값으로 두면 씬에 저장된 옛 숫자가 다시 살아나므로 필드째 지웠다.
    //   낮 어둠은 코드가 0 으로 못박고, 밤 어둠은 `DayNight.밤어둠` 이 정한다.
    [Range(0f, 0.6f)] [Tooltip("밤에 밝은 안쪽도 멀수록 어두워지는 정도 (낮엔 안 걸린다)")]
    public float falloff = 0.08f;

    // ★★실내(동굴 속) — `굴가림` 이 매 틱 알려 준다. 0 이면 바깥, 1 이면 굴 속.
    //   부드럽게 오가야 문턱에서 화면이 번쩍이지 않는다
    [Header("실내 (동굴 속)")]
    [Tooltip("굴 안에서 보이는 거리 (m) — 밤 시야와 비슷하게")] public float 실내거리 = 13f;
    // ★★★굴 안은 **완전 암흑**이다 (2026-08-11 사용자 "동굴안에 있을때, 주변이 진짜 아예
    //   암흑시야였으면좋겠어"). 0.82 는 18% 가 남아 굴 안이 훤히 보였다.
    //   ☆1.0 이면 굴 밖 풍경도 같이 사라진다 — `실내거리`(13m) 밖은 셰이더에서 lit=0 이 되고,
    //     하늘도 같은 값으로 덮인다 (Vision.shader 49·63·74행). 값 하나가 둘 다 한다.
    //   ☆1.0 은 **너무 답답했다** (2026-08-11 사용자 "아예 까만색이니까 너무 답답하긴하다
    //     조금은 보여야할듯"). 0.93 이면 7% 가 남아 벽의 윤곽만 겨우 읽힌다 —
    //     길은 안 잃되 굴은 여전히 캄캄하다. (0.82 는 18% 라 훤했다)
    [Range(0f, 1f)] [Tooltip("굴 안의 어둠 — 1 이면 순수 검정, 0.93 이면 윤곽만 겨우")] public float 실내어둠 = 0.93f;
    [Tooltip("들어가고 나올 때 밝기가 바뀌는 빠르기")] public float 실내전환 = 3.5f;

    public static float 실내목표;                 // 굴가림이 매 프레임 1 로 올린다
    float 실내정도;

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

        // 굴가림이 이번 프레임에 1 로 올려 놓았나 — 읽고 나서 0 으로 되돌린다
        실내정도 = Mathf.MoveTowards(실내정도, 실내목표, 실내전환 * Time.deltaTime);
        실내목표 = 0f;

        var p = hero.transform.position + Vector3.up * (hero.height * 0.8f);
        var d = hero.LookDir;

        // 밤이면 보이는 거리가 줄고 어둠이 짙어진다 — 밤을 「다른 규칙의 시간」으로 만드는 자리.
        // 낮(낮정도 1)에는 어둠도 감쇠도 0 이라 덮개가 아무것도 안 그린다.
        float dist = day != null ? distance * day.시야배 : distance;
        float dark = day != null ? day.어둠() : 0f;
        // 감쇠도 어둠과 같은 곡선을 탄다 — 어둠이 0 인 낮엔 이것도 0 이어야 자국이 안 남는다
        float fall = day != null && day.밤어둠 > 1e-4f ? falloff * (dark / day.밤어둠) : falloff;

        // ★★★**동굴 안은 낮이어도 어둡다** (2026-08-11 사용자 "동굴안에 들어가면 다른곳
        //   시야가 막혀서 안보이지않나..? 좀보이드는?"). 맞는 지적이다 — 좀보이드는 내가
        //   **있는 방만** 보이지 건물 전체가 투시되지 않는다.
        //   그런데 이 시야는 **밤에만** 걸리게 만들어져 있어서, 낮에 굴에 들어가면 여전히
        //   훤히 다 보인다. → 굴 안에서는 밤과 같은 규칙을 쓴다.
        //   ☆실내 정도는 `굴가림` 이 알려 준다 (0 = 바깥 · 1 = 굴 속).
        if (실내정도 > 0.001f)
        {
            dist = Mathf.Lerp(dist, 실내거리, 실내정도);
            dark = Mathf.Max(dark, 실내어둠 * 실내정도);
            fall = Mathf.Max(fall, falloff * 실내정도);
        }

        Shader.SetGlobalVector("_VisionPos", p);
        Shader.SetGlobalVector("_VisionDir", new Vector4(d.x, 0f, d.z, 0f));
        Shader.SetGlobalVector("_VisionAngle", new Vector4(halfAngle, Mathf.Max(0.5f, edgeSoft), 0f, 0f));
        Shader.SetGlobalVector("_VisionDist",
            new Vector4(dist, Mathf.Max(0.5f, distSoft), nearRadius, Mathf.Max(0.5f, nearSoft)));
        Shader.SetGlobalVector("_VisionDark", new Vector4(dark, fall, 0f, 0f));

        생물가리기(p, d, dist, dark);
    }

    // ★★★★**시야 밖의 생물은 「어둡게」가 아니라 「안 보이게」** (2026-08-11 사용자
    //   "내 시야에 보이는게 아니면 밖에 돌아다니는 팻은 보이면안돼지").
    //
    //   시야는 화면을 **덮어서 어둡게 하는** 방식이라, 어둠이 0.93 이면 밖의 것도 7% 로
    //   남는다. 지형은 그 7% 가 있어야 길을 안 잃는데(그래서 1.0 을 안 쓴다),
    //   **생물은 그 7% 로도 눈에 띈다** — 어두운 바탕에 밝은 몸이라 더 그렇다.
    //   → 지형은 그대로 두고 **생물만** 문턱 아래면 안 그린다. 좀보이드가 그렇다:
    //     본 적 있는 건물 구조는 기억으로 남지만 좀비는 눈에 들어와야 보인다.
    //
    //   ☆셰이더와 **같은 식**을 쓴다 (부채꼴·거리 감쇠·코앞) — 안 그러면 화면의 밝기와
    //     보임/안보임이 어긋나서 "밝은데 안 보인다"가 난다.
    //   ☆몸 반지름을 얹는다 — 큰 놈은 가장자리가 걸치기만 해도 보여야 한다.
    //   ☆9-4: 야생은 스무 마리 남짓이라 매 프레임 돌아도 싸다. 렌더러 목록은
    //     **보임이 바뀔 때만** 읽는다 (매 프레임 `GetComponentsInChildren` 는 금지).
    [Header("★시야 밖 생물 가리기")]
    [Tooltip("시야 밖의 생물을 아예 안 그린다")] public bool 생물숨김 = true;
    [Tooltip("이 밝기 아래면 안 그린다 (0 = 전부 보임 · 1 = 부채꼴 안만)")]
    [Range(0f, 1f)] public float 숨김문턱 = 0.35f;
    [Tooltip("어둠이 이보다 옅으면(=대낮 바깥) 아무도 안 숨긴다")]
    [Range(0f, 1f)] public float 숨김최소어둠 = 0.25f;

    void 생물가리기(Vector3 p, Vector3 look, float dist, float dark)
    {
        if (!생물숨김 || dark < 숨김최소어둠)
        {
            if (가린적있음) { foreach (var c in Critter.All) if (c != null) c.보이기(true); 가린적있음 = false; }
            return;
        }
        가린적있음 = true;
        for (int i = 0; i < Critter.All.Count; i++)
        {
            var c = Critter.All[i];
            if (c == null) continue;
            var v = c.transform.position - p; v.y = 0f;
            float dd = v.magnitude;
            float r = c.Radius;

            // ① 거리 — 끝에서 부드럽게 사라진다 (셰이더 62~63행과 같은 식)
            float far = 1f - Mathf.SmoothStep(dist - distSoft, dist, Mathf.Max(0f, dd - r));
            // ② 코앞은 등 뒤라도 안다
            float near = 1f - Mathf.SmoothStep(nearRadius, nearRadius + nearSoft, Mathf.Max(0f, dd - r));
            // ③ 각도 — 몸 반지름만큼 부채꼴을 더 연다
            float 옆각 = dd > 0.01f ? Mathf.Atan2(r, Mathf.Max(0.3f, dd)) * Mathf.Rad2Deg : 90f;
            float ang = dd > 0.01f ? Vector3.Angle(look, v / dd) : 0f;
            float cone = 1f - Mathf.SmoothStep(halfAngle + 옆각, halfAngle + 옆각 + Mathf.Max(0.5f, edgeSoft), ang);

            float lit = Mathf.Max(cone * far, near);
            c.보이기(lit >= 숨김문턱);
        }
    }
    bool 가린적있음;
}
