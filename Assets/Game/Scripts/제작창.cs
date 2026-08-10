using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// 제작창 (C) — **앞에 무엇이 있느냐가 목록을 정한다.**
///
/// ★빈 땅에서 열면 지을 것이 나오고, 모닥불 앞에서 열면 불에 할 일이 나온다.
///   좌클릭이 「앞에 있는 것」을 보고 때릴지 캘지 정하는 것과 같은 원리다 (4장).
///   → 항목이 늘어도 조작은 C 하나로 끝난다.
///
/// ★★고른다고 완성되지 않는다 (9-0 인과와 행위). 모닥불을 고르면 **터**가 놓일 뿐이고,
///   재료는 그 앞에서 F 로 부어 넣어야 한다.
[RequireComponent(typeof(Hero))]
public class 제작창 : MonoBehaviour
{
    [Tooltip("모닥불 터를 몸에서 얼마나 앞에 놓나 (m)")] public float 놓는거리 = 2.6f;
    [Tooltip("이 거리 안의 모닥불을 「앞에 있는 것」으로 본다 (m)")] public float 닿는거리 = 3f;


    public bool 열림 { get; private set; }

    /// 방금 벌어진 일 — 화면에 잠깐 띄운다
    public string 알림 { get; private set; } = "";
    float 알림T;

    Hero hero;
    HeroAttack 손;
    bool 손껐다;

    /// 창에 그릴 한 줄
    public struct 항목
    {
        public string 제목, 곁들임;
        public bool 됨;                 // 재료가 되나 (안 되면 흐리게)
        public System.Action 하기;
    }

    readonly List<항목> 목록 = new List<항목>();

    void Awake() { hero = GetComponent<Hero>(); 손 = GetComponent<HeroAttack>(); }

    void Update()
    {
        float dt = Time.deltaTime;
        if (알림T > 0f) { 알림T -= dt; if (알림T <= 0f) 알림 = ""; }

#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        bool 열기 = k != null && k.cKey.wasPressedThisFrame;
        bool 닫기 = k != null && k.escapeKey.wasPressedThisFrame;
#else
        bool 열기 = Input.GetKeyDown(KeyCode.C);
        bool 닫기 = Input.GetKeyDown(KeyCode.Escape);
#endif
        if (열기) 열림 = !열림;
        if (닫기) 열림 = false;
        if (!hero.Alive) 열림 = false;

        // ★창이 열려 있는 동안은 손이 안 나간다 — 항목을 누르다 헛스윙하면 안 된다
        if (손 != null)
        {
            if (열림 && 손.enabled) { 손.enabled = false; 손껐다 = true; }
            else if (!열림 && 손껐다) { 손.enabled = true; 손껐다 = false; }
        }
    }

    /// ★★목록은 **제작법 표에서 나온다** — 만들 것을 코드에 박지 않는다 (2026-08-10).
    ///   도구가 있어야 뜨는 것도, 재료가 모자라 흐린 것도 전부 표가 정한다
    void 목록짜기()
    {
        목록.Clear();
        var 불 = 모닥불.가까운것(transform.position, 닿는거리);

        // 짓다 만 터 앞이면 창에 할 일이 없다 (F 로 붓는다)
        if (불 != null && !불.섰다)
        {
            목록.Add(new 항목 {
                제목 = "짓는 중",
                곁들임 = $"돌 {불.든돌}/{불.필요돌}   나무 {불.든나무}/{불.필요나무}",
                됨 = false, 하기 = null });
            return;
        }

        foreach (var r in 제작법표.전부)
        {
            var 법 = r;                                  // 클로저용
            bool 도구 = 제작법표.도구있나(법);
            bool 재료 = 제작법표.재료있나(법);
            목록.Add(new 항목 {
                제목 = 법.이름,
                곁들임 = 제작법표.재료글(법) + (string.IsNullOrEmpty(법.곁들임) ? "" : "  —  " + 법.곁들임),
                됨 = 도구 && 재료,
                하기 = () => 만들기(법)
            });
        }
    }

    void 만들기(제작법 법)
    {
        // ★땅에 세우는 것은 「터」만 놓고 재료는 그 앞에서 F 로 붓는다 (9-0 인과와 행위)
        if (!string.IsNullOrEmpty(법.세울것)) { 터놓기(); return; }
        if (제작법표.만들기(법)) 띄움(법.이름);
    }

    /// ★터만 놓는다. 재료는 그 앞에서 F 로 붓는다 — 저절로 서지 않는다
    void 터놓기()
    {
        var 앞 = transform.position + transform.forward * 놓는거리;
        if (모닥불.가까운것(앞, 2.5f) != null) { 띄움("자리가 좁다"); return; }

        모닥불.터놓기(앞);
        열림 = false;
    }

    void 띄움(string s) { 알림 = s; 알림T = 2f; }

    // ────────────────────────────────────────── 그리기

    Texture2D px;

    void OnGUI()
    {
        if (!열림) return;
        if (px == null) { px = new Texture2D(1, 1); px.SetPixel(0, 0, Color.white); px.Apply(); }

        목록짜기();

        var 제목투 = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleLeft };
        var 곁투 = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.UpperLeft, wordWrap = true };

        // ★칸 높이는 내용이 정한다 (2026-08-11 "텍스트가 다 깨졌어") — 고정 54px 시절엔
        //   긴 설명이 두 줄로 접히고 아랫줄이 칸 밑으로 잘려 글자가 깨져 보였다
        float w = 300f, 여백 = 10f;
        float 글폭 = w - 여백 * 2f - 24f;
        var 칸h = new float[목록.Count];
        float h = 여백 * 2f;
        for (int i = 0; i < 목록.Count; i++)
        {
            float 설명h = string.IsNullOrEmpty(목록[i].곁들임)
                ? 0f : 곁투.CalcHeight(new GUIContent(목록[i].곁들임), 글폭);
            칸h[i] = 28f + 설명h + 6f;
            h += 칸h[i] + 6f;
        }
        float x = Screen.width * 0.5f - w * 0.5f;
        float y = Screen.height * 0.5f - h * 0.5f;

        // 바탕
        GUI.color = new Color(0.06f, 0.06f, 0.07f, 0.88f);
        GUI.DrawTexture(new Rect(x, y, w, h), px);
        GUI.color = Color.white;

        float 줄y = y + 여백;
        for (int i = 0; i < 목록.Count; i++)
        {
            var it = 목록[i];
            var r = new Rect(x + 여백, 줄y, w - 여백 * 2f, 칸h[i]);
            줄y += 칸h[i] + 6f;

            bool 위에 = r.Contains(Event.current.mousePosition);
            GUI.color = it.됨
                ? (위에 ? new Color(0.28f, 0.3f, 0.34f, 1f) : new Color(0.16f, 0.17f, 0.19f, 1f))
                : new Color(0.11f, 0.11f, 0.12f, 1f);
            GUI.DrawTexture(r, px);
            GUI.color = Color.white;

            제목투.normal.textColor = it.됨 ? Color.white : new Color(0.45f, 0.45f, 0.47f);
            곁투.normal.textColor = it.됨 ? new Color(0.68f, 0.7f, 0.74f) : new Color(0.5f, 0.5f, 0.53f);
            GUI.Label(new Rect(r.x + 12f, r.y + 6f, r.width - 24f, 22f), it.제목, 제목투);
            GUI.Label(new Rect(r.x + 12f, r.y + 28f, r.width - 24f, r.height - 34f), it.곁들임, 곁투);

            if (it.됨 && it.하기 != null && Event.current.type == EventType.MouseDown && 위에)
            {
                it.하기();
                Event.current.Use();
            }
        }
    }
}
