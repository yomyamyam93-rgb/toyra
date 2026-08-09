using UnityEngine;

/// 캐릭터 둘레에 **먼지가 은은하게 흩날린다.**
///
/// ★"은은하게" 가 사양이다 (2026-08-09 사용자). 눈에 띄면 실패다 —
///   ①입자를 적게(수십) ②아주 작게 ③천천히 ④가장자리에서 흐려지게.
/// ★**공기 중을 떠다니는 것**이라 캐릭터가 움직여도 입자는 제자리에 머문다
///   (`Simulation Space = World`). 딸려다니면 「붙어 있는 이펙트」로 보인다.
/// ★캐릭터를 중심으로 낼 뿐, 캐릭터의 동작을 그리지 않는다 — 이건 **공기**지 타격 이펙트가 아니다.
[ExecuteAlways]
public class 흩날림 : MonoBehaviour
{
    [Tooltip("한 번에 떠 있는 입자 수")] // ★★넓게 뿌려야 「흩날린다」로 읽힌다 (2026-08-09 사용자 "범위가 좁아서 흩날리는
    //   느낌이 하나도 안 나네"). 화면이 제일 좁을 때도 가로 36m, 넓으면 78m 다 —
    //   반지름이 11m 면 캐릭터 둘레 22m 에만 있어서 **화면 한가운데 뭉쳐 보인다.**
    [Range(4, 600)] public int 개수 = 180;
    [Tooltip("퍼지는 반지름 (m) — 화면 가로가 36~78m 다")] public float 반지름 = 26f;
    [Tooltip("얼마나 높은 데서 떨어지나 (m)")] public float 높이 = 5.5f;
    // ★★**떨어지는 것**이지 떠오르는 게 아니다 (2026-08-09 사용자 "날아 오르는느낌도아니고").
    //   꽃잎은 천천히 내려오면서 좌우로 흔들린다 — 그 흔들림이 「꽃잎」의 정체다.
    [Tooltip("가라앉는 속도 (m/s) — 작을수록 하늘하늘")] public float 가라앉기 = 0.35f;
    [Tooltip("좌우로 흔들리는 폭 (m/s)")] public float 흔들림 = 0.5f;
    [Tooltip("빙글빙글 도는 속도 (도/초)")] public float 회전 = 70f;
    // ★★크기를 **화면 픽셀로** 잡는다 (2026-08-09 사용자 "파티클도없음"). 처음에 4.5cm 로
    //   넣었는데, 이 카메라는 78m 를 1920px 로 그리므로 1m 가 24.6px — 4.5cm 는 **1픽셀**이라
    //   있어도 안 보인다. 0.25m 라야 6픽셀쯤 되어 「먼지」로 읽힌다.
    [Tooltip("입자 크기 (m) — 1m 가 화면에서 약 25픽셀이다")] public float 크기 = 0.25f;
    [Tooltip("처음 튀어나가는 속도 (m/s)")] public float 속도 = 0.12f;
    [Tooltip("바람")] public Vector3 바람 = new Vector3(0.55f, 0f, 0.25f);
    // 꽃잎 색 두 가지 사이에서 고른다
    public Color 색 = new Color(1f, 0.86f, 0.90f, 0.75f);
    public Color 색2 = new Color(1f, 0.97f, 0.86f, 0.70f);

    ParticleSystem ps;
    Transform 주인공;

    void OnEnable() { 만들기(); }
    void OnValidate() { if (isActiveAndEnabled) 만들기(); }

    void LateUpdate()
    {
        if (주인공 == null)
        {
            var h = FindFirstObjectByType<Hero>();
            if (h != null) 주인공 = h.transform;
        }
        // ★입자는 월드에 머물고 **뿌리는 자리만** 따라간다 — 그래야 지나온 자리에 남는다
        if (주인공 != null) transform.position = 주인공.position + Vector3.up * (높이 * 0.35f);
    }

    [ContextMenu("다시 만들기")]
    void 만들기()
    {
        ps = GetComponent<ParticleSystem>();
        if (ps == null) ps = gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;   // ★공기 중에 머문다
        main.startLifetime = new ParticleSystem.MinMaxCurve(높이 / Mathf.Max(0.05f, 가라앉기) * 0.9f,
                                                            높이 / Mathf.Max(0.05f, 가라앉기) * 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(속도 * 0.2f, 속도);
        main.startSize = new ParticleSystem.MinMaxCurve(크기 * 0.7f, 크기 * 1.5f);
        main.startColor = new ParticleSystem.MinMaxGradient(색, 색2);
        main.maxParticles = Mathf.Max(8, 개수 * 2);
        // ★아래로 **천천히** 가라앉는다. 중력을 그대로 쓰면 돌멩이처럼 떨어진다
        main.gravityModifier = 0f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);   // 처음부터 제각각 기울어
        main.startRotation3D = false;

        var em = ps.emission;
        em.enabled = true;
        float 수명 = 높이 / Mathf.Max(0.05f, 가라앉기);
        em.rateOverTime = 개수 / Mathf.Max(0.5f, 수명);

        // ★**머리 위에서** 난다 — 발밑에서 나면 솟아오르는 것처럼 보인다
        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(반지름 * 2f, 0.6f, 반지름 * 2f);
        sh.position = new Vector3(0f, 높이, 0f);

        // ★불규칙하게 떠돈다 — 곧게 내려오면 「비」로 보인다. 이게 하늘거림의 절반이다
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 흔들림;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.25f;
        noise.damping = false;
        noise.octaveCount = 2;

        // 가라앉기 + 바람
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(바람.x);
        vel.y = new ParticleSystem.MinMaxCurve(-가라앉기);
        vel.z = new ParticleSystem.MinMaxCurve(바람.z);

        // ★빙글빙글 — 꽃잎은 떨어지면서 돈다. 도는 게 없으면 종잇조각이 된다
        var rot = ps.rotationOverLifetime;
        rot.enabled = 회전 > 0.01f;
        rot.z = new ParticleSystem.MinMaxCurve(-회전 * Mathf.Deg2Rad, 회전 * Mathf.Deg2Rad);

        // ★★들어올 때 흐리게, 나갈 때 흐리게 — **툭 나타났다 툭 사라지면** 눈에 걸린다
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.18f),
                    new GradientAlphaKey(1f, 0.78f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var 렌 = GetComponent<ParticleSystemRenderer>();
        렌.renderMode = ParticleSystemRenderMode.Billboard;
        렌.alignment = ParticleSystemRenderSpace.View;
        렌.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        렌.receiveShadows = false;
        렌.sortingFudge = -20f;
        if (렌.sharedMaterial == null || 렌.sharedMaterial.name != "흩날림")
        {
            // 기본 입자 셰이더를 쓴다 — 그림 파일 없이 부드러운 점을 낸다
            var s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s != null)
            {
                var m = new Material(s) { name = "흩날림" };
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);   // 반투명
                if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", 점그림());
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", 점그림());
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                렌.sharedMaterial = m;
            }
        }

        if (Application.isPlaying) ps.Play();
    }

    static Texture2D 꽃잎캐시;
    /// 꽃잎 한 장 — 위가 뾰족하고 아래가 둥근 타원. 네모난 입자로 보이지 않게 가장자리를 지운다
    static Texture2D 점그림()
    {
        if (꽃잎캐시 != null) return 꽃잎캐시;
        const int n = 48;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "흩날림_꽃잎", wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f;     // -1 ~ 1
                float v = (y + 0.5f) / n * 2f - 1f;
                // ★위로 갈수록 좁아지는 타원 = 꽃잎. 폭을 세로 위치의 함수로 준다
                float 폭 = 0.62f * Mathf.Sqrt(Mathf.Clamp01(1f - v)) * (0.55f + 0.45f * Mathf.Clamp01(1f + v));
                float d = Mathf.Abs(u) / Mathf.Max(0.02f, 폭);
                float a = Mathf.Clamp01(1f - d);
                a *= Mathf.Clamp01(1f - Mathf.Abs(v));   // 위아래 끝도 지운다
                a = Mathf.SmoothStep(0f, 1f, a);
                // 가운데가 살짝 짙어 잎맥처럼 보인다
                byte c = (byte)Mathf.Clamp(Mathf.RoundToInt((0.88f + 0.12f * (1f - Mathf.Abs(u))) * 255f), 0, 255);
                px[y * n + x] = new Color32(255, c, c, (byte)(a * 255f));
            }
        t.SetPixels32(px); t.Apply(true);
        꽃잎캐시 = t;
        return t;
    }
}
