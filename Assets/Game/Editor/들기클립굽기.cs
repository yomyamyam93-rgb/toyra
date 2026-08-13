using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// 집어들기(숙임 → 잡음 → 올림 → 유지)를 **애니메이션 클립으로 굽는다**.
///
/// ★★왜 (2026-08-13 사용자 "팻 머리위로 들고가는거 동작 만들라니까 애니메이션으로도 뽑고
///   그것도 추가가안됐네"). 절차 모션만 만들어 놓으면 애니메이션 창에 고칠 것이 없다 —
///   9-2 의 「만들어져 있는데 부르는 데가 없다」와 같은 꼴이다.
///
/// ★`맨손클립굽기` 와 같은 사상이다: **빈 클립을 만들지 않고** 지금 절차 자세를 떠서 준다.
///   맨바닥에서 각도를 지어내지 않는다.
///
/// ★★★★**뼈 기본값은 절대 건드리지 않는다** (2026-08-13 에 이걸 어겨 사고를 냈다).
///   ☆`HeroHold.뼈잡기()` 는 **지금 자세를 「기본」으로 저장**한다. 뼈를 돌린 뒤에 부르면
///     그 자세가 기준이 되어 온 자세가 어긋난다. 이 도구는 그 함수를 부르지 않는다.
///   ☆끝나면 기준 클립의 0초를 찍어 몸을 되돌린다 (`맨손클립굽기` 와 같은 방식).
///   ☆★**애니메이션 창이 미리보기·녹화 중이면 아예 시작하지 않는다.** 그 상태에서 뼈를
///     돌리면 **열려 있는 클립에 그 자세가 적힌다** — 실제로 `갈무리_여자` 를 그렇게 망쳤다.
public static class 들기클립굽기
{
    const int 초당 = 30;
    const float 각도허용 = 0.75f;    // ° — 이보다 적게 어긋나는 중간 키는 지운다
    const float 자리허용 = 0.002f;   // m

    [MenuItem("Tools/토이라/집어들기를 클립으로 굽기")]
    public static void 굽기()
    {
        // ★★안전문 ① — 애니메이션 창이 열려 뼈를 쥐고 있으면 손대지 않는다
        if (AnimationMode.InAnimationMode())
        {
            EditorUtility.DisplayDialog("멈췄다",
                "애니메이션 창이 미리보기(또는 녹화) 중이다.\n\n" +
                "그 상태에서 뼈를 돌리면 **열려 있는 클립에 그 자세가 적힌다.**\n" +
                "애니메이션 창의 미리보기를 끄고 다시 눌러라.", "알았다");
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
        var 기본 = 클립찾기($"정지_{뒷말}");
        if (기본 == null) { Debug.LogError($"[들기클립] 기준이 될 정지_{뒷말} 클립이 없다."); return; }

        string 이름 = $"들기_{뒷말}";
        string 경로 = $"Assets/Game/Animations/{이름}.anim";

        // ★★안전문 ② — 이미 있으면 손으로 고친 것이 날아간다. 반드시 물어본다
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(경로) != null &&
            !EditorUtility.DisplayDialog("집어들기 다시 굽기",
                $"{이름} 을 다시 굽는다.\n\n애니메이션 창에서 손으로 고친 것이 **전부 사라진다.**\n그래도 굽나?",
                "굽는다 (수정 버림)", "그만둔다"))
            return;

        // 길이는 `HeroCarry` 의 세 순간을 더한 것 — 코드와 클립이 같은 자를 써야 안 어긋난다
        float 길이 = carry.숙임시간 + carry.잡음시간 + carry.올림시간;
        int 프레임수 = Mathf.Max(2, Mathf.RoundToInt(길이 * 초당));

        // ── 뼈 담기: `Armature` 아래 **전부** (안 움직이는 뼈도 커브를 만들어 애니메이션 창에서 잡히게)
        Transform 뿌리뼈 = null;
        foreach (var t in an.GetComponentsInChildren<Transform>(true))
            if (t.name == "Armature") { 뿌리뼈 = t; break; }
        var 담을것 = 뿌리뼈 != null ? 뿌리뼈.GetComponentsInChildren<Transform>(true)
                                    : an.GetComponentsInChildren<Transform>(true);
        var 뼈 = new List<Transform>();
        var 길 = new List<string>();
        var 본 = new HashSet<string>();
        foreach (var t in 담을것)
        {
            if (t == an.transform || 본.Contains(t.name)) continue;
            본.Add(t.name); 뼈.Add(t); 길.Add(뼈길(an.transform, t));
        }
        if (뼈.Count == 0) { Debug.LogError("[들기클립] 뼈를 하나도 못 찾았다."); return; }

        // 절차 모션 내부 (전부 private 이라 리플렉션)
        const BindingFlags 숨 = BindingFlags.NonPublic | BindingFlags.Instance;
        var f줍기지금   = typeof(HeroHold).GetField("줍기지금", 숨);
        var f들올림지금 = typeof(HeroHold).GetField("들올림지금", 숨);
        var m잡기       = typeof(HeroHold).GetMethod("LateUpdate", 숨);
        var f든자세     = typeof(HeroAttack).GetField("드는자세", 숨);
        var m몸통       = typeof(HeroAttack).GetMethod("몸통스윙", 숨);
        var f상태       = typeof(HeroAttack).GetField("state", 숨);
        var 상태형      = typeof(HeroAttack).GetNestedType("State", BindingFlags.NonPublic);
        if (f줍기지금 == null || f들올림지금 == null || m잡기 == null || m몸통 == null
            || f상태 == null || 상태형 == null)
        { Debug.LogError("[들기클립] 절차 모션 내부를 못 잡았다 — 코드가 바뀌면 이 도구도 고쳐야 한다."); return; }
        if (f든자세 != null) f든자세.SetValue(hero, hold);
        f상태.SetValue(hero, System.Enum.Parse(상태형, "쉼"));

        // ── 매 프레임 자세를 떠서 담는다
        var 회전 = new Quaternion[뼈.Count][];
        var 자리 = new Vector3[뼈.Count][];
        for (int b = 0; b < 뼈.Count; b++) { 회전[b] = new Quaternion[프레임수]; 자리[b] = new Vector3[프레임수]; }

        for (int f = 0; f < 프레임수; f++)
        {
            float 초 = 길이 * f / (프레임수 - 1);

            // ① 기준 자세(정지 0초)에서 시작한다 — 걷기 도중 자세가 섞이지 않게
            기본.SampleAnimation(an.gameObject, 0f);

            // ② `HeroCarry` 와 **똑같은 식**으로 네 순간을 계산한다
            float t1 = Mathf.Max(0.01f, carry.숙임시간);
            float t2 = t1 + Mathf.Max(0.01f, carry.잡음시간);
            float 숙 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(초 / t1));
            float 올 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((초 - t2) / Mathf.Max(0.01f, carry.올림시간)));

            // ③ 값을 밀고 자세 코드를 손으로 돌린다 (보간을 건너뛰려고 「지금」 값도 같이)
            hero.줍기굽힘 = 숙 * (1f - 올);
            hold.줍기 = 숙 * (1f - 올);
            hold.들올림 = 올;
            f줍기지금.SetValue(hold, hold.줍기);
            f들올림지금.SetValue(hold, hold.들올림);
            hold.두손 = true;                 // 두 손으로 받친다

            // ★★**한 프레임에 한 번씩만 부른다.** 여러 번 부르면 안 된다 —
            //   `몸통스윙` 은 척추·머리를 `localRotation * ...` 로 **얹으므로** 부른 횟수만큼
            //   누적된다 (24번 부르면 24배 돌아간다).
            //   ☆보간(`웅크림` 등)은 프레임을 넘어가며 이어진다 — 뼈만 리셋되고 그 값은 남는다
            m몸통.Invoke(hero, new object[] { 1f / 초당 });   // 다리·골반·척추
            m잡기.Invoke(hold, null);                        // 어깨·팔·팔뚝·머리

            for (int b = 0; b < 뼈.Count; b++)
            { 회전[b][f] = 뼈[b].localRotation; 자리[b][f] = 뼈[b].localPosition; }
        }

        // ── ★몸을 되돌린다. **`뼈잡기()` 를 부르지 않는다** (기본값이 어긋난다)
        기본.SampleAnimation(an.gameObject, 0f);
        hero.줍기굽힘 = 0f; hero.들기진행 = -1f;
        hold.줍기 = 0f; hold.들올림 = 0f;
        f줍기지금.SetValue(hold, 0f); f들올림지금.SetValue(hold, 0f);

        // ── 클립 만들기
        var clip = new AnimationClip { frameRate = 초당 };
        for (int b = 0; b < 뼈.Count; b++)
        {
            커브넣기(clip, 길[b], "localRotation.x", 프레임수, 길이, f => 회전[b][f].x, 각도허용 * 0.01f);
            커브넣기(clip, 길[b], "localRotation.y", 프레임수, 길이, f => 회전[b][f].y, 각도허용 * 0.01f);
            커브넣기(clip, 길[b], "localRotation.z", 프레임수, 길이, f => 회전[b][f].z, 각도허용 * 0.01f);
            커브넣기(clip, 길[b], "localRotation.w", 프레임수, 길이, f => 회전[b][f].w, 각도허용 * 0.01f);
            // 자리는 **움직인 뼈만** — 뼈 길이가 안 변하는 리그에서 대개 골반뿐이다
            bool 움직임 = false;
            for (int f = 1; f < 프레임수 && !움직임; f++)
                if ((자리[b][f] - 자리[b][0]).sqrMagnitude > 자리허용 * 자리허용) 움직임 = true;
            if (!움직임) continue;
            커브넣기(clip, 길[b], "localPosition.x", 프레임수, 길이, f => 자리[b][f].x, 자리허용);
            커브넣기(clip, 길[b], "localPosition.y", 프레임수, 길이, f => 자리[b][f].y, 자리허용);
            커브넣기(clip, 길[b], "localPosition.z", 프레임수, 길이, f => 자리[b][f].z, 자리허용);
        }

        var 있던것 = AssetDatabase.LoadAssetAtPath<AnimationClip>(경로);
        if (있던것 != null) { EditorUtility.CopySerialized(clip, 있던것); clip = 있던것; }
        else AssetDatabase.CreateAsset(clip, 경로);
        EditorUtility.SetDirty(clip);

        // ── 컨트롤러의 공격층·공격하체층에 「들기」 상태로 넣는다 (재생 쪽이 이름으로 찾는다)
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

        // ── `HeroAttack.들기클립` 칸에 꽂는다 — 이게 있어야 재생 쪽이 찾는다 (9-2)
        var so = new SerializedObject(hero);
        var pr = so.FindProperty("들기클립");
        if (pr != null) { pr.objectReferenceValue = clip; so.ApplyModifiedProperties(); }

        AssetDatabase.SaveAssets();
        Selection.activeGameObject = an.gameObject;      // 애니메이션 창이 바로 잡게
        Debug.Log($"[들기클립] {이름} 구웠다 — 뼈 {뼈.Count}개 · {길이:F2}초 · {프레임수}프레임 · "
                + $"컨트롤러 상태 {넣은수}자리. `{an.gameObject.name}` 를 고른 채 애니메이션 창에서 고치면 된다.");
    }

    /// 매 프레임 떠 놓고 **모양이 거의 안 변하는 중간 키만 지운다**.
    /// ★전부 남기면 한 자세를 고쳐도 옆 프레임이 그대로라 못 고친다. 봉우리·꺾임은 남는다.
    static void 커브넣기(AnimationClip clip, string 길, string 속성, int 프레임수, float 길이,
                         System.Func<int, float> 값, float 허용)
    {
        var cur = new AnimationCurve();
        for (int f = 0; f < 프레임수; f++) cur.AddKey(길이 * f / (프레임수 - 1), 값(f));

        for (int i = cur.length - 2; i >= 1; i--)
        {
            float 앞 = cur[i - 1].value, 뒤 = cur[i + 1].value;
            float 사이 = Mathf.Lerp(앞, 뒤, 0.5f);
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
