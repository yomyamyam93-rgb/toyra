using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// 동작 진열 — **F1** 로 켜고 끈다.
///
/// ★하는 일은 하나다: `Resources/rig` 에 있는 리깅된 몸을 **한 종에 딱 한 마리씩**
///   세워 놓고, 저마다 대기→걷기→뛰기→공격→피격→죽음 을 쭉 돌린다.
///
/// ★★**겹치지 않게 놓는 것이 이 파일의 전부다** (2026-08-06 사용자 —
///   "전부 배치 바꿔서 한개씩만 배치되게").
///   전에는 간격이 7m 로 못박혀 있어서, 키를 3m 로 맞춰도 브론토처럼 **긴** 몸은
///   옆칸을 덮었다. 두 마리가 겹쳐 보이면 그게 곧 "모델이 두 겹"으로 읽힌다.
///   → 간격을 숫자로 정하지 않는다. **세워 놓고 제일 긴 놈을 재서** 그만큼 벌린다.
///
/// ★카메라는 건드리지 않는다 (2026-08-05 사용자 "돌아다니면서 봐야하는데").
///   격자를 걸어 들어갈 수 있는 크기로 펴 놓고, 카메라는 평소대로 캐릭터를 따라간다.
///
/// ★진열 동안만 젖히는 것 셋 — 안 젖히면 세워 놓고도 못 본다:
///   ①밤 ②시야 부채꼴 ③야생(진열대 위를 돌아다니면 남의 짐승이 겹쳐 선다).
///   원래 값을 적어 뒀다가 F1 을 다시 누르면 되돌린다.
public class 동작진열 : MonoBehaviour
{
    /// 굽는 쪽(`네발동작표.만들기`)과 같은 순서·같은 이름이어야 한다
    public static readonly string[] 동작들 = { "대기", "걷기", "뛰기", "공격", "피격", "죽음" };

    // ★★모든 몸을 같은 크기로 맞추지 않는다 (2026-08-06 사용자 — "각 팻마다 사이즈가
    //   있으니까 맞춰서 조금 조절해줘"). 다람쥐와 브론토가 같은 크기로 서 있으면
    //   **무엇을 보고 있는지 감이 안 온다.** 종마다 제 키로 세운다.
    //
    // ★★그러면 몸마다 차지하는 자리가 달라진다 → 간격을 하나로 못 정한다.
    //   → **선반에 얹듯** 재면서 붙여 놓는다 (아래 `선반배치`). 큰 놈 하나 때문에
    //     격자 전체가 벌어지는 일이 없어진다.
    [Tooltip("전체 크기 손잡이 — 1 이 기준. 작게 하면 다 같이 작아진다")] public float 크기배 = 1f;
    [Tooltip("이웃과 벌리는 틈 (m)")] public float 틈 = 1.2f;
    [Tooltip("한 줄이 이 폭을 넘으면 다음 줄로 (m)")] public float 줄폭 = 60f;

    /// 종마다의 키 (m) — 사람이 1.8. **이름 앞머리로 고른다.**
    /// ★여기 숫자는 눈으로 보고 정하는 자리다 (CLAUDE.md 6장 "크기 규칙은 물려받지 않는다").
    static readonly (string 앞머리, float 키)[] 종키 =
    {
        // ★진짜 생태 비율이 아니라 **진열에서 읽히는 비율**이다 (2026-08-06 사용자
        //   "사이즈가 다 너무 커서" + "한눈에 안들어와"). 브론토를 제 비율(사람의 4배)로
        //   세우면 그놈 하나가 화면을 다 먹어 나머지를 못 본다 — 위쪽을 눌러 놓는다.
        ("dino", 9.6f),          // ★브론토 — 압도적으로 커야 한다 (사용자 "브론토는 커야지")
        ("티라노", 4.8f),
        ("스테고", 3.6f),
        ("트리케", 3.4f),
        ("테러버드", 2.4f),       // ×0.8 (2026-08-06 사용자)
        ("큰뿔사슴", 3.6f),       // 사슴 무리 — ×1.2
        ("사슴", 3.12f),
        ("랩터", 2.6f),
        ("다이어울프", 2.28f),    // 늑대 무리 — 1.2배 (2026-08-06 사용자)
        ("늑대", 2.04f),
        ("검치다람쥐", 1.98f),    // 다람쥐 무리 — 1.8배
        ("다람쥐", 1.62f),
    };

    static float 종의키(string 이름)
    {
        foreach (var (앞머리, 키) in 종키) if (이름.StartsWith(앞머리)) return 키;
        return 1.4f;
    }
    [Tooltip("한 동작이 끝나고 다음으로 넘어가기 전 쉼 (초)")] public float 다시쉼 = 0.6f;
    [Tooltip("도는 동작(대기·걷기·뛰기)을 몇 초씩 보여주나")] public float 반복보기 = 3f;
    [Tooltip("카메라 기준 몇 도 돌려 세우나 (90 = 옆모습)")] public float 세우는각 = 90f;
    [Tooltip("캐릭터에서 격자 앞줄까지 (m)")] public float 앞으로 = 10f;

    GameObject 그릇;
    readonly List<한마리> 목록 = new();

    /// 한 마리가 여섯 동작을 차례로 다 한다 — 한 몸이 어떻게 움직이는지를 눈으로 따라가려면
    /// 대기하는 놈과 공격하는 놈이 **같은 자리**에 있어야 한다.
    class 한마리
    {
        public Animator anim;
        public RuntimeAnimatorController[] 컨트롤러;
        public string[] 상태;
        public float[] 길이;
        public bool[] 반복;
        public int 지금 = -1;
        public float 바꿀때;
        public TextMesh 이름표;
    }

    // 되돌리려고 적어 두는 것들
    VisionCone 시야; DayNight 낮밤; Renderer 덮개; Wildlife 야생;
    bool 옛시야, 옛낮밤, 옛덮개, 옛야생;
    List<GameObject> 치운짐승;

    public bool 켜짐 { get; private set; }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null && k.f1Key.wasPressedThisFrame) { if (켜짐) 치우기(); else 세우기(); }
#endif
        if (!켜짐) return;
        for (int i = 0; i < 목록.Count; i++) 넘기기(목록[i]);
    }

    // ───────────────────────────────── 세우기

    void 세우기()
    {
        var 몸들 = 몸찾기();
        if (몸들.Count == 0)
        { Debug.LogError("[동작진열] Resources/rig 에 리깅된 모델이 없다"); return; }

        치우기();
        그릇 = new GameObject("동작진열");

        // 카메라 각도를 **읽기만** 한다 — 화면이 45° 기울어 있으니 격자도 같이 기울어야 반듯하다
        var 눈 = FindFirstObjectByType<IsoCam>();
        float yaw = 눈 != null ? 눈.yaw : 45f;
        var 화면 = Quaternion.Euler(0f, yaw, 0f);
        var 오른 = 화면 * Vector3.right;
        var 위쪽 = 화면 * Vector3.forward;

        var 가운데 = 그릇위치() + 위쪽 * 앞으로;
        그릇.transform.position = 가운데;

        // ①먼저 **전부 만들어서 제 키로 맞추고, 화면 축으로 얼마나 차지하는지 잰다.**
        //   ★자리는 그 다음에 정한다 — 재기 전에 놓으면 긴 놈이 옆칸을 덮는다.
        var 만든것 = new List<(GameObject go, 한마리 m, float 폭, float 깊이)>();
        var 빠진 = new List<string>();

        foreach (var 몸 in 몸들)
        {
            var ctrls = new List<RuntimeAnimatorController>();
            var 상태들 = new List<string>();
            foreach (var d in 동작들)
            {
                var c = Resources.Load<RuntimeAnimatorController>("rig/" + d + "_" + 몸.name);
                if (c == null) { 빠진.Add(d + "_" + 몸.name); continue; }
                ctrls.Add(c); 상태들.Add(d);
            }
            if (ctrls.Count == 0) continue;

            var go = Instantiate(몸, 가운데, Quaternion.Euler(0f, yaw + 세우는각, 0f), 그릇.transform);
            go.name = 몸.name;

            // ★★뼈 뿌리를 `Armature` 껍데기 안에 넣는다 — **이걸 빼면 한 대도 안 움직인다.**
            //   구운 클립의 경로가 언제나 `Armature/spine_hip/…` 인데 glb 에는 그 한 겹이 없다.
            //   경로가 한 글자만 달라도 **에러 없이 조용히 무시**된다.
            var 뿌리 = go.transform.Find("spine_hip");
            if (뿌리 != null)
            {
                var 껍 = new GameObject("Armature").transform;
                껍.SetParent(go.transform, false);
                뿌리.SetParent(껍, true);
            }

            var anim = go.GetComponent<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            // ★glTF 로 들여온 것은 아바타가 안 딸려 온다. 계층을 바꾼 **뒤에** 만들어야 한다.
            var av = AvatarBuilder.BuildGenericAvatar(go, "");
            av.name = go.name + "_아바타";
            anim.avatar = av;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var m = new 한마리
            {
                anim = anim,
                컨트롤러 = ctrls.ToArray(),
                상태 = 상태들.ToArray(),
                길이 = new float[ctrls.Count],
                반복 = new bool[ctrls.Count],
            };
            for (int k = 0; k < ctrls.Count; k++)
            {
                var 클립 = ctrls[k].animationClips;
                m.길이[k] = 클립.Length > 0 ? Mathf.Max(0.1f, 클립[0].length) : 1f;
                m.반복[k] = 클립.Length > 0 && 클립[0].isLooping;
            }

            키맞춤(go, 종의키(몸.name) * Mathf.Max(0.05f, 크기배));
            m.이름표 = 이름표달기(go, 몸.name);
            재기(go, 오른, 위쪽, out float 폭, out float 깊이);
            만든것.Add((go, m, 폭, 깊이));
        }

        // ②**선반에 얹듯** 왼쪽부터 붙여 놓는다 — 줄이 `줄폭` 을 넘으면 다음 줄로.
        //   큰 놈 하나가 격자 전체를 벌리지 못한다.
        float x = 0f, z = 0f, 줄깊이 = 0f;
        var 자리 = new List<(int i, float x, float z)>();
        var 줄들 = new List<(int 처음, int 끝, float 폭)>();
        int 줄시작 = 0;
        for (int i = 0; i < 만든것.Count; i++)
        {
            float w = 만든것[i].폭 + 틈, d = 만든것[i].깊이 + 틈;
            if (i > 줄시작 && x + w > 줄폭)
            {
                줄들.Add((줄시작, i, x));
                z += 줄깊이; x = 0f; 줄깊이 = 0f; 줄시작 = i;
            }
            자리.Add((i, x + w * 0.5f, z + d * 0.5f));
            x += w; 줄깊이 = Mathf.Max(줄깊이, d);
        }
        줄들.Add((줄시작, 만든것.Count, x));
        float 전체깊이 = z + 줄깊이;

        // 줄마다 가운데 정렬 — 오른쪽이 들쭉날쭉하면 격자로 안 읽힌다
        foreach (var (처음, 끝, 줄의폭) in 줄들)
            for (int i = 처음; i < 끝; i++)
                자리[i] = (i, 자리[i].x - 줄의폭 * 0.5f, 자리[i].z);

        for (int i = 0; i < 자리.Count; i++)
        {
            var at = 가운데 + 오른 * 자리[i].x + 위쪽 * (자리[i].z - 전체깊이 * 0.5f);
            var go = 만든것[i].go;
            go.transform.position = new Vector3(at.x, go.transform.position.y, at.z);
            넘기기(만든것[i].m);          // 첫 동작을 바로 시작한다
            목록.Add(만든것[i].m);
        }

        켜짐 = true;
        밝게();
        Debug.Log($"[동작진열] {목록.Count}마리 · {줄들.Count}줄 · 밭 {줄폭:0}m × {전체깊이:0.0}m. F1 로 치웁니다.");
        if (빠진.Count > 0)
            Debug.LogWarning("[동작진열] 클립이 없어 건너뛴 동작: " + string.Join(", ", 빠진));
    }

    /// 이 몸이 **화면 가로·세로로** 얼마나 차지하나 (m).
    /// ★세계 좌표축이 아니라 화면 축으로 재야 한다 — 화면이 45° 기울어 있어서
    ///   세계 축으로 재면 실제보다 1.4배쯤 크게 나오고, 그만큼 격자가 헛되이 벌어진다.
    static void 재기(GameObject go, Vector3 오른, Vector3 위쪽, out float 폭, out float 깊이)
    {
        float a0 = float.MaxValue, a1 = float.MinValue, b0 = float.MaxValue, b1 = float.MinValue;
        bool 있음 = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            if (r.name == "이름표") continue;
            Mesh m = null; bool 구움 = false;
            if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
            { m = new Mesh(); smr.BakeMesh(m, true); 구움 = true; }
            else m = r.GetComponent<MeshFilter>()?.sharedMesh;
            if (m == null) continue;
            var mtx = r.transform.localToWorldMatrix;
            foreach (var v in m.vertices)
            {
                var p = mtx.MultiplyPoint3x4(v);
                float u = Vector3.Dot(p, 오른), w = Vector3.Dot(p, 위쪽);
                a0 = Mathf.Min(a0, u); a1 = Mathf.Max(a1, u);
                b0 = Mathf.Min(b0, w); b1 = Mathf.Max(b1, w);
                있음 = true;
            }
            if (구움) Destroy(m);
        }
        폭 = 있음 ? a1 - a0 : 1f;
        깊이 = 있음 ? b1 - b0 : 1f;
    }

    /// 다음 동작으로 넘긴다. 반복 클립(대기·걷기·뛰기)은 `반복보기` 만큼만 보여 준다.
    void 넘기기(한마리 m)
    {
        if (m.anim == null || m.컨트롤러 == null || m.컨트롤러.Length == 0) return;
        if (m.지금 >= 0 && Time.time < m.바꿀때) return;

        m.지금 = (m.지금 + 1) % m.컨트롤러.Length;
        // ★컨트롤러를 갈아끼운 **뒤에** Play 해야 한다 — 먼저 부르면 옛 컨트롤러에 대고 부른다
        m.anim.runtimeAnimatorController = m.컨트롤러[m.지금];
        m.anim.Play(m.상태[m.지금], 0, 0f);

        float 볼시간 = m.반복[m.지금] ? Mathf.Max(반복보기, m.길이[m.지금]) : m.길이[m.지금];
        m.바꿀때 = Time.time + 볼시간 + 다시쉼;

        if (m.이름표 != null) m.이름표.text = m.anim.gameObject.name + " · " + m.상태[m.지금];
    }

    /// 머리 위 이름표 — 지금 무슨 동작인지 같이 적는다
    static TextMesh 이름표달기(GameObject go, string 이름)
    {
        var t = new GameObject("이름표");
        t.transform.SetParent(go.transform, false);
        var tm = t.AddComponent<TextMesh>();
        tm.text = 이름;
        tm.characterSize = 0.16f;
        tm.fontSize = 64;
        tm.anchor = TextAnchor.LowerCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;
        return tm;
    }

    /// `Resources/rig` 안에서 **스킨 메시가 든 모델**만, 이름 하나에 하나씩
    static List<GameObject> 몸찾기()
    {
        var 본것 = new HashSet<string>();
        var 찾은 = new List<GameObject>();
        foreach (var go in Resources.LoadAll<GameObject>("rig"))
        {
            if (go == null) continue;
            if (go.GetComponentInChildren<SkinnedMeshRenderer>(true) == null) continue;
            if (!본것.Add(go.name)) continue;      // 같은 이름은 딱 한 번만
            찾은.Add(go);
        }
        찾은.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return 찾은;
    }

    /// 격자의 기준점 — 캐릭터가 선 자리
    Vector3 그릇위치()
    {
        var h = FindFirstObjectByType<Hero>();
        var p = h != null ? h.transform.position : Vector3.zero;
        return new Vector3(p.x, 0f, p.z);
    }

    // ───────────────────────────────── 치우기

    void 치우기()
    {
        목록.Clear();
        if (그릇 != null) Destroy(그릇);
        그릇 = null;
        되돌리기();
        켜짐 = false;
    }

    // ───────────────────────────────── 밝기 — 진열 동안만 젖힌다

    void 밝게()
    {
        // ①밤이면 아무것도 안 보인다 → 낮으로 못박는다
        낮밤 = FindFirstObjectByType<DayNight>();
        if (낮밤 != null) { 옛낮밤 = 낮밤.enabled; 낮밤.enabled = false; }
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional)
            {
                l.transform.rotation = Quaternion.Euler(60f, 35f, 0f);
                l.intensity = 1.35f; l.color = Color.white;
                break;
            }
        RenderSettings.ambientLight = new Color(0.45f, 0.46f, 0.5f);

        // ②시야 부채꼴 — 켜져 있으면 옆줄이 통째로 캄캄하다
        시야 = FindFirstObjectByType<VisionCone>();
        if (시야 != null) { 옛시야 = 시야.enabled; 시야.enabled = false; }
        var d = GameObject.Find("시야_덮개");
        덮개 = d != null ? d.GetComponent<Renderer>() : null;
        if (덮개 != null) { 옛덮개 = 덮개.enabled; 덮개.enabled = false; }

        // ③야생을 재우고 이미 나와 있는 것도 치운다 — 진열대 위에 겹쳐 서면 같은 종이 둘로 보인다
        야생 = FindFirstObjectByType<Wildlife>();
        if (야생 != null) { 옛야생 = 야생.enabled; 야생.enabled = false; }
        치운짐승 = new List<GameObject>();
        foreach (var c in Critter.All.ToArray())
            if (c != null && c.side != Critter.Side.내편)
            { c.gameObject.SetActive(false); 치운짐승.Add(c.gameObject); }
    }

    void 되돌리기()
    {
        if (낮밤 != null) { 낮밤.enabled = 옛낮밤; 낮밤 = null; }
        if (시야 != null) { 시야.enabled = 옛시야; 시야 = null; }
        if (덮개 != null) { 덮개.enabled = 옛덮개; 덮개 = null; }
        if (야생 != null) { 야생.enabled = 옛야생; 야생 = null; }
        if (치운짐승 != null)
        {
            foreach (var g in 치운짐승) if (g != null) g.SetActive(true);
            치운짐승 = null;
        }
    }

    // ───────────────────────────────── 키 맞추기

    /// ★구운 메시의 실제 높이로 잰다 — 바운즈는 파일마다 부풀림이 달라 못 믿는다.
    ///   **키**를 맞추고, 발바닥을 땅에 붙인다.
    static void 키맞춤(GameObject go, float 키)
    {
        go.transform.localScale = Vector3.one;
        if (!구운상자(go, out var 작, out var 큰)) return;
        float h = 큰.y - 작.y;
        if (h > 0.0001f) go.transform.localScale = Vector3.one * (키 / h);
        if (구운상자(go, out 작, out _))
            go.transform.position += Vector3.up * -작.y;
    }

    static bool 구운상자(GameObject go, out Vector3 작, out Vector3 큰)
    {
        작 = Vector3.positiveInfinity; 큰 = Vector3.negativeInfinity; bool 있음 = false;
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
                var p = mtx.MultiplyPoint3x4(v);
                작 = Vector3.Min(작, p); 큰 = Vector3.Max(큰, p);
                있음 = true;
            }
            if (구움) Destroy(m);
        }
        return 있음;
    }
}
