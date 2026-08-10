using System.Collections.Generic;
using UnityEngine;

/// 제작법 — **표로 돈다. 만들 것을 코드에 박지 않는다.**
///
/// ★★★도구가 **여러 곳에 걸려야** 만들 이유가 생긴다 (2026-08-10 사용자 —
///   *"연결이 잘 되는 도구였음"*). 하나에 하나씩 대응시키면 도구가 그냥 열쇠가 된다.
///
///   ☆실제로 엮인 사슬:
///     맨손 → 나무·돌 (아주 느리다)
///       → **돌도끼**  : 나무 3배 · **구조물을 지을 수 있게 됨** · 곡괭이를 만들 수 있게 됨
///       → **돌곡괭이**: 돌 3배 · **파기**(굴·매장 난생)가 열린다
///       → **뼈칼**    : 갈무리에서 **가죽·뼈가 나온다** · 밧줄 · 창
///          → 가죽 → **가방**(무게가 는다) · **물통**(더 멀리 간다)
///     ★뼈칼 하나가 「가죽 계열 전부」를 연다 — 그래서 뼈칼을 만들 이유가 생긴다
///
/// ★좀보이드처럼 **도구는 가방에 있기만 하면 된다** — 쓸 때 저절로 든다 (4장: 교체 조작 없음)
public class 제작법
{
    public string 이름;
    /// 무엇이 드나 — (아이템 이름, 개수)
    public (string 재료, int 개수)[] 든것;
    /// 이 도구가 있어야 만들 수 있다 (비우면 맨손으로도 된다). `아이템종.도구쓰임` 과 맞춘다
    public string 필요도구;
    /// 만들면 인벤에 이 아이템이 생긴다 (비우면 아래 `세울것`)
    public string 나올것;
    /// 인벤이 아니라 **땅에 세우는 것** (모닥불 터 등)
    public string 세울것;
    /// 창에 띄울 한 줄 — 무엇이 열리는지
    public string 곁들임;
}

public static class 제작법표
{
    public static readonly List<제작법> 전부 = new List<제작법>();

    static void 하나(string 이름, string 필요도구, string 나올것, string 세울것,
                     string 곁들임, params (string, int)[] 든것)
        => 전부.Add(new 제작법 {
            이름 = 이름, 필요도구 = 필요도구, 나올것 = 나올것, 세울것 = 세울것,
            곁들임 = 곁들임, 든것 = 든것
        });

    static 제작법표()
    {
        // ── 맨손으로 되는 것 — 첫 도구는 도구 없이 만들어야 사슬이 시작된다
        하나("돌도끼", null, "돌도끼", null, "나무를 빨리 캔다 · 지을 수 있게 된다",
             ("나무", 2), ("돌", 3));
        하나("뼈칼", null, "뼈칼", null, "갈무리에서 가죽이 나온다",
             ("뼈", 2), ("나무", 1));
        하나("모닥불", null, null, "모닥불", "요리 · 야생을 쫓는다 · 쉬면 피로가 풀린다",
             ("돌", 5), ("나무", 3));

        // ── 돌도끼가 여는 것
        하나("돌곡괭이", "나무", "돌곡괭이", null, "돌을 빨리 캔다 · 팔 수 있게 된다",
             ("나무", 3), ("돌", 4));

        // ── 뼈칼이 여는 것 — ★가죽 계열 전부가 여기 매달려 있다
        하나("밧줄", "가죽", "밧줄", null, "끌고 오기 · 가방을 만든다",
             ("풀", 6));
        하나("창", "가죽", "창", null, "사거리 3m · 세다. 다만 기절은 못 시킨다",
             ("나무", 3), ("뼈", 1));
        하나("가죽물통", "가죽", "가죽물통", null, "물을 담아 더 멀리 간다",
             ("가죽", 3), ("밧줄", 1));
        하나("가죽자루", "가죽", "가죽자루", null, "+10kg",
             ("가죽", 4), ("밧줄", 1));
        하나("가죽배낭", "가죽", "가죽배낭", null, "+20kg",
             ("가죽", 8), ("밧줄", 2));
    }

    /// 이 제작법에 쓸 도구가 지금 있나 (없으면 null, 도구가 필요 없으면 맨손 취급)
    public static 아이템 쓸도구(제작법 r)
    {
        if (r == null || string.IsNullOrEmpty(r.필요도구)) return null;
        return 인벤.어느통에든도구(r.필요도구);
    }

    public static bool 도구있나(제작법 r)
        => r == null || string.IsNullOrEmpty(r.필요도구) || 쓸도구(r) != null;

    /// 재료가 다 있나
    public static bool 재료있나(제작법 r)
    {
        if (r == null) return false;
        foreach (var (재료, 개수) in r.든것)
            if (인벤.다합쳐개수(재료) < 개수) return false;
        return true;
    }

    /// 부족한 것을 사람이 읽을 수 있게 — `나무 1/2  돌 0/3`
    public static string 재료글(제작법 r)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var (재료, 개수) in r.든것)
        {
            if (sb.Length > 0) sb.Append("   ");
            sb.Append($"{재료} {인벤.다합쳐개수(재료)}/{개수}");
        }
        if (!string.IsNullOrEmpty(r.필요도구))
            sb.Append(도구있나(r) ? "   ✓도구" : "   ✗도구");
        return sb.ToString();
    }

    /// 만든다 — 재료를 빼고 결과를 준다. 도구는 조금 닳는다
    public static bool 만들기(제작법 r)
    {
        if (r == null || !재료있나(r) || !도구있나(r)) return false;
        foreach (var (재료, 개수) in r.든것) 인벤.다합쳐꺼내기(재료, 개수);

        var 도구 = 쓸도구(r);
        if (도구 != null) 인벤.어느통에서든닳음(도구, 2f);

        if (!string.IsNullOrEmpty(r.나올것))
        {
            var d = 아이템표.찾기(r.나올것);
            if (d != null) 인벤.어디든넣기(d, 1);
        }
        return true;
    }
}
