using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// 인벤토리 창 (Tab) — **좀보이드 형식** (기획 5-7).
///
/// ★★사용자 확정 (2026-08-10):
///   *"좀보이드는 어떤 가방을 가지고 있냐에 따라 가방마다 인벤토리가 생성되고,
///     거기서 다 사용하고 장착하고 하잖아."* — 맞다. 그래서:
///   ① 왼쪽 위에 **통 버튼 줄** — 주머니 + 착용한 가방마다 제 칸이 있다
///   ② **우클릭하면 그 물건으로 할 수 있는 것**이 뜬다 (먹기·굽기·쥐기·메기·박기…)
///
/// ★★★**행동을 코드에 박지 않는다** — 메뉴는 `아이템종.쓸것` 에서 나온다.
///   그전에는 먹기·굽기·땔감·표식이 전부 `제작창` 에 손으로 박은 항목이었다.
///   새 아이템에 쓰임 한 줄을 적으면 메뉴가 저절로 생긴다.
///
/// ★창을 열어도 게임이 안 멈춘다 (사용자 확정).
[RequireComponent(typeof(Hero))]
public class 인벤창 : MonoBehaviour
{
    [Tooltip("이 거리 안의 무더기를 「앞에 있는 것」으로 본다 (m)")] public float 닿는거리 = 2.6f;

    public bool 열림 { get; private set; }

    Hero hero;
    HeroAttack 손;
    생존 삶;
    bool 손껐다;
    Texture2D px;
    Vector2 왼쪽스크롤, 오른쪽스크롤;
    int 고른통;

    // 우클릭 메뉴
    아이템 메뉴대상; 인벤 메뉴통; Vector2 메뉴자리;
    readonly List<(string 이름, System.Action 하기)> 메뉴 = new List<(string, System.Action)>();

    // ★★★**끌어다 놓기** (2026-08-12 사용자 — 기획 5-7 의 「드래그 없음」을 갱신했다).
    //   클릭 한 번으로 옮기는 길은 **그대로 둔다** — 끌기는 더한 것이지 바꾼 게 아니다.
    //   ☆그래서 누르는 순간이 아니라 **손을 뗄 때** 판정한다. 안 끌었으면 옛 클릭 그대로다.
    //   ☆끌기가 여는 것 하나: **어느 통에 넣을지 콕 집을 수 있다** (가방 버튼 위에 놓기).
    아이템 끈것; 인벤 끈통; bool 끈내것; 땅무더기 끈무더기;
    Vector2 누른자리; bool 끄는중;
    Rect 왼칸, 오른칸;
    readonly List<Rect> 통칸 = new List<Rect>();

    // ★★몸의 칸 — **표다** (5-7 의 *"코드에 박지 않는다 — 표로 돈다"*).
    //   ☆칸 이름은 **좀보이드에 실제로 있는 것**만 골랐다. 지어내지 않았다.
    //   ☆옷 아이템은 아직 하나도 없다 — **칸만 서 있는다.** 옷을 만드는 날
    //     `아이템종.입는칸` 에 칸 이름 한 줄만 적으면 그날 바로 걸린다.
    static readonly (string 이름, int 열, int 줄)[] 몸표 = {
        ("머리", 0, 0), ("얼굴", 0, 1), ("목",   0, 2), ("장갑", 0, 3),
        ("가방", 1, 0), ("상의", 1, 1), ("하의", 1, 2), ("신발", 1, 3),
    };
    readonly Dictionary<string, Rect> 몸칸자리 = new Dictionary<string, Rect>();
    Rect 몸칸, 손칸;

    // ★★★**창을 원하는 데로 옮긴다** (2026-08-12 사용자 "좀보이드는 인벤토리 같은것들을
    //   자유롭게 옮길 수 있나봐?"). 맞다 — 좀보이드는 창을 끌어 옮기고 크기도 바꾼다.
    //   ☆**제목 줄을 잡아야만** 옮겨진다. 안 그러면 아이템 끌기와 다툰다 —
    //     칸 위에서 누른 건 이미 이벤트를 먹은 뒤라 창까지 끌리지 않는다.
    //   ☆옮긴 자리는 창을 닫아도 남는다. 기본 자리로 되돌리려면 제목 줄을 **더블클릭**한다.
    readonly Vector2?[] 창자리 = new Vector2?[3];
    int 옮기는창 = -1; Vector2 잡은차이;

    string 알림; float 알림T;

    void Awake() { hero = GetComponent<Hero>(); 손 = GetComponent<HeroAttack>(); 삶 = GetComponent<생존>(); }

    void Update()
    {
        if (알림T > 0f) { 알림T -= Time.deltaTime; if (알림T <= 0f) 알림 = null; }
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        bool 토글 = k != null && k.tabKey.wasPressedThisFrame;
        bool 닫기 = k != null && k.escapeKey.wasPressedThisFrame;
#else
        bool 토글 = Input.GetKeyDown(KeyCode.Tab);
        bool 닫기 = Input.GetKeyDown(KeyCode.Escape);
#endif
        if (토글) { 열림 = !열림; 메뉴닫기(); }
        if (닫기) { if (메뉴대상 != null) 메뉴닫기(); else 열림 = false; }
        if (!hero.Alive) 열림 = false;
        if (!열림 && 끈것 != null) { 끈것 = null; 끈통 = null; 끈무더기 = null; 끄는중 = false; }

        if (손 != null)
        {
            if (열림 && 손.enabled) { 손.enabled = false; 손껐다 = true; }
            else if (!열림 && 손껐다) { 손.enabled = true; 손껐다 = false; }
        }

        // ★부패는 창을 안 열어도 돈다 — 시간은 그냥 흐른다
        // ★★★**매 프레임 돌 이유가 없다** (2026-08-11 — 「매번 읽는 것」 전수 조사에서 나왔다).
        //   세계에 땅무더기가 **9,882개** 깔려 있는데(어제 넣은 바닥 줍이) 그 전부의
        //   부패를 **프레임마다** 갱신하고 있었다. 상하는 데 걸리는 시간은 분·시간 단위라
        //   0.5초에 한 번이면 눈에 똑같다 — **비용은 30분의 1**이 된다.
        //   ☆쌓아 둔 시간을 한꺼번에 넘기므로 총량은 정확히 같다.
        부패쌓임 += Time.deltaTime;
        if (부패쌓임 >= 부패간격)
        {
            float dt = 부패쌓임; 부패쌓임 = 0f;
            for (int i = 0; i < 인벤.통들.Count; i++) 인벤.통들[i].시간(dt);
            for (int i = 땅무더기.All.Count - 1; i >= 0; i--)
                if (땅무더기.All[i] != null) 땅무더기.All[i].속.시간(dt);
        }
    }

    [Tooltip("부패를 몇 초마다 갱신하나 — 상하는 시간은 분 단위라 0.5 면 눈에 똑같다")]
    public float 부패간격 = 0.5f;
    float 부패쌓임;

    // ══════════════════════════════════════════════════════════ 그리기

    void OnGUI()
    {
        if (!열림) return;
        if (px == null) { px = new Texture2D(1, 1); px.SetPixel(0, 0, Color.white); px.Apply(); }

        고른통 = Mathf.Clamp(고른통, 0, Mathf.Max(0, 인벤.통들.Count - 1));

        // ★★★**장비 패널이 인벤 창에 붙어 같이 열린다** (2026-08-12 사용자 "인벤토리 말고
        //   장비착용하는 창도 있지않아? 좀보이드는" → "캐릭터인형 + 슬롯 필요하고, 좀보이드처럼,
        //   따로인지 합쳐져있는지 보고 만들어줘").
        //   ☆찾아보니 **좀보이드 바닐라엔 페이퍼돌이 없다.** 그걸 넣는 대표 모드(Equipment UI)가
        //     쓰는 방식이 **「인벤 창에 붙어 같이 열리고 같이 접히되, 떼어낼 수도 있다」** 였다.
        //   ☆그대로 따른다 — **키를 안 늘리고**(Tab 하나), 창은 눈으로 갈린다.
        //     장비도 결국 **물건 옮기기**라 인벤 쪽이 맞다. 「행동」인 제작(C)과는 여전히 갈린다 (5-7).
        float 장비w = 210f, w = 330f, h = 400f, 틈 = 8f;
        float 전체 = 장비w + 틈 + w + 틈 + w;
        float x = Screen.width * 0.5f - 전체 * 0.5f;
        float y = Screen.height * 0.5f - h * 0.5f;

        var 무더기 = 땅무더기.가까운것(transform.position, 닿는거리);

        // 기본 자리는 늘 이 셋이다 — 옮긴 창만 제 자리로 간다 (하나를 옮겨도 나머지가 안 밀린다)
        var 몸기본 = new Rect(x, y, 장비w, h);
        var 왼기본 = new Rect(x + 장비w + 틈, y, w, h);
        var 오른기본 = new Rect(왼기본.xMax + 틈, y, w, h);
        몸칸 = 창자리잡기(0, 몸기본);
        왼칸 = 창자리잡기(1, 왼기본);
        오른칸 = 창자리잡기(2, 오른기본);

        장비패널(몸칸);
        왼쪽패널(왼칸);
        오른쪽패널(오른칸, 무더기);

        // ★그린 뒤에 본다 — 칸이 먼저 이벤트를 먹을 기회를 준다
        창끌기(0, 몸칸); 창끌기(1, 왼칸); 창끌기(2, 오른칸);

        if (!string.IsNullOrEmpty(알림))
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            s.normal.textColor = new Color(1f, 0.92f, 0.75f);
            GUI.Label(new Rect(0f, y + h + 6f, Screen.width, 22f), 알림, s);
        }

        메뉴그리기();
        끌기그리기();
    }

    // ══════════════════════════════════════════════════════════ 끌어다 놓기

    /// 누르는 순간엔 **잡아만 둔다** — 옮길지 말지는 손을 뗄 때 정한다
    /// ★자리는 **화면 좌표로 받는다** — 부르는 쪽이 스크롤뷰 안이라 제 손으로는 알 수 없다
    void 집기(아이템 it, 인벤 통, bool 내것인가, 땅무더기 무더기, Vector2 화면자리)
    {
        끈것 = it; 끈통 = 통; 끈내것 = 내것인가; 끈무더기 = 무더기;
        누른자리 = 화면자리; 끄는중 = false;
    }

    void 끌기그리기()
    {
        var e = Event.current;
        if (끈것 == null) return;

        // 조금이라도 움직였으면 그때부터 「끄는 중」이다 (손떨림으로 끌리지 않게 5px)
        if (!끄는중 && e.type == EventType.MouseDrag
            && (e.mousePosition - 누른자리).sqrMagnitude > 25f) 끄는중 = true;

        if (끄는중)
        {
            // 놓을 수 있는 데를 테두리로 알려 준다
            for (int i = 0; i < 통칸.Count; i++)
                if (통칸[i].Contains(e.mousePosition)) 테두리(통칸[i], new Color(0.95f, 0.85f, 0.45f));
            if (왼칸.Contains(e.mousePosition)) 테두리(왼칸, new Color(0.5f, 0.7f, 0.95f, 0.7f));
            else if (오른칸.Contains(e.mousePosition)) 테두리(오른칸, new Color(0.5f, 0.7f, 0.95f, 0.7f));

            // 끌고 다니는 것 — 이름표 하나면 충분하다 (11장: 화면에 설명문을 안 쓴다)
            var r = new Rect(e.mousePosition.x + 12f, e.mousePosition.y - 10f, 150f, 22f);
            GUI.color = new Color(0.12f, 0.13f, 0.15f, 0.95f);
            GUI.DrawTexture(r, px);
            GUI.color = 끈것.종 != null ? 끈것.종.색 : Color.gray;
            GUI.DrawTexture(new Rect(r.x + 5f, r.y + 4f, 14f, 14f), px);
            GUI.color = Color.white;
            var s = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            s.normal.textColor = Color.white;
            GUI.Label(new Rect(r.x + 25f, r.y + 2f, r.width - 30f, 18f),
                      끈것.종 != null ? 끈것.종.이름 : "", s);
        }

        if (e.type == EventType.MouseUp && e.button == 0) 놓기(e.mousePosition, e.shift);
    }

    /// ★**어디에 놓았느냐가 정한다** — 4장의 좌클릭 원리와 같은 길이다
    void 놓기(Vector2 p, bool 전부)
    {
        var it = 끈것; var 출처 = 끈통; bool 내것 = 끈내것; var 무 = 끈무더기;
        bool 끌었다 = 끄는중;
        끈것 = null; 끈통 = null; 끈무더기 = null; 끄는중 = false;
        if (it == null || 출처 == null) return;

        // 안 끌었으면 그냥 클릭이다 — 옛 동작 그대로
        if (!끌었다) { 옮기기(it, 출처, 내것, 전부); if (무 != null) 무.갱신(); return; }

        // ⓪ ★장비 칸 위 — **몸에 걸친다.** 「어디에 놓았느냐가 정한다」의 그 길이다
        if (손칸.Contains(p)) { 손에쥐기(it); if (무 != null) 무.갱신(); return; }
        foreach (var kv in 몸칸자리)
            if (kv.Value.Contains(p)) { 몸에걸치기(kv.Key, it, 출처); if (무 != null) 무.갱신(); return; }

        // ① 가방 버튼 위 — 그 통에 콕 집어 넣는다
        for (int i = 0; i < 통칸.Count && i < 인벤.통들.Count; i++)
            if (통칸[i].Contains(p)) { 옮기기(it, 출처, 내것, 전부, 인벤.통들[i]); if (무 != null) 무.갱신(); return; }

        // ② 왼쪽 — 지금 보고 있는 통으로
        if (왼칸.Contains(p)) { 옮기기(it, 출처, 내것, 전부, 인벤.통들[고른통]); if (무 != null) 무.갱신(); return; }

        // ③ 오른쪽 — 앞에 있는 것(땅·사체) 속으로. 이미 거기 있던 것은 그대로 둔다
        if (오른칸.Contains(p) && 내것) { 옮기기(it, 출처, true, 전부, null, 무); if (무 != null) 무.갱신(); }
    }

    /// 옮겨 둔 자리가 있으면 그리로 — 화면 밖으로는 못 나간다 (되찾을 수 없게 되면 안 된다)
    Rect 창자리잡기(int i, Rect 기본)
    {
        if (!창자리[i].HasValue) return 기본;
        var p = 창자리[i].Value;
        p.x = Mathf.Clamp(p.x, -기본.width + 80f, Screen.width - 80f);
        p.y = Mathf.Clamp(p.y, 0f, Screen.height - 34f);
        return new Rect(p.x, p.y, 기본.width, 기본.height);
    }

    /// 제목 줄을 잡아 끈다 — 더블클릭하면 기본 자리로 돌아온다
    void 창끌기(int i, Rect r)
    {
        var e = Event.current;
        var 손잡이 = new Rect(r.x, r.y, r.width, 26f);
        if (e.type == EventType.MouseDown && e.button == 0 && 손잡이.Contains(e.mousePosition))
        {
            if (e.clickCount >= 2) { 창자리[i] = null; 옮기는창 = -1; }
            else { 옮기는창 = i; 잡은차이 = e.mousePosition - new Vector2(r.x, r.y); }
            e.Use();
        }
        else if (옮기는창 == i)
        {
            if (e.type == EventType.MouseDrag) { 창자리[i] = e.mousePosition - 잡은차이; e.Use(); }
            else if (e.type == EventType.MouseUp) 옮기는창 = -1;
        }
    }

    void 테두리(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), px);
        GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), px);
        GUI.DrawTexture(new Rect(r.x, r.y, 2f, r.height), px);
        GUI.DrawTexture(new Rect(r.xMax - 2f, r.y, 2f, r.height), px);
        GUI.color = Color.white;
    }

    // ══════════════════════════════════════════════════════════ 장비 (몸)

    void 장비패널(Rect r)
    {
        바탕(r);
        var 제목투 = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        제목투.normal.textColor = new Color(0.85f, 0.85f, 0.88f);
        GUI.Label(new Rect(r.x + 12f, r.y + 6f, r.width - 24f, 22f), "몸", 제목투);

        const float 칸 = 46f, 여백 = 8f, 줄틈 = 6f, 첫줄 = 34f;
        인형그리기(new Rect(r.x + r.width * 0.5f - 36f, r.y + 우측첫줄(첫줄), 72f, 190f));

        // ── 칸들 — 인형 좌우로 늘어선다
        몸칸자리.Clear();
        for (int i = 0; i < 몸표.Length; i++)
        {
            var (이름, 열, 줄) = 몸표[i];
            float sx = 열 == 0 ? r.x + 여백 : r.xMax - 여백 - 칸;
            var sr = new Rect(sx, r.y + 첫줄 + 줄 * (칸 + 줄틈), 칸, 칸);
            몸칸자리[이름] = sr;
            슬롯(sr, 이름, 걸친것(이름));
        }

        // ★손(무기)은 아래 가운데 — 좀보이드도 손은 옷과 따로 둔다
        손칸 = new Rect(r.x + r.width * 0.5f - 32f, r.y + 첫줄 + 4f * (칸 + 줄틈) + 16f, 64f, 64f);
        슬롯(손칸, "손", 인벤.쥔것);

        // ★칸을 누르면 벗는다 — 끌어다 놓기와 **클릭 한 번** 둘 다 되게 (5-7 의 그 규칙)
        var e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (손칸.Contains(e.mousePosition) && 인벤.쥔것 != null)
            {
                띄움($"{인벤.쥔것.종.이름} 을(를) 놓았다");
                인벤.쥔것 = null; e.Use();
            }
            else foreach (var kv in 몸칸자리)
            {
                if (!kv.Value.Contains(e.mousePosition)) continue;
                if (kv.Key == "가방")
                {
                    var 가방통 = 멘가방통();
                    if (가방통 != null) { 인벤.가방벗기(가방통); 고른통 = 0; 띄움("가방을 벗었다"); e.Use(); }
                    break;
                }
                if (인벤.입은것.TryGetValue(kv.Key, out var 입은) && 입은 != null)
                {
                    인벤.입은것.Remove(kv.Key);
                    인벤.어디든넣기(입은.종, 1);
                    띄움($"{입은.종.이름} 을(를) 벗었다");
                    e.Use();
                }
                break;
            }
        }
    }

    /// 인형을 칸 줄과 나란히 놓기 위한 자리 (칸 첫 줄과 같은 높이에서 시작한다)
    static float 우측첫줄(float 첫줄) => 첫줄 + 6f;

    /// 그 칸에 지금 뭐가 걸려 있나 — 가방만 제 길(`통들`)에서 가져온다
    아이템 걸친것(string 칸)
    {
        if (칸 == "가방") return 멘가방();
        return 인벤.입은것.TryGetValue(칸, out var it) ? it : null;
    }

    /// ★몸에 걸친다 — 그 칸에 맞는 것이 아니면 거절하고 **왜 안 되는지** 알린다
    void 몸에걸치기(string 칸, 아이템 it, 인벤 출처)
    {
        if (it == null || it.종 == null) return;

        if (칸 == "가방")
        {
            if (it.종.가방 > 0f) { 인벤.가방메기(it, 출처); 띄움($"{it.종.이름} 을(를) 멨다"); }
            else 띄움($"{it.종.이름} — 멜 수 있는 것이 아니다");
            return;
        }
        if (it.종.입는칸 != 칸) { 띄움($"{it.종.이름} — {칸} 에 걸칠 것이 아니다"); return; }

        if (인벤.입은것.TryGetValue(칸, out var 먼저) && 먼저 != null) 인벤.어디든넣기(먼저.종, 1);
        출처?.꺼내기(it.종.이름, 1);
        인벤.입은것[칸] = it;
        띄움($"{it.종.이름} 을(를) 걸쳤다");
    }

    /// 지금 메고 있는 가방 — 통 목록에 가방으로 붙은 것이 있으면 그것이다
    아이템 멘가방() => 멘가방통()?.가방아이템;

    인벤 멘가방통()
    {
        for (int i = 0; i < 인벤.통들.Count; i++)
            if (인벤.통들[i].가방아이템 != null) return 인벤.통들[i];
        return null;
    }

    /// ★캐릭터 인형 — 상자 몇 개면 충분하다. 6장의 「단순한 형태·크게 나눈 면」 그대로다
    void 인형그리기(Rect r)
    {
        GUI.color = new Color(0.40f, 0.43f, 0.48f);
        float cx = r.x + r.width * 0.5f;
        float 머리 = r.width * 0.34f;
        float 몸y = r.y + 머리 + 4f, 몸h = r.height * 0.40f;
        GUI.DrawTexture(new Rect(cx - 머리 * 0.5f, r.y, 머리, 머리), px);                        // 머리
        GUI.DrawTexture(new Rect(cx - r.width * 0.26f, 몸y, r.width * 0.52f, 몸h), px);          // 몸통
        GUI.DrawTexture(new Rect(cx - r.width * 0.46f, 몸y + 4f, r.width * 0.16f, 몸h * 0.86f), px);  // 왼팔
        GUI.DrawTexture(new Rect(cx + r.width * 0.30f, 몸y + 4f, r.width * 0.16f, 몸h * 0.86f), px);  // 오른팔
        float 다리y = 몸y + 몸h + 4f;
        GUI.DrawTexture(new Rect(cx - r.width * 0.24f, 다리y, r.width * 0.20f, r.height * 0.34f), px);  // 왼다리
        GUI.DrawTexture(new Rect(cx + r.width * 0.04f, 다리y, r.width * 0.20f, r.height * 0.34f), px);  // 오른다리
        GUI.color = Color.white;
    }

    /// 칸 하나 — 비었으면 무슨 칸인지 이름을 띄운다.
    /// ☆11장의 「설명문 금지」에 안 걸린다 — 빈 칸이 손인지 가방인지 모르면 **화면을 못 읽는다**
    void 슬롯(Rect r, string 이름, 아이템 it)
    {
        bool 위에 = r.Contains(Event.current.mousePosition);
        bool 놓으려는중 = 끄는중 && 끈것 != null;
        GUI.color = 놓으려는중 && 위에 ? new Color(0.36f, 0.42f, 0.28f)     // 놓을 수 있다
                  : 위에 ? new Color(0.26f, 0.28f, 0.32f) : new Color(0.14f, 0.15f, 0.17f);
        GUI.DrawTexture(r, px);
        테두리(r, 놓으려는중 && 위에 ? new Color(0.95f, 0.85f, 0.45f) : new Color(0.33f, 0.34f, 0.38f));

        if (it != null && it.종 != null)
        {
            GUI.color = it.종.색;
            GUI.DrawTexture(new Rect(r.x + 11f, r.y + 9f, r.width - 22f, r.height - 28f), px);
            GUI.color = Color.white;
            var ns = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter };
            ns.normal.textColor = new Color(0.95f, 0.9f, 0.75f);
            GUI.Label(new Rect(r.x, r.yMax - 18f, r.width, 15f), it.종.이름, ns);
        }
        else
        {
            var ls = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
            ls.normal.textColor = new Color(0.44f, 0.45f, 0.5f);
            GUI.Label(r, 이름, ls);
        }
    }

    /// ★손에 쥔다 — 무기가 아니면 거절하고 **왜 안 되는지** 알린다 (사건 알림은 11장 허용)
    void 손에쥐기(아이템 it)
    {
        if (it == null || it.종 == null) return;
        if (!it.종.무기) { 띄움($"{it.종.이름} — 손에 쥘 것이 아니다"); return; }
        인벤.쥔것 = it;
        띄움($"{it.종.이름} 을(를) 쥐었다");
    }

    void 왼쪽패널(Rect r)
    {
        바탕(r);

        // ── ★통 버튼 줄 (좀보이드) — 주머니 + 착용한 가방들
        float bx = r.x + 8f, by = r.y + 6f;
        통칸.Clear();
        for (int i = 0; i < 인벤.통들.Count; i++)
        {
            var 통 = 인벤.통들[i];
            var br = new Rect(bx, by, 94f, 22f);
            통칸.Add(br);                        // ★끌어다 놓을 자리로 기억해 둔다
            bool 고름 = i == 고른통;
            GUI.color = 고름 ? new Color(0.34f, 0.37f, 0.42f) : new Color(0.17f, 0.18f, 0.2f);
            GUI.DrawTexture(br, px);
            GUI.color = Color.white;

            var bs = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
            bs.normal.textColor = 고름 ? Color.white : new Color(0.62f, 0.62f, 0.66f);
            GUI.Label(br, $"{통.이름} {통.무게:F1}/{통.한도:F0}", bs);

            if (br.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown)
            { 고른통 = i; Event.current.Use(); }
            bx += 98f;
            if (bx + 94f > r.xMax - 8f) { bx = r.x + 8f; by += 24f; }
        }
        float 위 = by + 28f;

        // ── 전체 무게
        float t = 인벤.총한도 > 0f ? 인벤.총무게비 : 0f;
        var c = 인벤.총넘침 ? new Color(0.92f, 0.25f, 0.2f)
              : t > 0.75f ? new Color(0.9f, 0.7f, 0.25f) : new Color(0.45f, 0.7f, 0.5f);
        막대(r.x + 12f, 위, r.width - 24f, 8f, t, c);
        var ws = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleRight };
        ws.normal.textColor = 인벤.총넘침 ? new Color(1f, 0.6f, 0.55f) : new Color(0.72f, 0.72f, 0.76f);
        GUI.Label(new Rect(r.x + 12f, 위 + 8f, r.width - 24f, 16f),
                  인벤.총넘침 ? $"{인벤.총무게:F1} / {인벤.총한도:F0}kg — 못 걷는다"
                              : $"{인벤.총무게:F1} / {인벤.총한도:F0}kg", ws);
        위 += 28f;

        if (인벤.쥔것 != null)
        {
            var hs = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            hs.normal.textColor = new Color(0.95f, 0.85f, 0.5f);
            GUI.Label(new Rect(r.x + 12f, 위, r.width - 24f, 18f), "손: " + 인벤.쥔것.종.이름, hs);
            위 += 20f;
        }

        목록(new Rect(r.x + 8f, 위, r.width - 16f, r.yMax - 위 - 8f),
             인벤.통들[고른통], ref 왼쪽스크롤, true, null);
    }

    void 오른쪽패널(Rect r, 땅무더기 무더기)
    {
        바탕(r);
        var 제목투 = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        제목투.normal.textColor = new Color(0.85f, 0.85f, 0.88f);
        // ★이름표가 있으면 그것이 제목이다 (사체 — 「사슴 사체」)
        string 곳 = 무더기 != null && !string.IsNullOrEmpty(무더기.이름표) ? 무더기.이름표 : "땅바닥";
        GUI.Label(new Rect(r.x + 12f, r.y + 6f, r.width - 24f, 22f),
                  무더기 == null ? "땅바닥 (비어 있음)"
                  : 무더기.속.것들.Count == 0 ? $"{곳} (비어 있음)"
                  : $"{곳}  {무더기.속.무게:F1}kg", 제목투);

        if (무더기 == null) return;
        목록(new Rect(r.x + 8f, r.y + 32f, r.width - 16f, r.height - 40f),
             무더기.속, ref 오른쪽스크롤, false, 무더기);
    }

    void 목록(Rect 안, 인벤 것, ref Vector2 스크롤, bool 내것인가, 땅무더기 무더기)
    {
        if (것 == null) return;
        float 속높이 = 것.것들.Count * 26f + 3 * 20f + 10f;
        스크롤 = GUI.BeginScrollView(안, 스크롤, new Rect(0, 0, 안.width - 18f, 속높이));

        float yy = 0f;
        foreach (갈래 g in new[] { 갈래.재료, 갈래.먹을것, 갈래.물건 })
        {
            bool 머리썼나 = false;
            for (int i = 0; i < 것.것들.Count; i++)
            {
                var it = 것.것들[i];
                if (it.종 == null || it.종.갈래 != g) continue;

                if (!머리썼나)
                {
                    var gs = new GUIStyle(GUI.skin.label) { fontSize = 11 };
                    gs.normal.textColor = new Color(0.55f, 0.55f, 0.6f);
                    GUI.Label(new Rect(4f, yy, 200f, 18f), "▪ " + g, gs);
                    yy += 20f; 머리썼나 = true;
                }

                var 행 = new Rect(0f, yy, 안.width - 18f, 24f);
                bool 위에 = 행.Contains(Event.current.mousePosition);
                bool 쥔것 = 내것인가 && it == 인벤.쥔것;
                bool 끄는행 = 끄는중 && it == 끈것;

                GUI.color = 끄는행 ? new Color(0.20f, 0.22f, 0.26f)      // 끌고 나간 자리는 옅게
                          : 쥔것 ? new Color(0.32f, 0.30f, 0.16f)
                          : 위에 ? new Color(0.28f, 0.3f, 0.34f) : new Color(0.13f, 0.14f, 0.16f);
                GUI.DrawTexture(행, px);
                GUI.color = it.종.색;
                GUI.DrawTexture(new Rect(행.x + 5f, 행.y + 5f, 14f, 14f), px);
                GUI.color = Color.white;

                var ns = new GUIStyle(GUI.skin.label) { fontSize = 13 };
                ns.normal.textColor = it.상했나 ? new Color(0.62f, 0.68f, 0.45f) : Color.white;
                GUI.Label(new Rect(행.x + 26f, 행.y + 3f, 150f, 18f), it.종.이름, ns);

                var cs = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleRight };
                cs.normal.textColor = new Color(0.65f, 0.67f, 0.72f);
                GUI.Label(new Rect(행.x + 150f, 행.y + 3f, 80f, 18f), it.곁들임, cs);
                GUI.Label(new Rect(행.x + 232f, 행.y + 3f, 행.width - 238f, 18f), $"{it.무게:F1}kg", cs);

                // ★★★**메뉴가 떠 있으면 그 아래는 안 받는다** (2026-08-12 사용자 "우클릭
                //   손에쥐기를 했는데 땅에 버리네").
                //   ☆IMGUI 는 **그리는 순서대로** 이벤트를 준다. 목록이 메뉴보다 먼저 그려지니,
                //     메뉴 항목을 눌러도 **그 밑의 아이템 행이 먼저** 클릭을 받아 「집기」가 돌았다.
                //     그리고 손을 뗄 때 메뉴가 떠 있던 자리(오른쪽 패널 = 땅)에 놓아 버렸다.
                //   ☆메뉴는 뜬 순간부터 **덮개**다 — 닫히기 전엔 아래가 아무것도 안 받는다.
                if (위에 && Event.current.type == EventType.MouseDown && 메뉴대상 == null)
                {
                    // ★★★**스크롤뷰 안의 `mousePosition` 은 화면 좌표가 아니다** (2026-08-12
                    //   사용자 "마우스 우클릭한 위치가아니라 그 메뉴가").
                    //   ☆`BeginScrollView` 안에서는 원점이 **스크롤 내용의 왼쪽 위**로 바뀐다.
                    //     그 값을 그대로 들고 나가면, 메뉴는 스크롤뷰 **바깥**에서 그려지므로
                    //     화면 왼쪽 위 엉뚱한 데 떴다. 목록을 아래로 굴릴수록 더 어긋난다.
                    //   ☆끌기 판정도 같이 틀어져 있었다 — 누른 자리는 로컬인데 뗀 자리는
                    //     화면 좌표라, 두 값의 차가 커서 **누르자마자 「끄는 중」**이 됐다.
                    var 화면p = Event.current.mousePosition + 안.position - 스크롤;
                    if (Event.current.button == 1) 메뉴열기(it, 것, 내것인가, 무더기, 화면p);   // ★우클릭
                    else 집기(it, 것, 내것인가, 무더기, 화면p);   // ★잡아만 둔다 — 판정은 손 뗄 때
                    Event.current.Use();
                }
                yy += 26f;
            }
        }
        GUI.EndScrollView();
    }

    // ══════════════════════════════════════════════════════════ 우클릭 메뉴

    /// ★메뉴는 **아이템 표에서 나온다** — 여기에 행동을 박지 않는다
    void 메뉴열기(아이템 it, 인벤 통, bool 내것인가, 땅무더기 무더기, Vector2 화면자리)
    {
        메뉴.Clear();
        메뉴대상 = it; 메뉴통 = 통; 메뉴자리 = 화면자리;   // ★화면 좌표다 (스크롤뷰 로컬이 아니라)

        var 불 = 모닥불.가까운것(transform.position, 3f);
        var d = it.종;

        if (내것인가)
        {
            foreach (var u in d.쓸것)
            {
                switch (u)
                {
                    case 쓰임.먹기:
                        메뉴.Add(("먹기", () => 먹기(it, 통)));
                        break;
                    case 쓰임.굽기:
                        if (불 != null && 불.탄다) 메뉴.Add(("굽기", () => 불에올리기(it, 통, "구운고기")));
                        break;
                    case 쓰임.말리기:
                        if (불 != null && 불.탄다) 메뉴.Add(("말리기", () => 불에올리기(it, 통, "말린고기")));
                        break;
                    case 쓰임.땔감:
                        if (불 != null && 불.섰다) 메뉴.Add(("불에 넣기", () => 땔감(it, 통, 불)));
                        break;
                    case 쓰임.박기:
                        메뉴.Add(($"표식 박기 (나무 {표식나무})", () => 표식박기(통)));
                        break;
                    case 쓰임.펫주기:
                        메뉴.Add(("펫 주기", () => 펫주기(it, 통)));
                        break;
                }
            }
            // ★입고 벗기는 **우클릭과 장비창 둘 다** 된다 (2026-08-12 사용자 확정).
            //   ☆5-7 의 태도 그대로다 — *"끌기를 더하되 클릭 한 번을 없애지 않는다."*
            //     길을 늘리는 것과 길을 갈아치우는 것은 다르다.
            if (d.무기) 메뉴.Add((인벤.쥔것 == it ? "손에서 놓기" : "손에 쥐기", () => 쥐기(it)));
            if (d.가방 > 0f) 메뉴.Add(($"메기 (+{d.가방:F0}kg)", () => { 인벤.가방메기(it, 통); 메뉴닫기(); }));
            if (통.가방아이템 != null) 메뉴.Add(("이 가방 벗기", () => { 인벤.가방벗기(통); 고른통 = 0; 메뉴닫기(); }));
            메뉴.Add(("내려놓기", () => 옮기기(it, 통, true, false)));
        }
        else 메뉴.Add(("줍기", () => 옮기기(it, 통, false, false)));
    }

    void 메뉴그리기()
    {
        if (메뉴대상 == null || 메뉴.Count == 0) return;
        float w = 170f, h = 메뉴.Count * 24f + 8f;
        var r = new Rect(메뉴자리.x, 메뉴자리.y, w, h);
        if (r.yMax > Screen.height) r.y = Screen.height - h - 4f;

        GUI.color = new Color(0.1f, 0.1f, 0.12f, 0.97f);
        GUI.DrawTexture(r, px);
        GUI.color = Color.white;

        for (int i = 0; i < 메뉴.Count; i++)
        {
            var 행 = new Rect(r.x + 4f, r.y + 4f + i * 24f, w - 8f, 22f);
            bool 위에 = 행.Contains(Event.current.mousePosition);
            GUI.color = 위에 ? new Color(0.3f, 0.33f, 0.38f) : new Color(0.15f, 0.16f, 0.18f);
            GUI.DrawTexture(행, px);
            GUI.color = Color.white;
            var s = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            s.normal.textColor = Color.white;
            GUI.Label(new Rect(행.x + 8f, 행.y + 2f, 행.width - 12f, 18f), 메뉴[i].이름, s);

            if (위에 && Event.current.type == EventType.MouseDown)
            { var f = 메뉴[i].하기; 메뉴닫기(); f?.Invoke(); Event.current.Use(); return; }
        }
        // 바깥을 누르면 닫힌다
        if (Event.current.type == EventType.MouseDown && !r.Contains(Event.current.mousePosition))
        { 메뉴닫기(); Event.current.Use(); }
    }

    void 메뉴닫기() { 메뉴대상 = null; 메뉴통 = null; 메뉴.Clear(); }

    // ══════════════════════════════════════════════════════════ 행동

    void 먹기(아이템 it, 인벤 통)
    {
        if (!통.빼기(it)) return;
        hero.hp = Mathf.Clamp(hero.hp + it.종.회복, 1f, hero.maxHp);
        if (삶 != null) 삶.먹었다(it.종.배부름);
        띄움(it.종.회복 < 0f ? $"{it.종.이름} — 탈이 났다" : $"{it.종.이름}  +{it.종.회복:F0}");
    }

    void 불에올리기(아이템 it, 인벤 통, string 된다)
    {
        var 불 = 모닥불.가까운것(transform.position, 3f);
        if (불 == null || !불.탄다) { 띄움("불이 없다"); return; }
        var 다음 = 아이템표.찾기(된다);
        if (다음 == null || !통.빼기(it)) return;
        불.올리기(다음);
        띄움($"{it.종.이름} → {된다}");
    }

    void 땔감(아이템 it, 인벤 통, 모닥불 불)
    {
        if (!통.꺼내기(it.종.이름, 1)) return;
        불.땔감받기();
        띄움("불에 넣었다");
    }

    [Tooltip("표식 하나에 드는 나무")] public int 표식나무 = 2;
    void 표식박기(인벤 통)
    {
        if (인벤.다합쳐개수("나무") < 표식나무) { 띄움("나무가 모자란다"); return; }
        인벤.다합쳐꺼내기("나무", 표식나무);
        표식.박기(transform.position + transform.forward * 1.2f);
        띄움("표식을 박았다");
    }

    void 펫주기(아이템 it, 인벤 통)
    {
        var 안음 = GetComponent<HeroCarry>();
        if (안음 == null) return;
        var 대상 = 안음.가까운대상();
        if (대상 == null) { 띄움("줄 놈이 없다"); return; }
        if (!통.빼기(it)) return;
        // 상한 것도 펫은 먹는다 — 다만 신뢰가 덜 오른다 (기획 5-7)
        float 배 = it.상했나 ? 0.5f : (it.종.이름 == "구운고기" || it.종.이름 == "말린고기" ? 1.7f : 1f);
        대상.먹이받음(배);
        띄움($"{대상.종.이름}  {Mathf.RoundToInt(대상.신뢰)}");
    }

    void 쥐기(아이템 it)
    {
        인벤.쥔것 = (인벤.쥔것 == it) ? null : it;
        띄움(인벤.쥔것 != null ? $"{it.종.이름} 을(를) 쥐었다" : "손을 비웠다");
    }

    /// ★`받을통`·`받을무더기` 는 **끌어다 놓았을 때** 채워진다 — 비면 옛 길(발밑·고른통) 그대로다
    void 옮기기(아이템 it, 인벤 통, bool 내것에서, bool 전부, 인벤 받을통 = null, 땅무더기 받을무더기 = null)
    {
        var 자리 = transform.position + transform.forward * 0.9f;
        if (내것에서)
        {
            // 내 통 → 내 다른 통 (가방 버튼 위에 놓기). 같은 통이면 아무 일도 없다
            if (받을통 != null)
            {
                if (받을통 == 통) return;
                if (인벤.쥔것 == it) 인벤.쥔것 = null;
                if (it.뭉치나 && !전부 && it.개수 > 1) { if (받을통.넣기(it.종, 1) > 0) it.개수--; return; }
                if (받을통.받기(it)) 통.빼기(it);
                return;
            }

            if (인벤.쥔것 == it) 인벤.쥔것 = null;

            // 앞에 있는 무더기(땅·사체) 속으로
            if (받을무더기 != null)
            {
                if (it.뭉치나 && !전부 && it.개수 > 1) { it.개수--; 받을무더기.속.넣기(it.종, 1); return; }
                if (통.빼기(it)) 받을무더기.속.받기(it);
                return;
            }

            if (it.뭉치나 && !전부 && it.개수 > 1) { it.개수--; 땅무더기.여기(자리).속.넣기(it.종, 1); return; }
            if (통.빼기(it)) 땅무더기.내려놓기(it, 자리);
            return;
        }
        var 넣을통 = 받을통 ?? 인벤.통들[고른통];
        if (it.뭉치나 && !전부 && it.개수 > 1) { if (넣을통.넣기(it.종, 1) > 0) it.개수--; return; }
        if (넣을통.받기(it)) 통.빼기(it);
    }

    void 띄움(string s) { 알림 = s; 알림T = 2f; }

    // ── 자잘한 것
    void 바탕(Rect r)
    {
        GUI.color = new Color(0.06f, 0.06f, 0.07f, 0.93f);
        GUI.DrawTexture(r, px);
        GUI.color = Color.white;
    }
    void 막대(float x, float y, float w, float h, float t, Color c)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(x, y, w, h), px);
        GUI.color = c;
        GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(t), h), px);
        GUI.color = Color.white;
    }
}
