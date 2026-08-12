using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// 맨손 몸짓(오른주먹·왼주먹·발차기)을 **애니메이션 클립으로 굽는다**.
///
/// ★★왜 (2026-08-12 사용자 "맨손공격 펀치와 발차기 만들어달라고했고 애니메이션 … 왜
///   안들어와있어? 제발 모션은 제작하면 좀 넣어주면안돼?").
///   맞는 지적이다. 주먹·발차기를 **절차 모션으로만** 만들어 놓고(`HeroHold.지르기` ·
///   `HeroAttack.차기허벅/차기정강/차기발목`) 클립을 안 구웠다. 그래서 애니메이션 창을
///   열어도 고칠 것이 없었다 — 9-2 의 「만들어져 있는데 부르는 데가 없다」와 같은 꼴이다.
///
/// ★**빈 클립을 만들지 않는다.** 지금 절차 자세가 이미 3막으로 짜여 있으니 그걸 떠서
///   시작점으로 준다 (`공격클립굽기` 와 같은 사상 — 맨바닥에서 각도를 지어내지 않는다).
///
/// ★길이는 정확히 1.00초. 공속은 클립 배속으로 낸다 (`공격_*` 과 같은 규칙).
public static class 맨손클립굽기
{
    const int 초당 = 30;
    const float 길이 = 1.00f;
    /// 솎아낼 때 허용하는 어긋남 — 이보다 적게 어긋나는 중간 키는 지운다.
    /// ★키우면 키가 줄고 그림이 뭉툭해진다. 줄이면 그림은 그대로고 키가 는다.
    const float 각도허용 = 0.75f;    // °
    const float 자리허용 = 0.002f;   // m (2mm)

    // 굽는 뼈 — 맨손 몸짓이 실제로 건드리는 것만. 나머지는 걷기 클립이 계속 갖는다
    static readonly string[] 뼈들 = {
        "Hips", "Spine02", "Spine01", "Spine", "Head",
        "RightShoulder", "RightArm", "RightForeArm",
        "LeftShoulder", "LeftArm", "LeftForeArm",
        "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot",
    };

    /// `HeroAttack.맨손짓` 과 **같은 번호**다 (0 = 오른주먹 · 1 = 왼주먹 · 2 = 발차기).
    /// 번호가 어긋나면 굽은 그림과 실제로 나가는 몸짓이 달라진다.
    /// ★왼주먹(1)은 은퇴했다 (2026-08-12 사용자 "왼손은 지워주고") — 목록에서 뺐다.
    ///   `HeroAttack` 쪽 각도와 코드는 남아 있다 (은퇴는 삭제가 아니라 스위치).
    static readonly (int 짓, string 이름)[] 몸짓들 = { (0, "주먹오른"), (2, "발차기") };

    /// 스크립트가 부를 때만 켠다 — 덮어쓰기 확인창을 건너뛴다.
    /// ★사람이 메뉴로 누를 때는 늘 묻는다 (손으로 고친 것이 날아가는 걸 막는 게 그 창의 일이다).
    public static bool 묻지않기;

    [MenuItem("Tools/토이라/맨손 몸짓을 클립으로 굽기 (전부)")]
    public static void 굽기() => 굽기(몸짓들);

    /// ★**발차기만 다시 굽는 길** (2026-08-12 사용자 "내가 오른손 펀치는 고쳤으니까 건드리지마").
    ///   손으로 고친 클립을 지키려면 「전부 굽기」를 누르면 안 된다. 그래서 하나만 굽는 문을 냈다.
    [MenuItem("Tools/토이라/발차기만 다시 굽기 (오른주먹은 안 건드림)")]
    public static void 발차기만굽기() => 굽기(new[] { (2, "발차기") });

    public static void 굽기((int 짓, string 이름)[] 굽을것)
    {
        var hero = Object.FindFirstObjectByType<HeroAttack>();
        if (hero == null) { Debug.LogError("[맨손클립] 씬에 HeroAttack 이 없다."); return; }
        var hold = hero.GetComponent<HeroHold>();
        if (hold == null) { Debug.LogError("[맨손클립] HeroHold 가 없다."); return; }
        var an = hero.GetComponentInChildren<Animator>();
        if (an == null) { Debug.LogError("[맨손클립] 켜져 있는 몸이 없다."); return; }

        string 뒷말 = an.transform.name.Contains("여자") ? "여자" : "남자";
        var 기본 = 클립찾기($"정지_{뒷말}");
        if (기본 == null) { Debug.LogError($"[맨손클립] 기준이 될 정지_{뒷말} 클립이 없다."); return; }

        // ★★**이미 있는 클립은 통째로 덮어쓴다 — 손으로 고친 것이 다 날아간다.** 한 번 물어본다.
        //   (`공격클립굽기` 와 같은 이유 — 애니메이션 창에서 고치기 시작한 뒤에 무심코 다시
        //    누르면 하루치가 사라진다)
        foreach (var m in 굽을것)
        {
            if (묻지않기) break;
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Game/Animations/{m.이름}_{뒷말}.anim") == null) continue;
            var 목록 = string.Join(" · ", System.Array.ConvertAll(굽을것, x => x.이름));
            if (!EditorUtility.DisplayDialog("맨손 몸짓 다시 굽기",
                    $"다시 구울 것: {목록}\n\n애니메이션 창에서 손으로 고친 것이 **전부 사라진다.**\n그래도 굽나?",
                    "굽는다 (수정 버림)", "그만둔다"))
                return;
            break;
        }

        // 뼈 잡기 — ★★★경로는 **애니메이터가 붙은 몸 기준**이다. 캐릭터 뿌리 기준으로 구우면
        //   애니메이션 창이 아무것도 못 잡는다 (다른 클립들이 전부 `Armature/…` 인 이유).
        //
        // ★★★★**뼈를 골라 담지 않는다 — 전부 담는다** (2026-08-12 사용자 "키프레임 전부다
        //   찍어서 전체 싹다 모든 부위 모든키프레임").
        //   ☆옛 방식은 미리 정한 17개 목록만 담고, 그중에서도 0.5° 미만으로 움직인 뼈는
        //     뺐다. 그래서 「왜 이 뼈는 애니메이션 창에 없냐」가 계속 생겼다.
        //   ☆이제 `Armature` 아래 **모든 뼈**를 담는다. 안 움직이는 뼈도 커브가 생기므로
        //     애니메이션 창에서 바로 잡고 고칠 수 있다.
        var 뼈 = new Dictionary<string, Transform>();
        var 길 = new Dictionary<string, string>();
        Transform 뿌리뼈 = null;
        foreach (var t in an.GetComponentsInChildren<Transform>(true))
            if (t.name == "Armature") { 뿌리뼈 = t; break; }
        var 담을것 = 뿌리뼈 != null ? 뿌리뼈.GetComponentsInChildren<Transform>(true)
                                    : an.GetComponentsInChildren<Transform>(true);
        foreach (var t in 담을것)
        {
            if (t == an.transform || 뼈.ContainsKey(t.name)) continue;
            뼈[t.name] = t; 길[t.name] = 뼈길(an.transform, t);
        }
        if (뼈.Count == 0) { Debug.LogError("[맨손클립] 뼈를 하나도 못 찾았다."); return; }

        // 절차 모션을 손으로 돌리기 위한 준비 (전부 private 이라 리플렉션)
        const BindingFlags 숨 = BindingFlags.NonPublic | BindingFlags.Instance;
        var f지금       = typeof(HeroHold).GetField("<지금>k__BackingField", 숨);
        var f지르기지금 = typeof(HeroHold).GetField("지르기지금", 숨);
        var m잡기       = typeof(HeroHold).GetMethod("LateUpdate", 숨);
        var f상태       = typeof(HeroAttack).GetField("state", 숨);
        var f시각       = typeof(HeroAttack).GetField("t", 숨);
        var f든자세     = typeof(HeroAttack).GetField("드는자세", 숨);
        var m몸통       = typeof(HeroAttack).GetMethod("몸통스윙", 숨);
        var 상태형      = typeof(HeroAttack).GetNestedType("State", BindingFlags.NonPublic);
        var f맨손짓     = typeof(HeroAttack).GetField("맨손짓", 숨);
        var f지난맨손짓 = typeof(HeroAttack).GetField("지난맨손짓", 숨);
        var f지난state  = typeof(HeroAttack).GetField("지난state", 숨);
        var f가드       = typeof(HeroAttack).GetField("가드", 숨);      // 킥표가 정하는 팔 가드
        var f킥t        = typeof(HeroAttack).GetField("킥t", 숨);       // 킥표 시계 — 굽기 시작마다 0 으로
        if (f지금 == null || f지르기지금 == null || m잡기 == null || f상태 == null || m몸통 == null
            || 상태형 == null || f맨손짓 == null || f지난state == null)
        { Debug.LogError("[맨손클립] 절차 모션 내부를 못 잡았다 — 코드가 바뀌었으면 이 도구도 고쳐야 한다."); return; }
        f든자세.SetValue(hero, hold);

        var f지난자리 = typeof(HeroAttack).GetField("지난자리", 숨);
        var f이동평활 = typeof(HeroAttack).GetField("이동평활", 숨);

        // ★★★★**매 프레임 뜨고, 그다음 줄인다** (2026-08-12 사용자 "키프레임도 띠엄띠엄이고,
        //   각동작별로 계산해서 하나하나 프레임 찍어줘야 하지 않을까? 발차기가 발이 안움직이네").
        //
        //   ☆**왜 옛 방식(3막 경계 5키)이 틀렸나** — 맨손 몸짓 세기는 `짓세기 = sin(u·π)` 라
        //     **휘두름 가운데서 최대고 양 끝에서 정확히 0**이다. 경계에만 찍으면 **주먹·발차기
        //     기여가 0인 순간만** 떠서 무기 스윙 자세가 그대로 구워진다.
        //     실측: 세 클립의 `RightArm.x` 키가 전부 14.0/-36.8/-21.0/-20.8 로 **똑같았고**,
        //     `RightUpLeg.x` 는 폭 1.3° 라 **발이 아예 안 움직였다.**
        //     `공격클립굽기` 가 5키로 됐던 건 무기 스윙의 yaw·pitch 가 막마다 단조롭기 때문이다.
        //     봉우리가 막 **안쪽**에 있는 곡선은 경계만 찍으면 봉우리를 통째로 놓친다.
        //
        //   ☆**그렇다고 31키를 그대로 두지도 않는다** (2026-08-09 사용자 "말도안돼 시발;;" —
        //     모든 프레임에 키가 박히면 한 자세를 고쳐도 옆 프레임이 그대로라 못 고친다).
        //     → 매 프레임 떠 놓고, **곡선 모양이 거의 안 변하는 중간 키만 지운다.**
        //       봉우리·꺾이는 자리는 남으므로 그림은 그대로고, 손댈 키는 확 준다.
        var 구운것 = new List<AnimationClip>();
        foreach (var 몸짓 in 굽을것)
            구운것.Add(한벌굽기(몸짓.짓, $"{몸짓.이름}_{뒷말}", hero, hold, an, 기본, 뼈, 길,
                             f지금, f지르기지금, m잡기, f상태, f시각, m몸통, 상태형,
                             f맨손짓, f지난맨손짓, f지난state, f지난자리, f이동평활, f가드, f킥t));

        // 원래대로 되돌린다 (씬에 공격 자세가 눌러붙지 않게)
        기본.SampleAnimation(an.gameObject, 0f);
        hold.목표 = 0f; hold.침 = 0f; hold.지르기 = 0f; hold.왼주먹 = false;
        f지금.SetValue(hold, 0f); f지르기지금.SetValue(hold, 0f);
        f상태.SetValue(hero, System.Enum.Parse(상태형, "쉼")); f시각.SetValue(hero, 0f);

        // 컨트롤러의 **공격층·공격하체층 둘 다**에 상태로 넣는다 — 애니메이션 창 목록에 뜨라고.
        // ★`공격`·`갈무리` 가 이미 두 층에 다 있다. 같은 자리에 나란히 둔다.
        // ★전이는 안 잇는다. 재생은 `HeroAttack` 이 판정과 같은 시계로 직접 한다.
        var ctrl = an.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        int 넣은수 = 0;
        if (ctrl != null)
            foreach (var L in ctrl.layers)
            {
                if (L.name != "공격층" && L.name != "공격하체층") continue;
                for (int i = 0; i < 굽을것.Length; i++)
                {
                    if (구운것[i] == null) continue;
                    var sm = L.stateMachine;
                    UnityEditor.Animations.AnimatorState 자리 = null;
                    foreach (var cs in sm.states) if (cs.state.name == 굽을것[i].이름) 자리 = cs.state;
                    if (자리 == null) 자리 = sm.AddState(굽을것[i].이름, sm.entryPosition + new Vector3(320f, 260f + i * 70f, 0f));
                    자리.motion = 구운것[i]; 자리.writeDefaultValues = false;
                    넣은수++;
                }
                EditorUtility.SetDirty(ctrl);
            }
        AssetDatabase.SaveAssets();

        // 굽은 클립을 `HeroAttack` 에 꽂아 둔다 — ★이게 있어야 재생 쪽이 찾는다 (9-2)
        var so = new SerializedObject(hero);
        for (int i = 0; i < 굽을것.Length; i++)
        {
            var pr = so.FindProperty(굽을것[i].이름 + "클립");
            if (pr != null) pr.objectReferenceValue = 구운것[i];
        }
        so.ApplyModifiedProperties();

        Selection.activeGameObject = an.gameObject;      // 애니메이션 창이 바로 잡게
        Debug.Log($"[맨손클립] {구운것.Count}벌 구웠다 (뼈 {뼈.Count}개 · {길이:F2}초) — "
                + $"컨트롤러 상태 {넣은수}자리. `{an.gameObject.name}` 를 고른 채 애니메이션 창에서 고치면 된다.");
    }

    /// 몸짓 하나를 굽는다. `공격클립굽기` 의 한 바퀴와 같되 **맨손짓을 못박는다.**
    static AnimationClip 한벌굽기(
        int 짓, string 이름, HeroAttack hero, HeroHold hold, Animator an, AnimationClip 기본,
        Dictionary<string, Transform> 뼈, Dictionary<string, string> 길,
        FieldInfo f지금, FieldInfo f지르기지금, MethodInfo m잡기, FieldInfo f상태, FieldInfo f시각,
        MethodInfo m몸통, System.Type 상태형, FieldInfo f맨손짓, FieldInfo f지난맨손짓,
        FieldInfo f지난state, FieldInfo f지난자리, FieldInfo f이동평활, FieldInfo f가드, FieldInfo f킥t)
    {
        // ★★이동 평활값을 재우고 시작한다 — 첫 프레임에 속도가 튀면 `목표웅크림` 이 0으로
        //   눌려 **다리가 하나도 안 굽은 채로** 구워진다 (`공격클립굽기` 가 겪은 그대로).
        f지난자리?.SetValue(hero, hero.transform.position);
        f이동평활?.SetValue(hero, 0f);
        // ★★킥표 시계를 0 으로 되돌린다 — 실행 중엔 `맨손짓뽑기` 가 하는 일이다.
        //   안 하면 앞 몸짓을 굽던 시계가 이어져 **발차기가 이미 끝난 자세로** 구워진다.
        f킥t?.SetValue(hero, 0f);
        foreach (var n in new[] { "웅크림", "무게", "내딛음", "몸yaw", "몸pitch", "무릎듦", "뻗음정도", "가드", "킥젖힘값" })
            typeof(HeroAttack).GetField(n, BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(hero, 0f);

        var 회전 = new Dictionary<string, AnimationCurve[]>();
        var 위치 = new Dictionary<string, AnimationCurve[]>();
        var 지난각 = new Dictionary<string, Vector3>();
        foreach (var n in 뼈.Keys)
        {
            회전[n] = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
            위치[n] = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
        }

        float 총 = hero.예비 + hero.휘두름 + hero.여운;
        // ★자세를 만들려면 절차 모션이 **시간을 따라 흘러야** 한다 (누적값이 있다).
        //   계산은 30fps 로 다 돌리고 **키만 골라 찍는다.**
        int 잔프레임 = Mathf.RoundToInt(길이 * 초당) + 1;
        for (int f = 0; f < 잔프레임; f++)
        {
            float 초 = f / (float)(잔프레임 - 1) * 길이;

            // ① 걷기와 안 싸우게 **정지 자세로 되돌린 뒤** 그 위에 얹는다
            //    (절차 쪽이 곱해서 얹으므로 안 되돌리면 프레임마다 누적된다)
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
            {
                상태 = System.Enum.Parse(상태형, "쉼"); 상태t = 0f;
                침 = 1f; 든정도 = 1f - Mathf.SmoothStep(0f, 1f, (초 - 총) / Mathf.Max(0.01f, 길이 - 총));
            }

            f상태.SetValue(hero, 상태); f시각.SetValue(hero, 상태t);
            // ★★★**몸짓을 못박는다.** `몸통스윙` 은 상태가 바뀌는 순간 몸짓을 랜덤으로 뽑는다
            //   (`state != 지난state` 일 때). `지난state` 를 지금 상태로 맞춰 두면 그 뽑기가
            //   안 돌고, 우리가 넣은 번호가 그대로 쓰인다. 안 그러면 **구울 때마다 다른 몸짓이
            //   나와** 「주먹오른」 클립에 발차기가 구워진다.
            f지난state.SetValue(hero, 상태);
            f맨손짓.SetValue(hero, 짓);
            f지난맨손짓?.SetValue(hero, 짓);

            // ★발차기는 팔이 몽둥이 아크를 안 그린다 — 「든 자세」를 가드로 빌려 쓸 뿐이라
            //   침(내려침)은 0 이고, 얼마나 들지는 **몸통스윙이 정한 뒤에** 아래에서 넣는다.
            if (짓 == 2) { 든정도 = 0f; 침 = 0f; }

            hold.목표 = 든정도; hold.침 = 침;
            f지금.SetValue(hold, 든정도);

            // ③ 몸통 먼저(운동 사슬), 그다음 팔 — 실제 실행 순서와 같게
            //   ★자리는 안 움직이니 매 프레임 지난자리를 지금 자리로 못박는다 (속도 0)
            f지난자리?.SetValue(hero, hero.transform.position);
            m몸통.Invoke(hero, new object[] { 1f / 초당 });

            // ★★**킥표가 정한 가드를 팔에 넣는다** — `HeroAttack` 이 실행 중에 하는 것과 같게
            //   (거기선 `드는자세.목표 = 가드 * 가드세기`). 이 줄이 없으면 찰 때 팔이
            //   축 늘어진 채로 구워진다. 반드시 `몸통스윙` **뒤**여야 한다 — 가드값을 거기서 정한다.
            if (짓 == 2 && f가드 != null)
            {
                float g = Mathf.Clamp01((float)f가드.GetValue(hero) * hero.가드세기);
                hold.목표 = g; hold.침 = 0f; f지금.SetValue(hold, g);
            }

            // ★★`몸통스윙` 이 방금 정한 주먹 세기를 **그대로 못박는다.** 편집 모드에선
            //   `Time.deltaTime` 이 못 믿을 값이라 `HeroHold` 의 평활이 안 자란다 —
            //   그냥 두면 팔이 하나도 안 뻗은 채로 구워진다.
            f지르기지금.SetValue(hold, Mathf.Clamp01(hold.지르기));
            m잡기.Invoke(hold, null);

            // ④ **매 프레임 받아 적는다** — 솎아내기는 다 뜨고 난 뒤에 한다 (위 주석)
            float 키초 = 초;

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

        // ★★★**있는 파일을 갈아끼운다. 지우고 새로 만들지 않는다** — `DeleteAsset` +
        //   `CreateAsset` 은 GUID 를 바꿔 컨트롤러가 물던 참조를 끊는다 (목록에서 사라진다).
        string 저장 = $"Assets/Game/Animations/{이름}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(저장);
        bool 새로만듦 = clip == null;
        if (새로만듦) clip = new AnimationClip();
        else clip.ClearCurves();
        clip.frameRate = 초당;
        clip.name = 이름;

        // ★★**솎아내지 않는다. 모든 뼈에 모든 프레임을 찍는다** (2026-08-12 사용자 지시).
        //   ☆직전엔 곡선 모양이 안 변하는 중간 키를 지웠는데(RDP), 그러면 뼈마다 키 시각이
        //     달라지고 「왜 여긴 키가 없냐」가 생긴다. 이제 31프레임을 그대로 남긴다.
        int 넣은뼈 = 0, 남긴키 = 0;
        foreach (var kv in 뼈)
        {
            넣은뼈++;
            string path = 길[kv.Key];
            for (int i = 0; i < 3; i++)
            {
                남긴키 += 회전[kv.Key][i].length;
                매끈(회전[kv.Key][i]);
                // `localEulerAnglesRaw` = 애니메이션 창에 `Rotation X/Y/Z` (도 단위)로 뜬다.
                // 쿼터니언으로 구우면 `.x .y .z .w` 네 줄이라 **사람이 못 고친다.**
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

        var 설정 = AnimationUtility.GetAnimationClipSettings(clip);
        설정.loopTime = false;              // 한 방 동작이다 — 되풀이는 코드가 시계로 돌린다
        AnimationUtility.SetAnimationClipSettings(clip, 설정);

        if (새로만듦) AssetDatabase.CreateAsset(clip, 저장);
        EditorUtility.SetDirty(clip);
        Debug.Log($"[맨손클립] {저장} — 뼈 {넣은뼈}개 전부 · 회전 키 {남긴키}개 (프레임마다 다 찍음)");
        return clip;
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

    static string 뼈길(Transform 뿌리, Transform t)
    {
        var s = t.name;
        for (var p = t.parent; p != null && p != 뿌리; p = p.parent) s = p.name + "/" + s;
        return s;
    }

    static void 매끈(AnimationCurve c) { for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f); }

    /// ★★**곡선 모양을 지키면서 중간 키만 솎아낸다** (Ramer–Douglas–Peucker).
    ///   양 끝은 늘 남기고, **지워도 직선보간으로 `허용` 안쪽에 들어오는** 키만 지운다.
    ///   ☆봉우리와 꺾이는 자리는 **반드시 남는다** — 거기가 지웠을 때 제일 크게 어긋나는
    ///     자리라서 알고리즘이 먼저 집어낸다. 발차기 정점이 날아갈 일이 없다.
    ///   ☆같은 입력이면 결과가 늘 같다 (뽑기가 아니다).
    static void 줄이기(AnimationCurve c, float 허용)
    {
        if (c.length <= 2) return;
        var 원본 = c.keys;
        var 남길 = new bool[원본.Length];
        남길[0] = 남길[원본.Length - 1] = true;
        솎기(원본, 0, 원본.Length - 1, 허용, 남길);

        var 새키 = new List<Keyframe>();
        for (int i = 0; i < 원본.Length; i++) if (남길[i]) 새키.Add(원본[i]);
        c.keys = 새키.ToArray();
    }

    /// 두 끝을 직선으로 이었을 때 **가장 크게 어긋나는 키**를 남기고, 좌우를 다시 따진다
    static void 솎기(Keyframe[] k, int a, int b, float 허용, bool[] 남길)
    {
        if (b - a < 2) return;
        float t0 = k[a].time, t1 = k[b].time, v0 = k[a].value, v1 = k[b].value;
        float 최대 = -1f; int 최대i = -1;
        for (int i = a + 1; i < b; i++)
        {
            float u = Mathf.Approximately(t1, t0) ? 0f : (k[i].time - t0) / (t1 - t0);
            float 어긋남 = Mathf.Abs(k[i].value - Mathf.Lerp(v0, v1, u));
            if (어긋남 > 최대) { 최대 = 어긋남; 최대i = i; }
        }
        if (최대i < 0 || 최대 < 허용) return;      // 사이가 전부 직선 안쪽 — 통째로 지운다
        남길[최대i] = true;
        솎기(k, a, 최대i, 허용, 남길);
        솎기(k, 최대i, b, 허용, 남길);
    }

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
