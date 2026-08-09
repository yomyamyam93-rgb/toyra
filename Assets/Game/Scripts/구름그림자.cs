using UnityEngine;

/// 땅 위로 **구름 그림자가 흘러간다.**
///
/// ★★그리지 않는다 — **땅 셰이더가 직접 어둡게 한다.** 그림자 판때기를 따로 깔면
///   드로콜이 늘고 물체 위에 얹히는 문제가 생긴다. 여기는 전역 값 두 개만 넘긴다:
///   `_CloudTex`(무늬) · `_CloudParams`(1/크기, 흐른x, 흐른z, 짙기). **드로콜이 0** 이다.
///
/// ★★처음엔 **라이트 쿠키**로 만들었는데 화면에 아무것도 안 나왔다 (2026-08-09 사용자
///   "구름그림자도없고"). 쿠키는 URP 쪽 설정·아틀라스·포맷에 좌우돼서 「됐는지」를
///   눈으로만 알 수 있다. → 내가 가진 셰이더에서 직접 곱하는 쪽이 확실하다.
///
/// ★무늬는 코드가 만든다 (이음매 없는 노이즈 두 겹). 그림 파일을 안 들고 다닌다.
[ExecuteAlways]
public class 구름그림자 : MonoBehaviour
{
    [Tooltip("구름 한 덩이가 덮는 크기 (m) — 클수록 뭉근하게 지나간다")]
    public float 구름크기 = 260f;
    [Tooltip("흘러가는 속도 (m/s)")] public float 속도 = 3.5f;
    [Tooltip("흘러가는 쪽")] public Vector2 방향 = new Vector2(1f, 0.35f);
    [Tooltip("그늘이 얼마나 짙나 (0 = 없음)")] [Range(0f, 1f)] public float 짙기 = 0.42f;
    [Tooltip("구름이 하늘을 덮은 비율 — 클수록 그늘진 데가 넓다")] [Range(0f, 1f)]
    public float 덮은비율 = 0.42f;
    [Tooltip("무늬 해상도")] public int 해상도 = 256;
    public int 씨 = 3;

    Texture2D 무늬;
    Vector2 흐른거리;
    float 만든짙기 = -1f, 만든덮음 = -1f;

    static readonly int ID무늬 = Shader.PropertyToID("_CloudTex");
    static readonly int ID값 = Shader.PropertyToID("_CloudParams");

    void OnEnable() { 챙기기(); 넘기기(); }
    void OnValidate() { if (isActiveAndEnabled) { 만든짙기 = -1f; 챙기기(); 넘기기(); } }
    void OnDisable() { Shader.SetGlobalVector(ID값, Vector4.zero); }   // 끄면 흔적도 없다

    void 챙기기()
    {
        if (무늬 == null || 만든짙기 != 짙기 || 만든덮음 != 덮은비율) 무늬만들기();
    }

    void 넘기기()
    {
        if (무늬 == null) return;
        Shader.SetGlobalTexture(ID무늬, 무늬);
        float k = 1f / Mathf.Max(1f, 구름크기);
        Shader.SetGlobalVector(ID값,
            new Vector4(k, 흐른거리.x * k, 흐른거리.y * k, Mathf.Clamp01(짙기)));
    }

    void 무늬만들기()
    {
        int n = Mathf.Clamp(해상도, 32, 1024);
        if (무늬 == null || 무늬.width != n)
        {
            if (무늬 != null) DestroyImmediate(무늬);
            무늬 = new Texture2D(n, n, TextureFormat.RGBA32, true)
                { name = "구름무늬", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        }

        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // ★이어 붙는 노이즈 — 무늬가 되풀이돼도 이음매가 안 보이게 네 귀퉁이를 섞는다
                float u = x / (float)n, v = y / (float)n;
                float c = 이음매없는(u, v, 3f) * 0.62f + 이음매없는(u, v, 7f) * 0.38f;
                // 덮은 비율만큼만 그늘로 — 문턱 아래는 맑은 하늘(1)
                float t = Mathf.InverseLerp(1f - 덮은비율, 1f - 덮은비율 * 0.25f, c);
                float 밝기 = Mathf.Lerp(1f, 1f - 짙기, Mathf.SmoothStep(0f, 1f, t));
                byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(밝기 * 255f), 0, 255);
                px[y * n + x] = new Color32(b, b, b, b);
            }
        무늬.SetPixels32(px);
        무늬.Apply(true);
        만든짙기 = 짙기; 만든덮음 = 덮은비율;
    }

    /// 주기 f 로 되풀이돼도 이음매가 없는 노이즈 (원환 좌표로 네 번 섞는다)
    float 이음매없는(float u, float v, float f)
    {
        float s = 씨 * 91.7f;
        float a = Mathf.PerlinNoise(u * f + s, v * f + s);
        float b = Mathf.PerlinNoise((u - 1f) * f + s, v * f + s);
        float c = Mathf.PerlinNoise(u * f + s, (v - 1f) * f + s);
        float d = Mathf.PerlinNoise((u - 1f) * f + s, (v - 1f) * f + s);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    void LateUpdate()
    {
        if (무늬 == null) { 챙기기(); return; }
        var dir = 방향.sqrMagnitude < 1e-4f ? Vector2.right : 방향.normalized;
        흐른거리 += dir * (속도 * Time.deltaTime);
        넘기기();
    }
}
