using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// 네발 동작 진열 — **F1** 로 켜고 끈다 (2026-08-05 사용자 "실제로 보기가 어려워서").
///
/// ★구워 놓은 클립은 이미 다 있다 (`Resources/rig` 에 몸 10 × 동작 6 = 60개).
///   없던 것은 **그걸 씬에 세워 보여주는 자리**다. 이 파일이 그 자리다.
///
/// ★한 마리에 한 동작씩 넣는다. 가로가 동작(대기·걷기·뛰기·공격·피격·죽음),
///   세로가 몸이다. 격자로 놓으면 "다이어울프의 뛰기"와 "스테고의 뛰기"가
///   **같은 세로줄에 나란히** 서서 한눈에 견줘진다.
///
/// ★진열에서는 **모든 몸을 같은 키로 맞춘다.** 크기를 견주는 자리는 따로 있고
///   (`Tools/토이라기/㉥ 리깅 공룡 뿌리기`), 여기는 **동작을 보는 자리**다.
///   브론토가 사람 4배 그대로면 격자가 100m 를 넘어 아무것도 안 보인다.
///
/// ★공격·피격·죽음은 한 번만 재생되는 클립이라 그냥 두면 첫 1초만 움직이고 만다.
///   → 끝나면 잠깐 쉬었다 **자동으로 다시 돌린다.**
///
/// ★★**카메라는 건드리지 않는다** (2026-08-05 사용자 "화면이 캐릭터 따라 이동이 안돼네,
///   돌아다니면서 봐야하는데"). 처음엔 격자 전체를 한 화면에 담으려고 카메라를 격자에
///   묶었는데, 그러면 60마리가 개미만 해지고 **걸어다니며 볼 수가 없다.**
///   → 격자를 **캐릭터 앞에 걸어 들어갈 수 있는 크기로** 펴 놓고, 카메라는 평소대로 둔다.
///
/// ★진열 동안만 젖히는 것 둘 — 안 젖히면 세워 놓고도 못 본다:
///   ①밤(어두우면 안 보인다) ②시야 부채꼴(옆줄이 통째로 캄캄하다).
///   **원래 값을 적어 뒀다가 F1 을 다시 누르면 되돌린다** (은퇴는 삭제가 아니라 스위치).
public class 동작진열 : MonoBehaviour
{
    /// 굽는 쪽(`네발동작표.만들기`)과 같은 순서·같은 이름이어야 한다
    public static readonly string[] 동작들 = { "대기", "걷기", "뛰기", "공격", "피격", "죽음" };

    [Tooltip("진열할 때 모든 몸을 이 키로 맞춘다 (m)")] public float 진열키 = 3f;
    [Tooltip("가로(동작) 간격 (m)")] public float 가로간격 = 7f;
    [Tooltip("세로(몸) 간격 (m)")] public float 세로간격 = 7f;
    [Tooltip("한 번 끝난 동작을 다시 돌리기 전 쉼 (초)")] public float 다시쉼 = 0.6f;

    /// ★몸을 바라보는 각도 — 카메라에서 몇 도 돌려 세우나.
    ///   90 = **옆모습**. 걷기·뛰기는 다리가 앞뒤로 움직이므로 정면에서 보면
    ///   아무 일도 안 일어나는 것처럼 보인다. 옆으로 세워야 걸음이 읽힌다.
    [Tooltip("카메라 기준 몇 도 돌려 세우나 (90 = 옆모습)")] public float 세우는각 = 90f;

    GameObject 그릇;
    readonly List<한마리> 목록 = new();

    class 한마리
    {
        public Animator anim;
        public string 상태;      // 컨트롤러 안의 상태 이름 = 동작 이름
        public float 길이;
        public bool 반복;        // 클립 자체가 도는가 (대기·걷기·뛰기)
        public float 다음;       // 이 시각이 지나면 다시 돌린다
    }

    // 되돌리려고 적어 두는 것들
    VisionCone 시야; DayNight 낮밤; Renderer 덮개;
    bool 옛시야, 옛낮밤, 옛덮개;

    public bool 켜짐 { get; private set; }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null && k.f1Key.wasPressedThisFrame) { if (켜짐) 치우기(); else 세우기(); }
#endif
        if (!켜짐) return;

        // 한 번짜리 동작을 다시 돌린다
        for (int i = 0; i < 목록.Count; i++)
        {
            var m = 목록[i];
            if (m.반복 || m.anim == null) continue;
            if (Time.time < m.다음) continue;
            m.anim.Play(m.상태, 0, 0f);
            m.다음 = Time.time + m.길이 + 다시쉼;
        }
    }

    // ───────────────────────────────── 세우기

    void 세우기()
    {
        var 몸들 = 몸찾기();
        if (몸들.Count == 0)
        { Debug.LogError("[동작진열] Resources/rig 에 리깅된 모델이 없다"); return; }

        치우기();                       // 남은 게 있으면 먼저 지운다
        그릇 = new GameObject("동작진열");

        // 카메라 각도만 **읽는다** — 격자를 화면 축에 맞춰 놓으려는 것이지, 카메라를
        // 건드리려는 게 아니다. 화면이 45° 로 기울어 있으니 격자도 같이 기울어야 반듯하게 보인다.
        var 눈 = FindFirstObjectByType<IsoCam>();
        float yaw = 눈 != null ? 눈.yaw : 45f;
        var 화면 = Quaternion.Euler(0f, yaw, 0f);
        var 오른 = 화면 * Vector3.right;      // 화면 오른쪽 (가로 = 동작)
        var 위쪽 = 화면 * Vector3.forward;    // 화면 위쪽 (세로 = 몸)

        int 열 = 동작들.Length, 행 = 몸들.Count;

        // ★캐릭터 **바로 앞**에 편다 — 걸어 들어가서 보는 곳이라 멀면 안 된다
        var 가운데 = 그릇위치() + 위쪽 * ((행 - 1) * 0.5f * 세로간격 + 앞으로);
        그릇.transform.position = 가운데;

        int 만든수 = 0;
        var 빠진 = new List<string>();
        for (int r = 0; r < 행; r++)
        {
            var 몸 = 몸들[r];
            for (int c = 0; c < 열; c++)
            {
                var ctrl = Resources.Load<RuntimeAnimatorController>("rig/" + 동작들[c] + "_" + 몸.name);
                // ★안 구워진 칸은 **비워 두고 이름을 적어 둔다** — 조용히 건너뛰면
                //   "왜 저 자리만 안 움직이지?" 를 영원히 모른다
                if (ctrl == null) { 빠진.Add(동작들[c] + "_" + 몸.name); continue; }

                var at = 가운데
                       + 오른 * ((c - (열 - 1) * 0.5f) * 가로간격)
                       + 위쪽 * (((행 - 1) * 0.5f - r) * 세로간격);

                var go = Instantiate(몸, at, Quaternion.Euler(0f, yaw + 세우는각, 0f), 그릇.transform);
                go.name = 몸.name + "·" + 동작들[c];

                // ★★뼈 뿌리를 `Armature` 껍데기 안에 넣는다 — **이걸 빼면 한 대도 안 움직인다.**
                //   구운 클립의 경로가 언제나 `Armature/spine_hip/…` 인데 glb 로 내보내면 그
                //   한 겹이 없다. 경로가 한 글자만 달라도 **에러 없이 조용히 무시**된다
                //   (`네발동작저작.굽기` 와 `ToyShowcase.리깅세우기` 가 같은 이유로 이걸 한다).
                var 뿌리 = go.transform.Find("spine_hip");
                if (뿌리 != null)
                {
                    var 껍 = new GameObject("Armature").transform;
                    껍.SetParent(go.transform, false);
                    뿌리.SetParent(껍, true);
                }

                var anim = go.GetComponent<Animator>();
                if (anim == null) anim = go.AddComponent<Animator>();
                // ★glTF 로 들여온 것은 **아바타가 안 딸려 온다** — 없으면 한 프레임도 안 움직인다
                //   (`DinoRigShowcase` 에서 겪은 그것). 계층을 바꾼 **뒤에** 만들어야 한다.
                var av = AvatarBuilder.BuildGenericAvatar(go, "");
                av.name = go.name + "_아바타";
                anim.avatar = av;
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;                // 제자리에서 보여준다
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                키맞춤(go, 진열키);

                var 클립 = ctrl.animationClips;
                float 길이 = 클립.Length > 0 ? 클립[0].length : 1f;
                bool 반복 = 클립.Length > 0 && 클립[0].isLooping;
                목록.Add(new 한마리
                {
                    anim = anim, 상태 = 동작들[c],
                    길이 = Mathf.Max(0.1f, 길이), 반복 = 반복,
                    다음 = Time.time + Mathf.Max(0.1f, 길이) + 다시쉼
                });
                만든수++;
            }
        }

        켜짐 = true;
        밝게();
        Debug.Log($"[동작진열] 몸 {행} × 동작 {열} = {만든수}마리 — 걸어 들어가서 보세요. F1 로 치웁니다.");
        if (빠진.Count > 0)
            Debug.LogWarning("[동작진열] 클립이 없어 빈 자리: " + string.Join(", ", 빠진));
    }

    /// `Resources/rig` 안에서 **스킨 메시가 든 모델**만 골라 이름순으로
    static List<GameObject> 몸찾기()
    {
        var 찾은 = new List<GameObject>();
        foreach (var go in Resources.LoadAll<GameObject>("rig"))
            if (go != null && go.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                찾은.Add(go);
        찾은.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return 찾은;
    }

    [Tooltip("캐릭터에서 격자 앞줄까지 (m)")] public float 앞으로 = 10f;

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
    //
    // ★카메라는 여기 없다. 캐릭터를 따라다니는 평소 그대로 둔다 — 걸어다니며 보는 곳이다.

    void 밝게()
    {
        // ①밤이면 아무것도 안 보인다 → 낮으로 못박는다.
        //   `DayNight` 를 끄기만 하면 되고, 되켜면 그쪽이 알아서 원래 조명으로 돌려놓는다.
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
    }

    void 되돌리기()
    {
        if (낮밤 != null) { 낮밤.enabled = 옛낮밤; 낮밤 = null; }
        if (시야 != null) { 시야.enabled = 옛시야; 시야 = null; }
        if (덮개 != null) { 덮개.enabled = 옛덮개; 덮개 = null; }
    }

    // ───────────────────────────────── 키 맞추기

    /// ★구운 메시의 실제 높이로 잰다 — 바운즈는 파일마다 부풀림이 달라 못 믿는다
    ///   (`HeroSetup.키맞춤` · `DinoRigShowcase` 에서 두 번 데인 그것)
    static void 키맞춤(GameObject go, float 키)
    {
        go.transform.localScale = Vector3.one;
        if (!구운높이(go, out float 바닥, out float 머리)) return;
        float h = 머리 - 바닥;
        if (h > 0.0001f) go.transform.localScale = Vector3.one * (키 / h);
        if (구운높이(go, out 바닥, out _))
            go.transform.position += Vector3.up * -바닥;
    }

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
            if (구움) Destroy(m);
        }
        return 있음;
    }
}
