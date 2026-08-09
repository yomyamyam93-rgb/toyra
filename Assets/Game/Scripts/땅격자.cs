using UnityEngine;

/// **땅 높이의 단 하나뿐인 출처.** 격자 메시도, 캐릭터도, 펫도 전부 여기서 높이를 묻는다.
///
/// ★★왜 한 군데인가 (2026-08-09 사용자 "격자가 높낮이가 하나도 없어서 적용이 안 된 것 같아,
///   그리고 캐릭터 팻들이 모두 땅에 박혀있어"). 전엔 격자 메시가 제 노이즈로 높이를 만들고
///   캐릭터는 `y = 0f` 로 박혀 있었다 — 그림과 판정이 따로 놀아 발이 묻혔다.
///   → **그림 = 판정.** 높이를 묻는 곳이 하나면 어긋날 수가 없다.
///
/// ★칸 단위로 평평하다 — 한 칸 안에서는 높이가 안 변한다. 그래야 격자로 보이고,
///   걷다가 발밑이 계속 오르내리는 멀미도 없다.
public static class 땅격자
{
    /// 칸 크기 (m). `GroundPaint.격자칸` 이 정한다 — 색 경계와 같은 격자여야 한다.
    /// ★★기본값을 **실제 값과 맞춰 둔다** (2026-08-09 사용자 "격자가 넓었다가 인게임에서
    ///   갑자기 좁아지는데"). 이 값은 `세계` 가 땅을 지을 때 들어오므로, 에디터에서는
    ///   여기 적힌 값으로 그려진다. 어긋나 있으면 플레이 순간 격자가 한 번 다시 지어지며
    ///   눈에 띄게 바뀐다. (1440m ÷ 2048텍셀 = 0.703m, 그 2배)
    public static float 칸 = 1.406f;
    /// 가장 낮은 칸과 높은 칸의 높이 차 (m)
    public static float 높이폭 = 0.07f;
    /// 제일 큰 기복 하나의 크기 (m)
    public static float 기복크기 = 40f;
    /// ★★**칸마다 제멋대로 흔드는 폭 (m).** 이게 없으면 격자가 눈에 안 보인다.
    ///   실측: 기복(40m)만 쓰면 이웃 칸(2.1m)끼리 높이차가 **평균 0.6cm** 라 벽이 6mm다.
    ///   큰 기복은 「어디가 높은 지대인가」를, 이 흔들림은 「칸 하나하나」를 보이게 한다.
    ///   ★★0.34 → 0.13 으로 내렸다 (2026-08-09 사용자 "높이 차이가 너무 심해").
    ///   격자선은 이제 **틈과 베벨이** 만든다. 높이는 「살짝 울퉁불퉁」만 맡으면 된다 —
    ///   전엔 높이가 격자선까지 겸하느라 과하게 컸다.
    public static float 칸흔들 = 0.045f;
    public static float 씨 = 7f;

    /// 칸 번호로 만드는 흰 잡음 — 이웃 칸끼리 상관이 없어야 벽이 생긴다
    static float 칸잡음(float cx, float cz)
    {
        float s = Mathf.Sin(cx * 127.1f + cz * 311.7f + 씨 * 13.7f) * 43758.5453f;
        return s - Mathf.Floor(s) - 0.5f;          // -0.5 ~ +0.5
    }

    static float 노이즈(float x, float z, float 크기, float 씨앗)
        => Mathf.PerlinNoise((x + 씨앗) / 크기, (z + 씨앗 * 0.7f) / 크기) - 0.5f;

    /// ★★규칙적으로 물결치면 안 된다 (2026-08-09 사용자 "규칙적인 높낮이가 계속 반복되잖아").
    ///   「길 규칙」과 똑같은 세 겹 보장:
    ///     ① **비배음 3겹** (1 : 2.3 : 5.7) — 배수면 무늬가 되풀이돼 보인다
    ///     ② **진폭 변조** — 진폭 자체를 흔들어 어떤 구역은 평평하고 어떤 구역은 굽게.
    ///        이게 「불규칙」의 핵심이다
    ///     ③ **치우침** — 제곱으로 몰아 큰 굴곡은 드물고 평지가 넓게
    public static float 높이(float wx, float wz)
    {
        if (높이폭 <= 0.0001f && 칸흔들 <= 0.0001f) return 0f;

        // ★칸 가운데에서 잰다 — 한 칸은 통째로 평평하다
        float 흔들 = 0f;
        if (칸 > 0.01f)
        {
            float cx = Mathf.Floor(wx / 칸), cz = Mathf.Floor(wz / 칸);
            흔들 = 칸잡음(cx, cz) * 칸흔들;
            wx = (cx + 0.5f) * 칸;
            wz = (cz + 0.5f) * 칸;
        }

        float S = 씨 * 137.31f;
        float L = Mathf.Max(4f, 기복크기);

        float a = 노이즈(wx, wz, L, S);
        float b = 노이즈(wx, wz, L / 2.3f, S + 613f);
        float c = 노이즈(wx, wz, L / 5.7f, S + 1451f);

        float ma = 0.25f + 1.5f * Mathf.PerlinNoise((wx + S + 77f) / (L * 2.9f), (wz + S) / (L * 2.9f));
        float mb = 0.15f + 1.7f * Mathf.PerlinNoise((wx + S + 311f) / (L * 1.3f), (wz + S + 90f) / (L * 1.3f));

        float n = a * ma + b * 0.45f * mb + c * 0.18f;
        n = Mathf.Sign(n) * Mathf.Pow(Mathf.Clamp01(Mathf.Abs(n) * 1.7f), 1.6f);
        return n * 높이폭 * 0.5f + 흔들;
    }

    public static float 높이(Vector3 w) => 높이(w.x, w.z);

    /// ★★**걸어다니는 것들이 쓰는 높이 — 칸 사이를 이어서 준다.**
    ///
    ///   `높이()` 는 칸마다 평평해서 한 칸을 넘을 때 몇 cm 씩 **뚝** 튄다. 그런데 카메라가
    ///   주인공의 y 를 그대로 따라가므로(`IsoCam.look`), 걸을 때마다 화면 전체가 떨렸다
    ///   (2026-08-09 사용자 "격자가 부들부들 위치가 바뀌어").
    ///
    ///   → **땅은 각지게 두고 발만 부드럽게** 딛는다. 이웃 네 칸 가운데 값을 섞는다.
    ///     칸끼리 높이차가 평균 1.5cm 라 그림과의 어긋남은 눈에 안 보인다.
    public static float 걷는높이(float wx, float wz)
    {
        if (칸 <= 0.01f) return 높이(wx, wz);

        // 칸 「가운데」를 격자점으로 삼는다 — 그래야 칸 한복판에서 값이 정확히 맞는다
        float fx = wx / 칸 - 0.5f, fz = wz / 칸 - 0.5f;
        float ix = Mathf.Floor(fx), iz = Mathf.Floor(fz);
        float tx = fx - ix, tz = fz - iz;
        tx = tx * tx * (3f - 2f * tx);           // 부드럽게 — 칸 경계에서 기울기가 안 꺾인다
        tz = tz * tz * (3f - 2f * tz);

        float h00 = 높이((ix + 0.5f) * 칸, (iz + 0.5f) * 칸);
        float h10 = 높이((ix + 1.5f) * 칸, (iz + 0.5f) * 칸);
        float h01 = 높이((ix + 0.5f) * 칸, (iz + 1.5f) * 칸);
        float h11 = 높이((ix + 1.5f) * 칸, (iz + 1.5f) * 칸);
        return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);
    }

    public static float 걷는높이(Vector3 w) => 걷는높이(w.x, w.z);
}
