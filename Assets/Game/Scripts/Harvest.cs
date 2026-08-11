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

    // ★★★**자리를 기억해 둔다** (2026-08-11 실측 — 이동 중 렉의 정체였다).
    //   `대상표시.찾기()` 가 0.08초마다 이 목록을 **전부** 훑는데, 세계에는 나무·바위가
    //   2만 6천 개 있다. 그때마다 `transform.position` 을 읽는 게 문제였다 —
    //   그건 그냥 값 읽기가 아니라 **엔진 안쪽으로 들어갔다 나오는 호출**이라 하나에
    //   0.6마이크로초쯤 든다. 3만 6천 번이면 22ms 다 (실측 `[대상표시-느림] 22.5ms`).
    //   → 캘 것은 **움직이지 않는다.** 등록할 때 한 번 적어 두고 그 값을 읽는다.
    //   ☆움직일 일이 생기면(나무가 쓰러진다) `자리갱신()` 을 부른다.
    [HideInInspector] public Vector3 자리;
    public void 자리갱신() { 자리 = transform.position; }

    void OnEnable() { All.Add(this); baseScale = transform.localScale; 자리 = transform.position; }
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

    float 진행틱;

    /// 이 자원에 맞는 도구의 쓰임새 — 나무는 도끼, 돌은 곡괭이
    string 맞는쓰임 => kind == Stock.Kind.나무 ? "나무"
                    : kind == Stock.Kind.돌 ? "돌" : null;

    void Chop(Vector3 방향)
    {
        // ★도구가 속도를 정한다 (헌법 5-4 "맨손도 되긴 하되 아주 느리다").
        //   기존 hits 숫자는 「맞는 도구 기준」 그대로 — 맨손은 3타에 한 번만 진전된다.
        float 일 = 3f;                                  // 도구 개념이 없는 것(사체 등)은 지금 그대로
        if (맞는쓰임 != null)
        {
            var 도구 = 인벤.어느통에든도구(맞는쓰임);
            if (도구 != null) { 일 = 도구.종.성능; 인벤.어느통에서든닳음(도구); }
            else 일 = 1f;
        }

        bool 흔들던중 = shake > 0f;
        shake = 1f;
        if (!흔들던중) StartCoroutine(흔들기());     // 이미 돌고 있으면 새로 안 띄운다

        진행틱 += 일;
        while (진행틱 >= 3f)
        {
            진행틱 -= 3f;
            if (!한타(방향)) return;                     // 파괴가 걸렸으면 즉시 끝
        }
    }

    /// 실제 한 히트 — 지급·소모·쓰러짐. **계속 패도 되면 true**
    bool 한타(Vector3 방향)
    {
        // perHit 이 0 이면 아무것도 안 나온다 — 선 나무를 패는 단계가 그렇다.
        // 나무는 **통나무가 된 뒤에** 나온다 (인과와 행위)
        // ★돌은 인벤에 바로 꽂히지 않는다 — **돌맹이가 튀어 떨어지고, 줍는 것이 수확이다**
        //   (9-0 인과: 결과는 행위에서. 2026-08-11 사용자 "주변에 툭툭 떨어지게")
        if (kind == Stock.Kind.돌 && perHit > 0) { for (int i = 0; i < perHit; i++) 돌맹이튀기(); }
        else if (perHit > 0) Stock.Add(kind, perHit);

        if (--hits > 0) return true;

        if (쓰러짐 != null) { 쓰러짐.시작(방향); Destroy(this); return false; }
        if (장애물치우기) Blocker.Remove(blockAt);
        Destroy(gameObject);
        return false;
    }

    /// 돌맹이 하나가 포물선으로 툭 떨어져 줍이가 된다
    void 돌맹이튀기()
    {
        float s = Random.Range(0.22f, 0.34f);
        var 돌알 = Grey.Box(transform.parent, transform.position + Vector3.up * 0.6f,
                 new Vector3(s, s * 0.7f, s * Random.Range(0.8f, 1.2f)),
                 new Color(0.45f, 0.45f, 0.43f), "돌맹이", 0f, Random.value * 360f);
        var 무 = 땅무더기.줍이(아이템표.찾기("돌"), 1, 돌알);

        var 끝 = transform.position + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized
                 * Random.Range(0.8f, 1.6f);
        if (무 != null) 무.StartCoroutine(툭(돌알.transform, 끝 + Vector3.up * (s * 0.35f)));
    }

    /// 짧은 포물선 — 이펙트가 아니라 실제로 그 자리로 간다 (그림 = 판정)
    static System.Collections.IEnumerator 툭(Transform t, Vector3 끝)
    {
        var 시작 = t.position;
        for (float u = 0f; u < 1f; u += Time.deltaTime / 0.38f)
        {
            if (t == null) yield break;
            var p = Vector3.Lerp(시작, 끝, u);
            p.y += Mathf.Sin(u * Mathf.PI) * 0.55f;    // 포물선 아치
            t.position = p;
            yield return null;
        }
        if (t != null) t.position = 끝;
    }

    /// 앞쪽 가장 가까운 자원을 한 번 캔다
    // ★★★**몸집을 봐 준다** (2026-08-11 사용자 "팻 죽이고나서 파밍하려하는데 파밍이
    //   잘안돼, 범위가 좀 이상해"). 갈무리는 **중심 한 점**까지의 거리만 봤다.
    //   그런데 사슴처럼 누운 사체는 몸이 1~2m 뻗어 있어서, **몸 옆에 딱 붙어 서 있어도
    //   중심은 범위 밖**이었다. 때리는 쪽은 뼈 점 전부(`실루엣판정`)로 재니
    //   "때릴 땐 맞는데 갈무리는 안 된다" 가 났다 — 두 자가 달랐던 것이다.
    //   → 그 물체의 반경만큼 거리에서 빼 준다. `Carcass` 가 몸을 재서 넣어 준다.
    [Tooltip("이 물체의 반경 (m) — 갈무리 거리에서 빼 준다. 사체는 몸을 재서 넣는다")]
    public float 반경 = 0f;

    // ★★**사체는 방향을 안 본다** (2026-08-11 사용자 "갈무리 범위가 내가 몸을 돌릴때마다
    //   됐다 안됐다하는데 바라보는 방향이나 공격 범위보단 그냥 주변에 있으면 가능하게").
    //   나무·돌은 **앞으로 도끼를 휘두르는** 것이라 방향이 맞다. 그런데 갈무리는 쪼그려
    //   앉아 뒤적이는 일이다 — 옆에 있으면 되지 어느 쪽을 보는지는 상관없다.
    [Tooltip("켜면 바라보는 방향을 안 본다 — 옆에 있기만 하면 된다 (사체가 그렇다)")]
    public bool 방향무관 = false;

    public static bool TryHarvest(Vector3 from, Vector3 look, float reach)
    {
        Harvest best = null; float bd = float.MaxValue;
        for (int i = All.Count - 1; i >= 0; i--)
        {
            var h = All[i];
            if (h == null) continue;
            var v = h.transform.position - from; v.y = 0f;
            float d = Mathf.Max(0f, v.magnitude - h.반경);   // ★몸집만큼 봐 준다
            if (d > reach || d > bd) continue;
            if (!h.방향무관 && v.sqrMagnitude > 0.01f
                && Vector3.Dot(v.normalized, look) < 0.2f) continue;   // 앞쪽만 (사체는 안 본다)
            bd = d; best = h;
        }
        if (best == null) return false;
        best.Chop(look);
        return true;
    }
}
