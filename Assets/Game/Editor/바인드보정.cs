using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

/// 리그의 **쉬는 자세(바인드 포즈)가 바뀐 뒤** 클립을 그 자리에 다시 앉힌다.
///
/// ★★왜 필요했나 (2026-08-09 사용자가 리그를 다시 맞춤 — "리깅이 좆같이 되어있어서 수정한건데").
///   관절 자리를 몸에 맞게 다시 잡은 건 옳다. 그런데 클립은 **뼈의 회전을 통째로 덮어쓰기**
///   때문에, 기준이 되는 쉬는 자세가 돌면 같은 회전값이 다른 방향을 가리킨다.
///   실측(정지_여자): 뼈 위치는 0.0cm 로 똑같은데 **살점**이 왼발 18.2° · 머리 7.4° 돌았다.
///
/// ★★수식 — 살점이 그대로려면 `M·bind⁻¹` 이 같아야 한다:
///     M_새(t) · W_새⁻¹ = M_옛(t) · W_옛⁻¹      (W = 바인드 시점 월드 행렬 = bindpose⁻¹)
///   → M_새 = M_옛 · D,     **D = W_옛⁻¹ · W_새 = bindpose_옛 · bindpose_새⁻¹**
///   로컬로 내리면 부모의 D 가 앞에서 상쇄되어야 하므로:
///     **L_새 = D_부모⁻¹ · L_옛 · D_자기**
///   뿌리 뼈는 `D_부모 = 단위행렬`.
///
/// ★언제나 `_원본/` 에서 다시 계산해 `_원본보정/` 에 쓴다. 제자리에서 고치면 두 번 돌릴 때
///   보정이 누적된다 (`자세보정` 과 같은 원칙). `자세보정` 은 `_원본보정/` 이 있으면 그걸 입력으로 쓴다.
public static class 바인드보정
{
    const string 폴더 = "Assets/Game/Animations";
    const string 원본 = "Assets/Game/Animations/_원본";
    const string 보정본 = "Assets/Game/Animations/_원본보정";
    const string 옛파일 = "modeling/옛바인드포즈.txt";

    // 리그가 바뀐 몸과, 그 몸이 쓰는 클립들
    const string 대상몸 = "사람_여자";
    static readonly string[] 대상클립 = {
        "정지_여자","걷기_여자","달리기_여자","뒤로걷기_여자","옆걸음_왼쪽_여자","옆걸음_오른쪽_여자",
    };

    [MenuItem("Tools/토이라/바인드 보정 (리그 바꾼 뒤)")]
    public static void 적용()
    {
        var 옛 = 옛바인드읽기(out var 옛쉬는위치);
        if (옛 == null) { Debug.LogError($"[바인드보정] {옛파일} 이 없다 — 리그를 바꾸기 전에 떠 뒀어야 한다."); return; }

        var smr = 새렌더러();
        if (smr == null) { Debug.LogError($"[바인드보정] 씬에서 {대상몸} 을 못 찾았다."); return; }

        // D = bindpose_옛 · bindpose_새⁻¹  (뼈 이름으로 짝짓는다)
        var D = new Dictionary<string, Matrix4x4>();
        var 부모 = new Dictionary<string, string>();
        for (int i = 0; i < smr.bones.Length; i++)
        {
            var b = smr.bones[i];
            부모[b.name] = b.parent != null ? b.parent.name : null;
            if (!옛.TryGetValue(b.name, out var 옛bp)) continue;          // 새로 생긴 뼈(손가락)는 클립에 없다
            D[b.name] = 옛bp * smr.sharedMesh.bindposes[i].inverse;
        }
        Debug.Log($"[바인드보정] 짝지은 뼈 {D.Count}개 / 새 뼈 {smr.bones.Length}개");

        if (!Directory.Exists(보정본)) { Directory.CreateDirectory(보정본); AssetDatabase.Refresh(); }

        foreach (var 이름 in 대상클립)
        {
            string 입 = $"{원본}/{이름}.anim";
            var src = AssetDatabase.LoadAssetAtPath<AnimationClip>(입);
            if (src == null) { Debug.LogWarning($"[바인드보정] 원본 없음: {입}"); continue; }

            string 출 = $"{보정본}/{이름}.anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(출) == null) AssetDatabase.CopyAsset(입, 출);
            var dst = AssetDatabase.LoadAssetAtPath<AnimationClip>(출);

            보정(src, dst, D, 부모, 옛쉬는위치);
            Debug.Log($"[바인드보정] {이름} → {출}");
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[바인드보정] 끝. 이어서 `자세보정` 을 돌리면 이 결과 위에 머리·무릎 손질이 얹힌다.");
    }

    static void 보정(AnimationClip src, AnimationClip dst, Dictionary<string, Matrix4x4> D,
                     Dictionary<string, string> 부모, Dictionary<string, Vector3> 옛쉬는위치)
    {
        // 경로 → (뼈이름, 회전커브4, 위치커브3)
        var 회전 = new Dictionary<string, AnimationCurve[]>();
        var 위치 = new Dictionary<string, AnimationCurve[]>();
        var 틀회전 = new Dictionary<string, EditorCurveBinding[]>();
        var 틀위치 = new Dictionary<string, EditorCurveBinding[]>();
        foreach (var b in AnimationUtility.GetCurveBindings(src))
        {
            var c = AnimationUtility.GetEditorCurve(src, b);
            if (b.propertyName.StartsWith("m_LocalRotation."))
            {
                int i = "xyzw".IndexOf(b.propertyName[b.propertyName.Length - 1]); if (i < 0) continue;
                if (!회전.ContainsKey(b.path)) { 회전[b.path] = new AnimationCurve[4]; 틀회전[b.path] = new EditorCurveBinding[4]; }
                회전[b.path][i] = c; 틀회전[b.path][i] = b;
            }
            else if (b.propertyName.StartsWith("m_LocalPosition."))
            {
                int i = "xyz".IndexOf(b.propertyName[b.propertyName.Length - 1]); if (i < 0) continue;
                if (!위치.ContainsKey(b.path)) { 위치[b.path] = new AnimationCurve[3]; 틀위치[b.path] = new EditorCurveBinding[3]; }
                위치[b.path][i] = c; 틀위치[b.path][i] = b;
            }
        }

        // 시간축 — 30fps 로 다시 샘플한다 (원본이 30fps 라 잃는 게 없다)
        float fps = Mathf.Max(1f, src.frameRate);
        int n = Mathf.Max(2, Mathf.RoundToInt(src.length * fps) + 1);

        dst.ClearCurves();
        foreach (var b in AnimationUtility.GetCurveBindings(src))
            AnimationUtility.SetEditorCurve(dst, b, AnimationUtility.GetEditorCurve(src, b));

        foreach (var kv in 회전)
        {
            string path = kv.Key;
            string 뼈 = 막(path);
            if (!D.TryGetValue(뼈, out var Db)) continue;
            var Dp = Matrix4x4.identity;
            if (부모.TryGetValue(뼈, out var p) && p != null && D.TryGetValue(p, out var dp)) Dp = dp;
            var Dpi = Dp.inverse;

            var rc = kv.Value; if (rc[0] == null || rc[3] == null) continue;
            위치.TryGetValue(path, out var pc);
            // ★★위치 커브가 **없는** 뼈가 함정이다 (2026-08-09). 커브가 없으면 그 뼈는
            //   모델의 쉬는 위치를 그대로 쓰는데, 리그를 바꾸면 그 위치가 달라진다.
            //   실제로 걷기·달리기·옆걸음에 Hips 위치 커브가 없어서 **전신이 6.2cm 밀렸다.**
            //   → 옛 쉬는 위치를 넣고 계산한 뒤, 없던 위치 커브를 새로 써 준다.
            옛쉬는위치.TryGetValue(뼈, out var 쉬는위치);
            var 새회전 = new AnimationCurve[4]; for (int i = 0; i < 4; i++) 새회전[i] = new AnimationCurve();
            var 새위치 = new AnimationCurve[3]; for (int i = 0; i < 3; i++) 새위치[i] = new AnimationCurve();

            for (int k = 0; k < n; k++)
            {
                float t = src.length * k / (n - 1);
                var q = new Quaternion(rc[0].Evaluate(t), rc[1].Evaluate(t), rc[2].Evaluate(t), rc[3].Evaluate(t)).normalized;
                // ★★축마다 따로 본다 (2026-08-09). 걷기의 Hips 는 **y 커브만** 있는데
                //   「x 가 없으면 세 축 다 쉬는 위치」로 뭉뚱그렸더니 위아래 출렁임(5.3cm)이
                //   통째로 날아갔다. 그게 남아 있던 3~5cm 어긋남의 정체였다.
                var pos = 쉬는위치;
                if (pc != null)
                    for (int ax = 0; ax < 3; ax++) if (pc[ax] != null) pos[ax] = pc[ax].Evaluate(t);
                var L = Matrix4x4.TRS(pos, q, Vector3.one);
                var Ln = Dpi * L * Db;
                var qn = Ln.rotation; var pn = new Vector3(Ln.m03, Ln.m13, Ln.m23);
                새회전[0].AddKey(t, qn.x); 새회전[1].AddKey(t, qn.y); 새회전[2].AddKey(t, qn.z); 새회전[3].AddKey(t, qn.w);
                새위치[0].AddKey(t, pn.x); 새위치[1].AddKey(t, pn.y); 새위치[2].AddKey(t, pn.z);
            }
            for (int i = 0; i < 4; i++) { 매끈(새회전[i]); AnimationUtility.SetEditorCurve(dst, 틀회전[path][i], 새회전[i]); }
            // 위치 커브는 **없었어도 새로 쓴다** — 안 그러면 새 리그의 쉬는 위치가 그대로 먹는다
            for (int i = 0; i < 3; i++)
            {
                매끈(새위치[i]);
                var bind = (pc != null && pc[i] != null)
                    ? 틀위치[path][i]
                    : EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition." + "xyz"[i]);
                AnimationUtility.SetEditorCurve(dst, bind, 새위치[i]);
            }
        }
        EditorUtility.SetDirty(dst);
    }

    static SkinnedMeshRenderer 새렌더러()
    {
        foreach (var smr in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
        {
            var t = smr.transform;
            while (t != null) { if (t.name == 대상몸) return smr; t = t.parent; }
        }
        return null;
    }

    /// 저장해 둔 옛 바인드 포즈 (`대상몸` 부분만). 쉬는 위치도 같이 돌려준다
    static Dictionary<string, Matrix4x4> 옛바인드읽기(out Dictionary<string, Vector3> 쉬는위치)
    {
        쉬는위치 = new Dictionary<string, Vector3>();
        string 길 = Path.GetFullPath(Path.Combine(Application.dataPath, "..", 옛파일));
        if (!File.Exists(길)) return null;
        var d = new Dictionary<string, Matrix4x4>();
        bool 안 = false;
        foreach (var line in File.ReadAllLines(길))
        {
            if (line == 대상몸) { 안 = true; continue; }
            if (line == "---") { if (안) break; continue; }
            if (!안 || line.Length < 5) continue;
            var p = line.Split('|'); if (p.Length < 3) continue;
            var v = p[1].Split(',');
            var m = new Matrix4x4();
            for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++)
                m[r, c] = float.Parse(v[r * 4 + c], CultureInfo.InvariantCulture);
            d[p[0]] = m;
            var lp = p[2].Split(',');
            쉬는위치[p[0]] = new Vector3(float.Parse(lp[0], CultureInfo.InvariantCulture),
                                       float.Parse(lp[1], CultureInfo.InvariantCulture),
                                       float.Parse(lp[2], CultureInfo.InvariantCulture));
        }
        return d.Count > 0 ? d : null;
    }

    static void 매끈(AnimationCurve c) { for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f); }
    static string 막(string path) { int i = path.LastIndexOf('/'); return i < 0 ? path : path.Substring(i + 1); }
}
