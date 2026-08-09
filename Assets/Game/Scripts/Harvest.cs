using System.Collections.Generic;
using UnityEngine;

/// 캘 수 있는 것 — 나무·바위에 붙는다.
///
/// ★알비온식: 도구를 골라 드는 조작이 없다. 앞에 있는 것을 치면 맞는 자원이 나온다.
/// ★다 캐면 사라지고, 자리는 남는다 (다시 자라는 건 나중에).
public class Harvest : MonoBehaviour
{
    public Stock.Kind kind = Stock.Kind.나무;
    [Tooltip("몇 번 쳐야 다 캐나")] public int hits = 4;
    [Tooltip("한 번에 나오는 양")] public int perHit = 2;

    public static readonly List<Harvest> All = new List<Harvest>();

    Vector3 baseScale;
    float shake;

    void OnEnable() { All.Add(this); baseScale = transform.localScale; }
    void OnDisable() { All.Remove(this); }

    // ★★★**`Update` 를 없앴다** (2026-08-06 실측 — 이게 렉의 정체였다).
    //
    //   숲에 나무가 **21,946그루**고 그루마다 `Harvest` 가 하나 붙는다. 이 `Update` 는
    //   `if (shake <= 0f) return;` 로 **평소엔 아무 일도 안 했는데도**, 유니티가 매 프레임
    //   2만 2천 번 부르는 값만으로 `BehaviourUpdate` 가 **38.9ms** 였다 (전체 47.4ms 의 82%).
    //   ☆비싼 건 몸통이 아니라 **부르는 것 자체**다. 아무것도 안 하는 `Update` 도 2만 개면 렉이다.
    //
    //   → 흔들림은 **팰 때만** 도는 코루틴으로 옮겼다. 컴포넌트를 끄는 수는 못 쓴다 —
    //     `OnEnable` 에서 `All` 에 등록하므로 끄면 캐기 자체가 안 된다.
    /// ★★★**스케일 눌림은 안 쓴다** (2026-08-09 사용자 "리깅을 쓰니까 스쿼시는 다 없애").
    ///   전에는 맞을 때마다 세로 1.12배·가로 0.89배로 늘렸다 줄였다 했는데,
    ///   `Harvest` 는 나무·바위만이 아니라 **사체에도 붙는다** — 리깅된 몸이 늘어나면 흉하다.
    ///   → 크기 대신 **기울여서** 흔든다. 리깅이든 아니든 형태가 안 망가진다.
    System.Collections.IEnumerator 흔들기()
    {
        var 기본회전 = transform.localRotation;
        while (shake > 0f)
        {
            shake = Mathf.Max(0f, shake - Time.deltaTime * 5f);
            float a = Mathf.Sin(Time.time * 34f) * shake * 3.5f;      // 3.5° 안쪽으로 파르르
            transform.localRotation = 기본회전 * Quaternion.Euler(a, 0f, a * 0.6f);
            yield return null;
        }
        transform.localRotation = 기본회전;
    }

    [Tooltip("다 캐면 이 자리의 장애물도 지운다 (안 지우면 보이지 않는 벽이 남는다)")]
    public bool 장애물치우기 = true;
    [HideInInspector] public Vector3 blockAt;

    [Tooltip("★다 팼을 때 사라지는 대신 이게 쓰러진다 (선 나무). 없으면 그냥 사라진다")]
    public TreeFall 쓰러짐;

    void Chop(Vector3 방향)
    {
        // perHit 이 0 이면 아무것도 안 나온다 — 선 나무를 패는 단계가 그렇다.
        // 나무는 **통나무가 된 뒤에** 나온다 (인과와 행위)
        if (perHit > 0) Stock.Add(kind, perHit);
        bool 흔들던중 = shake > 0f;
        shake = 1f;
        if (!흔들던중) StartCoroutine(흔들기());     // 이미 돌고 있으면 새로 안 띄운다
        if (--hits > 0) return;

        if (쓰러짐 != null) { 쓰러짐.시작(방향); Destroy(this); return; }
        if (장애물치우기) Blocker.Remove(blockAt);
        Destroy(gameObject);
    }

    /// 앞쪽 가장 가까운 자원을 한 번 캔다
    public static bool TryHarvest(Vector3 from, Vector3 look, float reach)
    {
        Harvest best = null; float bd = reach * reach;
        for (int i = All.Count - 1; i >= 0; i--)
        {
            var h = All[i];
            if (h == null) continue;
            var v = h.transform.position - from; v.y = 0f;
            float d2 = v.sqrMagnitude;
            if (d2 > bd) continue;
            if (d2 > 0.01f && Vector3.Dot(v.normalized, look) < 0.2f) continue;   // 앞쪽만
            bd = d2; best = h;
        }
        if (best == null) return false;
        best.Chop(look);
        return true;
    }
}
