using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// 집어들기(서기 → 쪼그려 앉아 손 뻗기 → 일어나며 머리 위로)를 **클립으로 굽는다**.
///
/// ★★★★**각도를 지어내지 않는다 — 이미 있는 포즈를 빌려 섞는다** (2026-08-13 사용자
///   "기본서있는자세(정지 애니메이션의 첫 키프레임참고) - 쪼그려앉게 Hip이 많이 내려와야함,
///    다리 구부리고 앉기 상태에서 허리를 앞쪽으로 구부리고 두손을 뻗음 - 그대로 일어나면서
///    머리위로 손을 향하게하고 들고있는 모션").
///
///   ☆옛 방식은 `HeroAttack.몸통스윙` 의 절차 자세를 떴다. 그런데 그건 **공격 예비 동작용**
///     이라 얕다 — 실측: 골반이 12단위(0.17m)만 내려갔다. 사용자 지적 *"다리를 쬐금 구부리고"*.
///   ☆★사용자가 만든 **`갈무리_여자` 가 이미 「쪼그려앉아 뒤적이는」 동작**이다. 그 0.3초
///     포즈는 골반 **30단위(0.43m)** · 무릎 **79°** · 허리 **6°(앞으로 숙임)** · 팔이 앞으로 뻗음.
///     원하시는 그림이 그 안에 다 있다. 빌려 쓰면 눈금을 지어낼 필요가 없다.
///
///   ☆그래서 이 도구는 **두 포즈를 떠서 섞는다:**
///       A = `정지_여자` 0초        (서기 — 사용자가 지목한 기준)
///       B = `갈무리_여자` 앉은 때   (쪼그려 앉아 허리 숙이고 손 뻗음)
///     ①A→B 로 앉고 ②B 를 잠깐 유지하고(잡는 순간) ③B→A 로 일어나면서 **팔만** 머리 위로.
///
/// ★★뼈 기본값(`HeroHold.뼈잡기`)은 **절대 부르지 않는다** (2026-08-13 에 이걸 어겨 사고를 냈다).
///   끝나면 A 포즈를 다시 찍어 몸을 되돌린다.
public static class 들기클립굽기
{
    const int 초당 = 30;
    const float 각도허용 = 0.004f;    // 쿼터니언 성분 — 작을수록 키가 는다
    const float 자리허용 = 0.05f;     // 로컬 단위 (골반 1단위 = 0.0143m)

    [MenuItem("Tools/토이라/집어들기를 클립으로 굽기")]
    public static void 굽기()
    {
        // ★★안전문 ① — 애니메이션 창이 뼈를 쥐고 있으면 손대지 않는다
        if (AnimationMode.InAnimationMode())
        {
            EditorUtility.DisplayDialog("멈췄다",
                "애니메이션 창이 미리보기(또는 녹화) 중이다.\n\n" +
                "그 상태에서 뼈를 돌리면 **열려 있는 클립에 그 자세가 적힌다.**\n" +
                "미리보기를 끄고 다시 눌러라.", "알았다");
            return;
        }

        var hero = Object.FindFirstObjectByType<HeroAttack>();
        if (hero == null) { Debug.LogError("[들기클립] 씬에 HeroAttack 이 없다."); return; }
        var hold = hero.GetComponent<HeroHold>();
        var carry = hero.GetComponent<HeroCarry>();
        if (hold == null || carry == null) { Debug.LogError("[들기클립] HeroHold / HeroCarry 가 없다."); return; }
        var an = hero.GetComponentInChildren<Animator>();
        if (an == null) { Debug.LogError("[들기클립] 켜져 있는 몸이 없다."); return; }

        string 뒷말 = an.transform.name.Contains("여자") ? "여자" : "남자";
        var A클립 = 클립찾기($"정지_{뒷말}");
        var B클립 = 클립찾기($"갈무리_{뒷말}");
        if (A클립 == null) { Debug.LogError($"[들기클립] 기준이 될 정지_{뒷말} 가 없다."); return; }
        if (B클립 == null) { Debug.LogError($"[들기클립] 쪼그려 앉은 포즈를 빌릴 갈무리_{뒷말} 가 없다."); return; }

        string 이름 = $"들기_{뒷말}";
        string 경로 = $"Assets/Game/Animations/{이름}.anim";

        // ★★안전문 ② — 이미 있으면 손으로 고친 것이 날아간다
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(경로) != null &&
            !EditorUtility.DisplayDialog("집어들기 다시 굽기",
                $"{이름} 을 다시 굽는다.\n\n애니메이션 창에서 손으로 고친 것이 **전부 사라진다.**\n그래도 굽나?",
                "굽는다 (수정 버림)", "그만둔다"))
            return;

        float 길이 = carry.숙임시간 + carry.잡음시간 + carry.올림시간;
        int N = Mathf.Max(3, Mathf.RoundToInt(길이 * 초당));

        // ── 뼈 담기: `Armature` 아래 전부
        Transform 뿌리뼈 = null;
        foreach (var t in an.GetComponentsInChildren<Transform>(true))
            if (t.name == "Armature") { 뿌리뼈 = t; break; }
        var 담을것 = 뿌리뼈 != null ? 뿌리뼈.GetComponentsInChildren<Transform>(true)
                                    : an.GetComponentsInChildren<Transform>(true);
        var 뼈 = new List<Transform>(); var 길 = new List<string>(); var 본 = new HashSet<string>();
        foreach (var t in 담을것)
        {
            if (t == an.transform || 본.Contains(t.name)) continue;
            본.Add(t.name); 뼈.Add(t); 길.Add(뼈길(an.transform, t));
        }
        if (뼈.Count == 0) { Debug.LogError("[들기클립] 뼈를 하나도 못 찾았다."); return; }

        // ── ★두 포즈를 떠 둔다
        var A회 = new Quaternion[뼈.Count]; var A자 = new Vector3[뼈.Count];
        var B회 = new Quaternion[뼈.Count]; var B자 = new Vector3[뼈.Count];
        A클립.SampleAnimation(an.gameObject, 0f);
        for (int b = 0; b < 뼈.Count; b++) { A회[b] = 뼈[b].localRotation; A자[b] = 뼈[b].localPosition; }
        // 앉은 때 — `갈무리앉는데` 를 지나면 완전히 앉아 있다 (그 뒤는 뒤적이는 반복)
        float 앉은때 = Mathf.Min(B클립.length * 0.35f, 0.4f);
        A클립.SampleAnimation(an.gameObject, 0f);
        B클립.SampleAnimation(an.gameObject, 앉은때);
        for (int b = 0; b < 뼈.Count; b++) { B회[b] = 뼈[b].localRotation; B자[b] = 뼈[b].localPosition; }

        // 팔 자세 코드 (머리 위로 올리는 것만 여기서 얹는다)
        const BindingFlags 숨 = BindingFlags.NonPublic | BindingFlags.Instance;
        var f줍기지금   = typeof(HeroHold).GetField("줍기지금", 숨);
        var f들올림지금 = typeof(HeroHold).GetField("들올림지금", 숨);
        var m잡기       = typeof(HeroHold).GetMethod("LateUpdate", 숨);
        if (f줍기지금 == null || f들올림지금 == null || m잡기 == null)
        { Debug.LogError("[들기클립] HeroHold 내부를 못 잡았다."); return; }

        // ── 프레임마다 포즈를 섞고, 일어나는 구간에만 팔을 머리 위로
        var 회전 = new Quaternion[뼈.Count][]; var 자리 = new Vector3[뼈.Count][];
        for (int b = 0; b < 뼈.Count; b++) { 회전[b] = new Quaternion[N]; 자리[b] = new Vector3[N]; }

        float t1 = Mathf.Max(0.01f, carry.숙임시간);                 // 앉기 끝
        float t2 = t1 + Mathf.Max(0.01f, carry.잡음시간);            // 잡기 끝 = 일어나기 시작

        for (int f = 0; f < N; f++)
        {
            float 초 = 길이 * f / (N - 1f);

            // ★앉음 정도 — ①0→1 앉고 ②1 유지(잡는다) ③1→0 일어난다
            float 앉음 = 초 <= t1 ? Mathf.SmoothStep(0f, 1f, 초 / t1)
                       : 초 <= t2 ? 1f
                       : 1f - Mathf.SmoothStep(0f, 1f, (초 - t2) / Mathf.Max(0.01f, carry.올림시간));
            // ★들어올림 — 일어나기 시작할 때부터 팔이 머리 위로
            float 올 = 초 <= t2 ? 0f
                     : Mathf.SmoothStep(0f, 1f, (초 - t2) / Mathf.Max(0.01f, carry.올림시간));

            // ①포즈를 섞는다 (전신)
            for (int b = 0; b < 뼈.Count; b++)
            {
                뼈[b].localRotation = Quaternion.Slerp(A회[b], B회[b], 앉음);
                뼈[b].localPosition = Vector3.Lerp(A자[b], B자[b], 앉음);
            }

            // ②일어나는 동안만 팔을 머리 위로 (앉아 있을 때는 갈무리 포즈의 뻗은 팔을 그대로 쓴다)
            if (올 > 0.001f)
            {
                hold.줍기 = 0f; hold.들올림 = 올; hold.두손 = true;
                f줍기지금.SetValue(hold, 0f); f들올림지금.SetValue(hold, 올);
                m잡기.Invoke(hold, null);
            }

            for (int b = 0; b < 뼈.Count; b++)
            { 회전[b][f] = 뼈[b].localRotation; 자리[b][f] = 뼈[b].localPosition; }
        }

        // ── ★몸을 되돌린다 (`뼈잡기()` 는 부르지 않는다)
        A클립.SampleAnimation(an.gameObject, 0f);
        hero.줍기굽힘 = 0f; hero.들기진행 = -1f;
        hold.줍기 = 0f; hold.들올림 = 0f;
        f줍기지금.SetValue(hold, 0f); f들올림지금.SetValue(hold, 0f);

        // ── 클립 만들기
        var clip = new AnimationClip { frameRate = 초당 };
        for (int b = 0; b < 뼈.Count; b++)
        {
            커브넣기(clip, 길[b], "localRotation.x", N, 길이, f => 회전[b][f].x, 각도허용);
            커브넣기(clip, 길[b], "localRotation.y", N, 길이, f => 회전[b][f].y, 각도허용);
            커브넣기(clip, 길[b], "localRotation.z", N, 길이, f => 회전[b][f].z, 각도허용);
            커브넣기(clip, 길[b], "localRotation.w", N, 길이, f => 회전[b][f].w, 각도허용);
            bool 움직임 = false;
            for (int f = 1; f < N && !움직임; f++)
                if ((자리[b][f] - 자리[b][0]).sqrMagnitude > 자리허용 * 자리허용) 움직임 = true;
            if (!움직임) continue;
            커브넣기(clip, 길[b], "localPosition.x", N, 길이, f => 자리[b][f].x, 자리허용);
            커브넣기(clip, 길[b], "localPosition.y", N, 길이, f => 자리[b][f].y, 자리허용);
            커브넣기(clip, 길[b], "localPosition.z", N, 길이, f => 자리[b][f].z, 자리허용);
        }

        // ★★이름을 먼저 넣는다 — `CopySerialized` 는 **이름까지 복사**한다. 무명 클립을 그대로
        //   덮어쓰면 기존 이름이 빈 문자열로 지워지고, 그러면 애니메이터가 클립을 못 잡는다
        //   (2026-08-13 실측: 클립 속은 멀쩡한데 화면에 아무것도 안 나왔다).
        clip.name = 이름;
        var 있던것 = AssetDatabase.LoadAssetAtPath<AnimationClip>(경로);
        if (있던것 != null) { EditorUtility.CopySerialized(clip, 있던것); clip = 있던것; clip.name = 이름; }
        else AssetDatabase.CreateAsset(clip, 경로);
        EditorUtility.SetDirty(clip);

        // ── 컨트롤러의 공격층·공격하체층에 「들기」 상태로 넣는다
        var ctrl = an.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        int 넣은수 = 0;
        if (ctrl != null)
        {
            foreach (var L in ctrl.layers)
            {
                if (L.name != "공격층" && L.name != "공격하체층") continue;
                var sm = L.stateMachine;
                UnityEditor.Animations.AnimatorState 자리상태 = null;
                foreach (var cs in sm.states) if (cs.state.name == "들기") 자리상태 = cs.state;
                if (자리상태 == null) 자리상태 = sm.AddState("들기", sm.entryPosition + new Vector3(320f, 470f, 0f));
                자리상태.motion = clip; 자리상태.writeDefaultValues = false;
                넣은수++;
            }
            EditorUtility.SetDirty(ctrl);
        }

        // ── 칸에 꽂고 **씬까지 저장**한다 — 안 하면 굽기 뒤 컴파일에 초기화되어 칸이 다시 빈다
        hero.들기클립 = clip;
        EditorUtility.SetDirty(hero);
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(hero.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        Selection.activeGameObject = an.gameObject;
        Debug.Log($"[들기클립] {이름} 구웠다 — 뼈 {뼈.Count}개 · {길이:F2}초 · {N}프레임 · "
                + $"컨트롤러 {넣은수}자리. 포즈는 정지_{뒷말}(서기) ↔ 갈무리_{뒷말} {앉은때:F2}초(쪼그려 앉기) 를 섞었다.");
    }

    /// 매 프레임 떠 놓고 **모양이 거의 안 변하는 중간 키만 지운다**
    static void 커브넣기(AnimationClip clip, string 길, string 속성, int N, float 길이,
                         System.Func<int, float> 값, float 허용)
    {
        var cur = new AnimationCurve();
        for (int f = 0; f < N; f++) cur.AddKey(길이 * f / (N - 1f), 값(f));
        for (int i = cur.length - 2; i >= 1; i--)
        {
            float 사이 = Mathf.Lerp(cur[i - 1].value, cur[i + 1].value, 0.5f);
            if (Mathf.Abs(cur[i].value - 사이) <= 허용) cur.RemoveKey(i);
        }
        clip.SetCurve(길, typeof(Transform), 속성, cur);
    }

    static string 뼈길(Transform 뿌리, Transform t)
    {
        var 조각 = new List<string>();
        for (var c = t; c != null && c != 뿌리; c = c.parent) 조각.Add(c.name);
        조각.Reverse();
        return string.Join("/", 조각);
    }

    static AnimationClip 클립찾기(string 이름)
    {
        foreach (var g in AssetDatabase.FindAssets($"{이름} t:AnimationClip"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
            if (c != null && c.name == 이름) return c;
        }
        return null;
    }
}
