using System.Collections.Generic;
using UnityEngine;

/// 가진 것 한 뭉치 — 사람의 배낭이기도 하고, 땅에 쌓인 무더기이기도 하다.
///
/// ★**칸을 세지 않는다. 무게만 본다** (기획 5-7 · 좀보이드 방식).
/// ★**한도가 있다** (사용자 확정). 넘으면 더 못 든다 — 넘칠 때는 땅에 떨어진다.
/// ★먹을 것은 **덩이마다 따로** 산다. 그래야 어제 잡은 고기와 방금 잡은 게 안 섞인다.
public class 인벤
{
    public readonly List<아이템> 것들 = new List<아이템>();

    /// 편하게 지고 다닐 수 있는 무게 (kg). 0 이면 무게를 안 따진다 (땅 무더기가 그렇다)
    public float 한도 = 14f;

    public float 무게
    {
        get { float w = 0f; for (int i = 0; i < 것들.Count; i++) w += 것들[i].무게; return w; }
    }
    public float 무게비 => 한도 <= 0f ? 0f : 무게 / 한도;
    public bool 넘침 => 한도 > 0f && 무게 > 한도;

    // ★넘치면 못 걷는다 — 통 전부를 합쳐서 따진다 (아래 `짐배` 참고)

    // ══════════════════════════════════════════════════════════

    /// 넣는다. ★**한도 때문에 거절하지 않는다** — 넘치면 못 걸을 뿐이다 (`짐배` 주석 참고)
    public int 넣기(아이템종 종, int 개수 = 1, float 신선 = 1f, float 내구 = -1f)
    {
        if (종 == null || 개수 <= 0) return 0;

        int 넣음 = 0;
        for (int i = 0; i < 개수; i++)
        {
            // ★★**뭉치는지는 `아이템.뭉치나` 하나가 정한다** (2026-08-12).
            //   여기 「갈래 == 재료」가 따로 박혀 있어서, 먹을 것을 뭉치게 바꿔도
            //   **넣을 때는 여전히 낱개로 쌓였다** — 규칙이 두 군데 있으면 반드시 갈라진다.
            //   ☆실측으로 잡았다: 생고기 5개를 넣었더니 다섯 칸이 됐다.
            if (종.갈래 == 갈래.재료 || 종.갈래 == 갈래.먹을것)
            {
                // 신선도가 다른 것은 안 섞는다 — 섞으면 「곧 상함」이 신선한 것에 묻는다
                var 같은것 = 것들.Find(a => a.종 == 종 && Mathf.Abs(a.신선 - 신선) < 0.02f);
                if (같은것 != null) 같은것.개수++;
                else { var b = new 아이템(종); b.신선 = 신선; 것들.Add(b); }
            }
            else
            {
                var a = new 아이템(종);
                a.신선 = 신선;
                if (내구 >= 0f) a.내구 = 내구;
                것들.Add(a);
            }
            넣음++;
        }
        return 넣음;
    }

    /// 이름으로 몇 개 있나
    public int 개수(string 이름)
    {
        int n = 0;
        for (int i = 0; i < 것들.Count; i++)
            if (것들[i].종 != null && 것들[i].종.이름 == 이름) n += 것들[i].뭉치나 ? 것들[i].개수 : 1;
        return n;
    }

    /// ★꺼낸다 — **오래된 것부터** (먹을 것은 상하기 직전 것을 먼저 쓰게 된다)
    public bool 꺼내기(string 이름, int 개수 = 1)
    {
        if (this.개수(이름) < 개수) return false;
        int 남음 = 개수;

        // 신선도가 낮은 것부터
        것들.Sort((a, b) => a.신선.CompareTo(b.신선));
        for (int i = 것들.Count - 1; i >= 0 && 남음 > 0; i--)
        {
            var it = 것들[i];
            if (it.종 == null || it.종.이름 != 이름) continue;
            if (it.뭉치나)
            {
                int 뺄것 = Mathf.Min(남음, it.개수);
                it.개수 -= 뺄것; 남음 -= 뺄것;
                if (it.개수 <= 0) 것들.RemoveAt(i);
            }
            else { 것들.RemoveAt(i); 남음--; }
        }
        return 남음 <= 0;
    }

    /// 덩이 하나를 통째로 뺀다 (창에서 옮길 때)
    public bool 빼기(아이템 it) => 것들.Remove(it);

    /// 덩이 하나를 통째로 받는다. ★한도로 막지 않는다 — 넘치면 못 걷는다
    public bool 받기(아이템 it)
    {
        if (it == null) return false;
        if (it.뭉치나)
        {
            var 같은것 = 것들.Find(a => a.종 == it.종);
            if (같은것 != null) { 같은것.개수 += it.개수; return true; }
        }
        것들.Add(it);
        return true;
    }

    // ══════════════════════════════════════════════════════════

    /// ★부패 — 시간이 지나면 상한다. **버리지 않는다. 상한 것이 될 뿐이다**
    ///   (상한 것도 펫은 먹는다 — 기획 5-7)
    public void 시간(float dt)
    {
        for (int i = 것들.Count - 1; i >= 0; i--)
        {
            var it = 것들[i];
            if (it.종 == null || it.종.상하는데 <= 0f || it.신선 <= 0f) continue;

            it.신선 -= dt / it.종.상하는데;
            if (it.신선 > 0f) continue;

            it.신선 = 0f;
            var 다음 = 아이템표.찾기(it.종.상하면);
            if (다음 == null) continue;

            // ★★★**뭉친 것은 한 덩이씩 상한다** (2026-08-12 사용자 "뭉치고, 한개씩 상하게").
            //   열 덩이가 한 칸에 있다고 열 덩이가 **동시에** 상하면, 신선한 것과 상한 것을
            //   갈라 쓸 수가 없다 (그게 원래 안 뭉치던 이유였다).
            //   → 시간이 되면 **한 덩이만** 떼어 상한고기로 보내고, 남은 것은 다시 신선하게
            //     시간을 센다. 그러면 더미가 조금씩 줄고 상한 더미가 조금씩 는다.
            if (it.뭉치나 && it.개수 > 1)
            {
                it.개수--;                       // 한 덩이를 뗀다
                it.신선 = 1f;                    // 남은 것은 다시 센다
                받기(new 아이템(다음, 1));        // 뗀 것이 상한고기가 되어 쌓인다
                continue;
            }

            it.종 = 다음; it.신선 = 1f;          // 마지막 한 덩이는 제자리에서 바뀐다
        }
    }

    /// ★가진 것 중 그 일에 **제일 좋은 도구** — 고르는 조작이 없다 (4장)
    public 아이템 제일좋은도구(string 쓰임)
    {
        아이템 best = null;
        for (int i = 0; i < 것들.Count; i++)
        {
            var it = 것들[i];
            if (it.종 == null || it.종.도구쓰임 != 쓰임) continue;
            if (it.종.내구 > 0f && it.내구 <= 0f) continue;      // 부러진 것은 안 쓴다
            if (best == null || it.종.성능 > best.종.성능) best = it;
        }
        return best;
    }

    /// 도구를 썼다 — 닳는다. 다 닳으면 부러져 사라진다
    public void 도구닳음(아이템 도구, float 만큼 = 1f)
    {
        if (도구 == null || 도구.종.내구 <= 0f) return;
        도구.내구 -= 만큼;
        if (도구.내구 <= 0f) 것들.Remove(도구);
    }

    // ══════════════════════════════════════════════════════════

    /// 이 통이 어느 가방에서 나왔나 (주머니는 null)
    public 아이템 가방아이템;
    public string 이름 = "주머니";

    // ══════════════════════════════════════════════════════════
    //  ★★짐 — **통이 여럿이다** (2026-08-10 사용자 —
    //  *"좀보이드는 어떤 가방을 가지고 있냐에 따라 가방마다 인벤토리가 생성되지 않음?"*)
    //
    //  맞다. 좀보이드는 왼쪽 패널 위에 **통 버튼 줄**이 있고, 착용한 가방마다
    //  제 무게 한도를 가진 칸이 따로 생긴다. 그전까지 여기는 **통이 하나뿐**이었다.
    // ══════════════════════════════════════════════════════════

    /// 맨몸 주머니 — 가방이 없어도 조금은 들고 다닌다
    public static readonly 인벤 주머니 = new 인벤 { 한도 = 6f, 이름 = "주머니" };

    /// 지금 들고 다니는 통 전부 (주머니 + 착용한 가방들)
    public static readonly List<인벤> 통들 = new List<인벤> { 주머니 };

    /// ★손에 쥔 것 — `HeroAttack` 이 여기서 무기 수치를 읽는다
    public static 아이템 쥔것;

    /// 옛 이름 — 부르는 데가 많아 남겨 둔다. **주머니를 가리킨다** (9장 3조)
    public static 인벤 내것 => 주머니;

    /// 통 전부를 합친 무게·한도
    public static float 총무게 { get { float w = 0f; for (int i = 0; i < 통들.Count; i++) w += 통들[i].무게; return w; } }
    public static float 총한도 { get { float w = 0f; for (int i = 0; i < 통들.Count; i++) w += 통들[i].한도; return w; } }
    public static bool 총넘침 => 총무게 > 총한도;
    public static float 총무게비 => 총한도 <= 0f ? 0f : 총무게 / 총한도;

    /// ★짐이 무거우면 느려지고, 넘으면 발이 안 나간다 (사용자 확정)
    public static float 짐배
    {
        get
        {
            float r = 총무게비;
            if (r > 1f) return 0f;
            return Mathf.Lerp(1f, 0.55f, Mathf.InverseLerp(0.6f, 1f, r));
        }
    }

    /// 어느 통에든 넣는다 — 빈자리가 있는 통부터
    public static int 어디든넣기(아이템종 종, int 개수 = 1)
    {
        for (int i = 0; i < 통들.Count; i++)
        {
            int n = 통들[i].넣기(종, 개수);
            if (n > 0) return n;
        }
        return 주머니.넣기(종, 개수);
    }

    /// 통 전부에서 개수를 센다
    public static int 다합쳐개수(string 이름)
    {
        int n = 0;
        for (int i = 0; i < 통들.Count; i++) n += 통들[i].개수(이름);
        return n;
    }

    /// 통 전부에서 꺼낸다
    public static bool 다합쳐꺼내기(string 이름, int 개수 = 1)
    {
        if (다합쳐개수(이름) < 개수) return false;
        int 남음 = 개수;
        for (int i = 0; i < 통들.Count && 남음 > 0; i++)
        {
            int 있음 = 통들[i].개수(이름);
            if (있음 <= 0) continue;
            int 뺄것 = Mathf.Min(남음, 있음);
            if (통들[i].꺼내기(이름, 뺄것)) 남음 -= 뺄것;
        }
        return 남음 <= 0;
    }

    /// ★가방을 멘다 — **통이 하나 늘어난다**
    public static void 가방메기(아이템 it, 인벤 있던통)
    {
        if (it == null || it.종 == null || it.종.가방 <= 0f) return;
        if (있던통 != null) 있던통.빼기(it);
        통들.Add(new 인벤 { 한도 = it.종.가방, 이름 = it.종.이름, 가방아이템 = it });
    }

    /// 가방을 벗는다 — 속에 든 것은 주머니로, 안 들어가면 그 자리에 남는다
    public static void 가방벗기(인벤 통)
    {
        if (통 == null || 통.가방아이템 == null) return;
        통들.Remove(통);
        주머니.받기(통.가방아이템);
        for (int i = 통.것들.Count - 1; i >= 0; i--) 주머니.받기(통.것들[i]);
        통.것들.Clear();
    }

    /// ★통 전부에서 그 일에 제일 좋은 도구 — **가방에 있기만 하면 된다** (좀보이드처럼).
    ///   고르는 조작이 없다 (4장)
    public static 아이템 어느통에든도구(string 도구쓰임)
    {
        아이템 best = null;
        for (int i = 0; i < 통들.Count; i++)
        {
            var it = 통들[i].제일좋은도구(도구쓰임);
            if (it != null && (best == null || it.종.성능 > best.종.성능)) best = it;
        }
        return best;
    }

    /// 어느 통에 있든 그 도구를 닳린다 — 다 닳으면 부러져 사라진다
    public static void 어느통에서든닳음(아이템 도구, float 만큼 = 1f)
    {
        if (도구 == null || 도구.종.내구 <= 0f) return;
        도구.내구 -= 만큼;
        if (도구.내구 > 0f) return;
        for (int i = 0; i < 통들.Count; i++) 통들[i].것들.Remove(도구);
        if (쥔것 == 도구) 쥔것 = null;
    }

    /// 쥔 것이 닳는다 — 다 닳으면 부러져 사라진다
    public static void 도구닳음쥔것(float 만큼 = 1f)
    {
        var it = 쥔것;
        if (it == null || it.종.내구 <= 0f) return;
        it.내구 -= 만큼;
        if (it.내구 > 0f) return;
        for (int i = 0; i < 통들.Count; i++) 통들[i].것들.Remove(it);
        쥔것 = null;
    }

    /// 판이 바뀌면 비운다 (`플레이초기화` 가 부른다 — 도메인 리로드를 껐기 때문)
    public static void 비우기()
    {
        주머니.것들.Clear();
        통들.Clear(); 통들.Add(주머니);
        쥔것 = null;
    }
}
