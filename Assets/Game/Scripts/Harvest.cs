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
    // ★★★**휘는 게 아니라 「진동」이다** (2026-08-11 사용자 "나무 스쿼시? 적용된거같은데..
    //   제발 다 빼줘, 그냥 진동 효과를 넣어줘").
    //   옛 코드는 **회전**을 3.5° 흔들었다. 나무가 9~15m 라 밑동에서 3.5° 면 **꼭대기가
    //   0.6m 넘게 휜다** — 그게 스쿼시(짜부)로 읽혔다.
    //   ☆이 프로젝트는 짜부를 두 번 걷어냈다 (8/3 "짜부되는 건 좀 아닌 거 같고",
    //     8/9 "팻 쳐맞는 모션 만들고 왜 이상한걸 쓰냐"). 같은 잘못을 또 한 셈이다.
    //   → **자리를 파르르 떤다.** 크기도 회전도 안 건드린다 — 나무는 나무 모양 그대로다.
    [Tooltip("맞았을 때 떠는 폭 (m)")] [Range(0f, 0.2f)] public float 흔들폭 = 0.045f;

    System.Collections.IEnumerator 흔들기()
    {
        var 기본자리 = transform.localPosition;
        while (shake > 0f)
        {
            shake = Mathf.Max(0f, shake - Time.deltaTime * 5f);
            float a = Mathf.Sin(Time.time * 46f) * shake * 흔들폭;
            transform.localPosition = 기본자리 + new Vector3(a, 0f, a * 0.55f);
            yield return null;
        }
        transform.localPosition = 기본자리;
        자리 = transform.position;      // 떨고 나서 기억해 둔 자리를 바로잡는다
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

    // ★★★★**갈무리한 것은 사체에 쌓인다 — 인벤으로 순간이동하지 않는다** (2026-08-12).
    //   9-0(인과와 행위)의 마지막 구멍이었다. 사체를 뒤적이는 행위는 넣어 놓고,
    //   **나온 것은 여전히 내 주머니로 텔레포트**했다 — 「꺼내서 챙긴다」가 없었다.
    //   → 뒤적이면 몸에 쌓이고, **Tab 을 열어 가져가야** 내 것이 된다.
    //   ☆무게 한도가 여기서 살아난다: 다 못 들면 사체에 남겨 두고 나중에 온다.
    [Tooltip("켜면 캔 것이 인벤이 아니라 이 물체 위에 쌓인다 (사체)")]
    public bool 그자리에쌓기 = false;

    땅무더기 담을곳;
    땅무더기 통찾기()
    {
        if (담을곳 == null) 담을곳 = GetComponent<땅무더기>();
        if (담을곳 == null)
        {
            담을곳 = gameObject.AddComponent<땅무더기>();
            담을곳.프롭모양 = true;             // 몸 자체가 모양이다 — 상자를 안 얹는다
            담을곳.안사라짐 = true;
            담을곳.반경 = 반경;
        }
        return 담을곳;
    }

    // ★★★★**갈무리에서 가죽과 뼈가 나온다** (2026-08-12 사용자 "가죽 재질의 팻들한테는
    //   가죽이 나오게끔 해서 제작대에서 제작할 수 있게").
    //
    //   ☆실측으로 확인한 것: 가죽자루(+10kg)·가죽배낭(+20kg) **제작법이 이미 있었고**,
    //     뼈칼의 곁들임에도 *"갈무리에서 가죽이 나온다"* 라고 적혀 있었다. 그런데
    //     **사체가 고기만 냈다** — 가죽도 뼈도 아무 데서도 안 나왔다.
    //     그래서 뼈칼(뼈 2개)조차 못 만들고 **가죽 계열 전체가 첫 칸에서 막혀 있었다.**
    //     규칙 9-2 그대로다: 자산은 앞서 있고 배선이 뒤처진다.
    //
    //   ★뼈는 **맨손으로도** 나온다 — 안 그러면 뼈칼을 못 만들어 사슬이 시작되지 않는다.
    //   ★가죽은 **뼈칼이 있어야** 나온다 (이미 `뼈칼.도구쓰임 = "가죽"` 으로 적혀 있다).
    //     기획 5-4 의 도구 사슬 그대로다: 뼈칼 → 가죽.
    [Header("★갈무리 곁들이 (사체일 때)")]
    [Tooltip("몇 번에 한 번 뼈가 나오나 (0 이면 안 나온다)")] [Range(0f, 1f)] public float 뼈확률 = 0.5f;
    [Tooltip("뼈칼이 있을 때 몇 번에 한 번 가죽이 나오나")] [Range(0f, 1f)] public float 가죽확률 = 0.6f;

    void 곁들이나오기()
    {
        var 통 = 통찾기();
        if (통 == null) return;

        if (뼈확률 > 0f && Random.value < 뼈확률)
        {
            var 뼈 = 아이템표.찾기("뼈");
            if (뼈 != null) 통.속.넣기(뼈, 1);
        }
        // 가죽은 뼈칼을 쥐고 있어야 나온다 — 도구가 행위를 연다 (기획 5-4)
        if (가죽확률 > 0f && 인벤.어느통에든도구("가죽") != null && Random.value < 가죽확률)
        {
            var 가죽 = 아이템표.찾기("가죽");
            if (가죽 != null) 통.속.넣기(가죽, 1);
        }
        통.갱신();
    }

    /// 실제 한 히트 — 지급·소모·쓰러짐. **계속 패도 되면 true**
    bool 한타(Vector3 방향)
    {
        // perHit 이 0 이면 아무것도 안 나온다 — 선 나무를 패는 단계가 그렇다.
        // 나무는 **통나무가 된 뒤에** 나온다 (인과와 행위)
        // ★돌은 인벤에 바로 꽂히지 않는다 — **돌맹이가 튀어 떨어지고, 줍는 것이 수확이다**
        //   (9-0 인과: 결과는 행위에서. 2026-08-11 사용자 "주변에 툭툭 떨어지게")
        if (kind == Stock.Kind.돌 && perHit > 0) { for (int i = 0; i < perHit; i++) 돌맹이튀기(); }
        else if (그자리에쌓기 && perHit > 0)
        {
            var 종 = Stock.종(kind);
            if (종 != null) { var 통 = 통찾기(); 통.속.넣기(종, perHit); 통.갱신(); }
            곁들이나오기();
        }
        else if (perHit > 0) Stock.Add(kind, perHit);

        if (--hits > 0) return true;

        if (쓰러짐 != null) { 쓰러짐.시작(방향); Destroy(this); return false; }
        // ★다 뒤적였어도 **몸은 남는다** — 쌓인 것을 가져가야 사라진다 (`Carcass` 가 지운다)
        if (그자리에쌓기) { Destroy(this); return false; }
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
        if (무 != null) 무.StartCoroutine(툭(무, 돌알.transform, 끝 + Vector3.up * (s * 0.35f)));
    }

    /// 짧은 포물선 — 이펙트가 아니라 실제로 그 자리로 간다 (그림 = 판정)
    // ★★★★**날아간 뒤에 자리를 다시 기억시킨다** (2026-08-11 사용자 "바닥에 떨어진 작은
    //   돌맹이 줍기가 안돼는게 버그좀고쳐줘").
    //   `땅무더기` 는 값이 비싸서 **자리를 기억해 두고** 그걸로 찾는다(Transform 을 안 읽는다).
    //   그런데 기억하는 시점이 **튀기 전**이라, 돌이 0.8~1.6m 날아가고 나면
    //   **눈에 보이는 곳과 등록된 곳이 어긋난다** → 돌 위에 서 있어도 못 줍는다.
    //   ☆같은 함정이 또 있을 수 있다 — 「자리를 기억하는 것」은 **움직이면 다시 기억시켜야** 한다.
    static System.Collections.IEnumerator 툭(땅무더기 무, Transform t, Vector3 끝)
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
        if (t == null) yield break;
        t.position = 끝;
        if (무 != null) 무.자리 = 끝;                  // ★도착한 자리로 고쳐 기억한다
    }

    /// 앞쪽 가장 가까운 자원을 한 번 캔다
    // ★★★**몸집을 봐 준다** (2026-08-11 사용자 "팻 죽이고나서 파밍하려하는데 파밍이
    //   잘안돼, 범위가 좀 이상해"). 갈무리는 **중심 한 점**까지의 거리만 봤다.
    //   그런데 사슴처럼 누운 사체는 몸이 1~2m 뻗어 있어서, **몸 옆에 딱 붙어 서 있어도
    //   중심은 범위 밖**이었다. 때리는 쪽은 뼈 점 전부(`실루엣판정`)로 재니
    //   "때릴 땐 맞는데 갈무리는 안 된다" 가 났다 — 두 자가 달랐던 것이다.
    //   → 그 물체의 반경만큼 거리에서 빼 준다. `Carcass` 가 몸을 재서 넣어 준다.
    [Tooltip("이 물체의 반경 (m) — 싼 거르개에만 쓴다. 0 이면 그림을 재서 채운다")]
    public float 반경 = 0f;

    // ★★★★**근본 해결 — 「중심 한 점」이 아니라 「실제 그림」으로 잰다** (2026-08-11 사용자
    //   "쓰러진 나무도 마찬가지야.. 선택이 안돼, 이상해 범위가,, 이거 계속 그럴거 같은데
    //   근본적으로 해결할 방법 없어?").
    //
    //   맞는 지적이었다. 나는 사체 반경 → 방향무관 → 쓰러진 나무 로 **하나씩 땜질**하고
    //   있었다. 뿌리는 하나다: 물체를 **중심점 + 반지름**(원)으로 봤다는 것.
    //   실제 물체는 **누워 있거나 길쭉하다** — 쓰러진 나무는 10m 짜리 막대인데
    //   원으로 재니 끝을 잡아도 안 걸린다. 사체도 같은 이유였다.
    //   → **렌더러 경계 상자**까지의 거리로 잰다. 상자 안이면 거리 0 이다.
    //     사체·쓰러진 나무·큰 바위가 한 번에 풀리고, 앞으로 어떤 모양이 와도 안 틀린다.
    //
    //   ☆9-4: 경계는 **한 번 재서 담아 둔다.** 자원이 수천 개라 매번 재면 그게 렉이다.
    //     ★단 **움직이면 다시 잰다** — 나무가 쓰러지면 경계가 통째로 바뀐다.
    Bounds 경계; bool 경계잼; Vector3 잰자리; Quaternion 잰돌기;

    void 경계확인()
    {
        // ☆진동(4~5cm)으로는 다시 재지 않는다 — 매 프레임 재면 그게 렉이다.
        //   나무가 쓰러지면 **회전**이 크게 바뀌므로 그건 걸린다.
        if (경계잼 && (transform.position - 잰자리).sqrMagnitude < 0.01f
                   && Quaternion.Angle(transform.rotation, 잰돌기) < 1f) return;
        잰자리 = transform.position; 잰돌기 = transform.rotation; 경계잼 = true;

        var rs = GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0)
        {
            경계 = new Bounds(잰자리, Vector3.one * 0.6f);
            if (반경 <= 0f) 반경 = 0.3f;
            return;
        }
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        경계 = b;
        반경 = Mathf.Max(b.extents.x, b.extents.z);   // 싼 거르개용 — 넉넉하게
    }

    /// ★이 물체의 **그림까지**의 수평 거리 (m) — 안에 서 있으면 0
    public float 수평거리(Vector3 p)
    {
        경계확인();
        // 높이는 안 본다 — 위아래로 긴 나무를 올려다보며 재면 늘 멀다
        var q = new Vector3(p.x, 경계.center.y, p.z);
        return Mathf.Sqrt(경계.SqrDistance(q));
    }

    /// 그림의 한가운데 — 방향(앞쪽인가)을 잴 때 쓴다. 쓰러진 나무는 뿌리가 아니라 몸통이 기준이다
    public Vector3 경계중심 { get { 경계확인(); return 경계.center; } }

    /// ★싼 거르개의 여유 (m) — 긴 물체(쓰러진 나무 10m)의 끝이 닿는 경우를 놓치지 않게
    const float 넉넉 = 12f;

    // ★★**사체는 방향을 안 본다** (2026-08-11 사용자 "갈무리 범위가 내가 몸을 돌릴때마다
    //   됐다 안됐다하는데 바라보는 방향이나 공격 범위보단 그냥 주변에 있으면 가능하게").
    //   나무·돌은 **앞으로 도끼를 휘두르는** 것이라 방향이 맞다. 그런데 갈무리는 쪼그려
    //   앉아 뒤적이는 일이다 — 옆에 있으면 되지 어느 쪽을 보는지는 상관없다.
    [Tooltip("켜면 바라보는 방향을 안 본다 — 옆에 있기만 하면 된다 (사체가 그렇다)")]
    public bool 방향무관 = false;

    // ★★★★**게이지 채집** (2026-08-11 사용자 "갈무리, 쪼그려앉아서 뒤적이는 모션 넣어줄래?
    //   그런다음 게이지 다 차게되면…" · "나무나 돌 캐기도 클릭하면 동작이 있고 게이지로").
    //   옛 방식은 **휘두름 한 번 = 한 칸**이었다. 이제 **누르고 있으면 게이지가 찬다.**
    //   ☆속도는 도구가 정한다 (헌법 5-4 "맨손도 되긴 하되 아주 느리다").
    [Tooltip("도구 성능 3 기준으로 한 칸을 캐는 데 걸리는 시간 (초)")]
    [Range(0.15f, 3f)] public float 한칸시간 = 0.55f;

    /// 지금 이 대상을 캐는 속도 (초당 몇 칸) — 도구가 없으면 3분의 1
    public float 캐는속도()
    {
        float 일 = 3f;                                   // 도구 개념이 없는 것(사체)은 기준 속도
        if (맞는쓰임 != null)
        {
            var 도구 = 인벤.어느통에든도구(맞는쓰임);
            일 = 도구 != null ? 도구.종.성능 : 1f;         // 맨손은 3분의 1
        }
        return 일 / 3f / Mathf.Max(0.05f, 한칸시간);
    }

    /// 한 칸 진행 — 도구를 닳리고 실제로 지급한다. **더 캘 수 있으면 true**
    public bool 한칸(Vector3 방향)
    {
        if (맞는쓰임 != null)
        {
            var 도구 = 인벤.어느통에든도구(맞는쓰임);
            if (도구 != null) 인벤.어느통에서든닳음(도구);
        }
        bool 흔들던중 = shake > 0f;
        shake = 1f;
        if (!흔들던중) StartCoroutine(흔들기());
        return 한타(방향);
    }

    /// ★마우스가 가리킨 땅점 둘레에서 고른다 — **방향을 안 본다** (클릭으로 집는 것이라)
    public static Harvest 찍기(Vector3 땅점, float 반경)
    {
        Harvest best = null; float bd = float.MaxValue;
        for (int i = All.Count - 1; i >= 0; i--)
        {
            var h = All[i];
            if (h == null) continue;
            var v = h.transform.position - 땅점; v.y = 0f;
            if (v.magnitude - h.반경 - 0.6f > 반경) continue;      // 싼 거르개
            float d = h.수평거리(땅점);                            // 정밀 (실제 그림)
            if (d > 반경 || d > bd) continue;
            bd = d; best = h;
        }
        return best;
    }

    /// 앞(또는 옆)의 가장 가까운 대상 — 캐기와 갈무리가 **같은 자**로 잰다
    public static Harvest 찾기(Vector3 from, Vector3 look, float reach)
    {
        Harvest best = null; float bd = float.MaxValue;
        for (int i = All.Count - 1; i >= 0; i--)
        {
            var h = All[i];
            if (h == null) continue;
            // ①싼 거르개 — **기억해 둔 자리**를 읽는다 (2026-08-11 실측: 2만 6천 개를
            //   Transform 으로 읽으면 22.5ms · 기억한 값이면 0.1ms 안쪽).
            //   ☆넉넉하게 자른다 — 쓰러진 나무처럼 **긴 것**은 중심이 멀어도 끝이 닿는다.
            var v자 = h.자리 - from; v자.y = 0f;
            if (v자.sqrMagnitude > (reach + 넉넉) * (reach + 넉넉)) continue;
            // ②정밀 — **실제 그림**까지의 거리. 누운 사체도 쓰러진 나무도 여기서 맞는다
            float d = h.수평거리(from);
            if (d > reach || d > bd) continue;
            var v = h.경계중심 - from; v.y = 0f;
            if (!h.방향무관 && d > 0.05f && v.sqrMagnitude > 0.01f
                && Vector3.Dot(v.normalized, look) < 0.2f) continue;   // 앞쪽만 (사체는 안 본다)
            bd = d; best = h;
        }
        return best;
    }

    public static bool TryHarvest(Vector3 from, Vector3 look, float reach)
    {
        var best = 찾기(from, look, reach);
        if (best == null) return false;
        best.Chop(look);
        return true;
    }
}
