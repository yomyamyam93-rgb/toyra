using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// 낮과 밤 — 하루가 돌아간다.
///
/// ★밤은 「어두운 낮」이 아니라 **다른 규칙의 시간**이다: 시야가 확 줄고, 어둠이 짙어진다.
///   그래서 낮엔 나가고 밤엔 돌아온다 — 목표를 안 주는 게임에 리듬을 만드는 장치.
///
/// ★밤은 짧게 잡았다. 밤에 할 일(먹이기·부화·정리)이 아직 없어서, 길면 그냥
///   기다리는 시간이 된다. 캠프 일거리가 생기면 그때 늘린다.
public class DayNight : MonoBehaviour
{
    [Header("하루 길이")]
    [Tooltip("하루가 몇 초인가 (1200 = 20분)")] public float 하루 = 1200f;
    [Tooltip("그중 밤의 비율 (0.3 = 30%)")] [Range(0.1f, 0.6f)] public float 밤비율 = 0.3f;
    [Tooltip("시작 시각 (0=자정 · 0.25=아침 · 0.5=정오)")] [Range(0f, 1f)] public float 시작 = 0.28f;

    [Header("해")]
    public float 낮밝기 = 0.85f, 밤밝기 = 0.04f;
    public Color 낮색 = new Color(1f, 0.97f, 0.88f);
    public Color 저녁색 = new Color(1f, 0.62f, 0.38f);
    public Color 밤색 = new Color(0.55f, 0.65f, 0.95f);

    [Header("주변광")]
    public Color 낮주변 = new Color(0.30f, 0.33f, 0.38f);
    public Color 밤주변 = new Color(0.03f, 0.04f, 0.07f);

    [Header("밤의 시야")]
    [Tooltip("밤엔 보이는 거리가 이 배로 줄어든다")] [Range(0.1f, 1f)] public float 밤시야배 = 0.25f;
    [Tooltip("밤엔 부채꼴 밖이 이만큼 어둡다")] [Range(0f, 1f)] public float 밤어둠 = 0.97f;

    /// 0~1 하루 중 어디쯤 (0 = 자정, 0.5 = 정오)
    public float 시각 { get; private set; }
    /// 1 = 대낮 · 0 = 한밤 (해의 높이에서 나온다)
    public float 낮정도 { get; private set; }

    Light sun;
    float baseAmbientSet;

    void Start()
    {
        시각 = Mathf.Repeat(시작, 1f);
        sun = FindFirstObjectByType<Light>();
        if (sun != null && sun.type != LightType.Directional) sun = null;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { sun = l; break; }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        Apply();
    }

    [Header("시험용 키 — 눈으로 보려고 둔 것. 나중에 끈다")]
    [Tooltip("N = 낮↔밤 즉시 전환 · M = 누르고 있으면 시간이 60배로 흐른다")]
    public bool 시험키 = true;

    void Update()
    {
        float mul = 1f;

#if ENABLE_INPUT_SYSTEM
        if (시험키)
        {
            var k = Keyboard.current;
            if (k != null)
            {
                // N — 낮이면 한밤으로, 밤이면 정오로. 눈으로 바로 비교할 수 있게
                if (k.nKey.wasPressedThisFrame) 시각 = 낮정도 > 0.5f ? 0f : 0.5f;
                // M — 누르고 있는 동안 시간이 빨리 흐른다 (해가 도는 걸 본다)
                if (k.mKey.isPressed) mul = 60f;
            }
        }
#endif

        시각 = Mathf.Repeat(시각 + Time.deltaTime * mul / Mathf.Max(1f, 하루), 1f);
        Apply();
    }

    void Apply()
    {
        // 해의 높이 — 자정에 -90°, 정오에 +90°
        float elev = Mathf.Sin((시각 - 0.25f) * Mathf.PI * 2f);

        // 밤비율만큼을 「해가 진 시간」으로 본다
        float night = Mathf.Lerp(-1f, 0f, 밤비율);          // 이 높이 아래면 밤
        낮정도 = Mathf.Clamp01(Mathf.InverseLerp(night - 0.12f, night + 0.30f, elev));

        if (sun != null)
        {
            // 해가 하늘을 가로지른다 — 그림자 방향이 하루 동안 돈다
            sun.transform.rotation = Quaternion.Euler((시각 - 0.25f) * 360f, 35f, 0f);
            sun.intensity = Mathf.Lerp(밤밝기, 낮밝기, 낮정도);
            // 지평선 근처면 붉게 (해가 낮게 뜰 때)
            float low = 1f - Mathf.Clamp01(Mathf.Abs(elev) * 3f);
            var c = Color.Lerp(밤색, 낮색, 낮정도);
            sun.color = Color.Lerp(c, 저녁색, low * 0.8f * 낮정도);
        }
        RenderSettings.ambientLight = Color.Lerp(밤주변, 낮주변, 낮정도);
    }

    /// 시야 거리에 곱할 값 (밤엔 줄어든다)
    public float 시야배 => Mathf.Lerp(밤시야배, 1f, 낮정도);
    /// 부채꼴 밖 어둠 (밤엔 짙어진다)
    public float 어둠(float 낮어둠) => Mathf.Lerp(밤어둠, 낮어둠, 낮정도);

    /// 화면에 띄울 시각 — 24시간으로 환산
    public string 시계
    {
        get
        {
            float h = 시각 * 24f;
            return $"{Mathf.FloorToInt(h):00}:{Mathf.FloorToInt((h % 1f) * 60f):00}";
        }
    }
}
