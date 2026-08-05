using UnityEngine;

/// 발끝 자취를 **각도로 푸는** 2본 IK.
///
/// ★★왜 필요한가 (2026-08-06 실측 — 사용자 "발이 앞쪽을 기준으로 왔다갔다하는거같은데
///   그래서 떠있는 느낌이었나봐, 테러버드 뿐만아니라 스테고도 그렇고 다그런거같아"):
///   재 보니 **모든 종이 그랬다.** 걷기 한 바퀴 동안 발끝 높이가
///     테러버드 0.149 0.126 0.160 … 0.064 0.234 0.296 …
///   처럼 **같은 값이 이어지는 구간이 하나도 없다.** 각도만 찍으니 발이 엉덩이를 축으로
///   원호를 그리는 게 전부였고, 그래서 늘 떠 있거나 땅을 긁었다.
///
/// ★★고침은 순서를 뒤집는 것이다: **각도를 적지 말고 「발끝이 어디를 지나는가」를 적는다.**
///   접지 구간엔 발끝을 땅에 못박고 뒤로 밀고, 흔드는 구간만 호를 그린다.
///   각도는 그 몸을 **재서** 푼다 — 다리뿌리 높이·허벅·정강 길이가 전부 실측이라
///   종마다 저절로 맞고, 새 몸이 들어와도 그냥 맞는다.
public static class 발IK
{
    /// 한 다리의 실측 치수 — **굽는 시점에 그 모델에서 잰다**
    public struct 다리자
    {
        public float 뿌리높이;    // 허벅 관절이 바닥에서 얼마나 높나 (m)
        public float 허벅;        // 허벅 길이
        public float 정강;        // 정강 길이 (관절 → 발끝)
        public Vector3 뿌리;      // 허벅 관절의 쉬는 자리 (월드)
        public Vector3 발끝;      // 발끝의 쉬는 자리 (월드)
        public float 길이 => 허벅 + 정강;
        public bool 잼 => 허벅 > 1e-4f && 정강 > 1e-4f;
    }

    /// 그 몸에서 다리 하나를 잰다.
    /// ★발끝: 정강 밑에 뼈가 더 있으면 **제일 아래 자손**, 없으면 **정강 바로 밑의 땅**을 쓴다.
    ///   쉬는 자세에서 짐승은 서 있으므로, 정강의 끝은 곧 지면이다 — 짐작이 아니라 사실이다.
    public static 다리자 재기(Transform 허벅, Transform 정강, float 바닥y)
    {
        var 자 = new 다리자();
        if (허벅 == null || 정강 == null) return 자;

        Transform 아래 = null;
        float 제일낮음 = float.MaxValue;
        foreach (var t in 정강.GetComponentsInChildren<Transform>(true))
        {
            if (t == 정강) continue;
            if (t.position.y < 제일낮음) { 제일낮음 = t.position.y; 아래 = t; }
        }

        자.뿌리 = 허벅.position;
        자.발끝 = 아래 != null ? 아래.position
                              : new Vector3(정강.position.x, 바닥y, 정강.position.z);
        자.허벅 = Vector3.Distance(허벅.position, 정강.position);
        자.정강 = Vector3.Distance(정강.position, 자.발끝);
        자.뿌리높이 = 허벅.position.y - 바닥y;
        return 자;
    }

    /// 발끝의 목표 자리 (다리뿌리 기준 · 앞뒤 z, 위아래 y).
    ///
    /// ★접지 구간은 y 가 **바닥**이다. 그동안 발은 뒤로 밀린다 —
    ///   몸이 앞으로 가는 게 아니라 **발이 뒤로 밀리는 것**을 그려야 안 미끄러진다.
    public static Vector2 목표(float u, float 보폭, float 들기, float 접지, float 중심, float 뿌리높이)
    {
        u = Mathf.Repeat(u, 1f);
        float 반 = 보폭 * 0.5f, z, y;
        if (u < 접지)
        {
            float s = 접지 > 1e-4f ? u / 접지 : 0f;
            z = Mathf.Lerp(반, -반, s);          // 앞 → 뒤로 등속 (발은 땅에 붙어 있다)
            y = 0f;
        }
        else
        {
            float s = 접지 < 0.9999f ? (u - 접지) / (1f - 접지) : 0f;
            z = Mathf.Lerp(-반, 반, s * s * (3f - 2f * s));
            // ★등속으로 오르내리면 기계로 보인다 — 들 때 빠르고 놓을 때 뜸을 들인다
            y = 들기 * Mathf.Sin(Mathf.Pow(s, 0.8f) * Mathf.PI);
        }
        return new Vector2(z + 중심, y - 뿌리높이);
    }

    /// 코사인 법칙 2본 IK — 목표까지의 「뿌리→발끝」 방향각과 무릎 굽힘각을 낸다 (도).
    /// ★다리를 **완전히 펴지 않는다**(`최대뻗음`). 일직선이면 무릎이 어느 쪽으로 접힐지
    ///   갈려서 한 프레임 만에 튄다 — IK 가 망가지는 제일 흔한 자리다.
    /// ★★`무릎쪽` — 무릎이 **앞으로 접히나 뒤로 접히나** (+1 / −1).
    ///   이걸 안 넘기면 허벅 각도가 반대편 해로 풀려 발이 엉뚱한 데 놓인다.
    ///   값은 `무릎방향재기` 가 그 몸을 재서 이미 갖고 있다 — 그대로 넘긴다.
    public static void 풀기(Vector2 목표, 다리자 자, float 최대뻗음, float 무릎쪽,
                            out float 허벅각, out float 무릎각)
    {
        허벅각 = 무릎각 = 0f;
        if (!자.잼) return;

        float L1 = 자.허벅, L2 = 자.정강;
        float d = Mathf.Clamp(목표.magnitude,
                              Mathf.Abs(L1 - L2) * 1.05f + 1e-4f,
                              (L1 + L2) * Mathf.Clamp01(최대뻗음));

        float 방향 = Mathf.Atan2(목표.x, -목표.y) * Mathf.Rad2Deg;   // 아래가 0°, 앞이 +
        float A = Mathf.Acos(Mathf.Clamp((L1*L1 + d*d - L2*L2) / (2f*L1*d), -1f, 1f)) * Mathf.Rad2Deg;
        float B = Mathf.Acos(Mathf.Clamp((L1*L1 + L2*L2 - d*d) / (2f*L1*L2), -1f, 1f)) * Mathf.Rad2Deg;

        float s = 무릎쪽 >= 0f ? 1f : -1f;
        허벅각 = 방향 - A * s;    // 「뿌리→발끝」 방향에서 A 만큼 벌어진 것이 허벅
        무릎각 = 180f - B;        // 쭉 폈을 때 0, 접을수록 커진다
    }

    /// 쉬는 자세의 각도 — 델타를 내려면 기준이 있어야 한다.
    /// 굽는 쪽은 **쉬는 자세에서 얼마나 더 돌리나**를 먹으므로, 절대각이 아니라 차이를 준다.
    public static void 쉬는각(다리자 자, float 무릎쪽, out float 허벅각, out float 무릎각)
    {
        허벅각 = 무릎각 = 0f;
        if (!자.잼) return;
        var 쉼 = new Vector2(자.발끝.z - 자.뿌리.z, 자.발끝.y - 자.뿌리.y);
        풀기(쉼, 자, 1f, 무릎쪽, out 허벅각, out 무릎각);
    }
}
