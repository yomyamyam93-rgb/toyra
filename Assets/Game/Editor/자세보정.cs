using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// 사람 클립의 **평소 자세**를 손본다 — 머리 젖힘과 뻣뻣한 다리.
///
/// ★★왜 필요했나 (2026-08-09 사용자 "모든 모션에서 캐릭터가 머리를 너무 뒤로 젖히고 있어,
///   걷기와 무기들고 걸을때도 다리를 너무 쭉 펴고 있으니까 진짜 이상한거같아").
///   실측:
///     · 정지_여자 — 머리끝이 **뒤로 15.8°** · 얼굴이 **위로 12.8°** (하늘을 본다)
///     · 정지_남자 — 반대로 앞으로 11.6° · 얼굴 아래 4.1°
///     · 무릎은 남녀 공통 **2°** — 완전히 편 막대다 (걷기도 디딤발 7°)
///
/// ★★**원본을 뜨고 거기서만 다시 계산한다.** 클립을 제자리에서 고치면 두 번 돌릴 때
///   보정이 누적되어, 다음 실행이 지난 실행 결과를 입력으로 읽는다 (CLAUDE.md 가 무기
///   정렬에서 경고한 그 함정). 그래서 첫 실행에 `_원본/` 으로 복사해 두고, 이후로는
///   **언제나 원본 → 보정 → 덮어쓰기** 다. 값을 0 으로 두고 다시 돌리면 원상복구된다.
///
/// ★부호는 실측이다:
///     Head·neck 로컬 **+X = 숙임** (1° 당 얼굴 1° — Head*+15 → 얼굴 12.8°→−2.2°)
///     허벅 **−X = 무릎이 앞으로** · 정강 **+X = 접힘** (부호가 갈려야 발이 제자리에 남는다)
///     −10 / +23 이 골반 0.09m 내림과 짝이다 (`HeroAttack.다리굽히기` 와 같은 값)
///     골반 로컬 1단위 = **0.0143m**
public static class 자세보정
{
    const string 폴더 = "Assets/Game/Animations";
    const string 원본폴더 = "Assets/Game/Animations/_원본";

    /// 클립 하나에 무릎을 얼마나 먹일지 (0 = 손 안 댐, 1 = 기준량). 머리와 골반은 **풀어서 정한다**
    struct 몫 { public string 클립; public float 굽힘; }

    // ★걷기·달리기는 이미 다리를 크게 쓰므로 배수를 낮춘다 — 정지에 맞춘 양을 그대로
    //   먹이면 내내 앉아서 걷는 꼴이 된다. 달리기는 무릎이 이미 153° 까지 접힌다.
    static readonly 몫[] 표 = {
        new 몫{ 클립="정지_여자",          굽힘=1f },
        new 몫{ 클립="걷기_여자",          굽힘=0.6f },
        new 몫{ 클립="달리기_여자",        굽힘=0.45f },
        new 몫{ 클립="뒤로걷기_여자",      굽힘=0.6f },
        new 몫{ 클립="옆걸음_왼쪽_여자",   굽힘=0.6f },
        new 몫{ 클립="옆걸음_오른쪽_여자", 굽힘=0.6f },
        new 몫{ 클립="정지_남자",          굽힘=1f },
        new 몫{ 클립="걷기_남자",          굽힘=0.6f },
        new 몫{ 클립="달리기_남자",        굽힘=0.45f },
        new 몫{ 클립="뒤로걷기_남자",      굽힘=0.6f },
        new 몫{ 클립="옆걸음_왼쪽_남자",   굽힘=0.6f },
        new 몫{ 클립="옆걸음_오른쪽_남자", 굽힘=0.6f },
    };

    // ★★상수로 못 박지 않고 **클립마다 재서 푼다** (2026-08-09). 클립마다 원래 자세가
    //   달라서 같은 보정을 먹이면 어떤 건 하늘을 보고 어떤 건 땅을 본다 — 실제로 첫 판에
    //   정지는 수평이 됐는데 걷기는 고개를 17° 숙였다. 발도 같은 이유로 4cm 내려앉았다.
    //   → 얼굴 각과 발 높이를 **원본과 대보고** 그 차이만큼만 되돌린다.
    [Tooltip("얼굴이 향할 상하각 (°) — 0 이면 수평, 음수면 살짝 아래")]
    const float 얼굴목표 = -2f;
    const float 허벅 = -10f, 정강 = 23f, 발목 = -13f;      // 부호가 갈려야 무릎이다
    const float 골반단위m = 0.0143f;

    [MenuItem("Tools/토이라/자세 보정 (머리·무릎)")]
    public static void 적용()
    {
        if (!Directory.Exists(원본폴더)) { Directory.CreateDirectory(원본폴더); AssetDatabase.Refresh(); }

        var 자 = new 자벌레();
        if (!자.준비됐나) { Debug.LogError("[자세보정] 씬에 사람 모델이 없다 — 클립을 재려면 그 몸이 있어야 한다."); return; }

        int n = 0;
        foreach (var m in 표)
        {
            string 길 = $"{폴더}/{m.클립}.anim";
            // ★리그를 바꾼 뒤라면 `바인드보정` 이 만든 것이 진짜 입력이다 (없으면 원본 그대로)
            string 원길 = $"{원본폴더}보정/{m.클립}.anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(원길) == null) 원길 = $"{원본폴더}/{m.클립}.anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(길) == null) { Debug.LogWarning($"[자세보정] 없음: {길}"); continue; }

            // ★첫 실행에만 원본을 뜬다. 이후로는 언제나 이 원본이 입력이다
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(원길) == null)
                AssetDatabase.CopyAsset(길, 원길);

            var 원본 = AssetDatabase.LoadAssetAtPath<AnimationClip>(원길);
            var 대상 = AssetDatabase.LoadAssetAtPath<AnimationClip>(길);
            Debug.Log($"[자세보정] {m.클립} — {보정(원본, 대상, m.굽힘, 자)}");
            n++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[자세보정] 클립 {n}개 — 언제나 원본에서 다시 계산한다. 표의 굽힘을 0 으로 두면 원상복구된다.");
    }

    static string 보정(AnimationClip 원본, AnimationClip 대상, float 굽힘몫, 자벌레 자)
    {
        // 원본이 어떤 자세였는지부터 잰다 — 되돌릴 기준이다
        float 원래발 = 자.최저발(원본);

        // ── 다리: 부호를 갈라 굽히고, 골반은 **재서** 내린다
        새로쓰기(원본, 대상);
        if (굽힘몫 > 0.001f)
            foreach (var side in new[] { "Left", "Right" })
            {
                회전더하기(대상, side + "UpLeg", Quaternion.Euler(허벅 * 굽힘몫, 0f, 0f));
                회전더하기(대상, side + "Leg", Quaternion.Euler(정강 * 굽힘몫, 0f, 0f));
                회전더하기(대상, side + "Foot", Quaternion.Euler(발목 * 굽힘몫, 0f, 0f));
            }
        // 무릎을 굽히면 다리가 짧아진다 — 짧아진 만큼만 골반을 내려야 발이 제자리다.
        // 짐작한 0.09m 로는 발이 4cm 내려앉았다 (실측). 그래서 **재서 그 차이만큼** 내린다.
        //   ★부호: 발이 원본보다 **올라갔으면 골반을 내려야** 도로 땅에 닿는다 (골반과 발은
        //     한 몸이라 1:1 로 따라간다). 뒤집어 쓰면 오차가 지워지는 게 아니라 두 배가 된다.
        float 굽힌뒤발 = 자.최저발(대상);
        float 골반보정 = -(굽힌뒤발 - 원래발);
        위치더하기(대상, "Hips", 골반보정 / 골반단위m);

        // ── 머리: 얼굴이 목표 각을 보게 목과 머리가 반씩 나눠 숙인다.
        //   응답이 거의 1°/1° 라 한 번이면 붙지만, 클립마다 축이 조금 달라 두 번 돌린다.
        float 먹인각 = 0f;
        for (int i = 0; i < 2; i++)
        {
            float 지금 = 자.얼굴각(대상);
            float d = 지금 - 얼굴목표;                 // 위를 보고 있으면 양수 → 그만큼 숙인다
            if (Mathf.Abs(d) < 0.3f) break;
            회전더하기(대상, "neck", Quaternion.Euler(d * 0.5f, 0f, 0f));
            회전더하기(대상, "Head", Quaternion.Euler(d * 0.5f, 0f, 0f));
            먹인각 += d;
        }

        EditorUtility.SetDirty(대상);
        return $"머리 {먹인각:F1}° 숙임 · 골반 {골반보정 * 100f:F1}cm · 얼굴 {자.얼굴각(대상):F1}° · 발오차 {(자.최저발(대상) - 원래발) * 100f:F2}cm";
    }

    static void 새로쓰기(AnimationClip 원본, AnimationClip 대상)
    {
        대상.ClearCurves();
        foreach (var b in AnimationUtility.GetCurveBindings(원본))
            AnimationUtility.SetEditorCurve(대상, b, AnimationUtility.GetEditorCurve(원본, b));
    }

    /// 씬의 사람 몸에 클립을 찍어 보고 **실제로 재는** 자.
    /// ★숫자를 짐작하지 않는다 — 뼈 축도 원래 자세도 클립마다 다르다.
    class 자벌레
    {
        readonly Transform 뿌리, 머리, 얼굴, 왼발, 오른발;
        readonly Animator 애니;
        public bool 준비됐나 => 뿌리 != null && 머리 != null && 얼굴 != null && 왼발 != null;

        public 자벌레()
        {
            foreach (var a in Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
            {
                if (!a.gameObject.activeInHierarchy) continue;
                Transform h = null, f = null, lf = null, rf = null;
                foreach (var t in a.GetComponentsInChildren<Transform>(true))
                    switch (t.name)
                    {
                        case "Head": h = t; break;
                        case "headfront": f = t; break;
                        case "LeftFoot": lf = t; break;
                        case "RightFoot": rf = t; break;
                    }
                if (h == null || f == null || lf == null) continue;
                애니 = a; 뿌리 = a.transform; 머리 = h; 얼굴 = f; 왼발 = lf; 오른발 = rf;
                break;
            }
        }

        /// 얼굴이 향하는 상하각 (°) — 양수면 위를 본다. 클립 전체의 평균
        public float 얼굴각(AnimationClip c)
        {
            float 합 = 0f; int n = 0;
            찍어보기(c, () => {
                var v = 뿌리.InverseTransformDirection((얼굴.position - 머리.position).normalized);
                합 += Mathf.Asin(Mathf.Clamp(v.y, -1f, 1f)) * Mathf.Rad2Deg; n++;
            });
            return n > 0 ? 합 / n : 0f;
        }

        /// 클립을 도는 동안 발이 내려간 가장 낮은 높이 (몸 기준 m) — 접지의 기준
        public float 최저발(AnimationClip c)
        {
            float y = float.MaxValue;
            찍어보기(c, () => {
                y = Mathf.Min(y, 뿌리.InverseTransformPoint(왼발.position).y);
                if (오른발 != null) y = Mathf.Min(y, 뿌리.InverseTransformPoint(오른발.position).y);
            });
            return y == float.MaxValue ? 0f : y;
        }

        void 찍어보기(AnimationClip c, System.Action 재기)
        {
            bool 켜짐 = 애니.enabled; 애니.enabled = false;
            for (int i = 0; i < 24; i++) { c.SampleAnimation(뿌리.gameObject, c.length * i / 24f); 재기(); }
            애니.enabled = 켜짐;
        }
    }

    /// 그 뼈의 회전 커브 전체에 **로컬 회전을 뒤에 곱한다** (뼈 자신의 축 기준 — 실측이 이 규약이다)
    static void 회전더하기(AnimationClip c, string 뼈, Quaternion 델타)
    {
        var 짝 = new Dictionary<string, AnimationCurve[]>();
        var 틀 = new Dictionary<string, EditorCurveBinding[]>();
        foreach (var b in AnimationUtility.GetCurveBindings(c))
        {
            if (!b.propertyName.StartsWith("m_LocalRotation.")) continue;
            if (막뼈(b.path) != 뼈) continue;
            int i = "xyzw".IndexOf(b.propertyName[b.propertyName.Length - 1]);
            if (i < 0) continue;
            if (!짝.ContainsKey(b.path)) { 짝[b.path] = new AnimationCurve[4]; 틀[b.path] = new EditorCurveBinding[4]; }
            짝[b.path][i] = AnimationUtility.GetEditorCurve(c, b);
            틀[b.path][i] = b;
        }

        foreach (var kv in 짝)
        {
            var cur = kv.Value;
            if (cur[0] == null || cur[1] == null || cur[2] == null || cur[3] == null) continue;
            int n = cur[0].keys.Length;
            var 새 = new AnimationCurve[4];
            for (int i = 0; i < 4; i++) 새[i] = new AnimationCurve();
            for (int k = 0; k < n; k++)
            {
                float t = cur[0].keys[k].time;
                var q = new Quaternion(cur[0].Evaluate(t), cur[1].Evaluate(t), cur[2].Evaluate(t), cur[3].Evaluate(t));
                q = q * 델타;
                새[0].AddKey(t, q.x); 새[1].AddKey(t, q.y); 새[2].AddKey(t, q.z); 새[3].AddKey(t, q.w);
            }
            for (int i = 0; i < 4; i++)
            {
                부드럽게(새[i]);
                AnimationUtility.SetEditorCurve(c, 틀[kv.Key][i], 새[i]);
            }
        }
    }

    /// 그 뼈의 y 위치 커브에 상수를 더한다 (클립 로컬 단위)
    static void 위치더하기(AnimationClip c, string 뼈, float dy)
    {
        foreach (var b in AnimationUtility.GetCurveBindings(c))
        {
            if (b.propertyName != "m_LocalPosition.y" || 막뼈(b.path) != 뼈) continue;
            var cur = AnimationUtility.GetEditorCurve(c, b);
            var keys = cur.keys;
            for (int i = 0; i < keys.Length; i++) keys[i].value += dy;
            cur.keys = keys;
            AnimationUtility.SetEditorCurve(c, b, cur);
        }
    }

    static void 부드럽게(AnimationCurve c) { for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f); }

    static string 막뼈(string path) { int i = path.LastIndexOf('/'); return i < 0 ? path : path.Substring(i + 1); }
}
