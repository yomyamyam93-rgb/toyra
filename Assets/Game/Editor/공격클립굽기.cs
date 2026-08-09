using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// 때리는 동작을 **애니메이션 클립으로 굽는다** — 그래야 애니메이션 창에서 고칠 수 있다.
///
/// ★★왜 (2026-08-09 사용자 "떄리는 모션은 왜 애니메이션 수정이 안돼?" → "공격모션 수정하게좀 넣어줘").
///   지금 때리는 동작은 클립이 아니라 **코드가 뼈를 직접 돌리는 절차 모션**이다
///   (`HeroHold` 의 어깨·팔·팔뚝 + `HeroAttack.몸통스윙` 의 척추·웅크림·발). 그래서
///   애니메이션 창을 열어도 고칠 키프레임이 없고, 인스펙터 숫자로만 만져진다.
///
/// ★**빈 클립을 만들지 않는다.** 지금 동작이 이미 3막(예비·휘두름·여운)으로 짜여 있으니
///   그걸 프레임마다 떠서 시작점으로 준다. 맨바닥에서 그리는 것보다 빠르고, 마음에 안 드는
///   키는 지우면 그만이다.
///
/// ★길이는 **정확히 1.00초** (프로젝트 규칙). 공속은 클립 배속으로 낸다.
public static class 공격클립굽기
{
    const int 초당 = 30;
    const float 길이 = 1.00f;

    // 굽는 뼈 — 공격이 실제로 건드리는 것만. 나머지는 걷기 클립이 계속 갖는다
    static readonly string[] 뼈들 = {
        "Hips", "Spine02", "Spine01", "Spine", "Head",
        "RightShoulder", "RightArm", "RightForeArm",
        "LeftShoulder", "LeftArm", "LeftForeArm",
        "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot",
    };

    [MenuItem("Tools/토이라/공격 모션을 클립으로 굽기")]
    public static void 굽기()
    {
        var hero = Object.FindFirstObjectByType<HeroAttack>();
        if (hero == null) { Debug.LogError("[공격클립] 씬에 HeroAttack 이 없다."); return; }

        // ★★**이미 있는 클립은 통째로 덮어쓴다 — 손으로 고친 것이 다 날아간다.**
        //   한 번 물어본다. (2026-08-09: 사용자가 애니메이션 창에서 고치기 시작한 뒤라
        //   무심코 다시 누르면 하루치 작업이 사라진다)
        foreach (var 뒤 in new[] { "여자", "남자" })
        {
            var 있는것 = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Game/Animations/공격_{뒤}.anim");
            if (있는것 == null) continue;
            if (!EditorUtility.DisplayDialog("공격 클립 다시 굽기",
                    $"공격_{뒤}.anim 이 이미 있다.\n\n다시 구우면 애니메이션 창에서 손으로 고친 것이 **전부 사라진다.**\n그래도 굽나?",
                    "굽는다 (수정 버림)", "그만둔다"))
                return;
            break;
        }
        var hold = hero.GetComponent<HeroHold>();
        if (hold == null) { Debug.LogError("[공격클립] HeroHold 가 없다."); return; }

        var an = hero.GetComponentInChildren<Animator>();
        if (an == null) { Debug.LogError("[공격클립] 켜져 있는 몸이 없다."); return; }
        string 몸이름 = an.transform.name;                       // 사람_여자 / 사람_남자
        string 뒷말 = 몸이름.Contains("여자") ? "여자" : "남자";
        var 기본 = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Game/Animations/정지_{뒷말}.anim");
        if (기본 == null) { Debug.LogError($"[공격클립] 기준이 될 정지_{뒷말} 클립이 없다."); return; }

        // 뼈 잡기 (켜진 몸에서만 — 꺼진 몸을 잡으면 엉뚱한 데가 움직인다)
        var 뼈 = new Dictionary<string, Transform>();
        var 길 = new Dictionary<string, string>();
        // ★★★경로는 **애니메이터가 붙은 몸 기준**이다 (2026-08-09 사용자 "공격모션을 쳐넣고
        //   재생하면 움직임이없는데"). 캐릭터 뿌리 기준(`사람_여자/Armature/…`)으로 구우면
        //   애니메이션 창이 **아무것도 못 잡는다** — 창은 애니메이터가 달린 오브젝트를
        //   기준으로 찾기 때문이다. 다른 클립들(`걷기_여자` 등)이 전부 `Armature/…` 인 이유.
        foreach (var t in an.GetComponentsInChildren<Transform>(false))
            if (System.Array.IndexOf(뼈들, t.name) >= 0 && !뼈.ContainsKey(t.name))
            { 뼈[t.name] = t; 길[t.name] = 경로(an.transform, t); }
        if (뼈.Count == 0) { Debug.LogError("[공격클립] 뼈를 하나도 못 찾았다."); return; }

        // 절차 모션을 손으로 돌리기 위한 준비 (전부 private 이라 리플렉션)
        var f지금 = typeof(HeroHold).GetField("<지금>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        var m잡기 = typeof(HeroHold).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        var f상태 = typeof(HeroAttack).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
        var f시각 = typeof(HeroAttack).GetField("t", BindingFlags.NonPublic | BindingFlags.Instance);
        var f든자세 = typeof(HeroAttack).GetField("드는자세", BindingFlags.NonPublic | BindingFlags.Instance);
        var m몸통 = typeof(HeroAttack).GetMethod("몸통스윙", BindingFlags.NonPublic | BindingFlags.Instance);
        var 상태형 = typeof(HeroAttack).GetNestedType("State", BindingFlags.NonPublic);
        if (f지금 == null || m잡기 == null || f상태 == null || m몸통 == null || 상태형 == null)
        { Debug.LogError("[공격클립] 절차 모션 내부를 못 잡았다 — 코드가 바뀌었으면 이 도구도 고쳐야 한다."); return; }
        f든자세.SetValue(hero, hold);

        // ★★**이동 평활값을 재우고 시작한다.** `몸통스윙` 은 「걷는 중엔 다리 간섭을 죽인다」고
        //   지난 프레임 자리와의 거리를 속도로 읽는데, 그 자리가 처음엔 (0,0,0) 이라
        //   첫 프레임에 **21,000m/s** 로 튄다. 그러면 `목표웅크림` 이 내내 0으로 눌려
        //   **다리가 하나도 안 굽은 채로 구워진다** (실측: 다리 커브 변화량 0.000).
        var f지난자리 = typeof(HeroAttack).GetField("지난자리", BindingFlags.NonPublic | BindingFlags.Instance);
        var f이동평활 = typeof(HeroAttack).GetField("이동평활", BindingFlags.NonPublic | BindingFlags.Instance);
        f지난자리?.SetValue(hero, hero.transform.position);
        f이동평활?.SetValue(hero, 0f);
        // 웅크림·몸yaw 같은 누적값도 0에서 시작하게 (지난 실행 결과를 입력으로 읽지 않게)
        foreach (var n in new[] { "웅크림", "무게", "내딛음", "몸yaw", "몸pitch" })
            typeof(HeroAttack).GetField(n, BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(hero, 0f);

        // ★★★**키는 3막 경계에만 찍는다** (2026-08-09 사용자 "말도안돼 시발;; …" — 모든
        //   프레임에 키가 박혀 있어서 한 자세를 고쳐도 옆 프레임이 그대로라 아무 데도 안 퍼진다.
        //   31프레임을 다 고쳐야 하니 사실상 못 고친다).
        //   모션 저작 규칙의 표준 5키 그대로: 시작 · 예비정점 · 오버슈트 · 되돌아옴 · 정지.
        //   사이는 **이징이 채운다** — 키가 많다고 좋아지지 않는다. 곡선만 울퉁불퉁해진다.
        float 총막 = hero.예비 + hero.휘두름 + hero.여운;
        var 키시각 = new List<float> { 0f, hero.예비, hero.예비 + hero.휘두름, 총막, 길이 };
        키시각.Sort();
        int 프레임 = 키시각.Count;
        // ★★★**각도로 굽는다. 쿼터니언으로 구우면 사람이 못 고친다** (2026-08-09 사용자
        //   "모션을 고칠수가없네"). 쿼터니언이면 애니메이션 창에 `Rotation.x .y .z .w` 네 줄이
        //   뜨는데 그건 손으로 만질 수 있는 숫자가 아니다. `localEulerAnglesRaw` 로 쓰면
        //   `Rotation X/Y/Z` 로 뜨고 도(°) 단위라 바로 만져진다.
        //   ☆대신 **연속성을 손으로 이어줘야** 한다 — 오일러는 ±180 을 넘을 때 튀어서,
        //     그대로 두면 그 프레임에 뼈가 한 바퀴 돈다.
        var 회전 = new Dictionary<string, AnimationCurve[]>();
        var 위치 = new Dictionary<string, AnimationCurve[]>();
        var 지난각 = new Dictionary<string, Vector3>();
        foreach (var n in 뼈.Keys)
        {
            회전[n] = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
            위치[n] = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
        }

        float 총 = 총막;
        // ★자세를 만들려면 절차 모션이 **시간을 따라 흘러야** 한다 (웅크림·몸yaw 가 누적값이라
        //   건너뛰면 값이 안 자란다). 그래서 계산은 30fps 로 다 돌리고, **키만 골라 찍는다.**
        int 잔프레임 = Mathf.RoundToInt(길이 * 초당) + 1;
        int 다음키 = 0;
        for (int f = 0; f < 잔프레임; f++)
        {
            float ph = f / (float)(잔프레임 - 1);          // 0~1
            float 초 = ph * 길이;

            // ① 걷기와 안 싸우게 **정지 자세로 되돌린 뒤** 그 위에 얹는다
            //    (`몸통스윙` 은 곱해서 얹으므로 안 되돌리면 프레임마다 누적된다)
            //
            // ★★★**몸 오브젝트에 찍어야 한다** (2026-08-09). `정지_여자` 의 커브 경로는
            //   `Armature/Hips/…` 라 캐릭터 뿌리에 찍으면 **하나도 안 걸린다.** 그러면
            //   되돌림이 없는 채로 웅크림이 31프레임 내내 쌓여 골반이 60단위(약 86cm)
            //   가라앉고 발이 55cm 떠 버린다 — 실제로 그렇게 구워져서 다리가 쫙 벌어졌다.
            기본.SampleAnimation(an.gameObject, 0f);

            // ② 3막 어디쯤인지 — 실제 상태 기계와 같은 비율
            object 상태; float 상태t; float 침; float 든정도;
            if (초 < hero.예비)
            {
                상태 = System.Enum.Parse(상태형, "예비"); 상태t = 초;
                침 = 0f; 든정도 = Mathf.SmoothStep(0f, 1f, 초 / Mathf.Max(0.01f, hero.예비));
            }
            else if (초 < hero.예비 + hero.휘두름)
            {
                상태 = System.Enum.Parse(상태형, "휘두름"); 상태t = 초 - hero.예비;
                float u = Mathf.Clamp01(상태t / Mathf.Max(0.01f, hero.휘두름));
                침 = u * u; 든정도 = 1f;
            }
            else if (초 < 총)
            {
                상태 = System.Enum.Parse(상태형, "여운"); 상태t = 초 - hero.예비 - hero.휘두름;
                침 = 1f; 든정도 = 1f;
            }
            else
            {   // 여운이 끝난 뒤 남은 구간에서 평소 자세로 풀린다
                상태 = System.Enum.Parse(상태형, "쉼"); 상태t = 0f;
                침 = 1f;
                든정도 = 1f - Mathf.SmoothStep(0f, 1f, (초 - 총) / Mathf.Max(0.01f, 길이 - 총));
            }

            f상태.SetValue(hero, 상태); f시각.SetValue(hero, 상태t);
            hold.목표 = 든정도; hold.침 = 침; f지금.SetValue(hold, 든정도);

            // ③ 몸통 먼저(운동 사슬), 그다음 팔 — 실제 실행 순서와 같게
            //   ★자리는 안 움직이니 매 프레임 지난자리를 지금 자리로 못박는다 (속도 0)
            f지난자리?.SetValue(hero, hero.transform.position);
            m몸통.Invoke(hero, new object[] { 1f / 초당 });
            m잡기.Invoke(hold, null);

            // ④ 뜬 자세를 그대로 받아 적는다
            // ④ **키로 남길 시각일 때만** 받아 적는다
            bool 찍나 = 다음키 < 키시각.Count &&
                        (초 >= 키시각[다음키] - 0.5f / 초당 || f == 잔프레임 - 1);
            if (!찍나) continue;
            float 키초 = 키시각[Mathf.Min(다음키, 키시각.Count - 1)];
            다음키++;

            foreach (var kv in 뼈)
            {
                var e = kv.Value.localEulerAngles; var p = kv.Value.localPosition;
                // 이어 붙이기 — 지난 키에서 가장 가까운 쪽으로 ±360 을 더한다
                if (지난각.TryGetValue(kv.Key, out var 앞))
                    for (int i = 0; i < 3; i++) e[i] = 앞[i] + Mathf.DeltaAngle(앞[i], e[i]);
                지난각[kv.Key] = e;

                var rc = 회전[kv.Key]; var pc = 위치[kv.Key];
                rc[0].AddKey(키초, e.x); rc[1].AddKey(키초, e.y); rc[2].AddKey(키초, e.z);
                pc[0].AddKey(키초, p.x); pc[1].AddKey(키초, p.y); pc[2].AddKey(키초, p.z);
            }
        }

        // 원래대로 되돌린다 (씬에 공격 자세가 눌러붙지 않게)
        기본.SampleAnimation(an.gameObject, 0f);
        hold.목표 = 0f; hold.침 = 0f; f지금.SetValue(hold, 0f);
        f상태.SetValue(hero, System.Enum.Parse(상태형, "쉼")); f시각.SetValue(hero, 0f);

        // ★★★**있는 파일을 갈아끼운다. 지우고 새로 만들지 않는다** (2026-08-09 사용자
        //   "사라졌잖아 공격이 또"). `DeleteAsset` + `CreateAsset` 을 하면 GUID 가 바뀌어
        //   **애니메이터 컨트롤러가 물고 있던 참조가 끊긴다** — 목록에서 통째로 사라진다.
        //   (모델을 새 이름으로 임포트하면 안 되는 것과 같은 함정)
        string 저장 = $"Assets/Game/Animations/공격_{뒷말}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(저장);
        bool 새로만듦 = clip == null;
        if (새로만듦) clip = new AnimationClip();
        else clip.ClearCurves();
        clip.frameRate = 초당;
        foreach (var kv in 뼈)
        {
            string path = 길[kv.Key];
            // ★안 움직이는 뼈는 커브를 아예 안 넣는다 — 목록이 지저분하면 고칠 것을 못 찾는다.
            //   빠진 뼈는 걷기 클립이 계속 갖는다.
            if (변화(회전[kv.Key]) < 0.5f) continue;
            for (int i = 0; i < 3; i++)
            {
                매끈(회전[kv.Key][i]);
                // `localEulerAnglesRaw` = 애니메이션 창에 `Rotation X/Y/Z` (도 단위)로 뜬다
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), "localEulerAnglesRaw." + "xyz"[i]), 회전[kv.Key][i]);
            }
            // 위치는 골반만 (나머지 뼈는 위치가 안 변한다 — 커브를 넣으면 파일만 커진다)
            if (kv.Key == "Hips")
                for (int i = 0; i < 3; i++)
                {
                    매끈(위치[kv.Key][i]);
                    AnimationUtility.SetEditorCurve(clip,
                        EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition." + "xyz"[i]), 위치[kv.Key][i]);
                }
        }

        if (새로만듦) AssetDatabase.CreateAsset(clip, 저장);
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();

        // 컨트롤러에 `공격` 상태로 넣어 둔다 — 애니메이션 창 목록에 뜨라고.
        // ★전이는 안 잇는다. 재생은 `HeroAttack` 이 판정과 같은 시계로 직접 한다.
        var ctrl = an.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        if (ctrl != null && ctrl.layers.Length > 0)
        {
            var sm = ctrl.layers[0].stateMachine;
            UnityEditor.Animations.AnimatorState 자리 = null;
            foreach (var cs in sm.states) if (cs.state.name == "공격") 자리 = cs.state;
            if (자리 == null) 자리 = sm.AddState("공격", sm.entryPosition + new Vector3(320f, 180f, 0f));
            자리.motion = clip; 자리.writeDefaultValues = false;
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
        }

        var 붙일곳 = Object.FindFirstObjectByType<HeroAttack>();
        if (붙일곳 != null) { 붙일곳.공격클립 = clip; EditorUtility.SetDirty(붙일곳); }

        Selection.activeGameObject = an.gameObject;      // 애니메이션 창이 바로 잡게
        Debug.Log($"[공격클립] {저장} — 뼈 {뼈.Count}개 · {프레임}프레임 · {길이:F2}초 · " +
                  $"컨트롤러 {(ctrl != null ? ctrl.name : "(없음)")} 에 「공격」 상태로 넣었다. " +
                  $"`{an.gameObject.name}` 를 선택한 채 애니메이션 창에서 고치면 된다.");
    }

    static string 경로(Transform 뿌리, Transform t)
    {
        var s = t.name;
        for (var p = t.parent; p != null && p != 뿌리; p = p.parent) s = p.name + "/" + s;
        return s;
    }

    static void 매끈(AnimationCurve c) { for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f); }

    /// 세 축 중 가장 크게 움직인 폭 (°)
    static float 변화(AnimationCurve[] cs)
    {
        float 큰 = 0f;
        foreach (var c in cs)
        {
            float mn = float.MaxValue, mx = float.MinValue;
            foreach (var k in c.keys) { mn = Mathf.Min(mn, k.value); mx = Mathf.Max(mx, k.value); }
            if (c.length > 0) 큰 = Mathf.Max(큰, mx - mn);
        }
        return 큰;
    }
}
