using UnityEngine;

/// 화면 표시 — **상태와 수치만.** 설명문은 쓰지 않는다.
/// (조작법·규칙 해설을 화면에 띄우는 건 매뉴얼이지 게임이 아니다)
public class HUD : MonoBehaviour
{
    Texture2D px;
    DayNight day;
    HeroCarry carry;

    void Awake()
    {
        px = new Texture2D(1, 1);
        px.SetPixel(0, 0, Color.white);
        px.Apply();
    }

    void OnGUI()
    {
        var h = Hero.Me;
        if (h == null) return;

        // ── 체력·지구력 (왼쪽 아래)
        float w = 240f, bh = 14f, x = 20f, y = Screen.height - 64f;
        Bar(x, y, w, bh, h.hp / Mathf.Max(1f, h.maxHp), new Color(0.85f, 0.25f, 0.25f));
        Bar(x, y + bh + 6f, w, bh * 0.7f, h.stamina / Mathf.Max(1f, h.maxStamina), new Color(0.9f, 0.8f, 0.3f));

        // ── ★★가진 것을 **글자로 나열하지 않는다** (2026-08-10 사용자 —
        //   *"돌이 몇갠지 나무가 몇갠지 UI로 표현해서 좆같았던 적이 있다. 아이템이 그것만
        //   있는 게 아닌데."*). 옛 코드는 `고기 X 나무 X 돌 X` 를 여기에 박아 놨었다 —
        //   종류가 늘 때마다 이 줄을 늘려야 했고 화면이 글자로 도배됐다.
        //   → **상시로 뜨는 것은 무게 막대 하나뿐.** 개수는 Tab 을 눌러야 본다 (기획 5-7)
        무게그리기();

        if (day == null) day = FindFirstObjectByType<DayNight>();
        if (day != null)
        {
            var ts = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.UpperRight };
            ts.normal.textColor = Color.Lerp(new Color(0.6f, 0.7f, 1f), Color.white, day.낮정도);
            GUI.Label(new Rect(Screen.width - 300f, 34f, 280f, 30f), day.시계, ts);
        }

        // ── 길들이는 중인 놈의 신뢰 · 알림 (상태와 수치만, 설명문은 쓰지 않는다)
        if (carry == null) carry = FindFirstObjectByType<HeroCarry>();
        if (carry != null)
        {
            var t = carry.가까운대상();
            if (t != null)
            {
                float bx = Screen.width * 0.5f - 90f;
                bool 내펫 = t.side == Critter.Side.내편;

                // ★내 펫은 신뢰가 아니라 **자란 정도**를 본다 — 이제 관심사가 "크느냐"다
                if (내펫)
                    Bar(bx, Screen.height - 110f, 180f, 10f, t.자람, new Color(0.55f, 0.75f, 0.95f));
                else
                    Bar(bx, Screen.height - 110f, 180f, 10f, t.신뢰 / 100f, new Color(0.4f, 0.8f, 0.5f));

                // 묶여 있으면 굶주림도 같이 — 안 먹이면 죽는다
                if (t.묶임)
                    Bar(bx, Screen.height - 96f, 180f, 6f, 1f - t.굶주림,
                        Color.Lerp(new Color(0.85f, 0.25f, 0.2f), new Color(0.75f, 0.6f, 0.3f), 1f - t.굶주림));

                var ns = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
                ns.normal.textColor = Color.white;
                string 값 = 내펫 ? (t.새끼 ? $"{Mathf.RoundToInt(t.자람 * 100f)}%" : "")
                                 : $"{Mathf.RoundToInt(t.신뢰)}";
                GUI.Label(new Rect(bx, Screen.height - 132f, 180f, 20f),
                          $"{t.종.이름}  {값}", ns);
            }
            if (!string.IsNullOrEmpty(carry.알림))
            {
                var ms = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
                ms.normal.textColor = new Color(1f, 0.95f, 0.8f);
                GUI.Label(new Rect(0f, Screen.height * 0.62f, Screen.width, 30f), carry.알림, ms);
            }
        }

        // ── 생존 — ★평소엔 아무것도 안 뜬다. 문턱을 넘을 때만 뜬다 (5-5 · 11장)
        생존그리기(h);

        채집그리기(h);

        // ── 모닥불 — 짓는 게이지 · 남은 불 · 알림 (수치와 상태만)
        모닥불그리기(h);

        // ── 죽었을 때
        if (!h.Alive)
        {
            var big = new GUIStyle(GUI.skin.label) { fontSize = 42, alignment = TextAnchor.MiddleCenter };
            big.normal.textColor = new Color(0.9f, 0.3f, 0.3f);
            GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 60f), "쓰러졌다", big);
        }
    }

    /// 짐 무게 — 상시로 뜨는 유일한 「가진 것」 표시
    void 무게그리기()
    {
        float 총 = 인벤.총무게, 한도 = 인벤.총한도;
        if (한도 <= 0f) return;

        float 비 = 인벤.총무게비;
        float x = 20f, y = Screen.height - 30f, w = 240f;

        var c = 인벤.총넘침 ? new Color(0.92f, 0.25f, 0.2f)
              : 비 > 0.75f ? new Color(0.9f, 0.7f, 0.25f) : new Color(0.5f, 0.55f, 0.6f);
        // ★넘쳐서 발이 안 나갈 때는 깜빡인다 — 왜 안 움직이는지 알아야 한다
        if (인벤.총넘침) c.a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 5f));
        Bar(x, y, w, 7f, Mathf.Clamp01(비), c);

        var s = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleLeft };
        s.normal.textColor = 인벤.총넘침 ? new Color(1f, 0.55f, 0.5f) : new Color(0.7f, 0.7f, 0.74f);
        GUI.Label(new Rect(x + w + 8f, y - 5f, 160f, 16f),
                  인벤.총넘침 ? $"{총:F1}kg  무거워서 못 걷는다" : $"{총:F1}kg", s);
    }

    생존 삶;

    /// ★★**게이지를 상시로 띄우지 않는다** (기획 5-5 · 11장).
    ///   평소엔 화면에 아무것도 없다가, 몸이 신호를 보낼 때만 뜬다.
    ///   ☆상시로 띄우면 「관리 게임」이 된다 — 눈이 계속 게이지에 가 있게 된다.
    void 생존그리기(Hero h)
    {
        if (삶 == null) { 삶 = h.GetComponent<생존>(); if (삶 == null) return; }

        float x = 20f, y = Screen.height - 104f, w = 240f;
        var s = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleLeft };

        void 한줄(string 이름, float 값, float 문턱, Color c)
        {
            if (값 < 문턱) return;                       // 아직 신호가 아니다 — 안 그린다
            float t = Mathf.InverseLerp(문턱, 1f, 값);
            // 한계에 가까우면 깜빡인다 — 소리 없이 재촉하는 유일한 수단
            float 깜 = 값 > 0.9f ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 4f)) : 1f;
            GUI.color = new Color(c.r, c.g, c.b, 깜);
            Bar(x, y, w, 8f, t, c);
            GUI.color = Color.white;
            s.normal.textColor = new Color(1f, 1f, 1f, 0.85f * 깜);
            GUI.Label(new Rect(x + w + 8f, y - 5f, 120f, 18f), 이름, s);
            y -= 14f;
        }

        한줄("목마름", 삶.목마름, 0.5f, new Color(0.35f, 0.65f, 0.95f));
        한줄("배고픔", 삶.배고픔, 0.55f, new Color(0.9f, 0.6f, 0.25f));
        한줄("피로",   삶.피로,   0.6f, new Color(0.6f, 0.5f, 0.75f));

        // 물가에 섰을 때만 — 상호작용 표시는 허용된다 (11장)
        if (삶.물가 && 삶.목마름 > 0.02f)
        {
            var ms = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            ms.normal.textColor = new Color(0.75f, 0.9f, 1f);
            GUI.Label(new Rect(0f, Screen.height * 0.68f, Screen.width, 22f), "F  물", ms);
        }
        if (!string.IsNullOrEmpty(삶.알림))
        {
            var ns = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            ns.normal.textColor = new Color(0.8f, 0.92f, 1f);
            GUI.Label(new Rect(0f, Screen.height * 0.64f, Screen.width, 24f), 삶.알림, ns);
        }
    }

    /// ★재료를 부을 때 게이지가 차오르는 게 보여야 한다 (2026-08-10 사용자).
    ///   불 위에 띄운다 — 어디를 보고 F 를 누르는지와 같은 자리여야 읽힌다.
    void 모닥불그리기(Hero h)
    {
        var 불 = 모닥불.가까운것(h.transform.position, 5f);
        if (불 == null) return;

        var cam = Camera.main;
        if (cam == null) return;
        var sp = cam.WorldToScreenPoint(불.transform.position + Vector3.up * 1.4f);
        if (sp.z <= 0f) return;
        float x = sp.x - 70f, y = Screen.height - sp.y;

        var s = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
        s.normal.textColor = Color.white;

        if (!불.섰다)
        {
            // 짓는 중 — 게이지와 필요 수량
            Bar(x, y, 140f, 9f, 불.진행, new Color(0.95f, 0.78f, 0.35f));
            GUI.Label(new Rect(x, y - 20f, 140f, 18f),
                      $"돌 {불.든돌}/{불.필요돌}   나무 {불.든나무}/{불.필요나무}", s);
        }
        else if (불.탄다)
        {
            // 남은 불 — 줄어드는 게 보여야 땔감을 넣으러 온다
            Bar(x, y, 140f, 7f, 불.연료 / Mathf.Max(1f, 불.최대연료), new Color(1f, 0.55f, 0.22f));
            string 굽기 = 불.굽는중 > 0 ? $"   굽는 중 {불.굽는중}" : "";
            GUI.Label(new Rect(x, y - 20f, 140f, 18f),
                      $"{Mathf.CeilToInt(불.연료 / 60f)}분{굽기}", s);
        }

        if (!string.IsNullOrEmpty(불.알림))
            GUI.Label(new Rect(x, y + 12f, 140f, 18f), 불.알림, s);
    }

    /// ★★채집 게이지 — **캘 때만** 뜬다 (11장 · 기획 5-7 "상시로 뜨는 것은 무게 막대 하나뿐").
    ///   머리 위에 띄운다 — 몸이 하는 일(쪼그려 앉아 뒤적임)과 같은 자리라야 읽힌다.
    ///   ☆글자를 안 쓴다. 막대가 스스로 말한다 (11장 "이 문장이 없으면 화면을 못 읽는가?").
    void 채집그리기(Hero h)
    {
        if (!HeroAttack.채집중) return;
        var cam = Camera.main;
        if (cam == null) return;
        var sp = cam.WorldToScreenPoint(h.transform.position + Vector3.up * (h.height + 0.45f));
        if (sp.z <= 0f) return;
        Bar(sp.x - 34f, Screen.height - sp.y, 68f, 7f,
            HeroAttack.채집게이지, new Color(0.85f, 0.80f, 0.55f));
    }

    void Bar(float x, float y, float w, float h, float t, Color c)
    {
        t = Mathf.Clamp01(t);
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(x, y, w, h), px);
        GUI.color = c;
        GUI.DrawTexture(new Rect(x, y, w * t, h), px);
        GUI.color = Color.white;
    }
}
