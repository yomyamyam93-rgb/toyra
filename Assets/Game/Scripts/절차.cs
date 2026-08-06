using UnityEngine;

/// 절차 생성의 **공용 도구** — 앞으로 무엇을 넣든 여기 것을 쓴다.
///
/// ★★마인크래프트가 단계마다 쓰는 장치를 조사해서 세 갈래로 추린 것이다
///   (2026-08-06 사용자 — "모든 과정과 단계를 마인크래프트에서는 어떻게 다양화하고 있는지
///   파악해서 진행", "전체적으로 앞으로도 적용될 수 있게").
///
///   ①**장(場)** — 연속으로 커져야 하는 것(물·굴·숲·지형)은 **도장으로 찍지 않는다.**
///                 노이즈가 문턱을 넘는 자리가 곧 그것이다. 크기가 저절로 전부 다르고,
///                 칸 경계를 넘어 이어지며, 두 번 같은 모양이 안 나온다.
///                 · MC: `noise` 단계에서 지형 높이가 해수면 아래면 곧 물이다. 호수를 안 찍는다.
///
///   ②**뽑기** — 지어진 것(폐허·굴·무리)의 **크기와 개수**는 손잡이에서 뽑는다.
///               MC 의 int provider(균등·사다리꼴)와 template pool 의 weight 자리다.
///               ★핵심은 **작은 것이 흔하고 큰 것이 드물어야** 한다는 것. 균등하게 뽑으면
///                 큰 게 너무 자주 나와서 「큰 것」이 특별하지 않게 된다.
///
///   ③**배치** — 칸마다 하나씩 꼬박꼬박 박으면 **격자가 눈에 보인다.**
///               MC 는 `spacing`(평균 간격)·`separation`(최소 간격)·`salt` 로 격자를 만들고
///               칸마다 흔들어 놓는다. 여기 `흩기` 가 그것이다.
///
/// ★모든 것이 **씨앗에서만** 나온다. 같은 씨앗이면 같은 세계다 (`Random` 을 안 건드린다).
public static class 절차
{
    // ───────────────────────────────── ① 장 (노이즈)

    /// 겹쳐 쌓은 노이즈 — 큰 흐름 위에 잔 흐름이 얹혀 자연스러운 얼룩이 된다.
    /// `칸` = 제일 큰 무늬 하나의 크기 (m) · `겹` = 몇 겹을 쌓나
    public static float 결(float x, float z, float 칸, int 겹 = 4, float 씨 = 0f)
    {
        float 합 = 0f, 폭 = 1f, 총폭 = 0f, 주기 = Mathf.Max(1f, 칸);
        for (int i = 0; i < Mathf.Max(1, 겹); i++)
        {
            합 += 폭 * Mathf.PerlinNoise(x / 주기 + 씨 + i * 37.7f, z / 주기 + 씨 + i * 91.3f);
            총폭 += 폭;
            폭 *= 0.5f; 주기 *= 0.5f;
        }
        return 합 / 총폭;                     // 0~1
    }

    /// 문턱을 넘은 만큼을 0~1 로 — 넘으면 0보다 크고, 깊을수록 1에 가깝다.
    /// ★가장자리가 **부드럽게** 나오므로 물가·숲가장자리가 계단으로 안 보인다.
    public static float 넘은만큼(float 값, float 문턱, float 폭 = 0.12f)
        => Mathf.Clamp01((값 - 문턱) / Mathf.Max(0.0001f, 폭));

    // ───────────────────────────────── ② 뽑기

    /// 0~1 난수 — 자리와 갈래에서 **바로** 뽑는다 (난수기를 안 건드린다).
    /// 같은 자리·같은 갈래면 언제나 같은 값이다.
    public static float 값(int a, int b, int 갈래)
    {
        unchecked
        {
            uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(갈래 * 83492791);
            h ^= h >> 13; h *= 0x85ebca6b; h ^= h >> 16; h *= 0xc2b2ae35; h ^= h >> 15;
            return (h & 0xFFFFFF) * (1f / 0x1000000);
        }
    }

    /// ★**작은 것이 흔하고 큰 것이 드물게** (0~1). `쏠림` 이 클수록 작은 쪽으로 몰린다.
    ///   MC 의 `biased_to_bottom` 자리다. 큰 폐허가 특별해지려면 이게 있어야 한다.
    public static float 작은쪽으로(float u, float 쏠림 = 2.2f) => Mathf.Pow(Mathf.Clamp01(u), 쏠림);

    /// ★**가운데가 흔하고 양끝이 드물게** (0~1). MC 의 `trapezoid`(삼각) 자리.
    ///   흔들어 놓을 때 쓰면 격자 자국이 덜 남는다.
    public static float 가운데로(float u1, float u2) => (u1 + u2) * 0.5f;

    /// 정수 범위에서 하나 — `쏠림` 을 주면 작은 쪽이 흔해진다
    public static int 정수(float u, int 최소, int 최대, float 쏠림 = 1f)
        => 최소 + Mathf.FloorToInt(작은쪽으로(u, 쏠림) * (최대 - 최소 + 0.999f));

    /// 실수 범위에서 하나
    public static float 실수(float u, float 최소, float 최대, float 쏠림 = 1f)
        => Mathf.Lerp(최소, 최대, 작은쪽으로(u, 쏠림));

    /// 가중치 목록에서 하나 고르기 — MC 템플릿 풀의 weight 자리.
    /// 가중치가 없으면(null·길이 0) 균등하게 고른다.
    public static int 가중치로(float u, float[] 무게, int 개수)
    {
        if (개수 <= 0) return -1;
        if (무게 == null || 무게.Length < 개수) return Mathf.Min(개수 - 1, Mathf.FloorToInt(u * 개수));
        float 총 = 0f;
        for (int i = 0; i < 개수; i++) 총 += Mathf.Max(0f, 무게[i]);
        if (총 <= 0f) return Mathf.Min(개수 - 1, Mathf.FloorToInt(u * 개수));
        float r = u * 총;
        for (int i = 0; i < 개수; i++) { r -= Mathf.Max(0f, 무게[i]); if (r <= 0f) return i; }
        return 개수 - 1;
    }

    // ───────────────────────────────── ③ 배치

    /// **흩기** — 칸마다 하나씩 박지 않고, 격자 안에서 흔들어 놓는다.
    /// MC 의 `random_spread` 그대로다:
    ///   · `간격` = 평균 간격 (m)  · `최소간격` = 이웃과 이만큼은 떨어진다 (m)
    ///   · `소금` = 종류마다 다른 수 — **안 바꾸면 두 종류가 같은 자리에 겹쳐 난다**
    ///   · `희귀` = 1/n 확률로만 실제로 놓는다 (MC 의 `rarity_filter`)
    /// 놓을 자리를 찾으면 true.
    public static bool 흩기(int 칸x, int 칸z, float 간격, float 최소간격, int 소금, int 씨앗,
                            float 희귀, out Vector3 자리)
    {
        자리 = default;
        int a = 칸x ^ (소금 * 0x2545F49), b = 칸z ^ (씨앗 * 0x9E3779B);

        if (희귀 > 1f && 값(a, b, 1) > 1f / 희귀) return false;

        // ★흔들기는 **가운데로 몰리게**(삼각) — 균등하게 흔들면 이웃끼리 딱 붙는 일이 잦다
        float 여유 = Mathf.Max(0f, 간격 - 최소간격);
        float ux = 가운데로(값(a, b, 2), 값(a, b, 3));
        float uz = 가운데로(값(a, b, 4), 값(a, b, 5));
        자리 = new Vector3((칸x + 0.5f) * 간격 + (ux - 0.5f) * 여유, 0f,
                           (칸z + 0.5f) * 간격 + (uz - 0.5f) * 여유);
        return true;
    }
}
