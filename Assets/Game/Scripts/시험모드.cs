using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// 시험 모드 — **F2** 로 켜고 끈다 (2026-08-11 사용자 "F2버튼에 테스트모드 넣어줄래?
/// 이속 10배, 스테미너 안닳게 좀 둘러보게").
///
/// ★세상을 둘러보려고 만든 것이다. 게임 규칙을 바꾸는 게 아니라 **잠깐 젖혀 두는** 것이라,
///   끄면 원래 값이 그대로 돌아온다 (켤 때 적어 둔다).
/// ★화면에 상시로 글자를 띄우지 않는다 (11장) — 켜져 있는 동안만 작은 표시 하나.
///   이건 「설명문」이 아니라 **상태 표시**라 허용 범위다.
[DefaultExecutionOrder(-50)]
public class 시험모드 : MonoBehaviour
{
    [Tooltip("켰을 때 걷기·달리기가 몇 배가 되나")] public float 속도배 = 10f;

    public static bool 켜짐 { get; private set; }

    Hero hero;
    float 옛걷기, 옛달리기, 옛살금;
    bool 적어둠;

    void Update()
    {
        if (눌렸나()) 뒤집기();

        if (!켜짐) return;
        if (hero == null) hero = Hero.Me;
        if (hero == null) return;
        hero.stamina = hero.maxStamina;      // 숨이 안 닳는다 — 매 틱 채워 둔다
    }

    static bool 눌렸나()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.F2);
#endif
    }

    void 뒤집기()
    {
        if (hero == null) hero = Hero.Me;
        if (hero == null) return;

        켜짐 = !켜짐;
        if (켜짐)
        {
            if (!적어둠) { 옛걷기 = hero.walk; 옛달리기 = hero.run; 옛살금 = hero.sneak; 적어둠 = true; }
            hero.walk = 옛걷기 * 속도배;
            hero.run = 옛달리기 * 속도배;
            hero.sneak = 옛살금 * 속도배;
            hero.stamina = hero.maxStamina;
        }
        else if (적어둠)
        {
            hero.walk = 옛걷기; hero.run = 옛달리기; hero.sneak = 옛살금;
        }
        Debug.Log($"[시험모드] {(켜짐 ? "켬 — 이속 " + 속도배 + "배 · 지구력 안 닳음" : "끔")}");
    }

    void OnGUI()
    {
        if (!켜짐) return;
        var st = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
        st.normal.textColor = new Color(1f, 0.85f, 0.3f);
        GUI.Label(new Rect(12f, Screen.height - 26f, 300f, 22f), $"시험모드 F2 · 이속 {속도배:0}배", st);
    }

    void OnDisable() { if (켜짐 && 적어둠 && hero != null) { hero.walk = 옛걷기; hero.run = 옛달리기; hero.sneak = 옛살금; } 켜짐 = false; }
}
