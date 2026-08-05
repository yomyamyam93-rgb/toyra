using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// 네발 동작을 **직접 찍는다** — 남의 클립을 옮겨 붙이지 않는다.
///
/// ★왜 이 방식인가 (2026-08-05 사용자 "이식하지말고 … 세세한 계획을 한 뒤 적용").
///   이식은 계속 어그러졌다. 원인을 재 보니 **뼈의 로컬 축이 모델마다 다르다**:
/// <code>
///     다이어울프 spine_hip  로컬Y = 세계(-0.02, 0.20, 0.98)   ← 위축이 앞을 본다
///     큰뿔사슴   spine_back 로컬X = 세계(0.07, -0.99, -0.16)  ← 옆축이 아래를 본다
/// </code>
///   즉 "뼈를 X로 20도" 라는 말이 모델마다 딴 뜻이다. 그래서 각도를 **몸 기준 세계축**으로
///   적고, 구울 때 그 모델의 쉬는 자세를 실측해 로컬로 환산한다:
/// <code>
///     새로컬 = 부모쉬는회전⁻¹ · 회전 · 부모쉬는회전 · 쉬는로컬
/// </code>
///   부모가 돌면 자식이 딸려 오므로 **한 번 적으면 어느 몸에서도 같은 동작**이 된다
///   (`HeroHold` 의 `몸 · Euler · 몸⁻¹` 과 같은 수법).
///
/// ★부호 — 전부 실측에서 나왔다 (모델은 전부 머리 +Z · 위 +Y · 꼬리 -Z):
/// <code>
///     옆(X) +  숙임 · 다리는 뒤로 밀기      -  듦 · 다리는 앞으로 뻗기
///     위(Y) +  오른쪽으로 돌림
///     앞(Z) +  오른쪽이 위로 (= 왼쪽으로 쓰러짐)
/// </code>
///
/// ★회전만 담는다 — 뼈 길이가 모델마다 달라 위치를 미터로 적으면 큰 놈이 찢어진다.
///   단 **뿌리뼈(`spine_hip`)의 위치만 예외**다. 회전으로는 "앞으로 튀어나가기" 를 못 만든다.
///   대신 미터가 아니라 **제 몸길이의 배수**로 적고 구울 때 실측 크기로 환산한다.
public static class 네발동작저작
{
    public const string 저장 = "Assets/Game/Resources/rig";
    public const int 초당 = 30;

    public enum 축 { 옆, 위, 앞 }

    /// ★`한번만` 은 **걷기·뛰기 같은 도는 동작**의 자리다 (2026-08-05 사용자 "달리기할때
    ///   팻들이 공중이 떠있음"). `안파묻힘` 은 프레임마다 몸을 들어 올리는데, 달리기는
    ///   다리를 크게 뻗어서 **거의 모든 프레임이 들려** 통째로 떠 버린다.
    ///   `한번만` 은 **한 사이클을 통틀어 딱 한 번** 재서 같은 값만큼 옮긴다 —
    ///   가장 깊은 프레임이 땅에 닿고 나머지는 그 위로 뜬다. 그게 곧 발 딛기와 도약이다.
    public enum 바닥 { 그대로, 안파묻힘, 딱붙임, 한번만 }

    /// 구간을 **어떻게** 건너는가. 등속(선형)은 기계처럼 보여 거의 안 쓴다.
    public enum 결
    {
        선형,       // 등속 — 거의 쓰지 않는다
        들어감,     // 천천히 시작해 폭발  (예비 정점 → 본동작)
        나감,       // 빠르게 갔다 감속    (예비·반동)
        부드럽게,   // 가속-감속 S곡선     (정착·이동)
        되튐,       // 지나쳤다 돌아옴     (여운 — 쫀득함의 정체)
    }

    public struct 키
    {
        public float t, 값; public 결 c;
        public 키(float t, float 값, 결 c = 결.부드럽게) { this.t = t; this.값 = 값; this.c = c; }
    }

    /// 한 뼈의 한 축(또는 뿌리 위치 한 성분)에 대한 키 열
    public class 줄
    {
        public string 뼈; public 축 축; public 키[] 키들;
        public float 위상;          // 0~1 — 다리 넷을 같은 키로 쓰되 시간만 어긋나게
        public int 위치성분 = -1;   // 0=x 1=y 2=z 이면 회전이 아니라 뿌리 위치 (몸길이 배수)
    }

    public class 동작
    {
        public string 이름; public float 길이 = 1f; public bool 반복;
        /// 구운 뒤 **실제로 재서** 뿌리 높이를 고친다 (아래 `땅에붙이기`).
        /// 기본은 `안파묻힘` — 아래로만 막으므로 뛰어오르는 건 그대로 산다.
        /// `딱붙임` 은 뜨는 것도 끌어내린다 (죽음처럼 반드시 바닥에 놓여야 하는 것).
        public 바닥 바닥맞춤 = 바닥.안파묻힘;
        public List<줄> 줄들 = new List<줄>();

        public 동작 회전(string 뼈, 축 축, params 키[] 키들)
        { 줄들.Add(new 줄 { 뼈 = 뼈, 축 = 축, 키들 = 키들 }); return this; }

        public 동작 회전(string 뼈, 축 축, float 위상, params 키[] 키들)
        { 줄들.Add(new 줄 { 뼈 = 뼈, 축 = 축, 키들 = 키들, 위상 = 위상 }); return this; }

        /// 뿌리 위치 — **몸길이 배수** (+z 앞 · +y 위)
        public 동작 뿌리(int 성분, params 키[] 키들)
        { 줄들.Add(new 줄 { 뼈 = "spine_hip", 위치성분 = 성분, 키들 = 키들 }); return this; }

        /// 다리 넷을 같은 키로 깔되 **위상만 어긋나게** 한다.
        /// ★대각 짝(FL↔BR)이 유지되므로 모델마다 좌우가 뒤집혀 있어도(실측 확인) 상관없다.
        ///
        /// ★★★정강이(무릎)는 **「접힘 = 양수」**로만 적는다. 앞으로 접히는지 뒤로 접히는지는
        ///   **모델마다 다르므로** 여기서 정하지 않는다 — `무릎방향재기` 가 그 몸을 재서 정한다
        ///   (2026-08-05 사용자 — "각 동물마다 달라, 사슴과 늑대가 다르잖아").
        public 동작 다리(string 다리, float 위상, 키[] 허벅, 키[] 정강)
        {
            줄들.Add(new 줄 { 뼈 = 다리 + "_thigh", 축 = 축.옆, 키들 = 허벅, 위상 = 위상 });
            줄들.Add(new 줄 { 뼈 = 다리 + "_shin", 축 = 축.옆, 키들 = 정강, 위상 = 위상 });
            return this;
        }

        // ══════════════════════════════════════ 발자취 (2본 IK)
        //
        // ★★★**각도 대신 「발끝이 어디를 지나는가」를 적는다** (2026-08-06 사용자
        //   "발이 앞쪽을 기준으로 왔다갔다하는거같은데 그래서 떠있는 느낌이었나봐 … 다그런거같아").
        //   실측해 보니 정말 모든 종이 그랬다 — 걷기 한 바퀴 동안 발끝 높이에
        //   **같은 값이 이어지는 구간이 하나도 없었다.** 각도만 찍으니 발이 엉덩이를 축으로
        //   원호를 그리는 게 전부였다.
        //   → 접지 구간엔 발끝을 땅에 못박고, 각도는 `발IK` 가 그 몸을 재서 푼다.
        public 동작 발자취(string 다리, float 위상, 발길 길)
        {
            길.다리 = 다리; 길.위상 = 위상;
            발길들.Add(길);
            return this;
        }

        public List<발길> 발길들 = new List<발길>();
    }

    /// 발끝이 한 사이클 동안 그리는 자취.
    /// ★단위는 **다리 길이 배수**다 — 미터로 적으면 큰 몸과 작은 몸이 같은 보폭을 걸어
    ///   하나는 종종거리고 하나는 다리가 찢어진다.
    public class 발길
    {
        public string 다리; public float 위상;
        /// 한 걸음에 발이 지나는 앞뒤 거리
        public float 보폭 = 0.55f;
        /// 흔들 때 발끝이 뜨는 높이
        public float 들기 = 0.16f;
        /// 한 사이클 중 **땅에 붙어 있는** 비율 (네발 걷기는 0.6~0.75)
        public float 접지 = 0.6f;
        /// 발을 다리뿌리보다 앞/뒤 어디에 놓나 (+ 가 앞)
        public float 중심 = 0f;
        /// 다리를 완전히 펴지 않는다 — 일직선이면 무릎이 튄다
        public float 최대뻗음 = 0.97f;
    }

    // ───────────────────────────────── 이징

    static float 곡선(float s, 결 c)
    {
        s = Mathf.Clamp01(s);
        switch (c)
        {
            case 결.선형: return s;
            case 결.들어감: return s * s;
            case 결.나감: return 1f - (1f - s) * (1f - s);
            case 결.되튐: { float u = s - 1f; return 1f + 2.70158f * u * u * u + 1.70158f * u * u; }
            default: return s * s * (3f - 2f * s);
        }
    }

    /// 키 열을 진행도 u(0~1)에서 읽는다. **곡선은 도착 키가 갖는다** (그 구간을 어떻게 건넜나).
    static float 값(줄 r, float u)
    {
        float x = r.위상 != 0f ? Mathf.Repeat(u + r.위상, 1f) : u;
        var k = r.키들;
        if (x <= k[0].t) return k[0].값;
        for (int i = 0; i < k.Length - 1; i++)
            if (x <= k[i + 1].t)
            {
                float s = k[i + 1].t > k[i].t ? (x - k[i].t) / (k[i + 1].t - k[i].t) : 1f;
                return Mathf.Lerp(k[i].값, k[i + 1].값, 곡선(s, k[i + 1].c));
            }
        return k[k.Length - 1].값;
    }

    // ───────────────────────────────── 굽기

    /// 그 몸의 **쉬는 자세를 실측해서** 클립을 굽는다.
    public static AnimationClip 굽기(동작 m, GameObject 견본)
    {
        var g = Object.Instantiate(견본);
        g.transform.position = Vector3.zero; g.transform.rotation = Quaternion.identity;
        g.transform.localScale = Vector3.one;

        // ★껍데기를 여기서도 씌운다 — 클립 경로가 `Armature/…` 라 없으면 **조용히 안 걸린다.**
        //   실제로 이것 때문에 `땅붙임` 이 껍데기 있는 브론토에서만 먹었다 (나머지는 보정 0).
        {
            var 뿌리 = g.transform.Find("spine_hip");
            if (뿌리 != null)
            {
                var 껍 = new GameObject("Armature").transform;
                껍.SetParent(g.transform, false);
                뿌리.SetParent(껍, true);
            }
        }

        // 뼈 찾기 — `Armature` 껍데기가 있는 모델도 없는 모델도 있어서 **이름으로** 훑는다
        var 뼈 = new Dictionary<string, Transform>();
        var 길 = new Dictionary<string, string>();
        foreach (var t in g.GetComponentsInChildren<Transform>(true))
        {
            if (뼈.ContainsKey(t.name)) continue;
            뼈[t.name] = t;
            string p = ""; var c = t;
            while (c != null && c != g.transform) { p = c.name + (p == "" ? "" : "/" + p); c = c.parent; }
            // 클립 경로는 언제나 `Armature/…` — 껍데기가 없는 모델은 세울 때 씌워 준다
            길[t.name] = p.StartsWith("Armature/") ? p : "Armature/" + p;
        }

        // 몸길이 — 뿌리 위치를 미터로 환산할 자. **실측**이다
        float 몸길이 = 1f;
        {
            bool 첫 = true; var b = new Bounds();
            foreach (var r in g.GetComponentsInChildren<Renderer>(true))
            { if (첫) { b = r.bounds; 첫 = false; } else b.Encapsulate(r.bounds); }
            if (!첫) 몸길이 = Mathf.Max(0.01f, Mathf.Max(b.size.z, b.size.y));
        }

        // 쉬는 자세 — 이게 이 도구의 심장이다
        var 쉬는로컬 = new Dictionary<string, Quaternion>();
        var 부모세계 = new Dictionary<string, Quaternion>();
        var 쉬는위치 = new Dictionary<string, Vector3>();
        foreach (var kv in 뼈)
        {
            쉬는로컬[kv.Key] = kv.Value.localRotation;
            쉬는위치[kv.Key] = kv.Value.localPosition;
            부모세계[kv.Key] = kv.Value.parent != null ? kv.Value.parent.rotation : Quaternion.identity;
        }

        // 줄을 뼈별로 모은다
        var 뼈별회전 = new Dictionary<string, List<줄>>();
        var 뼈별위치 = new Dictionary<string, List<줄>>();
        foreach (var r in m.줄들)
        {
            if (!뼈.ContainsKey(r.뼈)) continue;
            var d = r.위치성분 >= 0 ? 뼈별위치 : 뼈별회전;
            if (!d.TryGetValue(r.뼈, out var l)) d[r.뼈] = l = new List<줄>();
            l.Add(r);
        }

        var clip = new AnimationClip { name = m.이름, frameRate = 초당 };
        int 프레임 = Mathf.Max(2, Mathf.RoundToInt(m.길이 * 초당) + 1);

        // ★무릎이 어느 쪽으로 접히는지 — 그 몸을 **재서** 정한다 (아래 `무릎방향재기`)
        var 접힘 = 무릎방향재기(견본.name, g, 뼈);

        // ── 발자취(IK) — 프레임마다 각도를 **미리 풀어 둔다**
        //   ★쉬는 자세를 기준으로 한 **차이**를 넣는다. 굽는 쪽이 "쉬는 자세에서 얼마나 더
        //     돌리나"를 먹기 때문이다.
        //   ★부호: 월드 X 를 + 로 돌리면 다리가 **뒤로** 간다. IK 의 「방향」은 앞이 + 이므로
        //     허벅에는 **뒤집어서** 넣는다. 무릎은 표와 같은 「접힘 = 양수」라 그대로 둔다
        //     (아래 루프에서 `접힘부호` 가 곱해진다).
        var IK각 = new Dictionary<string, float[]>();
        if (m.발길들.Count > 0)
        {
            float 바닥y = float.MaxValue;
            foreach (var r in g.GetComponentsInChildren<Renderer>(true))
                바닥y = Mathf.Min(바닥y, r.bounds.min.y);
            if (바닥y == float.MaxValue) 바닥y = 0f;

            // ★★★**그 몸의 「앞」이 어디인지 잰다** (2026-08-06 사용자 "앞쪽으로 더 올라간거같은데
            //   발 위치가,, 반대로 하면돼"). 뒤집는 건 맞는데 **종마다 방향이 다르다** —
            //   재 보니 티라노는 뒤로, 테러버드는 앞으로 밀려 있었다. 통째로 뒤집으면
            //   하나는 맞고 하나는 더 틀어진다.
            //   → 머리와 엉덩이의 z 를 견줘 **그 몸의 앞**을 정한다. 블렌더에서 앞뒤가
            //     어느 쪽으로 들어왔든 상관없어진다 (모델 정리 때 못 정한 그 방향이다).
            float 앞부호 = 1f;
            if (뼈.TryGetValue("head", out var 머리뼈) && 뼈.TryGetValue("spine_hip", out var 골반뼈))
                앞부호 = Mathf.Sign(머리뼈.position.z - 골반뼈.position.z);
            if (앞부호 == 0f) 앞부호 = 1f;

            foreach (var 자취 in m.발길들)
            {
                if (!뼈.TryGetValue(자취.다리 + "_thigh", out var 허벅뼈)) continue;
                if (!뼈.TryGetValue(자취.다리 + "_shin", out var 정강뼈)) continue;

                var 자 = 발IK.재기(허벅뼈, 정강뼈, 바닥y);
                if (!자.잼) { Debug.LogWarning("[저작] 다리를 못 쟀다: " + 견본.name + " " + 자취.다리); continue; }
                // ★무릎이 접히는 쪽을 IK 에도 넘긴다 — 안 넘기면 반대편 해로 풀려 발이 딴 데 놓인다
                float 무릎쪽 = 접힘.TryGetValue(자취.다리 + "_shin", out var sg) ? sg : 1f;
                발IK.쉬는각(자, 무릎쪽, out float 쉼허벅, out float 쉼무릎);

                var 허벅값 = new float[프레임]; var 무릎값 = new float[프레임];
                for (int f = 0; f < 프레임; f++)
                {
                    float u = m.길이 > 0f ? Mathf.Repeat(f / (float)초당 / m.길이 + 자취.위상, 1f) : 0f;
                    // ★★★보폭의 **중심은 「발이 원래 있던 자리」**다 (2026-08-06 사용자
                    //   "다리가 뒤쪽으로 갔다 앞쪽으로갔다 이동하는데 이 구간이 앞쪽으로
                    //   몰쳐있어서 이상하다").
                    //   전에는 엉덩이 관절 **바로 밑**을 중심으로 잡았다. 그런데 짐승의 발은
                    //   엉덩이 밑에 있지 않다 — 수각류는 뒤쪽에, 네발은 바깥쪽에 선다.
                    //   그 차이만큼 보폭이 통째로 앞으로 밀려 있었다.
                    //   → 쉬는 자세의 발 자리를 **재서** 그걸 중심으로 삼는다. 짐작이 없다.
                    float 쉼앞뒤 = 자.발끝.z - 자.뿌리.z;
                    // ★보폭은 **그 몸의 앞** 방향으로 뻗는다 (`앞부호`). 중심은 이미 실측한
                    //   월드 값이라 부호를 안 곱한다 — 곱하면 두 번 뒤집는 셈이 된다.
                    var 목 = 발IK.목표(u, 자취.보폭 * 자.길이 * 앞부호, 자취.들기 * 자.길이, 자취.접지,
                                       쉼앞뒤 + 자취.중심 * 자.길이 * 앞부호, 자.뿌리높이);
                    발IK.풀기(목, 자, 자취.최대뻗음, 무릎쪽, out float 허벅각, out float 무릎각);
                    허벅값[f] = -(허벅각 - 쉼허벅);
                    무릎값[f] = 무릎각 - 쉼무릎;
                }
                IK각[자취.다리 + "_thigh"] = 허벅값;
                IK각[자취.다리 + "_shin"] = 무릎값;
                // 표에 줄이 없어도 그 뼈를 돌려야 하므로 자리를 만들어 둔다
                if (!뼈별회전.ContainsKey(자취.다리 + "_thigh")) 뼈별회전[자취.다리 + "_thigh"] = new List<줄>();
                if (!뼈별회전.ContainsKey(자취.다리 + "_shin")) 뼈별회전[자취.다리 + "_shin"] = new List<줄>();
            }
        }

        foreach (var kv in 뼈별회전)
        {
            var 이름 = kv.Key;
            var P = 부모세계[이름]; var Pi = Quaternion.Inverse(P); var R = 쉬는로컬[이름];
            // 정강이면 잰 방향을 곱한다. 표는 「접힘 = 양수」로만 적혀 있다
            float 접힘부호 = 접힘.TryGetValue(이름, out var sgn) ? sgn : 1f;
            var cx = new AnimationCurve(); var cy = new AnimationCurve();
            var cz = new AnimationCurve(); var cw = new AnimationCurve();
            Quaternion 앞것 = R;
            for (int f = 0; f < 프레임; f++)
            {
                float t = f / (float)초당, u = m.길이 > 0f ? Mathf.Clamp01(t / m.길이) : 0f;
                float 옆 = 0f, 위 = 0f, 앞 = 0f;
                foreach (var r in kv.Value)
                {
                    float v = 값(r, u);
                    if (r.축 == 축.옆) 옆 += v * 접힘부호; else if (r.축 == 축.위) 위 += v; else 앞 += v;
                }
                // 발자취(IK)가 푼 각도 — 표와 같은 길로 넣어야 `접힘부호` 가 무릎에 똑같이 먹는다
                if (IK각.TryGetValue(이름, out var ik)) 옆 += ik[f] * 접힘부호;
                var W = Quaternion.Euler(옆, 위, 앞);
                var q = (Pi * W * P) * R;
                if (Quaternion.Dot(q, 앞것) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w); // 뒤집힘 방지
                앞것 = q;
                cx.AddKey(t, q.x); cy.AddKey(t, q.y); cz.AddKey(t, q.z); cw.AddKey(t, q.w);
            }
            string p = 길[이름];
            clip.SetCurve(p, typeof(Transform), "m_LocalRotation.x", cx);
            clip.SetCurve(p, typeof(Transform), "m_LocalRotation.y", cy);
            clip.SetCurve(p, typeof(Transform), "m_LocalRotation.z", cz);
            clip.SetCurve(p, typeof(Transform), "m_LocalRotation.w", cw);
        }

        foreach (var kv in 뼈별위치)
        {
            var 이름 = kv.Key; var 기본 = 쉬는위치[이름];
            var cx = new AnimationCurve(); var cy = new AnimationCurve(); var cz = new AnimationCurve();
            for (int f = 0; f < 프레임; f++)
            {
                float t = f / (float)초당, u = m.길이 > 0f ? Mathf.Clamp01(t / m.길이) : 0f;
                Vector3 d = Vector3.zero;
                foreach (var r in kv.Value) d[r.위치성분] += 값(r, u) * 몸길이;
                cx.AddKey(t, 기본.x + d.x); cy.AddKey(t, 기본.y + d.y); cz.AddKey(t, 기본.z + d.z);
            }
            string p = 길[이름];
            clip.SetCurve(p, typeof(Transform), "m_LocalPosition.x", cx);
            clip.SetCurve(p, typeof(Transform), "m_LocalPosition.y", cy);
            clip.SetCurve(p, typeof(Transform), "m_LocalPosition.z", cz);
        }

        if (m.바닥맞춤 != 바닥.그대로 && 길.ContainsKey("spine_hip"))
            땅에붙이기(clip, g, 길["spine_hip"], 쉬는위치["spine_hip"], m.길이, m.바닥맞춤);

        var s = AnimationUtility.GetAnimationClipSettings(clip);
        s.loopTime = m.반복;
        AnimationUtility.SetAnimationClipSettings(clip, s);

        Object.DestroyImmediate(g);
        return clip;
    }

    /// ★★계산으로는 못 맞춘다 — **구운 걸 재서 고친다** (2026-08-05).
    ///   옆으로 굴러 눕는 높이를 `몸너비÷2 - 골반높이` 로 계산해 넣었는데 넷 다 땅에 파묻혔다
    ///   (늑대 0.25m · 브론토 0.67m). 굴러가는 동안 최저점이 골반이 아니라 **목·꼬리로
    ///   옮겨다니기** 때문이다. 그래서 프레임마다 실제 최저점을 재서 그만큼 올린다.
    ///   보정은 뿌리 높이 하나만 움직이므로 한 번에 정확히 맞는다 (선형 관계).
    static void 땅에붙이기(AnimationClip clip, GameObject g, string 뿌리길, Vector3 쉬는뿌리, float 길이, 바닥 모드)
    {
        bool 딱 = 모드 == 바닥.딱붙임;
        // ★이름을 `바닥` 이라 두면 **열거형 `바닥` 과 부딪힌다** — 이 안에서는 지역 변수가
        //   이겨서 `바닥.한번만` 이 컴파일 에러가 된다. 하는 일 그대로 `밑재기` 로 부른다.
        System.Func<float> 밑재기 = () =>
        {
            float lo = float.MaxValue;
            foreach (var r in g.GetComponentsInChildren<Renderer>(true))
            {
                var sk = r as SkinnedMeshRenderer; if (sk == null) continue;
                var mesh = new Mesh(); sk.BakeMesh(mesh, true);
                var mtx = sk.transform.localToWorldMatrix;
                foreach (var v in mesh.vertices) { float y = mtx.MultiplyPoint3x4(v).y; if (y < lo) lo = y; }
                Object.DestroyImmediate(mesh);
            }
            return lo;
        };

        // ★★★"서 있는 높이" 는 **클립을 재생하기 전** 에 재야 한다 (2026-08-05 사용자
        //   "달릴때 여전히 공중에 떠있음"). 전에는 `SampleAnimation(g, 0)` 뒤에 쟀는데,
        //   죽음·피격은 0프레임이 곧 쉬는 자세라 우연히 맞았을 뿐이다.
        //   **뛰기의 0프레임은 「신전」 — 네 다리를 쭉 뻗고 공중에 뜬 순간**이다.
        //   그 높이를 땅으로 삼았으니 나머지를 전부 거기까지 들어 올렸다.
        //   → 아무것도 재생하지 않은 지금이 곧 **서 있는 자세**다. 여기서 잰다.
        float 쉬는바닥 = 밑재기();
        clip.SampleAnimation(g, 0f);

        var 기존 = AnimationUtility.GetEditorCurve(clip,
            EditorCurveBinding.FloatCurve(뿌리길, typeof(Transform), "m_LocalPosition.y"));
        int 프레임 = Mathf.Max(2, Mathf.RoundToInt(길이 * 초당) + 1);

        // ★★도는 동작은 **한 사이클을 통틀어 한 번만** 옮긴다 — 프레임마다 들어 올리면
        //   달리기처럼 다리를 크게 뻗는 동작이 통째로 공중에 뜬다 (위 `바닥.한번만` 주석).
        if (모드 == 바닥.한번만)
        {
            float 제일깊이 = float.MaxValue;
            for (int f = 0; f < 프레임; f++)
            {
                clip.SampleAnimation(g, f / (float)초당);
                제일깊이 = Mathf.Min(제일깊이, 밑재기());
            }
            float 한번보정 = 쉬는바닥 - 제일깊이;      // 가장 깊은 프레임이 땅에 닿게
            if (기존 == null && Mathf.Abs(한번보정) < 1e-4f) return;
            var 한번 = new AnimationCurve();
            for (int f = 0; f < 프레임; f++)
            {
                float t = f / (float)초당;
                한번.AddKey(t, (기존 != null ? 기존.Evaluate(t) : 쉬는뿌리.y) + 한번보정);
            }
            clip.SetCurve(뿌리길, typeof(Transform), "m_LocalPosition.y", 한번);
            return;
        }

        var 새 = new AnimationCurve();
        for (int f = 0; f < 프레임; f++)
        {
            float t = f / (float)초당;
            clip.SampleAnimation(g, t);
            float 밑 = 밑재기();
            float y = 기존 != null ? 기존.Evaluate(t) : 쉬는뿌리.y;
            float 보정 = 쉬는바닥 - 밑;
            새.AddKey(t, y + (딱 ? 보정 : Mathf.Max(0f, 보정)));   // 딱이 아니면 **아래로만** 막는다
        }
        if (!딱)   // 아무 프레임도 안 파묻혔으면 굳이 커브를 남기지 않는다
        {
            bool 쓸모 = 기존 != null;
            for (int f = 0; f < 프레임 && !쓸모; f++)
                if (Mathf.Abs(새.Evaluate(f / (float)초당) - 쉬는뿌리.y) > 1e-4f) 쓸모 = true;
            if (!쓸모) return;
        }
        clip.SetCurve(뿌리길, typeof(Transform), "m_LocalPosition.y", 새);
    }

    // ───────────────────────────────── 무릎이 어느 쪽으로 접히나 — 재서 정한다

    /// ★★★**무릎의 접히는 방향은 모델마다 다르다** (2026-08-05 사용자 — "다리가 앞쪽
    ///   뒤쪽으로 굽혀지는지는 각 동물마다 달라, 사슴과 늑대가 다르잖아").
    ///   해부학으로 못박았다가 틀렸다. 이 몸들은 진짜 골격이 아니라 장난감 리그라
    ///   **뼈를 어느 쪽으로 심었느냐가 모델마다 다르다.** 정할 게 아니라 **잴 것**이다.
    ///
    /// ★재는 법 — 발끝을 양쪽으로 10° 씩 돌려 보고 **엉덩이에 가까워지는 쪽**이 접히는 쪽이다.
    ///   두 뼈짜리 다리는 **곧게 펴졌을 때 엉덩이~발 거리가 가장 멀다.** 접으면 반드시
    ///   짧아지고, 반대로 꺾으면 그 이상 못 가거나 관절이 뒤집힌다. 이 성질은 어느 짐승이든,
    ///   뼈를 어떻게 심었든 참이다.
    ///
    /// ★발끝은 **메시에서 찾는다** — 다리는 `_thigh` + `_shin` 두 개뿐이라 발 뼈가 없다
    ///   (실측: `다이어울프.glb` 의 뼈 이름 전부 확인). 정강이에 매달린 살점 중
    ///   무릎에서 가장 먼 곳이 곧 발끝이다.
    static readonly Dictionary<string, Dictionary<string, float>> 무릎캐시 = new Dictionary<string, Dictionary<string, float>>();

    static Dictionary<string, float> 무릎방향재기(string 몸이름, GameObject g, Dictionary<string, Transform> 뼈)
    {
        if (무릎캐시.TryGetValue(몸이름, out var 있던)) return 있던;

        var 결과 = new Dictionary<string, float>();
        var smrs = g.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var 적을것 = new List<string>();

        // ★두발(`legL`·`legR`)도 같이 잰다 (2026-08-06) — 안 재면 부호가 기본값 +1 로 남아
        //   무릎이 반대로 접힌다. 없는 이름은 아래에서 알아서 건너뛴다.
        foreach (var 다리 in new[] { "legFL", "legFR", "legBL", "legBR", "legL", "legR" })
        {
            string 정강이름 = 다리 + "_shin";
            if (!뼈.TryGetValue(정강이름, out var 정강) || !뼈.TryGetValue(다리 + "_thigh", out var 허벅)) continue;

            float 부호;
            if (발끝찾기(smrs, 정강, out var 발끝))
            {
                var 무릎 = 정강.position; var 엉덩이 = 허벅.position;
                var 팔 = 발끝 - 무릎;
                float 더하기 = ((무릎 + Quaternion.AngleAxis(10f, Vector3.right) * 팔) - 엉덩이).sqrMagnitude;
                float 빼기 = ((무릎 + Quaternion.AngleAxis(-10f, Vector3.right) * 팔) - 엉덩이).sqrMagnitude;
                부호 = 더하기 < 빼기 ? 1f : -1f;
                적을것.Add(다리 + (부호 > 0 ? " +" : " −"));
            }
            else
            {
                // 살점을 못 찾으면 어쩔 수 없이 해부학 기본값 — 앞다리는 뒤로, 뒷다리는 앞으로
                부호 = 다리.StartsWith("legB") ? -1f : 1f;
                적을것.Add(다리 + (부호 > 0 ? " +" : " −") + "(못 잼)");
            }
            결과[정강이름] = 부호;
        }

        // ★잰 값을 남긴다 — 짐작이 아니라 잰 것임을 눈으로 확인할 수 있어야 한다
        Debug.Log("[저작] " + 몸이름 + " 무릎 접히는 쪽: " + string.Join(" · ", 적을것));
        무릎캐시[몸이름] = 결과;
        return 결과;
    }

    /// 정강이에 매달린 살점 중 무릎에서 **가장 먼 점** = 발끝
    static bool 발끝찾기(SkinnedMeshRenderer[] smrs, Transform 정강, out Vector3 발끝)
    {
        발끝 = Vector3.zero;
        float 제일멀리 = -1f;
        foreach (var smr in smrs)
        {
            if (smr.sharedMesh == null || smr.bones == null) continue;
            int bi = System.Array.IndexOf(smr.bones, 정강);
            if (bi < 0) continue;

            var w = smr.sharedMesh.boneWeights;
            if (w == null || w.Length == 0) continue;

            var 구운 = new Mesh();
            smr.BakeMesh(구운, true);
            var v = 구운.vertices;
            var mtx = smr.transform.localToWorldMatrix;
            for (int i = 0; i < v.Length && i < w.Length; i++)
            {
                if (주뼈(w[i]) != bi) continue;
                var p = mtx.MultiplyPoint3x4(v[i]);
                float d = (p - 정강.position).sqrMagnitude;
                if (d > 제일멀리) { 제일멀리 = d; 발끝 = p; }
            }
            Object.DestroyImmediate(구운);
        }
        return 제일멀리 > 1e-8f;
    }

    /// 이 살점을 제일 세게 잡고 있는 뼈
    static int 주뼈(BoneWeight w)
    {
        int b = w.boneIndex0; float m = w.weight0;
        if (w.weight1 > m) { m = w.weight1; b = w.boneIndex1; }
        if (w.weight2 > m) { m = w.weight2; b = w.boneIndex2; }
        if (w.weight3 > m) { b = w.boneIndex3; }
        return b;
    }

    // ───────────────────────────────── 메뉴

    [MenuItem("Tools/토이라기/㉧ 네발 동작 찍기", priority = 7)]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        { Debug.LogError("[저작] 플레이 중엔 못 굽는다 — 멈추고 다시."); return; }

        무릎캐시.Clear();      // 구울 때마다 다시 잰다 — 잰 값이 콘솔에 남아야 확인할 수 있다

        var 모델 = new List<GameObject>();
        foreach (var g in AssetDatabase.FindAssets("t:GameObject", new[] { 저장 }))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (!p.EndsWith(".glb")) continue;
            var o = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (o != null && o.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) 모델.Add(o);
        }
        if (모델.Count == 0) { Debug.LogError("[저작] " + 저장 + " 에 리깅된 모델이 없다"); return; }

        int 만든수 = 0;
        foreach (var 몸 in 모델)
        {
            // ★★체형은 **뼈 이름으로** 가른다 (2026-08-06) — 모델 이름 목록을 코드에 안 적는다.
            //   `legL_thigh` 가 있으면 두발, 없으면 네발이다. 새 모델이 들어와도 저절로 갈린다.
            var 표 = 두발동작표.두발인가(몸) ? 두발동작표.만들기(몸.name) : 네발동작표.만들기(몸.name);
            foreach (var m in 표)
            {
                var clip = 굽기(m, 몸);
                string ap = 저장 + "/" + m.이름 + "_" + 몸.name + ".anim";
                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(ap) != null) AssetDatabase.DeleteAsset(ap);
                AssetDatabase.CreateAsset(clip, ap);

                string cp = 저장 + "/" + m.이름 + "_" + 몸.name + ".controller";
                if (AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(cp) != null)
                    AssetDatabase.DeleteAsset(cp);
                var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(cp);
                var st = ctrl.layers[0].stateMachine.AddState(m.이름);
                st.motion = clip; st.writeDefaultValues = true;
                ctrl.layers[0].stateMachine.defaultState = st;
                EditorUtility.SetDirty(ctrl);
                만든수++;
            }
            Debug.Log("[저작] " + 몸.name + "(" + (두발동작표.두발인가(몸) ? "두발" : "네발") + ") — 동작 " + 표.Count + "개");
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[저작] 클립 " + 만든수 + "개 완료");
    }
}
