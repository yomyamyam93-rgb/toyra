using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// 고정 아이소메트릭 카메라 — **회전 없음. 줌만.** (좀보이드 방식)
///
/// ★왜 고정인가: ①WASD 방향이 화면 기준으로 **언제나 같다** — 위 키를 누르면 언제나
///   화면 위로 간다. 위치선정이 핵심인 게임에서 이게 크다. ②평지라서 돌려 볼 이유가
///   없다 (옛 프로젝트가 회전을 되살린 이유는 6km 지형의 높낮이와 엄폐물이었다).
///
/// ★직교(Orthographic) 투영 — 멀리 있어도 작아지지 않아 화면 전체가 똑같이 읽힌다.
///
/// ★카메라가 마우스 쪽으로 살짝 밀린다 — **가려는 쪽을 조금 더 보여준다.**
///   좀보이드 손맛의 은근히 큰 부분.
[DefaultExecutionOrder(50)]
public class IsoCam : MonoBehaviour
{
    [Header("따라갈 대상 (비우면 Hero 를 찾는다)")]
    public Transform target;

    [Header("각도 — 고정")]
    [Tooltip("좌우 각도 (°)")] public float yaw = 45f;
    [Tooltip("내려보는 각도 (°) — 클수록 위에서 본다")] public float pitch = 40f;

    [Header("줌 (휠)")]
    [Tooltip("화면 세로 절반 (m). 14 면 세로 28m 가 보인다")] public float size = 14f;
    public float minSize = 6f, maxSize = 26f;
    public float zoomStep = 1.4f;

    [Tooltip("지금 줌·속도를 화면에 띄운다 (숫자를 정하는 동안만)")]
    public bool 값보기 = true;

    [Header("느낌")]
    [Tooltip("마우스 쪽으로 밀리는 정도 (0 = 캐릭터 정중앙)")]
    [Range(0f, 0.5f)] public float mouseLead = 0.18f;
    [Tooltip("클수록 딱 붙어 따라온다")] public float follow = 9f;

    Camera cam;
    PixelSnapper snapper;
    float sizeT;
    Vector3 look;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = gameObject.AddComponent<Camera>();
        cam.orthographic = true;
        cam.nearClipPlane = 1f;
        cam.farClipPlane = 800f;
        sizeT = size;
        if (target == null)
        {
            var h = FindFirstObjectByType<Hero>();
            if (h != null) target = h.transform;
        }
        if (target != null) look = target.position;
        Apply();
    }

    void LateUpdate()
    {
        if (target == null) return;

        // ★픽셀 화면이 켜져 있으면 줌·시야는 그쪽이 쥔다 — 픽셀 밀도를 고정해야 하므로
        //   시야 크기를 마음대로 바꿀 수 없다 (`PixelScreen.픽셀당미터`)
        var px = GetComponent<PixelScreen>();
        bool 픽셀이쥠 = px != null && px.enabled && px.켬;

        if (!픽셀이쥠)
        {
            // 줌 — 휠. 회전 입력은 아예 읽지 않는다 (고정 카메라)
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            float sc = m != null ? m.scroll.ReadValue().y : 0f;
#else
            float sc = Input.GetAxis("Mouse ScrollWheel") * 100f;
#endif
            if (Mathf.Abs(sc) > 0.01f) sizeT = Mathf.Clamp(sizeT - Mathf.Sign(sc) * zoomStep, minSize, maxSize);
            size = Mathf.Lerp(size, sizeT, 10f * Time.deltaTime);
            cam.orthographicSize = size;
        }
        else size = cam.orthographicSize;

        // 바라보는 지점 — 캐릭터 + 마우스 쪽으로 조금.
        // ★캐릭터는 픽셀 격자로 끊겨 움직이므로, 그 자리를 그대로 좇으면 카메라가
        //   한 칸씩 튄다. **끊기 전 진짜 자리**를 따라가야 부드럽다 (2026-08-04).
        if (snapper == null) snapper = FindFirstObjectByType<PixelSnapper>();
        var 기준 = snapper != null ? snapper.진짜자리(target) : target.position;
        var want = 기준 + MouseLead();
        look = Vector3.Lerp(look, want, follow * Time.deltaTime);

        Apply();
    }

    void Apply()
    {
        var rot = Quaternion.Euler(pitch, yaw, 0f);
        transform.rotation = rot;
        transform.position = look + rot * Vector3.back * 300f;   // 직교라 거리는 잘림면만 정한다
    }

    /// 숫자를 정하는 동안만 띄우는 값 — 화면 가로지르는 시간이 곧 체감 템포다
    void OnGUI()
    {
        if (!값보기 || cam == null) return;
        float w = size * 2f * cam.aspect;                       // 화면 가로 (m)
        var h = target != null ? target.GetComponent<Hero>() : null;
        float spd = h != null ? (h.Running ? h.run : h.walk) : 0f;
        var st = new GUIStyle(GUI.skin.label) { fontSize = 18 };
        st.normal.textColor = Color.white;
        GUI.Label(new Rect(12, 8, 700, 30),
            $"줌 {size:0.0}  (화면 가로 {w:0}m)   속도 {spd:0.0}m/s   가로지르기 {w / Mathf.Max(0.1f, spd):0.0}초", st);
    }

    /// 마우스가 가리키는 땅으로 조금 당겨진 오프셋
    Vector3 MouseLead()
    {
        if (mouseLead <= 0f || cam == null) return Vector3.zero;
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        if (m == null) return Vector3.zero;
        var ray = cam.ScreenPointToRay(m.position.ReadValue());
#else
        var ray = cam.ScreenPointToRay(Input.mousePosition);
#endif
        var plane = new Plane(Vector3.up, target.position);
        if (!plane.Raycast(ray, out float t)) return Vector3.zero;
        var v = ray.GetPoint(t) - target.position;
        v.y = 0f;
        // 화면 밖으로 튀지 않게 시야 크기에 비례해 제한
        float max = size * 1.6f;
        if (v.magnitude > max) v = v.normalized * max;
        return v * mouseLead;
    }

    /// 마우스가 가리키는 땅 위 지점 (다른 코드도 쓴다)
    public bool MouseGround(float y, out Vector3 hit)
    {
        hit = Vector3.zero;
        if (cam == null) return false;
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        if (m == null) return false;
        var ray = cam.ScreenPointToRay(m.position.ReadValue());
#else
        var ray = cam.ScreenPointToRay(Input.mousePosition);
#endif
        var plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
        if (!plane.Raycast(ray, out float t)) return false;
        hit = ray.GetPoint(t);
        return true;
    }
}
