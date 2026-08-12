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
    public float minSize = 4f, maxSize = 60f;      // ★픽셀 제거로 줌 폭을 넓혔다
    public float zoomStep = 1.4f;                  // (은퇴 — 아래 `줌비율` 이 쓰인다)
    [Tooltip("휠 한 칸에 몇 % 씩 — 0.10 이면 10% 씩 부드럽게")]
    [Range(0.02f, 0.3f)] public float 줌비율 = 0.10f;
    [Tooltip("클수록 목표 줌에 빨리 붙는다")] [Range(3f, 25f)] public float 줌따라붙기 = 12f;

    [Tooltip("지금 줌·속도를 화면에 띄운다 (숫자를 정하는 동안만)")]
    public bool 값보기 = true;

    [Header("느낌")]
    [Tooltip("마우스 쪽으로 밀리는 정도 (0 = 캐릭터 정중앙)")]
    [Range(0f, 0.5f)] public float mouseLead = 0.18f;
    [Tooltip("클수록 딱 붙어 따라온다")] public float follow = 9f;

    // ★★★카메라가 얼마나 뒤에 서나 (2026-08-04 사용자 "그림자 없어").
    //   직교 투영이라 이 값은 **화면을 안 바꾼다** — 그래서 예전엔 넉넉하게 300m 였다.
    //   그런데 **URP 의 그림자 거리는 카메라에서 잰다.** 300m 밖에 서 있으면 화면 안의
    //   모든 것이 그림자 범위(50m) 밖이라, 빛과 재질을 아무리 맞춰도 그림자가 안 나온다.
    //   → 잘림면만 안 걸릴 만큼만 물러난다. 그림자 거리 안에 화면이 통째로 들어온다.
    [Tooltip("카메라가 뒤로 물러나는 거리 (m) — 직교라 화면은 안 변한다. 그림자 거리 안에 들어와야 한다")]
    public float 물러남 = 50f;

    Camera cam;
    PixelSnapper snapper;
    인벤창 창열림; 제작창 제작열림;   // ★창이 열려 있으면 줌·마우스 밀림을 잠근다 (한 번만 찾는다)
    PixelScreen 픽셀;
    float sizeT;
    Vector3 look;

    /// ★마우스 자리를 **본 카메라가 실제로 그리는 화면 기준**으로 옮겨서 광선을 쏜다.
    ///   픽셀 화면이 켜져 있으면 본 카메라는 저해상도 텍스처에 그리므로, 화면 좌표를
    ///   그대로 넣으면 줌인할수록 엉뚱한 데를 가리킨다 (`PixelScreen.화면점` 주석 참고).
    bool 마우스광선(out Ray ray)
    {
        ray = default;
        if (cam == null) return false;
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        if (m == null) return false;
        Vector2 p = m.position.ReadValue();
#else
        Vector2 p = Input.mousePosition;
#endif
        if (픽셀 == null) 픽셀 = GetComponent<PixelScreen>();
        if (픽셀 != null && 픽셀.enabled) p = 픽셀.화면점(p);
        ray = cam.ScreenPointToRay(p);
        return true;
    }

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = gameObject.AddComponent<Camera>();
        cam.orthographic = true;
        // 물러난 거리에 맞춰 잘림면을 좁힌다 — 넓게 잡아 봐야 얻는 게 없고 깊이 정밀도만 잃는다
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 물러남 * 2f + 120f;
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

        // ★★★**창을 열면 카메라를 잠근다** (2026-08-12 사용자 "인벤토리 열었을때 카메라
        //   마우스 방향으로 따라가면서 밀리는거 없애주고, 스크롤로 확대축소 되는것도 막아줘").
        //   맞는 지적이다 — 창 안에서 마우스를 움직이면 그건 **칸을 고르는 손**이지
        //   시선이 아닌데, 카메라가 따라 밀리면 뒤에서 화면이 출렁인다.
        //   휠도 마찬가지다: 창에서 휠은 **목록을 굴리는 것**이라 줌이 같이 먹으면 안 된다.
        //   ☆WASD 는 그대로 둔다 — 창을 열어도 게임이 안 멈추는 것이 이 게임의 규칙이고
        //     (기획 5-7), 짐 정리하다 습격당하면 걸어서 피할 수 있어야 한다.
        //   ☆제작창(C)도 같이 본다 — 같은 이유다
        if (창열림 == null) 창열림 = FindFirstObjectByType<인벤창>();
        if (제작열림 == null) 제작열림 = FindFirstObjectByType<제작창>();
        bool 창 = (창열림 != null && 창열림.열림) || (제작열림 != null && 제작열림.열림);

        if (!픽셀이쥠 && !창)
        {
            // 줌 — 휠. 회전 입력은 아예 읽지 않는다 (고정 카메라)
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            float sc = m != null ? m.scroll.ReadValue().y : 0f;
#else
            float sc = Input.GetAxis("Mouse ScrollWheel") * 100f;
#endif
            // ★★**줌을 곱셈으로 바꿨다** (2026-08-07 사용자 "카메라도 좀 부드럽게 확대축소되게").
            //   더하기(±1.4m)면 가까이서는 한 칸이 확 뛰고 멀리서는 거의 안 움직인다 —
            //   같은 휠 한 칸인데 체감이 완전히 다르다. **비율**로 하면 어느 배율에서나 고르다.
            if (Mathf.Abs(sc) > 0.01f)
                sizeT = Mathf.Clamp(sizeT * (sc > 0f ? 1f - 줌비율 : 1f + 줌비율), minSize, maxSize);
            size = Mathf.Lerp(size, sizeT, 1f - Mathf.Exp(-줌따라붙기 * Time.deltaTime));
            cam.orthographicSize = size;
        }
        else size = cam.orthographicSize;

        // 바라보는 지점 — 캐릭터 + 마우스 쪽으로 조금.
        // ★캐릭터는 픽셀 격자로 끊겨 움직이므로, 그 자리를 그대로 좇으면 카메라가
        //   한 칸씩 튄다. **끊기 전 진짜 자리**를 따라가야 부드럽다 (2026-08-04).
        if (snapper == null) snapper = FindFirstObjectByType<PixelSnapper>();
        var 기준 = snapper != null ? snapper.진짜자리(target) : target.position;
        // ★창이 열려 있으면 마우스 쪽으로 안 민다 — 캐릭터 정중앙을 본다 (위 설명)
        var want = 기준 + (창 ? Vector3.zero : MouseLead());
        // ★★프레임률에 안 흔들리는 따라가기 (2026-08-04 사용자 "카메라가 너무 저프레임으로
        //   움직이는 느낌이 나"). `follow * dt` 를 그대로 섞으면 **한 프레임이 길어질 때마다
        //   더 많이 따라잡아** 프레임이 튀는 만큼 카메라가 덜컥거린다. 프레임이 고르지
        //   않을수록 심해져서 「저프레임처럼 보이는」 정체가 된다.
        //   지수식은 dt 가 얼마든 **같은 시간에 같은 만큼** 좁혀서 그 덜컥임이 사라진다.
        //   (`HeroAnim` 이 이미 쓰는 식과 같다)
        look = Vector3.Lerp(look, want, 1f - Mathf.Exp(-follow * Time.deltaTime));

        Apply();
    }

    void Apply()
    {
        var rot = Quaternion.Euler(pitch, yaw, 0f);
        transform.rotation = rot;
        transform.position = look + rot * Vector3.back * 물러남;
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
        if (!마우스광선(out var ray)) return Vector3.zero;
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
        if (!마우스광선(out var ray)) return false;
        var plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
        if (!plane.Raycast(ray, out float t)) return false;
        hit = ray.GetPoint(t);
        return true;
    }
}
