using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// 갈무리 동작을 **애니메이션 클립으로 굽는다** (2026-08-11 사용자 "애니메이션을 넣으라고,
/// 절차적으로 생성해서, 쪼그려 앉은다음 손으로 갈무리하는 동작으로 그냥 휘젓는 게 아니라
/// 갈무리하듯").
///
/// ★★`공격클립굽기` 와 다른 점 — 그쪽은 **이미 있는 절차 모션을 프레임마다 떠서** 굽는다.
///   갈무리는 절차 모션이 없다(있는 건 「살짝 굽힘」뿐이라 뜰 게 없다). 그래서 여기서는
///   **커브를 직접 쓴다.** 굽고 나면 애니메이션 창에서 손으로 고칠 수 있다 — 그게 목적이다.
///
/// ★동작 (1.30초 · 앞 0.30초는 앉는 구간 · 나머지 1초가 되풀이되는 갈무리)
///     0.00  서 있다
///     0.30  **쪼그려 앉는다** — 골반이 내려가고 무릎이 접히고 상체가 앞으로
///     0.30~1.30  **한 번의 갈무리** — 오른손이 몸 앞으로 들어가 아래로 눌러 썰고,
///                안쪽으로 당겨 뜯고, 다시 올린다. 왼손은 잡아 누른 채 조금씩 따라 움직인다.
///                ☆휘젓는 게 아니라 **누른다 → 썬다 → 당긴다** 의 세 박자다.
///
/// ★뼈 부호는 이 리그의 실측을 따른다 (`HeroAttack.몸통스윙` 주석):
///   허벅(UpLeg) −X = 엉덩이가 굽는다 · 정강(Leg) +X = 무릎이 접힌다 → 발이 제자리에 남는다.
///   골반 로컬 1단위 = **0.0143m** (2026-08-09 실측).
///
/// ★길이·경로 규칙은 공격 클립과 같다:
///   · 경로는 **애니메이터가 붙은 몸 기준** (`Armature/…`) — 캐릭터 뿌리 기준이면 창이 못 잡는다
///   · 회전은 `localEulerAnglesRaw` (도 단위라야 사람이 고친다. 쿼터니언이면 못 만진다)
public static class 갈무리클립굽기
{
    const float 앉는데 = 0.30f;      // 서 있다 → 앉기까지
    const float 한바퀴 = 1.00f;      // 갈무리 한 번
    const float 길이 = 앉는데 + 한바퀴;

    // 골반 로컬 1단위 = 0.0143m (실측) — 미터로 쓰고 여기서 바꾼다
    const float 단위 = 0.0143f;

    static readonly string[] 뼈들 = {
        "Hips", "Spine", "Spine01", "Spine02", "Head",
        "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot",
        "LeftShoulder", "LeftArm", "LeftForeArm",
        "RightShoulder", "RightArm", "RightForeArm",
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

        // ★이미 있으면 한 번 물어본다 — 손으로 고친 것이 다 날아간다 (공격 클립과 같은 규칙)
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(경로) != null &&
            !EditorUtility.DisplayDialog("갈무리 클립 다시 굽기",
                $"갈무리_{뒷말}.anim 이 이미 있다.\n\n다시 구우면 애니메이션 창에서 손으로 고친 것이 **전부 사라진다.**",
                "굽는다 (수정 버림)", "그만둔다"))
            return;

        // 뼈 경로 잡기 — 애니메이터가 붙은 몸 기준이어야 애니메이션 창이 잡는다
        var 길 = new Dictionary<string, string>();
        foreach (var t in an.GetComponentsInChildren<Transform>(true))
            if (System.Array.IndexOf(뼈들, t.name) >= 0 && !길.ContainsKey(t.name))
                길[t.name] = 뼈길(an.transform, t);
        if (길.Count == 0) { Debug.LogError("[갈무리클립] 뼈를 하나도 못 찾았다."); return; }

        var clip = new AnimationClip { frameRate = 30f };

        // ── 키 시각. 사이는 이징이 채운다 (키를 촘촘히 박으면 손으로 못 고친다)
        float A = 0f;                     // 서 있다
        float B = 앉는데;                 // 앉았다
        float C = 앉는데 + 한바퀴 * 0.28f; // 손을 넣어 누른다
        float D = 앉는데 + 한바퀴 * 0.55f; // 썬다 (아래로)
        float E = 앉는데 + 한바퀴 * 0.80f; // 당겨 뜯는다 (몸쪽으로)
        float F = 길이;                    // 다시 올린다 = B 자세로 이어진다

        // ── 하체: 앉는다. 앉은 뒤로는 그대로 (갈무리는 상체가 한다)
        //    허벅 −55 · 정강 +85 · 발목 −30 이면 발이 제자리에 남으면서 엉덩이가 내려간다
        곡선(clip, 길, "Hips", "m_LocalPosition.y", (A, 0f), (B, -0.26f / 단위 * 단위), (F, -0.26f));
        각(clip, 길, "Hips",       (A, 0, 0, 0), (B, 6, 0, 0), (F, 6, 0, 0));
        각(clip, 길, "LeftUpLeg",  (A, 0, 0, 0), (B, -55, 0, 4), (F, -55, 0, 4));
        각(clip, 길, "RightUpLeg", (A, 0, 0, 0), (B, -52, 0, -4), (F, -52, 0, -4));
        각(clip, 길, "LeftLeg",    (A, 0, 0, 0), (B, 85, 0, 0), (F, 85, 0, 0));
        각(clip, 길, "RightLeg",   (A, 0, 0, 0), (B, 82, 0, 0), (F, 82, 0, 0));
        각(clip, 길, "LeftFoot",   (A, 0, 0, 0), (B, -30, 0, 0), (F, -30, 0, 0));
        각(clip, 길, "RightFoot",  (A, 0, 0, 0), (B, -28, 0, 0), (F, -28, 0, 0));

        // ── 상체: 앞으로 숙이고, 써는 박자에 맞춰 조금씩 같이 움직인다
        //    (상체가 안 따라가면 팔만 도는 「휘젓기」가 된다 — 그게 지금 문제였다)
        각(clip, 길, "Spine",   (A, 0, 0, 0), (B, 16, 0, 0), (C, 20, 3, 0), (D, 24, 0, 0), (E, 17, -4, 0), (F, 16, 0, 0));
        각(clip, 길, "Spine01", (A, 0, 0, 0), (B, 10, 0, 0), (C, 13, 4, 0), (D, 16, 0, 0), (E, 11, -5, 0), (F, 10, 0, 0));
        각(clip, 길, "Spine02", (A, 0, 0, 0), (B, 8, 0, 0),  (C, 10, 4, 0), (D, 12, 0, 0), (E, 8, -5, 0),  (F, 8, 0, 0));
        각(clip, 길, "Head",    (A, 0, 0, 0), (B, 14, 0, 0), (C, 16, -2, 0), (D, 18, 0, 0), (E, 15, 2, 0), (F, 14, 0, 0));

        // ── 오른팔: **누른다 → 썬다 → 당긴다**. 이게 「갈무리하듯」의 알맹이다
        //    어깨는 조금만, 팔이 크게, 팔뚝이 접혔다 펴진다
        각(clip, 길, "RightShoulder", (A, 0, 0, 0), (B, -6, 0, 0), (C, -12, 0, -4), (D, -14, 0, -6), (E, -8, 0, -2), (F, -6, 0, 0));
        각(clip, 길, "RightArm",      (A, 0, 0, 0), (B, -28, 6, 0), (C, -52, 12, 0), (D, -64, 6, 0), (E, -44, -4, 0), (F, -28, 6, 0));
        각(clip, 길, "RightForeArm",  (A, 0, 0, 0), (B, 34, 0, 0), (C, 58, 0, 0), (D, 44, 0, 0), (E, 72, 0, 0), (F, 34, 0, 0));

        // ── 왼팔: 사체를 **잡아 누른 채** 조금씩만 따라간다 (양손이 똑같이 움직이면 춤이 된다)
        각(clip, 길, "LeftShoulder", (A, 0, 0, 0), (B, -5, 0, 0), (D, -8, 0, 3), (F, -5, 0, 0));
        각(clip, 길, "LeftArm",      (A, 0, 0, 0), (B, -34, -8, 0), (D, -40, -6, 0), (F, -34, -8, 0));
        각(clip, 길, "LeftForeArm",  (A, 0, 0, 0), (B, 46, 0, 0), (D, 52, 0, 0), (F, 46, 0, 0));

        var 설정 = AnimationUtility.GetAnimationClipSettings(clip);
        설정.loopTime = false;              // 되풀이는 코드가 시간으로 돌린다(앞 0.3초는 한 번만)
        AnimationUtility.SetAnimationClipSettings(clip, 설정);

        AssetDatabase.CreateAsset(clip, 경로);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 씬의 HeroAttack 에 바로 꽂아 준다 — 꽂는 걸 잊어서 "안 나온다" 가 되는 걸 막는다
        var so = new SerializedObject(hero);
        var p = so.FindProperty("갈무리클립");
        if (p != null) { p.objectReferenceValue = clip; so.ApplyModifiedProperties(); }

        Debug.Log($"[갈무리클립] {경로} 를 구웠다 · 길이 {길이:F2}초 (앉기 {앉는데:F2} + 갈무리 {한바퀴:F2})"
                + " · 애니메이션 창에서 고칠 수 있다");
        Selection.activeObject = clip;
    }

    // ── 아래는 커브를 쓰는 잔손질

    static string 뼈길(Transform 뿌리, Transform t)
    {
        var s = t.name;
        for (var p = t.parent; p != null && p != 뿌리; p = p.parent) s = p.name + "/" + s;
        return s;
    }

    static void 곡선(AnimationClip clip, Dictionary<string, string> 길, string 뼈, string 속성,
                     params (float 초, float 값)[] 키들)
    {
        if (!길.TryGetValue(뼈, out var path)) return;
        var c = new AnimationCurve();
        foreach (var k in 키들) c.AddKey(new Keyframe(k.초, k.값));
        부드럽게(c);
        clip.SetCurve(path, typeof(Transform), 속성, c);
    }

    /// 세 축을 한 번에 — 도(°) 단위라 애니메이션 창에서 그대로 만져진다
    static void 각(AnimationClip clip, Dictionary<string, string> 길, string 뼈,
                   params (float 초, float x, float y, float z)[] 키들)
    {
        if (!길.TryGetValue(뼈, out var path)) return;
        var cx = new AnimationCurve(); var cy = new AnimationCurve(); var cz = new AnimationCurve();
        foreach (var k in 키들)
        {
            cx.AddKey(new Keyframe(k.초, k.x));
            cy.AddKey(new Keyframe(k.초, k.y));
            cz.AddKey(new Keyframe(k.초, k.z));
        }
        부드럽게(cx); 부드럽게(cy); 부드럽게(cz);
        clip.SetCurve(path, typeof(Transform), "localEulerAnglesRaw.x", cx);
        clip.SetCurve(path, typeof(Transform), "localEulerAnglesRaw.y", cy);
        clip.SetCurve(path, typeof(Transform), "localEulerAnglesRaw.z", cz);
    }

    /// 키 사이를 부드럽게 — 뚝뚝 끊기면 로봇이 된다
    static void 부드럽게(AnimationCurve c)
    {
        for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
    }
}
