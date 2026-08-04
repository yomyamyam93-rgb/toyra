using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

/// 픽셀 화면 — **① 저해상도로 그리고 ② 픽셀 격자에 맞춰 확대**한다.
/// 나머지 마감(③④⑤⑥)은 `PixelDisplay.shader` 가 한다.
///
/// ★핵심은 ②다. 저해상도로만 그리면 카메라가 움직일 때 **픽셀이 기어다닌다**(픽셀 크롤).
///   그래서 **카메라를 픽셀 격자에 딱 맞춰 스냅**하고, 스냅하느라 버린 소수점만큼
///   **화면을 되민다.** 픽셀은 또렷한데 이동은 부드럽다 — 상용 결과물이 갈리는 자리.
///
/// ★렌더 텍스처에 여유 2픽셀을 두고 가운데만 보여준다. 안 그러면 되밀 때 가장자리가 샌다.
[DefaultExecutionOrder(200)]          // IsoCam(50) 다음에 돈다 — 카메라가 자리를 잡은 뒤 스냅
[RequireComponent(typeof(Camera))]
public class PixelScreen : MonoBehaviour
{
    [Header("① 저해상도")]
    [Tooltip("화면을 몇 분의 1로 그리나. 4 면 1920 화면이 480 으로 그려진다")]
    [Range(1, 12)] public int 축소 = 4;

    // ★★픽셀 밀도를 고정한다 (2026-08-04 사용자 — "줌할 때 해상도가 좋아지는 것 같다,
    //   픽셀은 고정돼야 하지 않나"). 맞다. 픽셀아트는 **1미터가 언제나 같은 픽셀 수**다.
    //   그래서 카메라의 시야 크기를 여기서 정한다: 시야 = 렌더 높이 ÷ (2 × 픽셀당미터).
    //   줌은 **축소 배율**로 한다 — 줌아웃하면 더 굵은 픽셀로 더 넓게 본다.
    // ★이 값이 곧 「계단이 얼마나 잘게 지나」다 (2026-08-04). 테두리를 바깥에 그리면
    //   비스듬한 면에서 계단이 도드라지는데, 픽셀이 고와지면 그게 눈에 덜 띈다.
    //   ★대가: 보이는 범위가 좁아진다 (보이는 폭 = 렌더 가로 ÷ 이 값).
    //     넓히려면 Game 창을 키우거나 `축소`를 낮춘다.
    [Tooltip("1미터가 몇 픽셀인가. 그림의 결과 계단 크기를 정한다 (18 = 사람 키가 32픽셀)")]
    [Range(4f, 64f)] public float 픽셀당미터 = 18f;
    [Tooltip("휠로 축소 배율을 바꾼다 (픽셀 밀도는 그대로, 보이는 범위가 달라진다)")]
    public bool 휠로줌 = true;
    [Range(1, 12)] public int 최소축소 = 2;
    [Range(1, 12)] public int 최대축소 = 8;

    [Header("② 픽셀 스냅")]
    [Tooltip("카메라를 픽셀 격자에 맞춘다 (끄면 지글거림이 보인다)")]
    public bool 스냅 = true;
    [Tooltip("되미는 방향이 반대로 보이면 체크")] public bool 밀기반대 = false;

    [Header("③④ 색")]
    [Tooltip("채널당 색 단계. 낮을수록 옛날 게임 같다 (2 미만이면 안 씀)")]
    [Range(2, 64)] public float 색단계 = 16f;

    // ★★디더링은 기본으로 끈다 (2026-08-04 사용자 — "픽셀 색상이 계속 변하니까 자글자글해").
    //   디더 무늬는 **화면에 고정**돼 있는데 세상이 그 위로 흘러간다. 그래서 같은 지점의
    //   색이 프레임마다 바뀌어 끓는 것처럼 보인다. 정지 화면에서는 예쁘고 움직이면 최악이다.
    //   → 색 단계를 넉넉히 두는 쪽이 낫다. 쓰더라도 0.1 언저리까지만.
    [Range(0f, 1f)] public float 디더링 = 0f;
    [Range(0f, 2f)] public float 채도 = 1f;

    // ★검정, 딱 한 칸, **실루엣만** (2026-08-04 사용자 확정 — "최외곽선만, 매시에 전부 넣는 게 아니라")
    [Header("⑤ 외곽선")]
    // ★테두리는 **실루엣 마스크**로 뽑는다. `Outliner` 가 흰 복사본을 전용 층에 만들고,
    //   마스크 카메라가 그것만 찍는다. 모두 같은 흰색이라 안쪽 경계가 아예 없다.
    [Tooltip("체크 = 실루엣 마스크로 테두리만 · 해제 = 색으로 모든 경계")]
    public bool 실루엣만 = true;
    [Range(0f, 1f)] [Tooltip("1 이면 완전히 새까맣다")] public float 외곽선세기 = 1f;
    [Tooltip("체크하면 물체 안쪽에 그린다 (모양을 한 칸 갉아먹는다). 기본은 바깥에 두르기")]
    public bool 안쪽에그리기 = false;
    [Range(0.01f, 1f)] [Tooltip("색으로 잡을 때의 문턱")] public float 외곽선문턱 = 0.10f;
    [Range(1f, 3f)] [Tooltip("한 칸이 기본. 올리면 뭉툭해진다")] public float 외곽선두께 = 1f;

    // ★★이게 「붉은색이면 붉은색과 어두운 붉은색, 두 단계」를 만드는 자리다
    //   (2026-08-04 사용자 — "잡다한 색들이 섞여 있어서"). 밝기만 계단으로 끊고
    //   색조는 그대로 두므로, 같은 색의 밝고 어두운 단만 남는다.
    // ★기본은 꺼 둔다 (2026-08-04 사용자 "펫 색상이 다 망가졌어"). 밝기를 몇 단으로
    //   끊으면 색이 정돈되는 대신 **원래 색이 뭉개진다** — 특히 중간 밝기의 펫이
    //   제일 어두운 칸으로 떨어져 시커멓게 나온다. 쓰려면 5~6단부터가 안전하다.
    [Header("⑥ 명암 끊기 — 세게 걸면 색이 뭉개진다")]
    [Tooltip("밝기를 몇 단으로 끊나 (0 이면 안 씀). 5~6이 무난, 3 이하는 색이 망가진다")]
    [Range(0f, 12f)] public float 명암단계 = 0f;

    [Header("켜고 끄기")]
    [Tooltip("F2 로도 켜고 끈다 — 있고 없고를 바로 비교하려고")]
    public bool 켬 = true;

    // ★삼각형이 픽셀보다 작으면 카메라가 조금만 움직여도 나타났다 사라졌다 하며 반짝인다
    //   (2026-08-04 — 캐릭터가 15만 삼각형인데 화면에선 56픽셀이다).
    //   저해상도 그림 **안에서만** 부드럽게 하는 것이라, 확대해도 픽셀은 또렷하다.
    [Tooltip("저해상도 그림 안의 지글거림을 줄인다 (1 = 안 씀 · 2~4 = 부드럽게)")]
    [Range(1, 8)] public int 부드럽게 = 2;

    [Header("진단")]
    [Tooltip("F3 — 마스크(컴퓨터가 보는 실루엣)를 그대로 띄운다. 검정=배경 초록=잔디 흰색=물체")]
    public bool 마스크보기 = false;

    Camera cam;
    Camera 출력;               // ★저해상도 텍스처를 화면에 띄우는 카메라
    Camera 마스크;             // 물체 + 잔디 (보이는 것)
    Camera 물체마스크;         // 물체만 (진짜 윤곽)
    RenderTexture rt, maskRT, objRT;
    RawImage 판;
    RectTransform 판rt;
    Canvas 캔버스;
    Material mat;
    int rtW, rtH, 지난축소, 지난W, 지난H;
    bool 지난켬;

    void OnEnable()
    {
        cam = GetComponent<Camera>();
        만들기();
    }

    void OnDisable() { 치우기(); }

    void 만들기()
    {
        치우기();
        if (!켬) return;

        rtW = Mathf.Max(16, Screen.width / Mathf.Max(1, 축소)) + 2;   // 여유 2픽셀
        rtH = Mathf.Max(16, Screen.height / Mathf.Max(1, 축소)) + 2;

        // ★HDR 이 아니라 보통 형식으로. HDR 값(1 초과)에 색 양자화를 걸면
        //   밝은 자리에서 색이 튄다 (사용자 "색상이 좀 이상해")
        rt = new RenderTexture(rtW, rtH, 24, RenderTextureFormat.Default)
        {
            name = "픽셀화면",
            filterMode = FilterMode.Point,          // ★확대할 때 뭉개지지 않게
            wrapMode = TextureWrapMode.Clamp,
            antiAliasing = Mathf.Clamp(Mathf.ClosestPowerOfTwo(부드럽게), 1, 8)
        };
        rt.Create();
        cam.targetTexture = rt;
        cam.cullingMask &= ~(1 << Outliner.층);        // 흰 복사본은 본 화면에 안 보이게
        cam.cullingMask &= ~(1 << Outliner.잔디층);
        cam.allowMSAA = 부드럽게 > 1;

        // ★★마스크를 **두 장** 찍는다 (2026-08-04):
        //   ①「보이는 것」 = 물체 + 잔디 → 잔디가 **가린 자리**를 알려 준다
        //   ②「물체만」   = 물체       → 테두리를 그릴 **진짜 윤곽**을 알려 준다
        //   한 장으로는 "잔디가 앞을 가린 것"과 "잔디가 그냥 옆에 있는 것"을 구분할 수
        //   없어서, 선을 빼면 테두리가 통째로 사라지고 넣으면 발밑이 새까매졌다.
        maskRT = 마스크판만들기("보이는것");
        objRT = 마스크판만들기("물체만");

        마스크 = 마스크카메라("실루엣_카메라", (1 << Outliner.층) | (1 << Outliner.잔디층), maskRT, cam.depth - 2);
        물체마스크 = 마스크카메라("물체_카메라", 1 << Outliner.층, objRT, cam.depth - 1);

        var sh = Shader.Find("Toyra/PixelDisplay");
        if (sh == null) { Debug.LogError("[픽셀] PixelDisplay.shader 를 못 찾았다"); 켬 = false; return; }
        mat = new Material(sh);

        // ★화면에 그리는 카메라가 하나는 있어야 한다. 메인 카메라는 저해상도 텍스처로
        //   보내 버렸으므로, 아무것도 안 찍고 UI 만 띄우는 카메라를 따로 세운다.
        //   (이게 없으면 유니티가 "No cameras rendering" 을 띄운다 — 실제로 그랬다)
        var camGo = new GameObject("픽셀_출력카메라");
        camGo.transform.SetParent(transform, false);
        출력 = camGo.AddComponent<Camera>();
        // ★UI 층만 찍는다. 0(아무것도 안 찍음)으로 두면 화면에 띄울 판까지 안 그려서
        //   온 화면이 까맣게 나온다 (실제로 그랬다)
        int uiLayer = LayerMask.NameToLayer("UI");
        출력.cullingMask = uiLayer >= 0 ? (1 << uiLayer) : ~0;
        출력.clearFlags = CameraClearFlags.SolidColor;
        출력.backgroundColor = Color.black;
        출력.orthographic = true;
        출력.depth = 100;
        출력.targetTexture = null;

        var go = new GameObject("픽셀_화면");
        if (uiLayer >= 0) go.layer = uiLayer;
        go.transform.SetParent(transform, false);
        캔버스 = go.AddComponent<Canvas>();
        캔버스.renderMode = RenderMode.ScreenSpaceCamera;
        캔버스.worldCamera = 출력;
        캔버스.planeDistance = 1f;
        캔버스.sortingOrder = -100;                  // 다른 UI 보다 아래
        go.AddComponent<CanvasScaler>();

        var img = new GameObject("판");
        if (uiLayer >= 0) img.layer = uiLayer;
        img.transform.SetParent(go.transform, false);
        판 = img.AddComponent<RawImage>();
        판.texture = rt;
        판.material = mat;
        판.raycastTarget = false;
        판.uvRect = new Rect(0f, 0f, 1f, 1f);      // 여유 2픽셀까지 다 쓴다 (되밀 자리)

        // ★★반드시 **정수 배**로 늘린다 (2026-08-04 사용자 "두께가 다 다른 것 같냐").
        //   화면에 맞춰 늘이면(스트레치) 640/2 처럼 딱 안 나눠질 때 어떤 픽셀은 2칸,
        //   어떤 픽셀은 3칸이 되어 **테두리 두께가 들쭉날쭉**해진다.
        //   가운데 정렬로 정수 배만큼만 깔고, 남는 가장자리는 그냥 비워 둔다.
        판rt = img.GetComponent<RectTransform>();
        판rt.anchorMin = 판rt.anchorMax = new Vector2(0.5f, 0.5f);
        판rt.pivot = new Vector2(0.5f, 0.5f);
        판rt.sizeDelta = new Vector2(rtW * 축소, rtH * 축소);
        판rt.anchoredPosition = Vector2.zero;

        지난축소 = 축소; 지난W = Screen.width; 지난H = Screen.height; 지난켬 = 켬;
    }

    RenderTexture 마스크판만들기(string 이름)
    {
        var t = new RenderTexture(rtW, rtH, 16, RenderTextureFormat.R8)
        {
            name = 이름, filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp, antiAliasing = 1
        };
        t.Create();
        return t;
    }

    Camera 마스크카메라(string 이름, int 층마스크, RenderTexture 판, float depth)
    {
        var go = new GameObject(이름);
        go.transform.SetParent(cam.transform, false);
        var c = go.AddComponent<Camera>();
        c.CopyFrom(cam);
        c.cullingMask = 층마스크;
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = Color.clear;
        c.targetTexture = 판;
        c.depth = depth;                                // 본 화면보다 먼저 찍는다
        c.allowHDR = false; c.allowMSAA = false;
        return c;
    }

    void 치우기()
    {
        if (cam != null) cam.targetTexture = null;
        if (캔버스 != null) { if (Application.isPlaying) Destroy(캔버스.gameObject); else DestroyImmediate(캔버스.gameObject); }
        if (출력 != null) { if (Application.isPlaying) Destroy(출력.gameObject); else DestroyImmediate(출력.gameObject); }
        if (마스크 != null) { if (Application.isPlaying) Destroy(마스크.gameObject); else DestroyImmediate(마스크.gameObject); }
        if (물체마스크 != null) { if (Application.isPlaying) Destroy(물체마스크.gameObject); else DestroyImmediate(물체마스크.gameObject); }
        캔버스 = null; 판 = null; 출력 = null; 마스크 = null; 물체마스크 = null;
        if (rt != null) { rt.Release(); Destroy(rt); rt = null; }
        if (maskRT != null) { maskRT.Release(); Destroy(maskRT); maskRT = null; }
        if (objRT != null) { objRT.Release(); Destroy(objRT); objRT = null; }
        mat = null;
    }

    void LateUpdate()
    {
#if ENABLE_INPUT_SYSTEM
        var k = UnityEngine.InputSystem.Keyboard.current;
        if (k != null && k.f2Key.wasPressedThisFrame) 켬 = !켬;
        if (k != null && k.f3Key.wasPressedThisFrame) 마스크보기 = !마스크보기;

        // ★줌 = 축소 배율. 픽셀 밀도는 그대로 두고 보이는 범위만 바꾼다
        if (켬 && 휠로줌)
        {
            var m = UnityEngine.InputSystem.Mouse.current;
            float sc = m != null ? m.scroll.ReadValue().y : 0f;
            // ★부호 주의: 픽셀 밀도를 고정했으므로 **축소 배율 = 확대 배율**이다.
            //   축소가 크면 픽셀이 굵어지고 보이는 범위가 좁아진다 = 줌인.
            //   (여기를 반대로 뒀다가 휠 방향이 뒤집혔다)
            if (Mathf.Abs(sc) > 0.01f)
                축소 = Mathf.Clamp(축소 + (sc > 0f ? 1 : -1), 최소축소, 최대축소);
        }
#endif
        // 설정이 바뀌면 다시 만든다
        if (켬 != 지난켬 || 축소 != 지난축소 || Screen.width != 지난W || Screen.height != 지난H)
        { 만들기(); if (!켬) return; }
        if (!켬 || mat == null || cam == null) return;

        // ★★픽셀 밀도 고정 — 1미터가 언제나 같은 픽셀 수가 되도록 시야를 정한다.
        //   (이걸 안 하면 줌할 때마다 물체의 픽셀이 잘아졌다 굵어졌다 한다)
        float ppu = Mathf.Max(1f, 픽셀당미터);
        if (cam.orthographic) cam.orthographicSize = (rtH - 2) / (2f * ppu);

        // ── ② 픽셀 스냅 + 되밀기
        Vector2 밀기 = Vector2.zero;
        if (스냅 && cam.orthographic)
        {
            var p = cam.transform.position;
            var right = cam.transform.right;
            var up = cam.transform.up;

            float a = Vector3.Dot(p, right), b = Vector3.Dot(p, up);
            float sa = Mathf.Round(a * ppu) / ppu;
            float sb = Mathf.Round(b * ppu) / ppu;

            cam.transform.position = p + right * (sa - a) + up * (sb - b);

            // ★부호 주의: 카메라를 sa 로 옮겨 찍었으므로, 원래 자리(a)에서 본 것처럼
            //   보이려면 화면을 **반대로** 밀어야 한다. 부호를 반대로 뒀더니 오차가
            //   상쇄되는 대신 두 배가 되어 카메라가 두두두둑 끊겼다 (2026-08-04).
            밀기 = new Vector2(-(a - sa) * ppu, -(b - sb) * ppu);
            if (밀기반대) 밀기 = -밀기;
        }

        // ★되밀기는 **화면 쪽**에서 한다. UV 로 밀면 점 필터라 한 텍셀씩 튀어서
        //   오히려 반짝거린다 (실제로 그랬다). 판 자체를 화면 픽셀 단위로 민다
        if (판rt != null)
            판rt.anchoredPosition = new Vector2(밀기.x * 축소, 밀기.y * 축소);
        mat.SetVector("_Offset", Vector4.zero);
        mat.SetFloat("_Levels", 색단계);
        mat.SetFloat("_Dither", 디더링);
        mat.SetFloat("_Sat", 채도);
        mat.SetFloat("_Bands", 명암단계 >= 2f ? 명암단계 : 0f);
        mat.SetFloat("_OutlineW", 외곽선두께);
        mat.SetFloat("_OutlineT", 외곽선문턱);
        mat.SetFloat("_OutlineS", 외곽선세기);
        mat.SetFloat("_UseDepth", 실루엣만 ? 1f : 0f);
        mat.SetFloat("_OutlineIn", 안쪽에그리기 ? 1f : 0f);
        mat.SetFloat("_ShowMask", 마스크보기 ? 1f : 0f);
        mat.SetTexture("_Mask", maskRT);
        mat.SetTexture("_ObjMask", objRT);

        // 마스크 카메라들은 본 카메라를 그대로 따라간다 (스냅까지 끝난 뒤라 어긋나지 않는다)
        foreach (var c in new[] { 마스크, 물체마스크 })
        {
            if (c == null) continue;
            c.orthographic = cam.orthographic;
            c.orthographicSize = cam.orthographicSize;
            c.nearClipPlane = cam.nearClipPlane;
            c.farClipPlane = cam.farClipPlane;
        }
    }
}
