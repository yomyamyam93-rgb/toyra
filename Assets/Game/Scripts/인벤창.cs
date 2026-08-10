using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// 인벤토리 창 (Tab) — **좀보이드 형식** (사용자 확정, 기획 5-7).
///
/// ★좌: 내가 가진 것 / 우: **앞에 있는 것**(땅 무더기). **물건 옮기기만 한다.**
///   짓고 요리하는 것은 제작창(C)이 맡는다 — 한 창에 섞으면 **클릭 결과가 예측이 안 된다.**
///
/// ★★**창을 열어도 게임이 안 멈춘다** (사용자 확정). 밤에 짐 정리하다 습격당한다.
///
/// ★종류를 코드에 박지 않는다 — `아이템표` 에 한 줄 늘면 여기 저절로 뜬다.
[RequireComponent(typeof(Hero))]
public class 인벤창 : MonoBehaviour
{
    [Tooltip("이 거리 안의 무더기를 「앞에 있는 것」으로 본다 (m)")] public float 닿는거리 = 2.6f;

    public bool 열림 { get; private set; }

    Hero hero;
    HeroAttack 손;
    bool 손껐다;
    Texture2D px;
    Vector2 왼쪽스크롤, 오른쪽스크롤;

    void Awake() { hero = GetComponent<Hero>(); 손 = GetComponent<HeroAttack>(); }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        bool 토글 = k != null && k.tabKey.wasPressedThisFrame;
        bool 닫기 = k != null && k.escapeKey.wasPressedThisFrame;
#else
        bool 토글 = Input.GetKeyDown(KeyCode.Tab);
        bool 닫기 = Input.GetKeyDown(KeyCode.Escape);
#endif
        if (토글) 열림 = !열림;
        if (닫기) 열림 = false;
        if (!hero.Alive) 열림 = false;

        // 창이 열린 동안은 손이 안 나간다 — 항목을 누르다 헛스윙하면 안 된다
        if (손 != null)
        {
            if (열림 && 손.enabled) { 손.enabled = false; 손껐다 = true; }
            else if (!열림 && 손껐다) { 손.enabled = true; 손껐다 = false; }
        }

        // ★부패는 창을 안 열어도 돈다 — 시간은 그냥 흐른다
        인벤.내것.시간(Time.deltaTime);
        for (int i = 땅무더기.All.Count - 1; i >= 0; i--)
            if (땅무더기.All[i] != null) 땅무더기.All[i].속.시간(Time.deltaTime);
    }

    // ══════════════════════════════════════════════════════════ 그리기

    void OnGUI()
    {
        if (!열림) return;
        if (px == null) { px = new Texture2D(1, 1); px.SetPixel(0, 0, Color.white); px.Apply(); }

        float w = 320f, h = 380f, 틈 = 8f;
        float x = Screen.width * 0.5f - w - 틈 * 0.5f;
        float y = Screen.height * 0.5f - h * 0.5f;

        var 무더기 = 땅무더기.가까운것(transform.position, 닿는거리);

        패널(new Rect(x, y, w, h), "가진 것", 인벤.내것, true, 무더기, ref 왼쪽스크롤);
        패널(new Rect(x + w + 틈, y, w, h), 무더기 != null ? "땅바닥" : "땅바닥 (비어 있음)",
             무더기 != null ? 무더기.속 : null, false, 무더기, ref 오른쪽스크롤);
    }

    void 패널(Rect r, string 제목, 인벤 것, bool 내것인가, 땅무더기 무더기, ref Vector2 스크롤)
    {
        GUI.color = new Color(0.06f, 0.06f, 0.07f, 0.92f);
        GUI.DrawTexture(r, px);
        GUI.color = Color.white;

        var 제목투 = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
        제목투.normal.textColor = new Color(0.85f, 0.85f, 0.88f);
        GUI.Label(new Rect(r.x + 12f, r.y + 6f, r.width - 24f, 22f), 제목, 제목투);

        // ── 무게 막대 (내 것만)
        float 위 = r.y + 30f;
        if (내것인가)
        {
            float t = 것.한도 > 0f ? Mathf.Clamp01(것.무게 / 것.한도) : 0f;
            var c = t > 0.95f ? new Color(0.9f, 0.3f, 0.25f)
                  : t > 0.75f ? new Color(0.9f, 0.7f, 0.25f) : new Color(0.45f, 0.7f, 0.5f);
            막대(r.x + 12f, 위, r.width - 24f, 8f, t, c);
            var ws = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleRight };
            ws.normal.textColor = new Color(0.75f, 0.75f, 0.78f);
            GUI.Label(new Rect(r.x + 12f, 위 + 8f, r.width - 24f, 16f),
                      $"{것.무게:F1} / {것.한도:F0}kg", ws);
            위 += 28f;
        }

        if (것 == null) return;

        var 안 = new Rect(r.x + 8f, 위, r.width - 16f, r.y + r.height - 위 - 8f);
        var 속높이 = 줄수(것) * 26f + 갈래수(것) * 20f + 10f;
        스크롤 = GUI.BeginScrollView(안, 스크롤, new Rect(0, 0, 안.width - 18f, 속높이));

        float yy = 0f;
        foreach (갈래 g in new[] { 갈래.재료, 갈래.먹을것, 갈래.물건 })
        {
            var 줄들 = 것들모으기(것, g);
            if (줄들.Count == 0) continue;

            var gs = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            gs.normal.textColor = new Color(0.55f, 0.55f, 0.6f);
            GUI.Label(new Rect(4f, yy, 200f, 18f), "▪ " + g, gs);
            yy += 20f;

            foreach (var it in 줄들)
            {
                var 행 = new Rect(0f, yy, 안.width - 18f, 24f);
                bool 위에 = 행.Contains(Event.current.mousePosition);

                GUI.color = 위에 ? new Color(0.28f, 0.3f, 0.34f) : new Color(0.13f, 0.14f, 0.16f);
                GUI.DrawTexture(행, px);

                // 아이콘 대신 색 네모 (6장 — 장난감 톤)
                GUI.color = it.종.색;
                GUI.DrawTexture(new Rect(행.x + 5f, 행.y + 5f, 14f, 14f), px);
                GUI.color = Color.white;

                var ns = new GUIStyle(GUI.skin.label) { fontSize = 13 };
                ns.normal.textColor = it.상했나 ? new Color(0.6f, 0.65f, 0.45f) : Color.white;
                GUI.Label(new Rect(행.x + 26f, 행.y + 3f, 150f, 18f), it.종.이름, ns);

                var cs = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleRight };
                cs.normal.textColor = new Color(0.65f, 0.67f, 0.72f);
                GUI.Label(new Rect(행.x + 150f, 행.y + 3f, 70f, 18f), it.곁들임, cs);
                GUI.Label(new Rect(행.x + 220f, 행.y + 3f, 행.width - 226f, 18f),
                          $"{it.무게:F1}kg", cs);

                if (위에 && Event.current.type == EventType.MouseDown)
                {
                    옮기기(it, 내것인가, 무더기, Event.current.shift);
                    Event.current.Use();
                }
                yy += 26f;
            }
        }
        GUI.EndScrollView();
    }

    /// ★클릭 한 번으로 옮긴다 (드래그 없음). Shift 면 뭉친 것 전부
    void 옮기기(아이템 it, bool 내것에서, 땅무더기 무더기, bool 전부)
    {
        var 자리 = transform.position + transform.forward * 0.9f;

        if (내것에서)
        {
            if (it.뭉치나 && !전부 && it.개수 > 1)
            {
                it.개수--;
                땅무더기.여기(자리).속.넣기(it.종, 1);
                return;
            }
            if (인벤.내것.빼기(it)) 땅무더기.내려놓기(it, 자리);
            return;
        }

        if (무더기 == null) return;
        if (it.뭉치나 && !전부 && it.개수 > 1)
        {
            if (인벤.내것.넣기(it.종, 1) > 0) it.개수--;
            return;
        }
        if (인벤.내것.받기(it)) 무더기.속.빼기(it);      // 무거우면 못 든다 (한도)
    }

    // ── 자잘한 것
    static List<아이템> 것들모으기(인벤 것, 갈래 g)
    {
        var l = new List<아이템>();
        for (int i = 0; i < 것.것들.Count; i++)
            if (것.것들[i].종 != null && 것.것들[i].종.갈래 == g) l.Add(것.것들[i]);
        return l;
    }
    static int 줄수(인벤 것) => 것.것들.Count;
    static int 갈래수(인벤 것)
    {
        int n = 0;
        foreach (갈래 g in new[] { 갈래.재료, 갈래.먹을것, 갈래.물건 })
            if (것들모으기(것, g).Count > 0) n++;
        return n;
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
