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

    /// 들 수 있는 무게 (kg). 0 이면 무제한 (땅 무더기가 그렇다)
    public float 한도 = 14f;

    public float 무게
    {
        get { float w = 0f; for (int i = 0; i < 것들.Count; i++) w += 것들[i].무게; return w; }
    }
    public float 남은무게 => 한도 <= 0f ? 9999f : Mathf.Max(0f, 한도 - 무게);
    public bool 꽉참 => 한도 > 0f && 무게 >= 한도 - 0.001f;

    // ══════════════════════════════════════════════════════════

    /// 넣는다. 무게가 넘치면 **넣은 개수만** 돌려준다 (재료는 일부만 들어갈 수 있다)
    public int 넣기(아이템종 종, int 개수 = 1, float 신선 = 1f, float 내구 = -1f)
    {
        if (종 == null || 개수 <= 0) return 0;

        int 넣음 = 0;
        for (int i = 0; i < 개수; i++)
        {
            if (한도 > 0f && 무게 + 종.무게 > 한도) break;      // 더는 못 든다

            if (종.갈래 == 갈래.재료)
            {
                var 같은것 = 것들.Find(a => a.종 == 종);
                if (같은것 != null) 같은것.개수++;
                else 것들.Add(new 아이템(종));
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

    /// 덩이 하나를 통째로 받는다. 무게가 넘치면 false
    public bool 받기(아이템 it)
    {
        if (it == null) return false;
        if (한도 > 0f && 무게 + it.무게 > 한도) return false;
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
            if (다음 != null) { it.종 = 다음; it.신선 = 1f; }   // 상한고기가 된다
        }
    }

    /// ★가진 것 중 그 일에 **제일 좋은 도구** — 고르는 조작이 없다 (4장)
    public 아이템 제일좋은도구(string 쓰임)
    {
        아이템 best = null;
        for (int i = 0; i < 것들.Count; i++)
        {
            var it = 것들[i];
            if (it.종 == null || it.종.쓰임 != 쓰임) continue;
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

    /// 사람이 지고 다니는 것
    public static readonly 인벤 내것 = new 인벤 { 한도 = 14f };

    /// 판이 바뀌면 비운다 (`플레이초기화` 가 부른다 — 도메인 리로드를 껐기 때문)
    public static void 비우기() { 내것.것들.Clear(); }
}
