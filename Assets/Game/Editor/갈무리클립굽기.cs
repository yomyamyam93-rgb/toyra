using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// 갈무리 동작을 **애니메이션 클립으로 굽는다** (2026-08-11 사용자 "애니메이션을 넣으라고,
/// 절차적으로 생성해서, 쪼그려 앉은다음 손으로 갈무리하는 동작으로 그냥 휘젓는 게 아니라
/// 갈무리하듯").
///
/// ★★★**뼈 각도를 지어내지 않는다** (2026-08-11 사용자 "이건좀 아니지않냐").
///   첫 판은 내가 이 리그의 회전 규약을 모르면서 숫자를 감으로 넣었고, 결과가 바닥에
///   널브러진 기괴한 자세였다. 손으로 쓴 각도는 **뼈의 축·부호를 모르면 반드시 틀린다.**
///
///   → **이미 제대로 앉아 있는 클립을 그대로 뜬다.** 프로젝트에 `웅크린정지` 가 있다.
///     다리·골반·척추는 거기서 **베끼므로 틀릴 수가 없다.** 내가 손대는 것은 **팔뿐**이고,
///     그것도 앉은 자세의 팔을 **기준으로 얹는 덧각**이라 몸이 무너지지 않는다.
///   ☆이게 `공격클립굽기` 와 같은 사상이다 — 맨바닥에서 그리지 않고 **있는 것을 떠서** 시작한다.
///   ☆9-2 의 교훈이기도 하다: 새로 만들기 전에 **이미 있는 것**을 먼저 찾는다.
///
/// ★동작 (앉기 0.30초 + 갈무리 1.00초)
///     0.00        `정지` 자세 (서 있다)
///     0.30        `웅크린정지` 자세 (앉았다)
///     0.30~1.30   앉은 자세 그대로, **팔만** — 누른다 → 썬다 → 당겨 뜯는다
///
/// ★굽고 나면 애니메이션 창에서 고칠 수 있다 — 회전은 도(°) 단위(`localEulerAnglesRaw`).
/// ★경로는 **애니메이터가 붙은 몸 기준**(`Armature/…`) — 캐릭터 뿌리 기준이면 창이 못 잡는다.
public static class 갈무리클립굽기
{
    const float 앉는데 = 0.30f;
    const float 한바퀴 = 1.00f;

    static readonly string[] 뼈들 = {
        "Hips", "Spine", "Spine01", "Spine02", "Head",
        "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot",
        "LeftShoulder", "LeftArm", "LeftForeArm",
        "RightShoulder", "RightArm", "RightForeArm",
    };

    /// 팔에 얹는 **덧각** — 앉은 자세의 팔에서 이만큼 더 돌린다.
    /// 바탕이 이미 옳으므로 여기 숫자가 좀 틀려도 몸이 무너지지 않는다 (팔만 어색해진다).
    struct 덧 { public float 초, 팔x, 팔y, 팔뚝x, 어깨x; }
    static readonly 덧[] 오른팔 = {
        new 덧 { 초 = 0.00f, 팔x =   0, 팔y =   0, 팔뚝x =  0, 어깨x =  0 },  // 앉은 그대로
        new 덧 { 초 = 0.28f, 팔x = -24, 팔y =  10, 팔뚝x = 20, 어깨x = -5 },  // 손을 넣어 누른다
        new 덧 { 초 = 0.55f, 팔x = -34, 팔y =   4, 팔뚝x =  6, 어깨x = -8 },  // 아래로 썬다
        new 덧 { 초 = 0.80f, 팔x = -16, 팔y =  -6, 팔뚝x = 38, 어깨x = -3 },  // 몸쪽으로 당겨 뜯는다
        new 덧 { 초 = 1.00f, 팔x =   0, 팔y =   0, 팔뚝x =  0, 어깨x =  0 },  // 되돌아온다
    };
    static readonly 덧[] 왼팔 = {
        new 덧 { 초 = 0.00f, 팔x =  0, 팔y =  0, 팔뚝x =  0, 어깨x = 0 },
        new 덧 { 초 = 0.55f, 팔x = -8, 팔y = -4, 팔뚝x = 10, 어깨x = 2 },   // 잡아 누른 채 조금만
        new 덧 { 초 = 1.00f, 팔x =  0, 팔y =  0, 팔뚝x =  0, 어깨x = 0 },
    };

    [MenuItem("Tools/토이라/갈무리 모션을 클립으로 굽기")]
    public static void 굽기()
    {
        var hero = Object.FindFirstObjectByType<HeroAttack>();
        if (hero == null) { Debug.LogError("[갈무리클립] 씬에 HeroAttack 이 없다."); return; }
        var an = hero.GetComponentInChildren<Animator>();
        if (an == null) { Debug.LogError("[갈무리클립] 켜져 있는 몸이 없다."); return; }

        string 뒷말 = an.transform.name.Contains("여자") ? "여자" : "남자";
        string 경로 = $"Assets/Game/Animations/갈무리_{뒷말}.anim";

        var 섬 = 클립찾기($"정지_{뒷말}");
        var 앉음 = 클립찾기("웅크린정지");
        if (섬 == null) { Debug.LogError($"[갈무리클립] 정지_{뒷말} 이 없다."); return; }
        if (앉음 == null) { Debug.LogError("[갈무리클립] 웅크린정지 가 없다 — 앉은 자세를 뜰 데가 없다."); return; }

        var 뼈 = new Dictionary<string, Transform>();
        var 길 = new Dictionary<string, string>();
        foreach (var t in an.GetComponentsInChildren<Transform>(true))
            if (System.Array.IndexOf(뼈들, t.name) >= 0 && !뼈.ContainsKey(t.name))
            { 뼈[t.name] = t; 길[t.name] = 뼈길(an.transform, t); }
        if (뼈.Count == 0) { Debug.LogError("[갈무리클립] 뼈를 하나도 못 찾았다."); return; }

        // ── ① 두 자세를 떠 둔다 (샘플 → 로컬값 읽기). **여기서 뜬 값은 틀릴 수가 없다**
        var 선자세 = 자세뜨기(섬, an.gameObject, 뼈, 0f);
        var 앉은자세 = 자세뜨기(앉음, an.gameObject, 뼈, 0f);

        // ── ② 키 시각
        var 키 = new List<float> { 0f, 앉는데 };
        foreach (var d in 오른팔) if (d.초 > 0f) 키.Add(앉는데 + d.초);
        foreach (var d in 왼팔) if (d.초 > 0f && !키.Contains(앉는데 + d.초)) 키.Add(앉는데 + d.초);
        키.Sort();

        var 회전 = new Dictionary<string, AnimationCurve[]>();
        var 위치 = new Dictionary<string, AnimationCurve[]>();
        foreach (var n in 뼈.Keys)
        {
            회전[n] = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
            위치[n] = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
        }

        // ★오일러 연속성 — ±180 을 넘을 때 튀면 그 프레임에 뼈가 한 바퀴 돈다
        var 지난각 = new Dictionary<string, Vector3>();

        foreach (var 초 in 키)
        {
            bool 서있나 = 초 <= 0.0001f;
            var 바탕 = 서있나 ? 선자세 : 앉은자세;
            float 팔때 = Mathf.Max(0f, 초 - 앉는데);

            foreach (var n in 뼈.Keys)
            {
                var (pos, eul) = 바탕[n];
                if (!서있나) eul += 팔덧각(n, 팔때);
                if (지난각.TryGetValue(n, out var 앞)) eul = 잇기(앞, eul);
                지난각[n] = eul;

                위치[n][0].AddKey(초, pos.x); 위치[n][1].AddKey(초, pos.y); 위치[n][2].AddKey(초, pos.z);
                회전[n][0].AddKey(초, eul.x); 회전[n][1].AddKey(초, eul.y); 회전[n][2].AddKey(초, eul.z);
            }
        }

        var clip = new AnimationClip { frameRate = 30f };
        foreach (var n in 뼈.Keys)
        {
            string p = 길[n];
            for (int i = 0; i < 3; i++) { 부드럽게(회전[n][i]); 부드럽게(위치[n][i]); }
            clip.SetCurve(p, typeof(Transform), "localEulerAnglesRaw.x", 회전[n][0]);
            clip.SetCurve(p, typeof(Transform), "localEulerAnglesRaw.y", 회전[n][1]);
            clip.SetCurve(p, typeof(Transform), "localEulerAnglesRaw.z", 회전[n][2]);
            // ★자리는 **골반만** — 다른 뼈의 자리를 건드리면 몸이 늘어난다
            if (n == "Hips")
            {
                clip.SetCurve(p, typeof(Transform), "m_LocalPosition.x", 위치[n][0]);
                clip.SetCurve(p, typeof(Transform), "m_LocalPosition.y", 위치[n][1]);
                clip.SetCurve(p, typeof(Transform), "m_LocalPosition.z", 위치[n][2]);
            }
        }

        var 설정 = AnimationUtility.GetAnimationClipSettings(clip);
        설정.loopTime = false;              // 되풀이는 코드가 시간으로 돌린다 (앞 0.3초는 한 번만)
        AnimationUtility.SetAnimationClipSettings(clip, 설정);

        섬.SampleAnimation(an.gameObject, 0f);      // 뜨느라 만진 몸을 되돌린다

        // ★`CopySerialized` 는 **이름까지 덮어쓴다** — 새 클립의 빈 이름이 에셋에 들어가
        //   애니메이터 목록이 빈칸으로 뜬다 (2026-08-11 실측). 미리 이름을 넣어 둔다.
        clip.name = $"갈무리_{뒷말}";
        var 있던 = AssetDatabase.LoadAssetAtPath<AnimationClip>(경로);
        if (있던 != null) { EditorUtility.CopySerialized(clip, 있던); clip = 있던; clip.name = $"갈무리_{뒷말}"; }
        else AssetDatabase.CreateAsset(clip, 경로);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var so = new SerializedObject(hero);
        var pr = so.FindProperty("갈무리클립");
        if (pr != null) { pr.objectReferenceValue = clip; so.ApplyModifiedProperties(); }

        Debug.Log($"[갈무리클립] {경로} · 길이 {앉는데 + 한바퀴:F2}초 · 뼈 {뼈.Count} · 키 {키.Count}"
                + " — 다리·골반·척추는 「웅크린정지」에서 그대로 떴다");
        Selection.activeObject = clip;
    }

    // ── 잔손질

    static AnimationClip 클립찾기(string 이름)
    {
        foreach (var g in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/Game/Animations" }))
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(g));
            if (c != null && c.name == 이름) return c;
        }
        return null;
    }

    /// 클립을 몸에 씌워 **그때의 로컬 자리·각도를 읽어 둔다**
    static Dictionary<string, (Vector3 pos, Vector3 eul)> 자세뜨기(
        AnimationClip c, GameObject 몸, Dictionary<string, Transform> 뼈, float 초)
    {
        c.SampleAnimation(몸, 초);
        var d = new Dictionary<string, (Vector3, Vector3)>();
        foreach (var kv in 뼈) d[kv.Key] = (kv.Value.localPosition, kv.Value.localEulerAngles);
        return d;
    }

    /// 앞 키와 이어지게 각도를 고른다 (±360 을 더해 제일 가까운 쪽으로)
    static Vector3 잇기(Vector3 앞, Vector3 지금)
    {
        return new Vector3(Mathf.Repeat(지금.x - 앞.x + 180f, 360f) - 180f + 앞.x,
                           Mathf.Repeat(지금.y - 앞.y + 180f, 360f) - 180f + 앞.y,
                           Mathf.Repeat(지금.z - 앞.z + 180f, 360f) - 180f + 앞.z);
    }

    /// 그 뼈에 얹을 덧각 — 팔이 아니면 0
    static Vector3 팔덧각(string 뼈, float 초)
    {
        if (!뼈.EndsWith("Shoulder") && !뼈.EndsWith("Arm") && !뼈.EndsWith("ForeArm")) return Vector3.zero;
        var 표 = 뼈.StartsWith("Right") ? 오른팔 : 왼팔;

        var a = 표[0]; var b = 표[표.Length - 1];
        for (int i = 0; i < 표.Length - 1; i++)
            if (초 >= 표[i].초 && 초 <= 표[i + 1].초) { a = 표[i]; b = 표[i + 1]; break; }
        float u = b.초 > a.초 ? Mathf.SmoothStep(0f, 1f, (초 - a.초) / (b.초 - a.초)) : 0f;

        if (뼈.EndsWith("ForeArm")) return new Vector3(Mathf.Lerp(a.팔뚝x, b.팔뚝x, u), 0f, 0f);
        if (뼈.EndsWith("Shoulder")) return new Vector3(Mathf.Lerp(a.어깨x, b.어깨x, u), 0f, 0f);
        return new Vector3(Mathf.Lerp(a.팔x, b.팔x, u), Mathf.Lerp(a.팔y, b.팔y, u), 0f);
    }

    static string 뼈길(Transform 뿌리, Transform t)
    {
        var s = t.name;
        for (var p = t.parent; p != null && p != 뿌리; p = p.parent) s = p.name + "/" + s;
        return s;
    }

    static void 부드럽게(AnimationCurve c)
    {
        for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
    }
}
