using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// 장난감 진열 — 옛 프로젝트에서 가져온 펫들을 **인게임에서** 쭉 세워 놓고 본다.
///
/// ★씬 창이 아니라 게임 화면이어야 하는 이유: 픽셀 마감·시야·낮밤이 전부 걸린 상태로
///   봐야 진짜 인상을 알 수 있다. 씬 창에서는 예뻤는데 게임에서는 안 읽히는 일이 흔하다.
///
/// F4 로 세우고 치운다. `Resources/toys` 에 있는 모델을 전부 불러온다.
public class ToyShowcase : MonoBehaviour
{
    [Tooltip("F4 를 누르면 세운다")] public bool 시작할때세우기 = false;
    [Tooltip("한 줄에 몇 마리")] public int 한줄 = 10;
    [Tooltip("좌우 간격 (m)")] public float 간격 = 4f;
    [Tooltip("줄 간격 (m)")] public float 줄간격 = 5f;
    [Tooltip("전부 이 키(m)로 맞춘다 — 원래 크기가 제각각이라 그대로 두면 비교가 안 된다")]
    public float 키 = 2f;
    [Tooltip("캠프에서 이만큼 앞에 세운다 (m)")] public float 앞으로 = 14f;

    Transform 진열;

    void Start() { if (시작할때세우기) 세우기(); }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        // ★F1 은 `동작진열`(네발 동작 60개 격자)이 가져갔다 — 이쪽은 F4 로 물러난다
        //   (2026-08-05 사용자 "F1 눌렀을 때 동작을 반복하도록"). 은퇴가 아니라 자리 옮김이다.
        if (k != null && k.f4Key.wasPressedThisFrame)
        {
            if (진열 != null) 치우기(); else 세우기();
        }
#endif
    }

    // ★★리깅된 것도 같이 세운다 (2026-08-05 사용자 "쭉 팻들 나열해뒀던거 보여줘").
    //   `Resources/rig` 은 뼈가 든 모델이다. 세울 때 애니메이터를 물려 **움직이는 채로** 보여야
    //   모션이 맞는지 판단할 수 있다. 정지 모델만 늘어놓으면 크기 비교밖에 안 된다.
    [Header("리깅된 것")]
    [Tooltip("Resources/rig 의 모델도 같이 세운다")] public bool 리깅도 = true;
    [Tooltip("리깅된 것에 물릴 공용 클립 (Resources 기준 경로)")]
    public string 공용컨트롤러 = "rig/네발_공용";
    [Tooltip("변종에 하나씩 돌려 물릴 동작 — `네발동작표` 가 굽는 이름과 같아야 한다")]
    public string[] 동작들 = { "걷기", "공격", "뛰기", "대기", "피격", "죽음" };

    public void 세우기()
    {
        치우기();
        var 모델 = Resources.LoadAll<GameObject>("toys");
        var 리깅 = 리깅도 ? Resources.LoadAll<GameObject>("rig") : new GameObject[0];
        if ((모델 == null || 모델.Length == 0) && 리깅.Length == 0)
        {
            Debug.LogWarning("[진열] Resources/toys · rig 에 모델이 없다.");
            return;
        }

        진열 = new GameObject("장난감_진열").transform;
        // ★캐릭터 앞에 세운다 — 월드 한가운데에 세우면 멀리 있을 때 못 본다
        var 주인 = Hero.Me;
        var c = 주인 != null ? 주인.transform.position : (Vector3)WorldGrid.Center;
        진열.position = new Vector3(c.x - 간격 * (한줄 - 1) * 0.5f, 0f, c.z + 앞으로);

        // 리깅된 것 먼저 — 앞줄에 세워야 눈에 띈다
        // ★★변종마다 **다른 동작**을 물린다 (2026-08-05 사용자 "한 종에 여러 가지 재질 팻이
        //   있으니까 각각 적용해서 보여주면 되잖아"). 한 동작씩 돌려 보면 앞의 것을 잊는다 —
        //   여섯 동작이 한 화면에 같이 돌아야 비교가 된다. 재질만 다른 브론토 다섯이 마침
        //   동작 수와 맞는다.
        for (int i = 0; i < 리깅.Length; i++)
        {
            var pos = 진열.position + new Vector3((i % 한줄) * 간격, 0f, -줄간격);
            var g = Instantiate(리깅[i], pos, Quaternion.identity, 진열);
            var 동작 = 동작들[i % 동작들.Length];
            g.name = 리깅[i].name + " · " + 동작;
            // ★그 몸에 **맞춰 구운** 컨트롤러를 찾는다 (`네발동작저작` 이 만든다).
            //   공용 클립을 그대로 걸면 뼈의 기본 자세가 달라 몸이 어그러진다.
            var 몸컨트롤러 = Resources.Load<RuntimeAnimatorController>("rig/" + 동작 + "_" + 리깅[i].name);
            if (몸컨트롤러 == null) 몸컨트롤러 = Resources.Load<RuntimeAnimatorController>("rig/걷기_" + 리깅[i].name);
            if (몸컨트롤러 == null) 몸컨트롤러 = Resources.Load<RuntimeAnimatorController>(공용컨트롤러);
            리깅세우기(g, 몸컨트롤러);
            맞춤(g, 키);
        }

        // 사람 1.8m — 옆에 없으면 큰지 작은지 알 수가 없다
        Grey.Box(진열, 진열.position + Vector3.left * 간격 + Vector3.up * 0.9f,
                 new Vector3(0.5f, 1.8f, 0.35f), new Color(0.9f, 0.9f, 0.9f), "사람_1.8m");

        var 이름들 = new List<string>();
        for (int i = 0; i < 모델.Length; i++)
        {
            var pos = 진열.position + new Vector3((i % 한줄) * 간격, 0f, (i / 한줄) * 줄간격);
            var g = Instantiate(모델[i], pos, Quaternion.identity, 진열);
            g.name = 모델[i].name;
            맞춤(g, 키);
            이름들.Add(모델[i].name);
        }
        Debug.Log($"[진열] {모델.Length}마리 — F1 로 치웁니다.");
    }

    public void 치우기()
    {
        if (진열 == null) return;
        Destroy(진열.gameObject);
        진열 = null;
    }

    /// 리깅된 모델을 **움직이는 채로** 세운다.
    /// ★뼈 뿌리를 `Armature` 껍데기 안에 넣어야 한다 — 공용 클립이 뼈를 찾는 경로가
    ///   `Armature/spine_hip/…` 인데, 블렌더에서 내보내면 그 한 겹이 사라진다.
    ///   한 글자만 달라도 **에러 없이 조용히 무시**되므로 여기서 맞춰 준다.
    /// ★아바타는 계층을 바꾼 **뒤에** 만들어야 한다 (계층이 바뀌면 무효가 된다).
    static void 리깅세우기(GameObject g, RuntimeAnimatorController 컨트롤러)
    {
        var 뿌리 = g.transform.Find("spine_hip");
        if (뿌리 != null)
        {
            var 껍 = new GameObject("Armature").transform;
            껍.SetParent(g.transform, false);
            뿌리.SetParent(껍, true);
        }
        var anim = g.GetComponent<Animator>();
        if (anim == null) anim = g.AddComponent<Animator>();
        anim.avatar = AvatarBuilder.BuildGenericAvatar(g, "");
        anim.runtimeAnimatorController = 컨트롤러;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    /// 모델마다 내보낸 크기가 제각각이라 키를 맞춰야 나란히 비교가 된다.
    /// ★스킨 메시는 `Renderer.bounds` 가 임포트 때 부풀린 값이라 못 믿는다 — 구워서 잰다
    ///   (`HeroSetup.키맞춤` 에서 두 번 데인 그것)
    static void 맞춤(GameObject g, float 키)
    {
        g.transform.localScale = Vector3.one;
        if (!구운높이(g, out float 바닥, out float 머리)) return;
        float h = 머리 - 바닥;
        if (h > 0.0001f) g.transform.localScale = Vector3.one * (키 / h);
        if (구운높이(g, out 바닥, out _))
            g.transform.position += Vector3.up * (g.transform.position.y - 바닥);
    }

    static bool 구운높이(GameObject g, out float 바닥, out float 머리)
    {
        바닥 = float.MaxValue; 머리 = float.MinValue; bool 있음 = false;
        foreach (var r in g.GetComponentsInChildren<Renderer>(true))
        {
            Mesh m = null; bool 구움 = false;
            if (r is SkinnedMeshRenderer sk && sk.sharedMesh != null)
            { m = new Mesh(); sk.BakeMesh(m, true); 구움 = true; }
            else { var mf = r.GetComponent<MeshFilter>(); m = mf != null ? mf.sharedMesh : null; }
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
