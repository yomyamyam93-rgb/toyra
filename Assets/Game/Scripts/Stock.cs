using UnityEngine;

/// 가진 것 — **은퇴한 창구다.** 속은 `인벤` 이 맡는다 (기획 5-7, 2026-08-10).
///
/// ★옛 방식이 왜 나빴나: 가진 것이 **`Dictionary<Kind,int>` — 숫자 네 개**였다.
///   무게도 신선도도 닳음도 표현할 수 없었고, 종류가 `enum` 에 박혀 있어서
///   하나 늘릴 때마다 화면 코드까지 고쳐야 했다.
///
/// ★그런데 `Harvest`·`Carcass`·`TreeFall`·`모닥불`·`제작창`·`HeroCarry` 가 전부 이걸 부른다.
///   한 번에 갈아치우면 여섯 군데가 동시에 흔들린다 → **다리를 놓고 속만 바꿨다.**
///   ☆은퇴는 삭제가 아니라 스위치 (9장 3조). 부르는 쪽을 하나씩 `인벤` 으로 옮기면 된다.
public static class Stock
{
    /// 옛 이름 → 새 아이템 이름
    public enum Kind { 고기, 나무, 돌, 구운고기 }

    static string 이름(Kind k)
    {
        switch (k)
        {
            case Kind.고기: return "생고기";
            case Kind.나무: return "나무";
            case Kind.돌: return "돌";
            default: return "구운고기";
        }
    }

    /// 이 갈래에 해당하는 아이템종 — **인벤 말고 다른 데 담을 때** 쓴다 (사체에 쌓기)
    public static 아이템종 종(Kind k) => 아이템표.찾기(이름(k));

    public static int Get(Kind k) => 인벤.다합쳐개수(이름(k));

    /// ★넣는다. **한도를 넘어도 다 들어간다 — 대신 못 걷는다** (`인벤.짐배`).
    ///   무엇을 버릴지는 게임이 아니라 **내가 고른다**
    public static void Add(Kind k, int n)
    {
        var 종 = 아이템표.찾기(이름(k));
        if (종 == null || n <= 0) return;
        인벤.어디든넣기(종, n);
        Recent = k; RecentAt = Time.time;
    }

    public static bool Take(Kind k, int n) => 인벤.다합쳐꺼내기(이름(k), n);

    public static void Clear() { 인벤.비우기(); }

    static Vector3 사람자리()
    {
        var h = Hero.Me;
        return h != null ? h.transform.position + h.transform.forward * 0.8f : Vector3.zero;
    }

    /// 방금 얻은 것 — 화면에 잠깐 띄우는 데 쓴다
    public static Kind Recent;
    public static float RecentAt = -99f;
}
