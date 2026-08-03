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

        // ── 가진 것 · 시각 (오른쪽 위)
        var st = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperRight };
        st.normal.textColor = Color.white;
        GUI.Label(new Rect(Screen.width - 300f, 10f, 280f, 26f),
            $"고기 {Stock.Get(Stock.Kind.고기)}   나무 {Stock.Get(Stock.Kind.나무)}   돌 {Stock.Get(Stock.Kind.돌)}", st);

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
                Bar(bx, Screen.height - 110f, 180f, 10f, t.신뢰 / 100f, new Color(0.4f, 0.8f, 0.5f));
                // 묶여 있으면 굶주림도 같이 — 안 먹이면 죽는다
                if (t.묶임)
                    Bar(bx, Screen.height - 96f, 180f, 6f, 1f - t.굶주림,
                        Color.Lerp(new Color(0.85f, 0.25f, 0.2f), new Color(0.75f, 0.6f, 0.3f), 1f - t.굶주림));
                var ns = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
                ns.normal.textColor = Color.white;
                GUI.Label(new Rect(bx, Screen.height - 132f, 180f, 20f),
                          $"{t.종.이름}  {Mathf.RoundToInt(t.신뢰)}", ns);
            }
            if (!string.IsNullOrEmpty(carry.알림))
            {
                var ms = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
                ms.normal.textColor = new Color(1f, 0.95f, 0.8f);
                GUI.Label(new Rect(0f, Screen.height * 0.62f, Screen.width, 30f), carry.알림, ms);
            }
        }

        // ── 죽었을 때
        if (!h.Alive)
        {
            var big = new GUIStyle(GUI.skin.label) { fontSize = 42, alignment = TextAnchor.MiddleCenter };
            big.normal.textColor = new Color(0.9f, 0.3f, 0.3f);
            GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 60f), "쓰러졌다", big);
        }
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
