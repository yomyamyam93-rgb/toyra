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
    // ★★★**보이는 폭 = 렌더 가로 ÷ (축소 × 픽셀당미터)** — 이 한 줄이 전부다.
    //   `오버스캔`은 화면 밖 여백이라 넓이를 **안 바꾼다** (2026-08-05 에 한 번 헷갈렸다).
    [Tooltip("1미터가 몇 픽셀인가. 그림의 결이자 계단 크기 (20 = 사람 키가 36픽셀)")]
    [Range(4f, 64f)] public float 픽셀당미터 = 20f;

    // ★★★**화면보다 몇 배 크게 그리나** (2026-08-05 사용자 — "넓은 화면이라치고 4k로
    //   보여지게하면 그 화면을 그냥 비율을 크게 만들고 줄이는 방식으로", "8K 한번해봐").
    //
    //   위 식의 「렌더 가로」를 화면보다 크게 만드는 손잡이다. 2 면 1920 화면을 3840 으로
    //   그린 뒤 **절반으로 줄여 깐다** → 같은 자리에서 **두 배 넓게** 보인다.
    //   픽셀당미터는 하나도 안 건드리므로 **그림의 결은 그대로**다.
    //
    //   ★크기는 이렇게 된다 (1920×1080 창 · 오버스캔 2 · 배수 2):
    //       축소 1 → 7680×4320 (**8K**) · 보이는 폭 192m · 판을 0.5배로 **줄여** 깖
    //       축소 2 → 3840×2160 (4K)     · 96m  · 정확히 1:1
    //       축소 4 → 1920×1080          · 48m  · 2배로 늘려 깖
    //   ★대가는 성능이다. 축소 1 에서만 8K 를 세 장(본화면·마스크 둘) 그린다 —
    //     **가장 넓게 볼 때만** 무겁고, 평소(축소 3)에는 2560×1440 이라 견딜 만하다.
    //     버벅이면 이 값을 1 로 되돌리거나 `최소축소` 를 2 로 올린다.
    [Tooltip("화면보다 몇 배 크게 그리나. 2 = 두 배 넓게 보인다 (축소 1 에서 8K)")]
    [Range(1, 4)] public int 해상배수 = 2;
    [Tooltip("휠로 줌한다")]
    public bool 휠로줌 = true;

    // ★★★줌 = **정수 칸 + 칸 사이 애니메이션** (2026-08-04 사용자 확정).
    //
    //   셋은 동시에 못 가진다: `화면픽셀/미터 = 축소 × 픽셀당미터`.
    //   줌한다는 건 왼쪽을 바꾸는 것이라, 오른쪽 둘 중 하나는 반드시 따라 움직인다.
    //     · 밀도를 놓으면 → 부드럽지만 픽셀 굵기가 변한다 (사용자가 취소)
    //     · 축소로 줌하면 → 픽셀은 완벽한데 18m ↔ 27m 로 뚝뚝 끊긴다 (사용자가 싫다 함)
    //
    //   → 빠져나가는 길: **화면보다 넓게 그려 두고(오버스캔)** 칸이 바뀔 때 그 여유분으로
    //     판을 스르륵 키우거나 줄인다. **멈춰 있을 때는 언제나 정확히 정수 배**라
    //     픽셀이 하나도 안 뭉개지고, 넘어가는 0.15초만 살짝 흐려진다.
    //     밀도(`픽셀당미터`)는 처음부터 끝까지 **한 번도 안 변한다.**
    [Range(1, 12)] public int 최소축소 = 1;
    [Range(1, 12)] public int 최대축소 = 10;
    // ★★오버스캔은 **가장 큰 칸 비율보다 커야 한다** (2026-08-05 사용자 "확대 축소가
    //   자유롭지 못한데"). 칸을 건널 때 판을 `축소/오버스캔` 배까지만 줄일 수 있는데
    //   (아래 `최소배`), 여유가 모자라면 거기서 **뚝 끊긴다.**
    //   칸 비율이 가장 큰 데는 **1↔2 구간으로 정확히 2배**다 (5↔6 은 1.2배뿐).
    //   → 1.7 로는 그 구간을 못 건넌다. **2.0** 이 그 자리에서 나온 숫자다 — 짐작이 아니다.
    //   ★대가: 축소 1 에서는 화면의 2배씩, 즉 픽셀을 4배 그린다 (1080 화면이면 딱 4K).
    //     가장 넓게 볼 때만 무거워지고, 평소(축소 3)에는 거의 그대로다.
    [Tooltip("화면보다 몇 배 넓게 그리나 — 칸 사이를 건너는 여유. 가장 큰 칸 비율(1↔2 = 2배)보다 커야 한다")]
    [Range(1f, 2.5f)] public float 오버스캔 = 2f;
    [Tooltip("클수록 칸을 빨리 건넌다 (0.15초쯤이 14~18)")]
    public float 칸넘김빠르기 = 16f;
    [Tooltip("지금 화면에 깔린 배율 — 멈추면 「축소」와 같아진다 (읽기용)")]
    public float 보이는축소 = 0f;

    // ★맞닿은 물체를 갈라 주는 문턱 — 한 물체 안에서는 값 차이가 정확히 0 이라 작아도 된다
    [Tooltip("겹친 물체 사이에 선을 넣을 최소 값 차이 (작을수록 잘 갈린다)")]
    [Range(0.002f, 0.2f)] public float 물체구분문턱 = 0.01f;

    /// 격자에 맞추는 쪽(스냅·테두리 두께)이 쓰는 밀도.
    /// ★이제 줌해도 **안 변한다** — 그게 이 방식의 요점이다.
    public float 유효픽셀당미터 => 픽셀당미터;

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
    int rtW, rtH, 지난축소, 지난W, 지난H, 지난배수;
    bool 지난켬;

    /// ★★화면의 한 점을 **저해상도 화면 기준**으로 옮긴다 (2026-08-04 사용자 "카메라가
    ///   가까이 있을때는 마우스가 있는 방향을 안보네").
    ///
    ///   본 카메라는 화면이 아니라 **저해상도 텍스처**에 그린다. 그래서
    ///   `cam.ScreenPointToRay(마우스자리)` 는 마우스 좌표를 **텍스처 크기 기준**으로 읽는다 —
    ///   화면은 1920 인데 텍스처는 `1920÷축소` 라, 줌인해서 축소가 커질수록 어긋남이 커진다.
    ///   축소 3 이면 640 짜리 화면인 줄 알고 읽으니, 오른쪽 끝을 가리켜도 세 배 밖을 가리킨 셈이 된다.
    ///   **줌아웃(축소 1)일 때는 멀쩡하고 줌인할수록 빗나가던** 것이 이 때문이다.
    ///
    ///   판은 가운데 정렬로 `축소` 배 늘려 깔고 되밀기만큼 밀려 있으므로, 그 역을 취하면 된다.
    public Vector2 화면점(Vector2 화면자리)
    {
        if (!켬 || 판rt == null) return 화면자리;
        var 밀림 = 판rt.anchoredPosition;
        // ★지금 판이 실제로 깔린 배율을 쓴다 — 칸을 건너는 중에도 커서가 안 어긋나게.
        //   `해상배수` 로 나눠 깔았으므로 여기서도 나눠야 한다
        float s = Mathf.Max(0.01f, (보이는축소 > 0f ? 보이는축소 : 축소) / Mathf.Max(1, 해상배수));
        return new Vector2(
            (화면자리.x - (Screen.width * 0.5f + 밀림.x)) / s + rtW * 0.5f,
            (화면자리.y - (Screen.height * 0.5f + 밀림.y)) / s + rtH * 0.5f);
    }

    // ★★★**씬에 저장된 옛 값이 코드 기본값을 이긴다.** 그래서 숫자를 새로 정할 때마다
    //   "씬을 다시 열고, 저장하지는 마세요" 를 시켜야 했다 — 사용자가 손으로 메울 일이
    //   아니다 (2026-08-05 사용자 "파일 오픈씬 안하게 그냥 너가그건 해주면안돼? 바로 적용").
    //
    //   → **정본 번호**를 하나 둔다. 씬에 적힌 번호가 아래 번호보다 낮으면 화면 숫자들을
    //     코드 것으로 덮어쓴다. 숫자를 새로 정할 때 이 번호를 하나 올리면, **씬을 안 건드려도
    //     그날부터 그 값이 쓰인다.**
    //   ★플레이 중 인스펙터로 만지는 건 그대로 먹는다 (그때는 이미 덮어쓴 뒤다).
    //     다음 플레이에 정본으로 돌아올 뿐이다 — 좋은 값을 찾으면 말해 주면 여기 적는다.
    const int 정본지금 = 3;
    [Tooltip("코드가 정한 값으로 맞춘 번호 — 건드리지 않는다")] public int 정본 = 0;

    // ★★★**8K 는 접었다** (2026-08-05 사용자 "가장 넓은 범위에서만 그러네" — 깜빡임이
    //   줄여 까는 칸에서만 났다). 그 실험이 `해상배수` 의 정체를 밝혔다:
    //     **줄여 깔 때만 넓어지고, 줄여 까는 칸이 곧 깜빡이는 칸이다.**
    //   줄이지 않는 칸에서는 크게 그려 놓고 그만큼 잘라 버리는 꼴이라 한 뼘도 안 넓어지면서
    //   픽셀만 네 배 쓴다. 그러니 얻는 게 없다. 기능은 남겨 두고 **1 로 꺼 둔다**
    //   (은퇴는 삭제가 아니라 스위치 — 나중에 화면 해상도가 커지면 다시 값을 한다).
    //
    // ★그래서 넓히는 길은 하나뿐이다: **줄여 깔지 않을 때 보이는 폭 = 화면 가로 ÷ 픽셀당미터.**
    //   화면 가로는 정해져 있으니 픽셀당미터를 낮추는 수밖에 없고, 대가는 성능이 아니라
    //   **굵기**다. 20 → 16 이면 96m → 120m 를 보고 사람 키가 36 → 29 픽셀이 된다.
    void 정본맞추기()
    {
        if (정본 >= 정본지금) return;
        픽셀당미터 = 16f;      // 96m → 120m (사람 키 29픽셀). 넓이와 굵기의 절충점
        해상배수 = 1;          // 8K 끔 — 줄여 깔 때만 값을 하는데 그 칸이 깜빡인다
        오버스캔 = 2f;         // 1↔2 칸이 정확히 2배라 여유도 2배여야 안 끊긴다
        최소축소 = 1;
        최대축소 = 10;
        // ★★★디더링은 **0 이어야 한다** (2026-08-04 사용자 "픽셀 색상이 계속 변하니까
        //   자글자글해" — 그때 코드 기본값을 0 으로 바꿨다). 그런데 **씬에 0.45 가 저장돼
        //   있어서 그게 이기고 있었다.** 2026-08-05 에 "자글자글은 진짜 너무 쎄다" 로
        //   다시 나왔고, 땅 결을 아무리 낮춰도 하나도 안 변한 이유가 이것이었다.
        //   → 정본이 들고 있어야 할 값이었다. 위 주석("기본으로 끈다")만으로는 못 막는다.
        디더링 = 0f;
        정본 = 정본지금;
    }

    // ★무엇이 실제로 걸렸는지 남긴다 — 화면만 보고는 「디더링」과 「땅 결」을 못 가린다
    //   (2026-08-05. 땅 결을 세 번 낮추는 동안 진짜 원인은 디더링이었다).
    void 값보고()
    {
        Debug.Log($"[픽셀] 디더링 {디더링:0.00} · 색단계 {색단계:0} · 축소 {축소} · 해상배수 {해상배수} · 정본 {정본}");
    }

    void OnEnable()
    {
        정본맞추기();
        값보고();
        cam = GetComponent<Camera>();
        만들기();
    }

    void OnDisable() { 치우기(); }

    void 만들기()
    {
        치우기();
        if (!켬) return;

        // ★★화면보다 넓게 그린다 (오버스캔) — 칸을 건널 때 판을 줄여도 가장자리가 안 비게.
        //   보이는 범위는 그대로다: 넓게 그린 만큼 판도 넓어져서 화면엔 가운데만 걸린다.
        float 넓게 = Mathf.Max(1f, 오버스캔);
        int 배수 = Mathf.Max(1, 해상배수);
        // ★여유 2픽셀도 **배수만큼** 줘야 한다 (2026-08-05 "땅이 아직도 반짝거림").
        //   +2 로 두면 8K 판이 홀수(7682)가 되고, 절반으로 줄여 깔면 3841 — 홀수라
        //   가운데 정렬에서 **반 픽셀 밀린다.** 그러면 화면 픽셀 하나가 텍셀 2×2 에
        //   딱 안 얹혀서 바닥 무늬가 반짝인다.
        rtW = Mathf.Max(16, Mathf.CeilToInt(Screen.width / (float)Mathf.Max(1, 축소) * 넓게 * 배수)) + 2 * 배수;
        rtH = Mathf.Max(16, Mathf.CeilToInt(Screen.height / (float)Mathf.Max(1, 축소) * 넓게 * 배수)) + 2 * 배수;

        // ★HDR 이 아니라 보통 형식으로. HDR 값(1 초과)에 색 양자화를 걸면
        //   밝은 자리에서 색이 튄다 (사용자 "색상이 좀 이상해")
        rt = new RenderTexture(rtW, rtH, 24, RenderTextureFormat.Default)
        {
            name = "픽셀화면",
            // ★늘려 깔 때는 **점 필터**여야 픽셀이 안 뭉개진다. 그런데 `해상배수` 를 쓰면
            //   가장 넓은 칸에서는 **줄여 깔게** 되는데, 그때 점 필터는 텍셀을 건너뛰며
            //   찍어서 움직일 때 자글거린다. 줄이는 칸에서만 부드럽게 바꾼다.
            filterMode = 축소 < 배수 ? FilterMode.Bilinear : FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            // ★판이 아주 커지면(8K) MSAA 까지 걸면 메모리가 몇 배로 뛴다 — 그 자리는 끈다.
            //   어차피 줄여 깔면서 저절로 부드러워지므로 잃는 게 거의 없다.
            antiAliasing = (long)rtW * rtH > 12_000_000L
                ? 1 : Mathf.Clamp(Mathf.ClosestPowerOfTwo(부드럽게), 1, 8)
        };
        rt.Create();
        cam.targetTexture = rt;
        cam.cullingMask &= ~(1 << Outliner.층);        // 흰 복사본은 본 화면에 안 보이게
        cam.cullingMask &= ~(1 << Outliner.잔디층);
        cam.allowMSAA = 부드럽게 > 1 && rt.antiAliasing > 1;

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
        // 크기는 매 프레임 「보이는축소」로 다시 잡는다 (칸 사이 애니메이션).
        // ★`해상배수` 로 크게 그렸으면 깔 때 그만큼 나눈다 — 화면에 걸리는 크기는 그대로다
        판rt.sizeDelta = new Vector2(rtW * 축소 / (float)배수, rtH * 축소 / (float)배수);
        판rt.anchoredPosition = Vector2.zero;

        지난축소 = 축소; 지난W = Screen.width; 지난H = Screen.height; 지난켬 = 켬; 지난배수 = 배수;
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

        // ★줌 = 축소 배율을 한 칸씩. 휠을 올리면 줌인(축소가 커진다 = 픽셀이 굵고 좁게 본다)
        if (켬 && 휠로줌)
        {
            var m = UnityEngine.InputSystem.Mouse.current;
            float sc = m != null ? m.scroll.ReadValue().y : 0f;
            if (Mathf.Abs(sc) > 0.01f)
                축소 = Mathf.Clamp(축소 + (sc > 0f ? 1 : -1), 최소축소, 최대축소);
        }
#endif
        // 설정이 바뀌면 다시 만든다
        // ★칸이 바뀌어도 「보이는축소」는 안 건드린다 — 그게 옛 배율에 머물러 있다가
        //   새 판 위에서 서서히 따라붙는 것이 곧 「칸 사이 애니메이션」이다.
        if (켬 != 지난켬 || 축소 != 지난축소 || Screen.width != 지난W || Screen.height != 지난H
            || Mathf.Max(1, 해상배수) != 지난배수)
        { 만들기(); if (!켬) return; }
        if (!켬 || mat == null || cam == null) return;

        // ★★픽셀 밀도 고정 — 1미터가 언제나 같은 픽셀 수가 되도록 시야를 정한다.
        //   (이걸 안 하면 줌할 때마다 물체의 픽셀이 잘아졌다 굵어졌다 한다)
        // ★줌을 곱한 값으로 시야를 정한다. 격자에 맞추는 계산도 전부 이 값을 쓴다
        float ppu = Mathf.Max(0.5f, 유효픽셀당미터);
        if (cam.orthographic) cam.orthographicSize = (rtH - 2) / (2f * ppu);

        // ── ② 픽셀 스냅 + 되밀기
        Vector2 밀기 = Vector2.zero;
        if (스냅 && cam.orthographic)
        {
            var p = cam.transform.position;
            var right = cam.transform.right;
            var up = cam.transform.up;

            // ★★★스냅 격자는 **화면에 실제로 찍히는 칸** 기준이어야 한다
            //   (2026-08-05 사용자 "바닥 타일이 반짝반짝 빛나는 버그").
            //   전에는 렌더 텍스처의 칸(= `픽셀당미터`)에 맞췄고, 그 칸이 언제나 화면 픽셀보다
            //   **크거나 같았으므로** 맞았다. 그런데 `해상배수` 로 크게 그려 **줄여 깔면**
            //   렌더 칸이 화면 픽셀의 절반이 된다 — 반 픽셀에 맞춰 붙이니 매 프레임
            //   어긋나 붙었고, 그게 바닥 무늬에서 **반짝임**으로 나왔다.
            //   → 둘 중 **성긴 쪽**에 맞춘다. 늘려 깔 때는 렌더 칸, 줄여 깔 때는 화면 픽셀.
            float 깖지금 = 축소 / (float)Mathf.Max(1, 해상배수);
            float 격자 = ppu * Mathf.Min(1f, 깖지금);

            float a = Vector3.Dot(p, right), b = Vector3.Dot(p, up);
            float sa = Mathf.Round(a * 격자) / 격자;
            float sb = Mathf.Round(b * 격자) / 격자;

            cam.transform.position = p + right * (sa - a) + up * (sb - b);

            // ★부호 주의: 카메라를 sa 로 옮겨 찍었으므로, 원래 자리(a)에서 본 것처럼
            //   보이려면 화면을 **반대로** 밀어야 한다. 부호를 반대로 뒀더니 오차가
            //   상쇄되는 대신 두 배가 되어 카메라가 두두두둑 끊겼다 (2026-08-04).
            밀기 = new Vector2(-(a - sa) * ppu, -(b - sb) * ppu);
            if (밀기반대) 밀기 = -밀기;
        }

        // ── ★★칸 사이 애니메이션 — 판을 「보이는축소」 배로 깐다.
        //   멈춰 있으면 이 값이 `축소` 와 정확히 같아져 **완전한 정수 배**가 된다.
        //   그때 픽셀은 한 톨도 안 뭉개진다. 흐려지는 건 건너는 그 짧은 순간뿐이다.
        if (보이는축소 <= 0f) 보이는축소 = 축소;                     // 처음 한 번
        보이는축소 = Mathf.Lerp(보이는축소, 축소, 1f - Mathf.Exp(-칸넘김빠르기 * Time.deltaTime));
        if (Mathf.Abs(보이는축소 - 축소) < 0.004f) 보이는축소 = 축소;  // 다 왔으면 정확히 붙인다

        // ★가장자리가 비지 않을 만큼만 줄인다. 오버스캔이 여유를 대는데, 휠을 여러 칸
        //   빠르게 굴리면 그 여유를 넘어설 수 있어서 여기서 막는다 (살짝 튀는 게
        //   화면 가에 검은 띠가 생기는 것보다 낫다).
        float 최소배 = 축소 / Mathf.Max(1f, 오버스캔);
        float 깔배율 = Mathf.Max(보이는축소, 최소배);
        if (판rt != null)
        {
            // ★`해상배수` 로 크게 그렸으면 깔 때 나눈다 (`만들기` 와 같은 계산)
            float 깖 = 깔배율 / Mathf.Max(1, 해상배수);

            // ★★★판이 **반드시 화면을 덮게** 못박는다 (2026-08-05 사용자 "축소 1 일때
            //   아랫부분이 좀 잘리는 버그"). 계산상으로는 오버스캔이 덮게 돼 있지만,
            //   되밀기로 판이 옆으로 밀리는 몫이 그 계산에 안 들어가 있어서 가장자리에
            //   빈 자리가 생겼다. 여기서 **실제 화면 크기와 밀린 거리를 재서** 모자라면 키운다.
            //   해상도가 얼마든, 오버스캔이 얼마든 다시는 안 잘린다.
            float 밀린만큼 = Mathf.Max(Mathf.Abs(밀기.x), Mathf.Abs(밀기.y)) * 깖 * 2f;
            깖 = Mathf.Max(깖, (Screen.width + 밀린만큼) / rtW, (Screen.height + 밀린만큼) / rtH);
            판rt.sizeDelta = new Vector2(rtW * 깖, rtH * 깖);
            // ★되밀기는 **화면 쪽**에서 한다. UV 로 밀면 점 필터라 한 텍셀씩 튀어서
            //   오히려 반짝거린다 (실제로 그랬다). 판 자체를 화면 픽셀 단위로 민다
            var 밀린 = new Vector2(밀기.x * 깖, 밀기.y * 깖);
            // ★★줄여 깔 때는 **화면 픽셀 단위로 끊어** 민다 (2026-08-05 "땅이 반짝거림").
            //   늘려 깔 때의 되밀기는 화면 픽셀보다 잘게 밀어 움직임을 매끄럽게 하는 것인데,
            //   줄여 깔 때 그러면 **평균 내는 창이 미끄러져** 바닥 무늬가 끓는다.
            //   여기서 잃는 매끄러움은 없다 — 가장 넓게 본 칸에서 한 픽셀이 10cm 라
            //   끊어 밀어도 사람 눈에 안 보인다.
            if (깖 < 1f) { 밀린.x = Mathf.Round(밀린.x); 밀린.y = Mathf.Round(밀린.y); }
            판rt.anchoredPosition = 밀린;
        }
        mat.SetVector("_Offset", Vector4.zero);
        mat.SetFloat("_Levels", 색단계);
        mat.SetFloat("_Dither", 디더링);
        mat.SetFloat("_Sat", 채도);
        mat.SetFloat("_Bands", 명암단계 >= 2f ? 명암단계 : 0f);
        mat.SetFloat("_OutlineW", 외곽선두께);
        mat.SetFloat("_OutlineT", 외곽선문턱);
        mat.SetFloat("_OutlineS", 외곽선세기);
        mat.SetFloat("_IdGap", 물체구분문턱);
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
