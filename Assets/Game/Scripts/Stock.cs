using System.Collections.Generic;

/// 가진 것 — 지금은 숫자만. 인벤토리 창은 나중에.
///
/// ★고기는 **나와 펫이 같이 먹는다** — "내가 먹나, 펫을 주나" 가 이 게임의 매일의 판단이다.
public static class Stock
{
    // ★구운고기 — 모닥불이 만든다. 생고기보다 몸에 더 되고, 펫도 더 좋아한다.
    //   "오늘 잡아온 고기를 내가 먹나, 펫을 주나" 가 여기서 한 겹 더 갈린다.
    public enum Kind { 고기, 나무, 돌, 구운고기 }

    static readonly Dictionary<Kind, int> have = new Dictionary<Kind, int>();

    public static int Get(Kind k) => have.TryGetValue(k, out var n) ? n : 0;

    public static void Add(Kind k, int n)
    {
        have[k] = Get(k) + n;
        Recent = k; RecentAt = UnityEngine.Time.time;
    }

    public static bool Take(Kind k, int n)
    {
        if (Get(k) < n) return false;
        have[k] = Get(k) - n;
        return true;
    }

    public static void Clear() { have.Clear(); }

    /// 방금 얻은 것 — 화면에 잠깐 띄우는 데 쓴다
    public static Kind Recent;
    public static float RecentAt = -99f;
}
