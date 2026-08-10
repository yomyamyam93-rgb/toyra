using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// 모닥불 — **이 게임 최초의 「집」이다.**
///
/// ★그전까지 집은 `HeroCarry` 안에 박힌 맵 정중앙 좌표였다. 보이지도 않는 지점이
///   집 행세를 했다. 모닥불을 세우면 **거기가 집이 된다** — 눈에 보이는 자리가 생긴다.
///
/// ★★**저절로 생기지 않는다** (9-0 인과와 행위). 제작창에서 고르면 완성품이 아니라
///   **터(반투명 실루엣)**가 놓이고, 재료를 손으로 부어 넣어야 선다:
///
///   제작창 → 터가 놓인다 → F 를 눌러 나무·돌을 붓는다(게이지가 찬다)
///          → 다 차면 연기가 팡 → 공중에서 통·통·통 튀며 앉는다 → 완성
///
/// ★★★**불은 꺼진다.** 이게 이 설계의 전부다. 영원히 타면 모닥불은 장식이지만,
///   꺼지면 **돌아올 이유**가 생긴다 — 나갔다가 땔감을 안고 돌아오는 하루가 만들어진다.
///   목표를 안 주는 게임(2장 1조)에서 루틴이 저절로 생기는 첫 자리다.
[DefaultExecutionOrder(-5)]          // F 를 `HeroCarry` 보다 먼저 본다 (`F먹음` 주석 참고)
public class 모닥불 : MonoBehaviour
{
    public enum 단계 { 터, 섬 }

    /// 세상에 있는 모닥불 전부 — 집을 찾을 때 훑는다
    public static readonly List<모닥불> All = new List<모닥불>();

    [Header("짓는 데 드는 것")]
    [Tooltip("화덕 테두리를 두를 돌")] public int 필요돌 = 5;
    [Tooltip("바닥에 깔 나무")] public int 필요나무 = 3;
    [Tooltip("F 를 누르고 있을 때 1초에 몇 개씩 들어가나")] public float 붓는속도 = 4f;

    [Header("불")]
    [Tooltip("나무 1개가 몇 초 타나 (120 = 2분. 하루가 1200초다)")] public float 나무당 = 120f;
    [Tooltip("한 번에 쌓을 수 있는 최대 연료 (초)")] public float 최대연료 = 900f;

    [Header("요리")]
    [Tooltip("고기 하나를 굽는 데 걸리는 시간 (초)")] public float 굽는시간 = 18f;

    [Header("손 닿는 거리")]
    [Tooltip("이 안에서 재료를 붓고 땔감을 넣고 굽는다 (m)")] public float 닿는거리 = 3f;

    // ★★★**「집」이라는 보이지 않는 원을 만들지 않는다** (2026-08-10 사용자 —
    //   *"집이라고 바운더리를 가정하지는 않았으면 좋겠어. 단지, 야생의 팻이나 공룡들은
    //   불을 무서워 하는 설정이면 좋겠다"*).
    //
    //   전에는 맵 정중앙 반경 22m 가 「집」이었다. 그건 규칙이지 세상이 아니다.
    //   → 안전지대를 **선언하지 않는다.** 대신 **불이 야생을 밀어낸다**는 사물의 성질만 둔다.
    //     그러면 캠프는 규칙으로 정해지는 게 아니라 **불 옆이 유리해서** 저절로 생긴다.
    //     ☆좀보이드식이다 — 세상은 규칙대로 굴러가고, 이야기는 플레이어 쪽에서 생긴다.
    //   ★★불이 꺼지면 밀어내기도 사라진다. 안전이 **연료에 매달려 있다.**
    /// ★끄면 야생이 불을 신경 쓰지 않는다 (은퇴는 삭제가 아니라 스위치 — 9장 3조)
    public static bool 불무서워함 = true;

    [Header("불을 무서워한다")]
    [Tooltip("활활 탈 때 야생이 이 거리에서 놀란다 (m)")] public float 겁내는거리 = 7f;

    /// 지금 이 불이 야생을 겁주는 거리 — 사그라들면 같이 줄어든다 (꺼지면 0)
    public float 겁주는거리 => !탄다 ? 0f : 겁내는거리 * Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(연료 / 60f));

    // ── 상태
    public 단계 지금 { get; private set; } = 단계.터;
    public int 든돌 { get; private set; }
    public int 든나무 { get; private set; }
    /// 남은 연료 (초). 0 이면 꺼져 있다
    public float 연료 { get; private set; }
    public bool 탄다 => 지금 == 단계.섬 && 연료 > 0f;
    /// 짓는 진행도 0~1 — 게이지가 이걸 그린다
    public float 진행 => (든돌 + 든나무) / Mathf.Max(1f, (float)(필요돌 + 필요나무));
    /// 다 지었나
    public bool 섰다 => 지금 == 단계.섬;

    /// 굽는 중인 고기 — 개수와 지금 것의 남은 시간
    public int 굽는중 => 굽는것.Count;
    public float 굽기남음 { get; private set; }

    /// 방금 벌어진 일 — 화면에 잠깐 띄운다
    public string 알림 { get; private set; } = "";
    float 알림T;

    // ── 몸
    Transform 몸;              // 통통 튀는 대상 (완성된 모닥불 전체)
    Transform 불꽃;
    Light 빛;
    Material 실루엣재질;
    bool 짓는중;               // 통통 튀는 연출이 도는 동안은 손대지 않는다

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); 빛자리해제(); }

    // ★★F 를 여기서 먼저 먹는다. `HeroCarry` 도 F 를 쓰는데(붙잡기), 모닥불 앞에
    //   서 있을 때는 이쪽이 먼저다. 실행 순서를 당겨 놓고 먹었으면 표시를 남긴다.
    //   (`HeroCarry` 는 이 표시를 보고 그 프레임의 F 를 넘긴다)
    //
    // ☆**프레임 번호로 적는다.** `bool` 을 매 Update 앞에서 false 로 되돌리면, 모닥불이
    //   둘일 때 **가까운 놈이 먹은 것을 먼 놈이 지워버린다.** 번호를 적으면 순서와 무관하다.
    static int F먹은프레임 = -1;
    public static bool F먹음 => F먹은프레임 == Time.frameCount;

    void Awake() { 터짓기(); }

    // ────────────────────────────────────────── 겉모습

    /// 반투명 실루엣 — 아직 아무것도 아닌 「터」
    void 터짓기()
    {
        몸 = new GameObject("모닥불몸").transform;
        몸.SetParent(transform, false);

        실루엣재질 = 반투명재질(new Color(0.95f, 0.85f, 0.55f, 0.32f));
        형태만들기(실루엣재질);
        지금 = 단계.터;
    }

    /// 돌 테두리 + 장작 — 상자로 짓는다 (1장: 상자 먼저, 모델 나중)
    void 형태만들기(Material 덮을재질)
    {
        // 돌 테두리 — 여덟 개를 빙 둘러
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f;
            var p = new Vector3(Mathf.Cos(a) * 0.62f, 0.11f, Mathf.Sin(a) * 0.62f);
            var g = Grey.Box(몸, Vector3.zero, new Vector3(0.28f, 0.22f, 0.28f),
                             new Color(0.46f, 0.45f, 0.43f), "돌", 0f, a * Mathf.Rad2Deg);
            g.transform.localPosition = p;
            담기(g, 덮을재질);
        }

        // 장작 — 우물 정(井) 자로 네 개
        for (int i = 0; i < 4; i++)
        {
            float a = i * 45f + 20f;
            var g = Grey.Box(몸, Vector3.zero, new Vector3(0.9f, 0.13f, 0.13f),
                             new Color(0.36f, 0.24f, 0.15f), "장작", 0f, a);
            g.transform.localPosition = new Vector3(0f, 0.14f + i * 0.035f, 0f);
            담기(g, 덮을재질);
        }

        // 불꽃 — 완성된 뒤 탈 때만 보인다
        불꽃 = new GameObject("불꽃").transform;
        불꽃.SetParent(몸, false);
        불꽃.localPosition = new Vector3(0f, 0.3f, 0f);
        for (int i = 0; i < 3; i++)
        {
            var g = Grey.Box(불꽃, Vector3.zero, new Vector3(0.3f - i * 0.07f, 0.42f - i * 0.1f, 0.3f - i * 0.07f),
                             i == 0 ? new Color(1f, 0.45f, 0.12f)
                                    : (i == 1 ? new Color(1f, 0.72f, 0.2f) : new Color(1f, 0.93f, 0.6f)),
                             "불꽃");
            g.transform.localPosition = new Vector3(0f, i * 0.16f, 0f);
            if (덮을재질 != null) 담기(g, 덮을재질);
        }
        불꽃.gameObject.SetActive(false);
    }

    void 담기(GameObject g, Material 덮을재질)
    {
        if (덮을재질 == null) return;
        var r = g.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = 덮을재질;
    }

    static Material 반투명재질(Color c)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = c;
        // URP Lit 을 반투명으로 — 표면 종류를 바꾸고 알파 블렌딩을 켠다
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Smoothness", 0.1f);
        m.renderQueue = 3000;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        return m;
    }

    // ────────────────────────────────────────── 매 프레임

    void Update()
    {
        float dt = Time.deltaTime;
        if (알림T > 0f) { 알림T -= dt; if (알림T <= 0f) 알림 = ""; }

        // ── 불이 탄다 → 줄어든다
        if (탄다)
        {
            연료 = Mathf.Max(0f, 연료 - dt);
            불꺼짐확인();
            굽기진행(dt);
            불흔들기();
        }

        var h = Hero.Me;
        if (h == null || 짓는중) return;

        float d2 = 수평거리제곱(h.transform.position);
        if (d2 > 닿는거리 * 닿는거리) return;

        // ── F — 터면 재료를 붓고, 다 지었으면 땔감을 넣는다
        bool F눌림 = false;
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null) F눌림 = 지금 == 단계.터 ? k.fKey.isPressed : k.fKey.wasPressedThisFrame;
#else
        F눌림 = 지금 == 단계.터 ? Input.GetKey(KeyCode.F) : Input.GetKeyDown(KeyCode.F);
#endif
        if (!F눌림) return;

        if (지금 == 단계.터) { 재료붓기(dt); F먹은프레임 = Time.frameCount; }
        else if (땔감넣기()) F먹은프레임 = Time.frameCount;
    }

    float 수평거리제곱(Vector3 p)
    {
        var v = p - transform.position; v.y = 0f;
        return v.sqrMagnitude;
    }

    // ────────────────────────────────────────── 짓기

    /// F 를 누르고 있는 동안 가진 것이 조금씩 들어간다 — 게이지가 차오르는 게 보인다
    float 부은양;
    void 재료붓기(float dt)
    {
        부은양 += 붓는속도 * dt;
        int 넣을수 = Mathf.FloorToInt(부은양);
        if (넣을수 < 1) return;
        부은양 -= 넣을수;

        for (int i = 0; i < 넣을수; i++)
        {
            if (든돌 < 필요돌 && Stock.Take(Stock.Kind.돌, 1)) { 든돌++; continue; }
            if (든나무 < 필요나무 && Stock.Take(Stock.Kind.나무, 1)) { 든나무++; continue; }

            // 넣을 게 없다 — 뭐가 모자란지 알려준다 (수치 표시, 설명문 아님)
            if (든돌 < 필요돌) 띄움($"돌 {든돌}/{필요돌}");
            else if (든나무 < 필요나무) 띄움($"나무 {든나무}/{필요나무}");
            return;
        }

        if (든돌 >= 필요돌 && 든나무 >= 필요나무) StartCoroutine(세우기());
    }

    /// ★연기가 팡 터지고, 공중에서 통·통·통 튀며 앉는다
    IEnumerator 세우기()
    {
        짓는중 = true;
        지금 = 단계.섬;

        // 실루엣을 진짜 몸으로 — 반투명을 벗기고 제 색을 되돌린다
        몸.gameObject.SetActive(false);
        foreach (Transform c in 몸) Destroy(c.gameObject);
        yield return null;
        형태만들기(null);
        몸.gameObject.SetActive(true);

        연기팡();

        // ── 통·통·통 — 위에서 떨어져 세 번 튀고, 닿을 때마다 납작해졌다 돌아온다
        float[] 높이 = { 2.4f, 0.75f, 0.26f, 0f };
        for (int b = 0; b < 높이.Length - 1; b++)
        {
            float h0 = 높이[b], h1 = 높이[b + 1];
            float 낙하 = Mathf.Sqrt(2f * h0 / 9.8f) * 1.15f;

            // 떨어진다
            for (float t = 0f; t < 낙하; t += Time.deltaTime)
            {
                float u = t / 낙하;
                몸.localPosition = new Vector3(0f, Mathf.Lerp(h0, 0f, u * u), 0f);
                yield return null;
            }
            몸.localPosition = Vector3.zero;

            // 닿는 순간 — 납작해졌다 돌아온다 (몸의 3막: 여운)
            float 눌림 = Mathf.Clamp01(h0 / 2.4f) * 0.35f;
            float 회복 = 0.14f;
            for (float t = 0f; t < 회복; t += Time.deltaTime)
            {
                float u = 1f - t / 회복;
                float s = 1f - 눌림 * u;
                몸.localScale = new Vector3(1f + 눌림 * u * 0.7f, s, 1f + 눌림 * u * 0.7f);
                yield return null;
            }
            몸.localScale = Vector3.one;

            if (h1 <= 0f) break;

            // 튀어 오른다
            float 상승 = Mathf.Sqrt(2f * h1 / 9.8f) * 1.15f;
            for (float t = 0f; t < 상승; t += Time.deltaTime)
            {
                float u = t / 상승;
                몸.localPosition = new Vector3(0f, Mathf.Lerp(0f, h1, 1f - (1f - u) * (1f - u)), 0f);
                yield return null;
            }
        }

        몸.localPosition = Vector3.zero;
        몸.localScale = Vector3.one;

        // 이제 길이 막힌다 — 뚫고 지나갈 수 없다
        Blocker.Add(new Vector3(transform.position.x, 0f, transform.position.z), 0.8f);

        짓는중 = false;
        띄움("모닥불");
    }

    /// 흰 덩어리들이 사방으로 퍼지며 커지고 옅어진다 — 파티클 대신 상자로 (장난감 스타일)
    void 연기팡()
    {
        for (int i = 0; i < 10; i++)
        {
            float a = i / 10f * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
            var 방향 = new Vector3(Mathf.Cos(a), Random.Range(0.5f, 1.4f), Mathf.Sin(a));
            StartCoroutine(연기한덩이(방향.normalized, Random.Range(0.24f, 0.42f)));
        }
    }

    IEnumerator 연기한덩이(Vector3 방향, float 크기)
    {
        var mat = 반투명재질(new Color(0.92f, 0.9f, 0.86f, 0.7f));
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Grey.Strip(g);
        g.name = "연기";
        g.transform.SetParent(transform, false);
        g.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        g.GetComponent<Renderer>().sharedMaterial = mat;

        float 산다 = Random.Range(0.55f, 0.9f);
        float 속 = Random.Range(2.2f, 3.6f);
        for (float t = 0f; t < 산다; t += Time.deltaTime)
        {
            float u = t / 산다;
            g.transform.localPosition += 방향 * 속 * (1f - u) * Time.deltaTime;
            float s = 크기 * Mathf.Lerp(0.4f, 2.1f, u);
            g.transform.localScale = new Vector3(s, s, s);
            var c = mat.color; c.a = 0.7f * (1f - u); mat.color = c;
            yield return null;
        }
        Destroy(g);
        Destroy(mat);
    }

    // ────────────────────────────────────────── 불

    /// 나무 하나를 넣는다 — 연료가 는다
    public bool 땔감넣기()
    {
        if (지금 != 단계.섬) return false;
        if (연료 >= 최대연료) { 띄움("가득"); return true; }
        if (!Stock.Take(Stock.Kind.나무, 1)) { 띄움("나무가 없다"); return true; }

        bool 꺼져있었다 = 연료 <= 0f;
        연료 = Mathf.Min(최대연료, 연료 + 나무당);
        if (꺼져있었다) 불켬();
        띄움($"{Mathf.CeilToInt(연료 / 60f)}분");
        return true;
    }

    void 불켬()
    {
        불꽃.gameObject.SetActive(true);
        if (빛 == null)
        {
            var g = new GameObject("불빛");
            g.transform.SetParent(몸, false);
            g.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            빛 = g.AddComponent<Light>();
            빛.type = LightType.Point;
            빛.color = new Color(1f, 0.68f, 0.36f);
            빛.range = 16f;
            빛.shadows = LightShadows.None;   // 그림자까지 켜면 비싸다
        }
        빛.enabled = true;
    }

    void 불꺼짐확인()
    {
        if (연료 > 0f) return;
        불꽃.gameObject.SetActive(false);
        빛자리해제();
        띄움("불이 꺼졌다");
    }

    void 빛자리해제() { if (빛 != null) 빛.enabled = false; }

    /// 불꽃이 흔들리고 빛이 일렁인다 — 실제로 타고 있다는 표시 (12장: 동작을 따라가는 것만)
    void 불흔들기()
    {
        float t = Time.time;
        // 연료가 적으면 사그라든다 — 꺼져 가는 게 눈에 보여야 땔감을 넣으러 온다
        float 기운 = Mathf.Clamp01(연료 / 60f);
        float 일렁 = 0.82f + Mathf.PerlinNoise(t * 3.4f, 0f) * 0.36f;

        불꽃.localScale = new Vector3(1f, Mathf.Lerp(0.45f, 1f, 기운) * 일렁, 1f);
        불꽃.localRotation = Quaternion.Euler(0f, t * 42f, 0f);
        if (빛 != null) 빛.intensity = Mathf.Lerp(0.6f, 3.1f, 기운) * 일렁;
    }

    // ────────────────────────────────────────── 요리

    /// ★인벤에서 우클릭으로 올린다 — 무엇이 되는지는 **아이템 표**가 정한다
    ///   (`제작창` 에 "고기 굽기" 를 박아 두지 않는다)
    public void 올리기(아이템종 될것)
    {
        if (!탄다 || 될것 == null) return;
        굽는것.Add(될것);
        if (굽는것.Count == 1) 굽기남음 = 굽는시간;
    }

    /// ★인벤에서 우클릭으로 땔감을 넣는다 (재료는 부르는 쪽이 이미 뺐다)
    public void 땔감받기()
    {
        bool 꺼져있었다 = 연료 <= 0f;
        연료 = Mathf.Min(최대연료, 연료 + 나무당);
        if (꺼져있었다) 불켬();
        띄움($"{Mathf.CeilToInt(연료 / 60f)}분");
    }

    readonly List<아이템종> 굽는것 = new List<아이템종>();


    /// ★불이 꺼지면 굽던 것도 멈춘다 (인과) — 도로 타기 시작하면 이어서 구워진다
    void 굽기진행(float dt)
    {
        if (굽는것.Count == 0) return;
        굽기남음 -= dt;
        if (굽기남음 > 0f) return;

        var 된것 = 굽는것[0];
        굽는것.RemoveAt(0);
        인벤.어디든넣기(된것, 1);
        굽기남음 = 굽는것.Count > 0 ? 굽는시간 : 0f;
        띄움(된것.이름);
    }

    void 띄움(string s) { 알림 = s; 알림T = 2.2f; }

    // ────────────────────────────────────────── 밖에서 묻는 것

    /// ★★이 자리를 겁주는 불 — 없으면 null.
    ///
    ///   ☆**밀어내지 않는다** (2026-08-10 사용자 — *"바로 그냥 밀려서 막혀서 못오는
    ///     모션으로 하지말고... 두려워하는 놀라는 모습을 하다가... 달려서 좀 도망가게끔"*).
    ///     장애물처럼 밀면 「보이지 않는 벽」이 하나 더 생길 뿐이고, 짐승이 벽에 비벼대는
    ///     모습은 고장처럼 보인다. 무서운 건 **몸으로 드러나야** 무서운 것이다.
    ///   ☆그래서 여기서는 **알려만 준다.** 놀라고 달아나는 건 `Critter` 쪽 일이다.
    public static 모닥불 무서운불(Vector3 자리)
    {
        if (!불무서워함) return null;
        모닥불 best = null; float bd = float.MaxValue;
        for (int i = All.Count - 1; i >= 0; i--)
        {
            var f = All[i];
            if (f == null) continue;
            float r = f.겁주는거리;
            if (r <= 0f) continue;                    // 꺼진 불은 아무도 안 무서워한다

            var v = 자리 - f.transform.position; v.y = 0f;
            float d2 = v.sqrMagnitude;
            if (d2 > r * r || d2 >= bd) continue;
            bd = d2; best = f;
        }
        return best;
    }

    /// 사람 가까이 있는 모닥불 — 제작창이 무엇을 보여줄지 정할 때 쓴다
    public static 모닥불 가까운것(Vector3 에서, float 거리)
    {
        모닥불 best = null; float bd = 거리 * 거리;
        for (int i = All.Count - 1; i >= 0; i--)
        {
            var f = All[i];
            if (f == null) continue;
            float d2 = f.수평거리제곱(에서);
            if (d2 > bd) continue;
            bd = d2; best = f;
        }
        return best;
    }

    /// 터를 하나 놓는다 — 제작창이 부른다
    public static 모닥불 터놓기(Vector3 자리)
    {
        var g = new GameObject("모닥불");
        g.transform.position = new Vector3(자리.x, 0f, 자리.z);
        return g.AddComponent<모닥불>();
    }
}
