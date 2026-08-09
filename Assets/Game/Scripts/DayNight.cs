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

    // ★낮은 훤해야 한다. 어둠은 밤의 몫이다 (2026-08-04 사용자)
    [Header("해 방향")]
    [Tooltip("해를 머리 위에 박아 둔다 — 그림자가 언제나 발밑에 짧게 진다")]
    public bool 해고정 = true;
    [Tooltip("고정할 때 해의 높이 (°) — 90 이면 정확히 수직. 조금 기울여야 그림자가 보인다")]
    [Range(45f, 90f)] public float 해높이 = 78f;
    [Tooltip("고정할 때 해가 어느 쪽에서 오나 (°)")]
    [Range(0f, 360f)] public float 해방위 = 35f;

    [Header("해")]
    public float 낮밝기 = 1.35f, 밤밝기 = 0.04f;
    // ★빛에 색을 섞지 않는다 (2026-08-04 사용자 — "잡다한 색들이 섞여 있어서").
    //   푸르스름한 햇빛·주변광은 물체의 원래 색에 파란 기를 얹어 색을 흐린다.
    //   낮에는 **무채색 빛**으로 두고, 색은 물체가 갖게 한다.
    public Color 낮색 = Color.white;
    public Color 저녁색 = new Color(1f, 0.72f, 0.52f);
    public Color 밤색 = new Color(0.62f, 0.68f, 0.85f);

    [Header("주변광")]
    public Color 낮주변 = new Color(0.58f, 0.58f, 0.58f);
    public Color 밤주변 = new Color(0.03f, 0.04f, 0.07f);

    // ★★해질녘·새벽을 길게 (2026-08-04 사용자 — "2분에 한 번씩 뚝 변경되는 게 아니라
    //   자연스럽게"). 전엔 해의 높이(sin)에 좁은 구간을 물려서 넘어가는 데 105초뿐이었고,
    //   조명이 4초에 한 칸씩 계단이라 그 안에 **25칸**밖에 안 들어갔다 — 한 칸이 전체 변화의
    //   4% 라 뚝뚝 끊겨 보인다. 이제 시각에서 직접 재고, 구간을 두 배로 늘려 칸을 잘게 쪼갠다.
    //   (계단 자체는 남긴다 — 없애면 픽셀 화면이 지지직 끓는다. 「조명단계」 주석 참고)
    [Tooltip("해질녘·새벽이 하루의 몇 할인가 (0.18 = 하루 20분 기준 약 3분 36초씩)")]
    [Range(0.02f, 0.45f)] public float 노을 = 0.18f;

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
        // ★★단색(Flat) → **삼색(Trilight)** (2026-08-07 사용자 "빛이 너무 조잡하다").
        //   단색 환경광은 모든 면을 같은 색으로 채워서 입체가 플라스틱처럼 죽는다.
        //   하늘(위)·수평·땅(아래)을 갈라 주면 **환경광만으로 형태가 읽힌다** — 이게
        //   스타일라이즈 게임들이 값싸게 「퀄리티 있어 보이는」 표준 수법이다.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
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

    // ★★해를 계단식으로 움직인다 (2026-08-04 사용자 — "가만히 서 있는데 지지지직 변한다").
    //   해가 매 프레임 조금씩 돌면 색이 끊임없이 양자화 경계를 넘나들어, 화면 전체가
    //   끓는 것처럼 보인다. 픽셀 화면에서는 **조명도 픽셀처럼 계단이어야** 한다.
    //   288단계 = 하루 20분 기준 약 4초에 한 번만 바뀐다.
    [Tooltip("해를 하루에 몇 단계로 나눠 움직이나 (클수록 부드럽고, 작을수록 안 흔들린다)")]
    [Range(24, 2048)] public int 조명단계 = 288;

    void Apply()
    {
        // 조명 계산에는 계단진 시각을 쓴다 (시계 표시는 원래 값 그대로)
        float 시각L = Mathf.Round(시각 * 조명단계) / 조명단계;

        // 해의 높이 — 자정에 -90°, 정오에 +90°
        float elev = Mathf.Sin((시각L - 0.25f) * Mathf.PI * 2f);

        // 밤비율만큼을 「해가 진 시간」으로 본다 — 자정(0)에서 얼마나 떨어졌나로 잰다.
        // ★해의 높이(sin)가 아니라 **시각**으로 재는 이유: sin 은 지평선 근처에서 빠르게
        //   지나가서, 같은 폭을 줘도 넘어가는 시간이 짧고 하루 길이를 바꾸면 또 달라진다.
        //   시각으로 재면 「노을이 하루의 몇 할」이 그대로 화면에 나온다.
        float 자정거리 = Mathf.Min(시각L, 1f - 시각L);       // 0 = 자정 · 0.5 = 정오
        float 밤가장자리 = 밤비율 * 0.5f;
        float 반 = Mathf.Max(0.005f, 노을 * 0.5f);
        // SmoothStep — 시작과 끝이 완만해야 "슬슬 어두워진다" 로 읽힌다. 직선이면 양 끝이 각진다
        낮정도 = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(밤가장자리 - 반, 밤가장자리 + 반, 자정거리));

        if (sun != null)
        {
            // ★★해를 **머리 위에 고정**할 수 있다 (2026-08-04 사용자 "수직으로 하늘위로
            //   태양놓고 그림자정도만 나오게").
            //
            //   해가 하루 동안 지평선까지 내려가면 그림자가 길게 누웠다 사라졌다 한다 —
            //   물체가 땅에 붙어 있다는 느낌(접지)이 시간마다 달라진다. 위에 박아 두면
            //   **언제나 발밑에 짧게** 져서, 그림자가 연출이 아니라 「거기 서 있다」는 표시가 된다.
            //   ★밝기·색의 하루 주기는 그대로 돈다 — 고정하는 건 **방향뿐**이다.
            sun.transform.rotation = 해고정
                ? Quaternion.Euler(해높이, 해방위, 0f)
                : Quaternion.Euler((시각L - 0.25f) * 360f, 35f, 0f);
            sun.intensity = Mathf.Lerp(밤밝기, 낮밝기, 낮정도);
            // 지평선 근처면 붉게 (해가 낮게 뜰 때)
            float low = 1f - Mathf.Clamp01(Mathf.Abs(elev) * 3f);
            var c = Color.Lerp(밤색, 낮색, 낮정도);
            sun.color = Color.Lerp(c, 저녁색, low * 0.8f * 낮정도);
        }
        var 주변 = Color.Lerp(밤주변, 낮주변, 낮정도);
        // 위는 살짝 차고 밝게(하늘 반사) · 수평은 그대로 · 아래는 어둡고 살짝 따뜻하게(땅 반사)
        RenderSettings.ambientSkyColor     = 주변 * 1.18f + new Color(0f, 0.012f, 0.03f);
        RenderSettings.ambientEquatorColor = 주변;
        RenderSettings.ambientGroundColor  = 주변 * 0.45f + new Color(0.02f, 0.012f, 0f);
    }

    // ★★시야는 **늦게 들어온다** (2026-08-04 사용자 — "밝은 날은 대부분 빼주고,
    //   어둑어둑해지기 시작할 때부터 은은하게"). 밝기와 나란히 직선으로 걸면 해가 아직
    //   남아 있는데도 절반이 어두워져 시야가 「낮의 것」처럼 느껴진다.
    //   제곱을 걸면 해가 반쯤 진 시점에 어둠이 4분의 1뿐이라, 어둑해질 무렵 은은하게
    //   들어왔다가 밤이 될수록 급히 조여든다 — 밤을 다른 시간으로 만드는 건 그 곡선이다.
    float 밤정도 { get { float n = 1f - 낮정도; return n * n; } }

    /// 시야 거리에 곱할 값 (밤엔 줄어든다)
    public float 시야배 => Mathf.Lerp(1f, 밤시야배, 밤정도);
    /// 부채꼴 밖 어둠 — 낮엔 0 이라 덮개가 아무것도 안 그린다
    public float 어둠() => 밤어둠 * 밤정도;

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
