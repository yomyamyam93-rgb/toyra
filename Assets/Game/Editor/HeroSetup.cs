using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// 캐릭터 넣기 — 뼈대와 애니메이션이 든 모델을 캐릭터 자리에 물리고,
/// **동작을 섞어 주는 블렌드 트리**를 만든다.
///
/// ★파일마다 메시가 통째로 들어 있지만(각 10MB) **쓰는 건 클립뿐**이다.
///   몸은 하나만 쓰고 나머지 파일에서는 클립만 꺼내 온다 — 뼈 이름과 계층이 같아서
///   그대로 물린다.
///
/// ★「정지」 동작이 없어서 걷기의 첫 프레임을 굳혀 만든다. Meshy 에서 Idle 을
///   받으면 그걸로 바꾸면 된다.
public static class HeroSetup
{
    // ★★몸이 둘이 됐다 (2026-08-04). 뼈대가 **완전히 같아서**(24개 · 이름·계층이 한 글자도
    //   안 다름) 클립을 양쪽이 그대로 돌려 쓴다 — 컨트롤러도 아바타 빼고는 하나면 된다.
    //   ★확인하는 법: glb 의 `skins[0].joints` 이름을 늘어놓고 비교한다. 짐작하지 말 것.
    const string 폴더 = "Assets/Game/Models/hero";          // 여자 — 먼저 있던 몸
    const string 남자폴더 = "Assets/Game/Models/hero_man";  // 남자 — 2026-08-04 추가
    // ★원본을 쓴다. 폴리곤을 줄였더니 메시가 망가졌다 (2026-08-04) —
    //   반짝임의 원인은 폴리곤이 아니라 **테두리가 모델 안쪽까지 그어지던 것**이었다.
    //   엉뚱한 데를 건드린 것이므로 되돌린다.
    const string 몸파일 = 폴더 + "/Walking.glb";
    const string 남자몸파일 = 남자폴더 + "/Walking.glb";
    const string 저장 = "Assets/Game/Animations";

    /// 모델이 향한 쪽 보정 (°) — 마우스 쪽을 등지면 180 으로 바꾼다
    public static float 모델회전 = 180f;

    // ★★걸음 템포는 **몸이 아니라 게임이 정한다** (2026-08-04 사용자 "걷기 템포가 남자랑
    //   여자랑 달라, 여자 걷기 템포가 좋은데").
    //   받아온 클립 길이가 제각각이라(여자 걷기 1.000 · 남자 1.067초) 몸을 바꾸면 걸음이
    //   느려진 것처럼 보였다. 배속으로 눌러 **한 바퀴 도는 시간을 통일**한다.
    //   ★값은 여자 걷기에서 따왔다 — 그게 좋다고 했다.
    // ★웅크리기도 같이 맞춘다. 네 방향 클립이 1.17~1.30 으로 다 달라, 안 맞추면 방향을
    //   섞는 순간 발이 서로 어긋난다 (블렌드 트리는 길이를 알아서 안 맞춰 준다).
    const float 걷기주기 = 1.0f;
    const float 달리기주기 = 0.667f;
    const float 웅크리기주기 = 1.2f;

    /// 살색 재질 — glb 에 재질이 안 딸려 오면 분홍색(재질 없음)으로 뜬다
    static void 재질입히기(GameObject go)
    {
        const string path = "Assets/Game/Models/hero/살.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(sh) { name = "살" };
            var c = new Color(0.85f, 0.78f, 0.68f);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(m, path);
        }

        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var arr = r.sharedMaterials;
            bool 비었나 = arr.Length == 0;
            for (int i = 0; i < arr.Length; i++)
                if (arr[i] == null || arr[i].shader == null || arr[i].shader.name.Contains("Hidden")) 비었나 = true;
            if (!비었나) continue;

            var 새 = new Material[Mathf.Max(1, arr.Length)];
            for (int i = 0; i < 새.Length; i++) 새[i] = m;
            r.sharedMaterials = 새;
        }
    }

    [MenuItem("Tools/토이라기/㉤ 캐릭터 넣기", priority = 4)]
    public static void Run()
    {
        var 몸 = AssetDatabase.LoadAssetAtPath<GameObject>(몸파일);
        if (몸 == null) { Debug.LogError($"[캐릭터] {몸파일} 이 없다"); return; }

        if (!AssetDatabase.IsValidFolder(저장)) AssetDatabase.CreateFolder("Assets/Game", "Animations");

        // ── ★★★걷는 클립은 **그 몸의 파일에서 뽑는다** (2026-08-04 — 두 번 데였다).
        //
        //    ①이름: `hero/Walking.glb` 안의 동작 이름이 **`walking_man`** 이고, 진짜 여자
        //      걸음인 `Walking_Woman.glb` 는 **한 번도 안 쓰이고 있었다** (사용자 "남자 걷기
        //      모션을 그냥 여자캐릭터 걷기로 넣었니?").
        //    ②뼈대: 그래서 컨트롤러만 갈랐더니 이번엔 남자가 **여자 뼈대로 만든 클립**을 썼다
        //      (사용자 "남자는 걷기가 안먹어"). 두 뼈대를 실제로 재보니 **평균 10% 어긋난다** —
        //      Hips 만 해도 여 (0, 99.6, 3.2) ↔ 남 (0, 96.4, −1.35) 이다.
        //      이 클립들은 24개 뼈의 **위치까지 통째로 덮어쓰므로**, 남의 뼈대 클립을 걸면
        //      몸이 제 바인드 자세를 잃고 뭉개진다. 「돌긴 도는데 걷는 것 같지 않다」가 그것이다.
        //
        //    → 각자 제 폴더에서 뽑는다. 공유해도 되는 건 **뼈대가 만드는 형태와 무관한 것**뿐인데,
        //      위치 커브가 들어 있는 한 그런 클립은 없다.
        //    ★웅크리기·의자는 남자 파일에만 있다 — 여자는 빌려 쓴다 (아래 참고).

        // ── ★웅크리기 (2026-08-04) — 남자 쪽 파일에만 있다. **네 방향이 다 있어서**
        //    옆걸음을 합성할 필요가 없다 (서서 걷기는 옆걸음이 없어 만들어 쓴다).
        //    ★여자 몸에도 걸린다 (뼈 이름·계층이 같다) — 사용자가 요청한 「둘 다 적용」이 이것.
        //      단 위 ②의 10% 어긋남을 그대로 안는다. 여자판 웅크리기 클립을 받으면 그때 갈린다.
        var 웅앞 = 제자리로("웅크리기_앞", 클립찾기("Crouch_Forward.glb", 남자폴더));
        var 웅뒤 = 제자리로("웅크리기_뒤", 클립찾기("Crouch_Backward.glb", 남자폴더));
        var 웅왼 = 제자리로("웅크리기_왼쪽", 클립찾기("Crouch_Left.glb", 남자폴더));
        var 웅오 = 제자리로("웅크리기_오른쪽", 클립찾기("Crouch_Right.glb", 남자폴더));
        var 웅정지 = 웅앞 != null ? 굳히기("웅크린정지", 웅앞, 0f) : null;

        // ── ★의자 (2026-08-04 사용자 "의자같은데 앉기랑 일어서기") — **웅크리기와 다른 것**이다.
        //    실측: 웅크린 Hips 는 77~86 인데 앉기는 97→56 으로 **바닥 높이까지 내려간다.**
        //    ★앉기 끝(55.9)과 일어서기 시작(69.1)이 서로 안 맞는다 — 다른 데서 딴 동작이라
        //      그렇다. 사이를 0.2초 넘게 섞어 그 턱을 가린다 (아래 전이 참고).
        //    ★여기서는 **제자리로 안 만든다** — 앉으려면 실제로 뒤로 물러나 앉아야 한다.
        var 의자앉기클립 = 클립한번만("의자앉기", 클립찾기("Sit.glb", 남자폴더));
        var 의자일어서기클립 = 클립한번만("의자일어서기", 클립찾기("Stand.glb", 남자폴더));
        var 의자앉아있기 = 의자앉기클립 != null ? 굳히기("의자앉아있기", 의자앉기클립, 1f) : null;

        // ── 성별마다 컨트롤러 하나. 걷는 클립은 **각자 제 폴더에서**
        var 여자판 = 컨트롤러만들기("캐릭터_여자", 폴더, "Walking_Woman.glb",
                                    웅앞, 웅뒤, 웅왼, 웅오, 웅정지,
                                    의자앉기클립, 의자앉아있기, 의자일어서기클립);
        var 남자판 = 컨트롤러만들기("캐릭터_남자", 남자폴더, "Walking.glb",
                                    웅앞, 웅뒤, 웅왼, 웅오, 웅정지,
                                    의자앉기클립, 의자앉아있기, 의자일어서기클립);
        if (여자판 == null || 남자판 == null) return;

        // ── 씬의 캐릭터에 물리기
        붙이기(여자판, 남자판, 몸);
    }

    /// 컨트롤러 한 벌 — 걷기·뒤로걷기·달리기·정지·옆걸음을 **그 몸의 폴더에서** 뽑는다
    static AnimatorController 컨트롤러만들기(
        string 이름, string 몸폴더, string 걷기파일,
        AnimationClip 웅앞, AnimationClip 웅뒤, AnimationClip 웅왼, AnimationClip 웅오, AnimationClip 웅정지,
        AnimationClip 의자앉기클립, AnimationClip 의자앉아있기, AnimationClip 의자일어서기클립)
    {
        string 꼬리 = 이름.Substring(이름.IndexOf('_'));      // "_여자" / "_남자"

        var 걷기 = 제자리로("걷기" + 꼬리, 클립찾기(걷기파일, 몸폴더));
        if (걷기 == null) { Debug.LogError($"[캐릭터] {몸폴더}/{걷기파일} 의 클립을 못 찾았다"); return null; }
        var 뒤로 = 제자리로("뒤로걷기" + 꼬리, 클립찾기("Walk_Backward.glb", 몸폴더));
        var 달리기 = 제자리로("달리기" + 꼬리, 클립찾기("Running.glb", 몸폴더));
        var 정지 = 굳히기("정지" + 꼬리, 걷기, 0f);

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(저장 + "/" + 이름 + ".controller");
        ctrl.AddParameter("앞뒤", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("좌우", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("빠르기", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("앉기", AnimatorControllerParameterType.Bool);   // Ctrl — 웅크리기
        ctrl.AddParameter("의자", AnimatorControllerParameterType.Bool);   // 의자에 앉아 있나

        var sm = ctrl.layers[0].stateMachine;

        // ── 옆걸음은 **그 몸의 앞걷기**에서 만들어 낸다 (아래 「옆걸음만들기」 참고)
        var 옆오른 = 옆걸음만들기("옆걸음_오른쪽" + 꼬리, 클립찾기(걷기파일, 몸폴더), 90f);
        var 옆왼 = 옆걸음만들기("옆걸음_왼쪽" + 꼬리, 클립찾기(걷기파일, 몸폴더), -90f);

        // ── 걷기 섞기 (앞뒤 × 좌우) — 네 방향이 유기적으로 이어진다
        // ★★★`SimpleDirectional2D` 를 쓰면 안 된다 (2026-08-04 사용자 "또 걷기가 멈춰,
        //   앉았다 일어나면 풀리긴 하는데").
        //
        //   이 방식은 **가운데 모션이 없으면** 마주 보는 클립끼리 그냥 섞인다 —
        //   앞걷기 + 뒤로걷기, 왼걸음 + 오른걸음이 서로 상쇄돼 **자세가 통째로 굳는다.**
        //   방향을 뒤집을 때마다 그 자리를 지나가니 걸음이 툭툭 멈췄다.
        //   (Ctrl 로 웅크렸다 펴면 상태를 다시 들어가면서 잠깐 풀린 것이지 고쳐진 게 아니다)
        //
        //   → `FreeformDirectional2D` + **가운데에 정지**. 이 방식은 이동용으로 만들어진
        //     것이라 가운데를 기준으로 방향을 나누고, 마주 보는 클립을 상쇄시키지 않는다.
        var 걷기트리 = new BlendTree
        {
            name = "걷기섞기",
            blendType = BlendTreeType.FreeformDirectional2D,
            blendParameter = "좌우",
            blendParameterY = "앞뒤",
            useAutomaticThresholds = false
        };
        AssetDatabase.AddObjectToAsset(걷기트리, ctrl);
        // ★★가운데 클립을 넣지 않는다 (2026-08-05). 넣어 두면 「멈춤」을 방향 좌표로
        //   표현하게 되는데, 그 자리(원점 근처)가 바로 가중치가 정의되지 않는 곳이라
        //   상태 길이가 0 이 되고 `normalizedTime` 이 터진다. 멈춤은 아래 `속도섞기` 담당.
        걷기트리.AddChild(걷기, new Vector2(0f, 1f));                       // 앞
        걷기트리.AddChild(뒤로 != null ? 뒤로 : 걷기, new Vector2(0f, -1f)); // 뒤
        걷기트리.AddChild(옆오른 != null ? 옆오른 : 걷기, new Vector2(1f, 0f));  // 오른쪽
        걷기트리.AddChild(옆왼 != null ? 옆왼 : 걷기, new Vector2(-1f, 0f));     // 왼쪽

        // ── 속도 섞기 (빠르기) — 정지 ↔ 걷기 ↔ 달리기
        //   ★같은 트리를 두 번 자식으로 넣으면 안 된다 (2026-08-04 — 그래서 안 돌았다)
        var 속도트리 = new BlendTree
        {
            name = "속도섞기",
            blendType = BlendTreeType.Simple1D,
            blendParameter = "빠르기",
            useAutomaticThresholds = false
        };
        AssetDatabase.AddObjectToAsset(속도트리, ctrl);
        속도트리.AddChild(정지, 0f);
        속도트리.AddChild(걷기트리, 1f);
        속도트리.AddChild(달리기 != null ? (Motion)달리기 : 걷기트리, 2f);

        // 걷기 네 방향은 같은 주기로 (`템포맞춤` 주석 참고)
        템포맞춤(걷기트리, 걷기주기);
        // 달리기만 따로 — 「정지」는 한 자세를 굳힌 것이라 배속이 아무 뜻이 없다
        if (달리기 != null && 달리기.length > 0.001f)
        {
            var cs = 속도트리.children;
            for (int i = 0; i < cs.Length; i++)
                if (cs[i].motion == 달리기) cs[i].timeScale = 달리기.length / 달리기주기;
            속도트리.children = cs;
        }

        var st = sm.AddState("이동", new Vector3(300, 0, 0));
        st.motion = 속도트리;
        st.writeDefaultValues = true;
        sm.defaultState = st;

        // ── ★웅크려 걷기 섞기 — 서서 걷기와 **같은 앞뒤·좌우**를 쓴다.
        //    가운데(0,0)에 웅크린 정지를 두면 멈췄을 때 저절로 웅크린 채 선다.
        AnimatorState 웅크리기상태 = null;
        if (웅앞 != null)
        {
            var 웅크리기트리 = new BlendTree
            {
                name = "웅크리기섞기",
                blendType = BlendTreeType.FreeformDirectional2D,   // 걷기와 같은 이유
                blendParameter = "좌우",
                blendParameterY = "앞뒤",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(웅크리기트리, ctrl);
            // ★★가운데(멈춤)를 여기 넣지 않는다 (2026-08-05). 방향 블렌드는 좌표가
            //   원점이거나 아주 작으면 가중치가 정의되지 않아 **상태 길이가 0** 이 되고,
            //   그러면 `normalizedTime` 이 터져 애니메이션이 통째로 굳는다.
            //   → 멈춤은 **아래 속도 트리**가 맡는다. 방향은 늘 원 위에만 있으면 된다.
            //     서서 걷기(`속도섞기`)와 정확히 같은 구조다.
            웅크리기트리.AddChild(웅앞, new Vector2(0f, 1f));
            웅크리기트리.AddChild(웅뒤 != null ? 웅뒤 : 웅앞, new Vector2(0f, -1f));
            웅크리기트리.AddChild(웅오 != null ? 웅오 : 웅앞, new Vector2(1f, 0f));
            웅크리기트리.AddChild(웅왼 != null ? 웅왼 : 웅앞, new Vector2(-1f, 0f));
            템포맞춤(웅크리기트리, 웅크리기주기);   // 네 방향 길이가 1.17~1.30 으로 제각각이라

            // 웅크린 채 멈춤 ↔ 웅크려 걷기
            var 웅크린속도 = new BlendTree
            {
                name = "웅크린속도",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "빠르기",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(웅크린속도, ctrl);
            웅크린속도.AddChild(웅정지 != null ? (Motion)웅정지 : 웅앞, 0f);
            웅크린속도.AddChild(웅크리기트리, 1f);

            웅크리기상태 = sm.AddState("웅크리기", new Vector3(300, 140, 0));
            웅크리기상태.motion = 웅크린속도;
            웅크리기상태.writeDefaultValues = true;

            // Ctrl 을 누르는 동안만 (사용자 확정). 0.18초면 바뀌는 게 보이면서 안 굼뜨다
            전이(st, 웅크리기상태, "앉기", true, 0.18f);
            전이(웅크리기상태, st, "앉기", false, 0.18f);
        }

        // ── ★의자 — 이동 → 앉기 → 앉아있기 → 일어서기 → 이동
        if (의자앉기클립 != null && 의자일어서기클립 != null)
        {
            var 앉기상태 = sm.AddState("의자앉기", new Vector3(620, 0, 0));
            앉기상태.motion = 의자앉기클립; 앉기상태.writeDefaultValues = true;
            var 앉아있기상태 = sm.AddState("의자앉아있기", new Vector3(620, 100, 0));
            앉아있기상태.motion = (Motion)의자앉아있기 ?? 의자앉기클립; 앉아있기상태.writeDefaultValues = true;
            var 일어서기상태 = sm.AddState("의자일어서기", new Vector3(620, 200, 0));
            일어서기상태.motion = 의자일어서기클립; 일어서기상태.writeDefaultValues = true;

            전이(st, 앉기상태, "의자", true, 0.15f);
            끝나면(앉기상태, 앉아있기상태, 0.1f);
            // ★여기만 길게 섞는다 — 앉은 높이가 서로 달라서(56 ↔ 69) 짧으면 툭 튄다
            전이(앉아있기상태, 일어서기상태, "의자", false, 0.25f);
            끝나면(일어서기상태, st, 0.15f);
        }

        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    /// 두 몸을 캐릭터 밑에 세우고 각자의 컨트롤러를 물린다
    static void 붙이기(AnimatorController 여자판, AnimatorController 남자판, GameObject 몸)
    {
        var hero = Object.FindFirstObjectByType<Hero>();
        if (hero == null) { Debug.LogError("[캐릭터] 씬에 Hero 가 없다 — ① 씬 짓기 를 먼저"); return; }

        var 옛몸 = hero.transform.Find("몸");
        if (옛몸 != null) 옛몸.gameObject.SetActive(false);      // 상자는 지우지 않고 끈다
        var 코 = hero.transform.Find("코");
        if (코 != null) 코.gameObject.SetActive(false);
        foreach (var 이름 in new[] { "사람", "사람_여자", "사람_남자" })
        {
            var 있던것 = hero.transform.Find(이름);
            if (있던것 != null) Undo.DestroyObjectImmediate(있던것.gameObject);
        }

        // ★★두 몸을 **둘 다 세워 놓고 하나만 켠다** (2026-08-04 사용자 "단축키 하나 넣어줘
        //   누르면 전환될 수 있도록"). 런타임에 모델을 불러오는 길(Resources·Addressables)을
        //   내지 않아도 즉시 갈아끼워진다. 꺼진 몸은 그리지도 애니메이션하지도 않으므로 공짜다.
        // 키를 잴 때 쓸 「서 있는 자세」 — 몸마다 제 것으로 재야 화면에서 같은 키가 된다
        var 여자정지 = AssetDatabase.LoadAssetAtPath<AnimationClip>(저장 + "/정지_여자.anim");
        var 남자정지 = AssetDatabase.LoadAssetAtPath<AnimationClip>(저장 + "/정지_남자.anim");

        var 여자몸 = 몸세우기(hero, 몸, "사람_여자", 여자판, "캐릭터아바타", 여자정지);
        var 남자원본 = AssetDatabase.LoadAssetAtPath<GameObject>(남자몸파일);
        var 남자몸 = 남자원본 != null
            ? 몸세우기(hero, 남자원본, "사람_남자", 남자판, "캐릭터아바타_남자", 남자정지) : null;
        if (남자원본 == null) Debug.LogWarning($"[캐릭터] {남자몸파일} 이 없다 — 여자 몸만 세운다");

        if (남자몸 != null) 남자몸.SetActive(false);       // 시작은 먼저 있던 몸으로

        if (hero.GetComponent<HeroAnim>() == null) Undo.AddComponent<HeroAnim>(hero.gameObject);
        if (hero.GetComponent<HeroHold>() == null) Undo.AddComponent<HeroHold>(hero.gameObject);
        if (hero.GetComponent<HeroSwap>() == null) Undo.AddComponent<HeroSwap>(hero.gameObject);

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(hero.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(hero.gameObject.scene);
        Debug.Log("[캐릭터] 완성 — 걷기·달리기·웅크리기(Ctrl)·의자. F4 로 남녀 전환.");
    }

    /// 몸 하나를 캐릭터 밑에 세우고 애니메이터까지 물린다
    static GameObject 몸세우기(Hero hero, GameObject 원본, string 이름,
                               AnimatorController ctrl, string 아바타이름, AnimationClip 정지자세)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(원본, hero.transform);
        go.name = 이름;
        go.transform.localPosition = Vector3.zero;
        // ★모델이 향한 쪽이 유니티 기준(+Z)과 다르면 여기서 돌린다
        go.transform.localRotation = Quaternion.Euler(0f, 모델회전, 0f);
        키맞춤(go, hero.height, 정지자세);

        재질입히기(go);

        var anim = go.GetComponent<Animator>();
        if (anim == null) anim = go.AddComponent<Animator>();

        // ★★아바타가 없으면 애니메이터가 클립을 **한 프레임도 못 돌린다** (2026-08-04).
        //   glTF 로 들여온 모델에는 아바타가 안 딸려 오므로 여기서 만들어 붙인다.
        // ★몸마다 따로 만든다 — 뼈 이름은 같아도 **기본 자세(뼈 길이)가 달라서**,
        //   한 아바타를 둘이 쓰면 한쪽 몸이 다른 쪽 비율로 뒤틀린다.
        string 아바타경로 = 저장 + "/" + 아바타이름 + ".asset";
        var av = AvatarBuilder.BuildGenericAvatar(go, "");
        av.name = 아바타이름;
        // ★있던 것을 지우고 새로 만들지 않는다 — 지운 자리에 바로 만들면 참조가 끊긴 채
        //   남을 수 있다. 내용만 덮어쓰면 이미 걸린 참조가 그대로 산다.
        var 있던아바타 = AssetDatabase.LoadAssetAtPath<Avatar>(아바타경로);
        if (있던아바타 != null)
        {
            EditorUtility.CopySerialized(av, 있던아바타);
            EditorUtility.SetDirty(있던아바타);
            av = 있던아바타;
        }
        else AssetDatabase.CreateAsset(av, 아바타경로);
        anim.avatar = av;

        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;                             // 이동은 코드가 한다
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        return go;
    }

    // ── 애니메이터 전이를 짧게 쓰기 위한 도우미 ─────────────────────────
    /// 트리 안의 클립들이 **목표 시간에 한 바퀴** 돌도록 배속을 맞춘다.
    /// (자식이 또 트리면 건드리지 않는다 — 그쪽은 제 주기로 따로 맞춘다)
    static void 템포맞춤(BlendTree 트리, float 목표)
    {
        if (트리 == null || 목표 < 0.001f) return;
        var cs = 트리.children;
        for (int i = 0; i < cs.Length; i++)
        {
            var c = cs[i].motion as AnimationClip;
            if (c == null || c.length < 0.001f) continue;
            cs[i].timeScale = c.length / 목표;
        }
        트리.children = cs;      // 구조체 배열이라 통째로 되돌려 놔야 반영된다
    }

    static void 전이(AnimatorState 에서, AnimatorState 로, string 조건, bool 값, float 시간)
    {
        var t = 에서.AddTransition(로);
        t.hasExitTime = false;
        t.duration = 시간;
        t.AddCondition(값 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, 조건);
    }

    static void 끝나면(AnimatorState 에서, AnimatorState 로, float 시간)
    {
        var t = 에서.AddTransition(로);
        t.hasExitTime = true;
        t.exitTime = 1f;
        t.duration = 시간;
    }

    static AnimationClip 클립찾기(string 파일, string 어느폴더 = 폴더)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(어느폴더 + "/" + 파일))
            if (o is AnimationClip ac && !ac.name.StartsWith("__")) return ac;
        return null;
    }

    /// ★제자리걸음으로 바꾼다 (2026-08-04 사용자 "반대로 걸으면 순간이동하는 버그").
    ///   받아온 클립은 **실제로 앞으로 나아가는** 동작이라, 이동은 코드가 하는데
    ///   애니메이션까지 몸을 밀어 버린다 → 한 바퀴 돌 때마다 원위치로 튕겨서 순간이동처럼 보인다.
    ///   → 뿌리 뼈(Hips)의 **앞뒤·좌우 이동 커브만 지운다.** 위아래 출렁임은 남긴다.
    static AnimationClip 제자리로(string 이름, AnimationClip 원본)
    {
        if (원본 == null) return null;
        string path = 저장 + "/" + 이름 + ".anim";
        var 있던 = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (있던 != null) return 있던;

        var c = new AnimationClip { name = 이름, frameRate = 원본.frameRate };
        foreach (var b in AnimationUtility.GetCurveBindings(원본))
        {
            bool 뿌리 = b.path.EndsWith("Hips") || b.path.EndsWith("Root") || b.path == "Armature";
            bool 수평이동 = b.propertyName == "m_LocalPosition.x" || b.propertyName == "m_LocalPosition.z";
            if (뿌리 && 수평이동) continue;                     // 이 커브만 버린다

            var curve = AnimationUtility.GetEditorCurve(원본, b);
            if (curve != null) AnimationUtility.SetEditorCurve(c, b, curve);
        }

        var s = AnimationUtility.GetAnimationClipSettings(원본);
        s.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(c, s);
        AssetDatabase.CreateAsset(c, path);
        return c;
    }

    // ══════════════════════════════════════════════════════════
    //  ★옆걸음을 **앞걷기에서 만들어 낸다** (2026-08-04 사용자 "지금 있는 모션 참고해서")
    //
    //  받은 동작에 옆걸음이 없다. 그런데 옆걸음은 결국
    //    **골반을 옆으로 돌려 다리가 옆으로 딛고, 상체는 앞을 보는 것**이다.
    //  그러니 앞걷기의 골반 회전에 90도를 더하고, 척추에서 그만큼 되돌리면 된다.
    //  (다리는 골반의 자식이라 저절로 따라 돌아간다)
    //
    //  ★척추 두 마디에 나눠서 되돌린다 — 한 마디에 몰면 허리가 꺾인 것처럼 보인다.
    // ══════════════════════════════════════════════════════════
    // ★★척추를 되돌리는 방식은 버렸다 (2026-08-04 사용자 "고개를 심각하게 뒤로 젖힌 게 이상하네").
    //   뼈의 회전은 **그 뼈의 부모 기준**이라, 척추처럼 축이 누워 있는 뼈에 세로축(Y) 회전을
    //   그대로 곱하면 **고개가 뒤로 젖혀진다.** 축이 달라서 생기는 일이라 각도로는 못 고친다.
    //
    //   → 훨씬 단순하고 안전한 길: **넓적다리만 돌린다.** 다리는 골반 바로 아래라 축이
    //     서 있고, 몸통·머리는 아예 안 건드리니 젖혀질 일이 없다.
    //     골반은 앞을 보고 다리만 옆으로 딛는다 — 그게 옆걸음이다.
    const string 왼다리 = "Armature/Hips/LeftUpLeg";
    const string 오른다리 = "Armature/Hips/RightUpLeg";

    static AnimationClip 옆걸음만들기(string 이름, AnimationClip 원본, float 각도)
    {
        if (원본 == null) return null;
        string path = 저장 + "/" + 이름 + ".anim";
        var 있던 = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (있던 != null) return 있던;

        var c = new AnimationClip { name = 이름, frameRate = 원본.frameRate };

        // 회전이 아닌 커브는 그대로 옮긴다 (단 뿌리 수평이동은 버린다 — 제자리걸음)
        foreach (var b in AnimationUtility.GetCurveBindings(원본))
        {
            if (b.propertyName.StartsWith("m_LocalRotation")) continue;
            bool 뿌리 = b.path.EndsWith("Hips") || b.path.EndsWith("Root");
            if (뿌리 && (b.propertyName == "m_LocalPosition.x" || b.propertyName == "m_LocalPosition.z")) continue;
            var cur = AnimationUtility.GetEditorCurve(원본, b);
            if (cur != null) AnimationUtility.SetEditorCurve(c, b, cur);
        }

        // 회전 커브는 뼈마다 통째로 다뤄야 한다 (x·y·z·w 를 같이 봐야 하므로)
        foreach (var 경로 in 회전경로들(원본))
        {
            // ★넓적다리는 **흔들리는 방향만** 돌린다 (2026-08-04 사용자 "다리와 발이
            //   전체적으로 걷는 방향쪽으로 이동되어 있어, 몸 전체가 기울어진 것처럼 보여").
            //   통째로 돌리면 **기본 자세까지** 옆으로 끌려가 다리가 몸 밖으로 뻗는다.
            //   기본 자세는 그대로 두고 「앞뒤로 흔들리던 것」을 「좌우로 흔들리게」만 바꾼다.
            bool 다리 = 경로 == 왼다리 || 경로 == 오른다리;
            float 흔들각 = 다리 ? 각도 : 0f;

            // 보폭은 다리 사슬 전체에서 줄인다
            float 폭 = 경로.Contains("UpLeg") || 경로.Contains("Leg") || 경로.Contains("Foot")
                     ? 보폭 : 1f;
            회전옮기기(원본, c, 경로, 흔들각, 폭);
        }

        var s = AnimationUtility.GetAnimationClipSettings(원본);
        s.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(c, s);
        AssetDatabase.CreateAsset(c, path);
        return c;
    }

    static List<string> 회전경로들(AnimationClip 원본)
    {
        var 목록 = new List<string>();
        foreach (var b in AnimationUtility.GetCurveBindings(원본))
            if (b.propertyName == "m_LocalRotation.x" && !목록.Contains(b.path)) 목록.Add(b.path);
        return 목록;
    }

    /// 옆걸음 보폭 (1 = 앞걷기와 같음 · 낮출수록 다리를 덜 벌린다)
    public static float 보폭 = 0.55f;

    /// 한 뼈의 회전 커브를 옮긴다.
    /// `흔들각` — 흔들리는 **방향**을 이만큼 돌린다 (기본 자세는 안 건드린다)
    /// `폭` — 흔들리는 **크기**를 이만큼으로 줄인다 (1 = 그대로)
    static void 회전옮기기(AnimationClip 원본, AnimationClip 새것, string 경로, float 흔들각, float 폭 = 1f)
    {
        var cx = 커브(원본, 경로, "m_LocalRotation.x");
        var cy = 커브(원본, 경로, "m_LocalRotation.y");
        var cz = 커브(원본, 경로, "m_LocalRotation.z");
        var cw = 커브(원본, 경로, "m_LocalRotation.w");
        if (cx == null || cy == null || cz == null || cw == null) return;

        var nx = new AnimationCurve(); var ny = new AnimationCurve();
        var nz = new AnimationCurve(); var nw = new AnimationCurve();

        // ★★기준 자세 = **한 바퀴의 평균** (2026-08-04 사용자 "옆으로 걸을때 다리가 좌측으로
        //   쏠려있어, 왼편으로가든 오른편으로가든").
        //
        //   전엔 **첫 프레임**을 기준으로 삼았다. 그런데 걷기 클립의 첫 프레임은 중립 자세가
        //   아니라 **성큼 내디딘 순간**이라, 거기서 잰 「흔들림」에는 한쪽으로 치우친 몫이
        //   상수처럼 섞여 있다. 그 상수까지 90° 돌아가니 **다리 전체가 옆으로 밀렸다.**
        //   실측: 앞걷기는 좌우 −0.011m 인데 옆걸음은 −0.138m · −0.120m 로, 어느 쪽으로 가든
        //   똑같이 왼쪽으로 쏠려 있었다.
        //
        //   평균을 기준으로 삼으면 흔들림의 평균이 0 이 되어, 돌려도 **제자리에서 방향만** 바뀐다.
        //   (기준을 무엇으로 잡든 원본 복원은 똑같다 — 무엇을 「돌릴 것」으로 볼지만 달라진다)
        var 기준 = 평균회전(cx, cy, cz, cw);
        var 기준역 = Quaternion.Inverse(기준);

        // 흔들리는 방향을 돌릴 회전 — 세로축을 **이 뼈 기준**으로 옮겨서 쓴다
        // (뼈마다 축이 누워 있어서, 그냥 Y축으로 돌리면 엉뚱하게 꺾인다)
        var 축 = 기준역 * Vector3.up;
        var 방향돌림 = Quaternion.AngleAxis(흔들각, 축);
        var 방향돌림역 = Quaternion.Inverse(방향돌림);

        for (int i = 0; i < cx.length; i++)
        {
            float t = cx[i].time;
            var q = new Quaternion(cx.Evaluate(t), cy.Evaluate(t), cz.Evaluate(t), cw.Evaluate(t));

            var 흔들림 = 기준역 * q;                                   // 기준에서 벗어난 만큼
            if (Mathf.Abs(흔들각) > 0.01f)
                흔들림 = 방향돌림 * 흔들림 * 방향돌림역;                // 흔들리는 **방향**만 회전
            if (폭 < 0.999f)
                흔들림 = Quaternion.Slerp(Quaternion.identity, 흔들림, 폭);   // 흔들리는 **크기**를 줄임

            q = 기준 * 흔들림;
            nx.AddKey(t, q.x); ny.AddKey(t, q.y); nz.AddKey(t, q.z); nw.AddKey(t, q.w);
        }

        AnimationUtility.SetEditorCurve(새것, 묶음(경로, "m_LocalRotation.x"), nx);
        AnimationUtility.SetEditorCurve(새것, 묶음(경로, "m_LocalRotation.y"), ny);
        AnimationUtility.SetEditorCurve(새것, 묶음(경로, "m_LocalRotation.z"), nz);
        AnimationUtility.SetEditorCurve(새것, 묶음(경로, "m_LocalRotation.w"), nw);
    }

    /// 한 바퀴 동안의 **평균 회전** — 「흔들림」을 재는 기준이 된다.
    /// ★사원수는 q 와 −q 가 같은 회전이라, 그냥 더하면 서로 상쇄돼 엉뚱한 값이 나온다.
    ///   첫 표본과 같은 쪽으로 부호를 맞춘 뒤 더한다.
    static Quaternion 평균회전(AnimationCurve cx, AnimationCurve cy, AnimationCurve cz, AnimationCurve cw)
    {
        float 끝 = cx.length > 0 ? cx[cx.length - 1].time : 0f;
        if (끝 <= 0f) return new Quaternion(cx.Evaluate(0f), cy.Evaluate(0f), cz.Evaluate(0f), cw.Evaluate(0f));

        const int 표본 = 48;
        var 첫 = new Vector4(cx.Evaluate(0f), cy.Evaluate(0f), cz.Evaluate(0f), cw.Evaluate(0f));
        var 합 = Vector4.zero;
        for (int i = 0; i < 표본; i++)
        {
            float t = 끝 * i / 표본;
            var q = new Vector4(cx.Evaluate(t), cy.Evaluate(t), cz.Evaluate(t), cw.Evaluate(t));
            if (Vector4.Dot(q, 첫) < 0f) q = -q;
            합 += q;
        }
        if (합.sqrMagnitude < 1e-8f) return new Quaternion(첫.x, 첫.y, 첫.z, 첫.w);
        합.Normalize();
        return new Quaternion(합.x, 합.y, 합.z, 합.w);
    }

    static EditorCurveBinding 묶음(string 경로, string 속성)
        => EditorCurveBinding.FloatCurve(경로, typeof(Transform), 속성);

    static AnimationCurve 커브(AnimationClip c, string 경로, string 속성)
        => AnimationUtility.GetEditorCurve(c, 묶음(경로, 속성));

    /// 「정지」 동작 만들기 — 걷기의 첫 프레임을 굳힌다
    static AnimationClip 정지만들기(AnimationClip 원본) => 굳히기("정지", 원본, 0f);

    /// 한 프레임을 굳혀 멈춘 자세를 만든다. `비율` 0 = 첫 프레임 · 1 = 마지막 프레임.
    /// ★서 있는 자세도, 웅크린 자세도, 의자에 앉아 있는 자세도 전부 이걸로 낸다 —
    ///   Meshy 에서 받은 것에 「가만히 있는 동작」이 하나도 없기 때문이다.
    static AnimationClip 굳히기(string 이름, AnimationClip 원본, float 비율)
    {
        if (원본 == null) return null;
        string path = 저장 + "/" + 이름 + ".anim";
        var 있던 = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (있던 != null) return 있던;

        float t = 원본.length * Mathf.Clamp01(비율);
        var c = new AnimationClip { name = 이름, legacy = false };
        foreach (var b in AnimationUtility.GetCurveBindings(원본))
        {
            var curve = AnimationUtility.GetEditorCurve(원본, b);
            if (curve == null || curve.length == 0) continue;
            float v = curve.Evaluate(t);
            AnimationUtility.SetEditorCurve(c, b, AnimationCurve.Constant(0f, 1f, v));
        }
        var s = new AnimationClipSettings { loopTime = true };
        AnimationUtility.SetAnimationClipSettings(c, s);
        AssetDatabase.CreateAsset(c, path);
        return c;
    }

    /// 한 번만 재생되는 클립으로 베껴 온다 (의자 앉기·일어서기).
    /// ★glb 안의 클립은 **읽기 전용**이라 반복 설정을 못 끈다 — 그래서 베낀다.
    /// ★수평 이동을 안 지운다: 앉으려면 실제로 몸이 뒤로 물러나야 한다.
    static AnimationClip 클립한번만(string 이름, AnimationClip 원본)
    {
        if (원본 == null) return null;
        string path = 저장 + "/" + 이름 + ".anim";
        var 있던 = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (있던 != null) return 있던;

        var c = new AnimationClip { name = 이름, frameRate = 원본.frameRate };
        foreach (var b in AnimationUtility.GetCurveBindings(원본))
        {
            var curve = AnimationUtility.GetEditorCurve(원본, b);
            if (curve != null) AnimationUtility.SetEditorCurve(c, b, curve);
        }
        var s = AnimationUtility.GetAnimationClipSettings(원본);
        s.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(c, s);
        AssetDatabase.CreateAsset(c, path);
        return c;
    }

    /// 모델의 실제 높이를 재서 사람 키에 맞춘다.
    ///
    /// ★★★**바운즈로 재면 안 된다** (2026-08-04 — 같은 자리에서 두 번 틀렸다).
    ///   ①`Renderer.bounds` — 임포트할 때 넉넉하게 부풀린 값이라 파일마다 여유가 다르다.
    ///     맞춰 놓으면 바운즈는 같은데 몸은 여자 1.925 · 남자 1.894 로 어긋났다.
    ///   ②`sharedMesh.bounds` — 꼭짓점에서 나온 값이라 믿었는데 이것도 틀렸다.
    ///     **바인드 자세의 제 좌표계** 넓이라, 뼈대 안쪽 노드의 크기 배율이 파일마다 다르면
    ///     같은 값이 화면에서 다른 크기가 된다. 1.670 · 1.690 으로 거의 같게 나왔는데
    ///     실제로 그려진 키는 **2.531m · 2.181m (16% 차이)** 였다 (사용자 "남자만 사이즈가 작아").
    ///
    /// ★유일하게 맞는 법: **스킨을 실제로 구워서**(`BakeMesh`) 월드 높이를 잰다. 그게 눈에
    ///   보이는 그 키다. 자세에 따라 달라지므로 **그 몸의 「정지」 자세로 맞춰 놓고** 잰다 —
    ///   비교 대상이 「서 있을 때의 키」이기 때문이다.
    static void 키맞춤(GameObject go, float 키, AnimationClip 자세)
    {
        go.transform.localScale = Vector3.one;
        if (자세 != null) 자세.SampleAnimation(go, 0f);

        if (!구운높이(go, out float 바닥, out float 머리)) return;
        float h = 머리 - 바닥;
        if (h > 0.0001f) go.transform.localScale = Vector3.one * (키 / h);

        // 발을 땅에 붙인다 — 크기를 바꾼 뒤 다시 재야 한다
        if (구운높이(go, out 바닥, out _))
            go.transform.position += Vector3.up * (go.transform.parent.position.y - 바닥);
    }

    /// 지금 자세 그대로 구운 메시의 **월드 높이** (바닥 y · 머리 y)
    static bool 구운높이(GameObject go, out float 바닥, out float 머리)
    {
        바닥 = float.MaxValue; 머리 = float.MinValue; bool 있음 = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            Mesh m = null; bool 구움 = false;
            if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
            { m = new Mesh(); smr.BakeMesh(m, true); 구움 = true; }
            else m = r.GetComponent<MeshFilter>()?.sharedMesh;
            if (m == null) continue;

            var mtx = r.transform.localToWorldMatrix;
            foreach (var v in m.vertices)
            {
                float y = mtx.MultiplyPoint3x4(v).y;
                if (y < 바닥) 바닥 = y;
                if (y > 머리) 머리 = y;
                있음 = true;
            }
            if (구움) Object.DestroyImmediate(m);
        }
        return 있음;
    }
}
