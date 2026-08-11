using UnityEngine;

/// 생물의 **동작 갈아타기** (2026-08-07).
///
/// ★회사에서 저작한 모션이 모델마다 여섯 벌 있다 — 대기·걷기·뛰기·공격·피격·죽음.
///
/// ★★2차 개편 (같은 날, 사용자 "또 뚝뚝 뚝"): 처음엔 동작마다 **컨트롤러를 통째로
///   갈아탔는데**, 그 방식은 **블렌드가 0** 이라 전환마다 자세가 스냅됐다.
///   → 정석으로: 공용 컨트롤러 하나(`rig/_공용동작` — 상태 6개, Any State 에서
///     int 「동작」 으로 전이, **0.15초 크로스페이드**)에 모델별 클립을
///     `AnimatorOverrideController` 로 끼운다. 전환은 int 하나, 이음새는 블렌드가 맡는다.
public class 몸짓 : MonoBehaviour
{
    // _공용동작 의 상태 순서와 같아야 한다
    const int 대기 = 0, 걷기 = 1, 뛰기 = 2, 공격 = 3, 피격 = 4, 죽음 = 5;
    static readonly string[] 동작이름 = { "대기", "걷기", "뛰기", "공격", "피격", "죽음" };

    Animator an;
    Critter c;
    string 모델;
    int 지금동작 = -1;
    Vector3 지난자리;
    float 속도평활, 죽은지, 피격남은;
    AnimationClip[] 내클립;                       // [동작] — 이 모델의 여섯 클립

    static RuntimeAnimatorController 공용;
    static Mesh 잼틀;
    static readonly System.Collections.Generic.Dictionary<string, AnimationClip[]> 클립캐시 =
        new System.Collections.Generic.Dictionary<string, AnimationClip[]>();
    // ★모델당 컨트롤러 하나 — 스폰 렉의 정체였다 (위 `준비` 참고)
    static readonly System.Collections.Generic.Dictionary<string, AnimatorOverrideController> 오버라이드캐시 =
        new System.Collections.Generic.Dictionary<string, AnimatorOverrideController>();
    static readonly System.Collections.Generic.List<float> 높이들 = new System.Collections.Generic.List<float>(2048);

    public void 준비(Animator a, string 모델이름)
    {
        an = a; 모델 = 모델이름;
        지난자리 = transform.position;

        if (공용 == null) 공용 = Resources.Load<RuntimeAnimatorController>("rig/_공용동작");
        if (공용 == null) { Debug.LogWarning("[몸짓] rig/_공용동작 없음 — 동작 전환을 못 한다"); return; }

        if (!클립캐시.TryGetValue(모델, out 내클립))
        {
            내클립 = new AnimationClip[동작이름.Length];
            for (int i = 0; i < 동작이름.Length; i++)
                내클립[i] = Resources.Load<AnimationClip>("rig/" + 동작이름[i] + "_" + 모델);
            클립캐시[모델] = 내클립;
        }

        // 자리표(다람쥐_1) 클립을 이 모델의 클립으로 갈아끼운다
        // ★★★**모델당 한 번만 만든다** (2026-08-11 사용자 "팻이 생성될때 그런거같은데 렉이
        //   심해 뚝뚝 끊겨"). 전에는 **짐승 한 마리마다** `AnimatorOverrideController` 를 새로
        //   만들고 `GetOverrides`/`ApplyOverrides` 를 돌렸다 — 둘 다 배열을 새로 할당하고
        //   컨트롤러를 통째로 다시 굽는 무거운 일이라, 스폰마다 프레임이 튀었다.
        //   ☆같은 모델이면 덮어쓰는 내용이 **똑같다** — 하나를 만들어 모두가 나눠 쓰면 된다.
        //     (`Animator` 는 컨트롤러를 공유해도 제 상태를 따로 갖는다)
        if (!오버라이드캐시.TryGetValue(모델, out var aoc) || aoc == null)
        {
            aoc = new AnimatorOverrideController(공용) { name = "동작_" + 모델 };
            var 짝 = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
            aoc.GetOverrides(짝);
            for (int k = 0; k < 짝.Count; k++)
            {
                var 자리표 = 짝[k].Key;
                if (자리표 == null) continue;
                for (int i = 0; i < 동작이름.Length; i++)
                    if (자리표.name.StartsWith(동작이름[i] + "_") && 내클립[i] != null)
                    { 짝[k] = new System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>(자리표, 내클립[i]); break; }
            }
            aoc.ApplyOverrides(짝);
            오버라이드캐시[모델] = aoc;
        }
        an.runtimeAnimatorController = aoc;
        지금동작 = -1;
        바꿈(대기);
    }

    void 바꿈(int 동작)
    {
        if (지금동작 == 동작 || an == null) return;
        if (내클립 == null || (동작 < 내클립.Length && 내클립[동작] == null)) return;   // 그 동작이 없으면 유지
        지금동작 = 동작;
        an.SetInteger("동작", 동작);
        an.speed = 1f;
    }

    /// 공격 클립의 실제 길이 — `Critter` 가 휘두름 커밋 시간을 여기에 맞춘다
    public float 공격클립길이()
        => 내클립 != null && 내클립[공격] != null ? 내클립[공격].length : 0.55f;

    /// 플레이 시작마다 부른다 (`플레이초기화`) — 도메인 리로드를 끄면 static 이 남는다.
    /// ★죽은 유니티 객체를 물고 있으면 안 되므로 컨트롤러 캐시를 비운다 (클립은 에셋이라 살아 있다)
    public static void 캐시비우기() { 오버라이드캐시.Clear(); }

    /// 맞았다 — 피격 모션.
    ///
    /// ★★**클립 길이와 재생 시간을 맞춘다** (2026-08-09 사용자 "그냥 모션이 뚝 나오는게
    ///   아니라, 좀 느리게 맞는 모션을 길게"). 전엔 클립이 0.40~0.45초인데 1.0초 동안 틀어서
    ///   **한 번 다 돌고 두 번째가 시작되다 끊겼다** — 그게 뚝뚝 끊기는 정체였다.
    ///   클립을 0.90초로 다시 저작했고, 여기도 그 길이에 맞춘다 (여운까지 딱 한 번).
    public const float 피격길이 = 0.90f;
    public void 맞았다() { 피격남은 = 피격길이; }

    /// 죽은 자세를 땅에 붙인다 — **뼈 기준** (2026-08-07 3차 개정).
    ///
    /// ★정점 기준의 실패사: ①최저 정점 → 뿔 끝으로 물구나무 ②하위 12% → 큰뿔사슴은
    ///   **뿔 정점이 수천 개**라 12% 조차 뿔 안에 떨어져 몸이 떴다.
    /// ★뼈는 뿔을 모른다 — 뼈는 몸통·다리·머리 관절에만 있다. **가장 낮은 뼈를 몸 두께의
    ///   절반(키의 12%)만큼 위에** 놓으면, 뿔이 어떤 괴물이어도 몸통은 땅에 눕는다.
    ///   뿔은 땅에 박히거나 하늘로 서거나 — 몸이 붙어 있는 한 자연스럽다.
    void 접지()
    {
        float 최저뼈 = 9e9f;
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var bs = smr.bones;
            if (bs == null) continue;
            for (int i = 0; i < bs.Length; i++)
                if (bs[i] != null && bs[i].position.y < 최저뼈) 최저뼈 = bs[i].position.y;
        }
        if (최저뼈 > 8e9f) return;
        // ★★★★**땅 밑으로는 절대 안 내려간다** (2026-08-11 사용자 "공격하던 팻이 한번씩
        //   바닥에 쳐박혀 들어가는 버그가 있어").
        //
        //   옛 코드는 가장 낮은 뼈를 **세계 y = 몸두께반** 에 맞췄다 — 「땅 = 0」을 가정한 것이다.
        //   그런데 ①땅은 `땅격자` 라 0 이 아니고 ②공격·비비적임으로 다리 뼈가 아래로
        //   뻗으면 그만큼 **몸을 통째로 땅에 밀어 넣었다.**
        //   ☆이 함수는 실행 순서 300 이라 `Critter` 가 매 프레임 되돌려 놓는 y 를 **덮어쓴다** —
        //     그래서 한 번 파고들면 스스로 못 빠져나온다.
        //   → 목표를 **그 자리의 땅 높이 기준**으로 잡고, 뿌리는 땅 아래로 못 가게 막는다.
        var p = transform.position;
        float 땅 = 땅격자.걷는높이(p.x, p.z);
        float 몸두께반 = (c != null ? c.종.키 : 1f) * 0.12f;
        float 새y = p.y - (최저뼈 - (땅 + 몸두께반));
        transform.position = new Vector3(p.x, Mathf.Max(땅, 새y), p.z);
    }

    // ★★★「클립별 높이 보정」은 **철거했다** (2026-08-07 밤).
    //   시도 자체가 성립하지 않았다: 정점 통계(최저점·하위 12%)로 지지선을 잡으면
    //   ①뿔 정점이 수천 개인 큰뿔사슴은 지지선이 뿔에 걸리고 ②서 있는 사슴은 다리
    //   정점이 몇 개 없어 지지선이 **배 높이**에 잡힌다 — 그래서 공중 부양을 고치려다
    //   산 사슴 7마리를 땅속(-0.7m)에 심었다 (실측).
    //   ☆남긴 것 셋이 정답이다: 스폰 때 「진짜맞춤」 · 뿌리 y 0 고정 · 죽음의 뼈 접지.
    //   피격 클립이 몸을 살짝 띄우는 건 클립의 개성으로 둔다 — 고치려면 클립을 고칠 일이다.

    void Update()
    {
        if (an == null || 내클립 == null) return;
        if (c == null) { c = GetComponent<Critter>(); if (c == null) return; }

        float dt = Time.deltaTime;
        var p = transform.position;
        float v = dt > 1e-5f ? (p - 지난자리).magnitude / dt : 0f;
        지난자리 = p;
        속도평활 = Mathf.Lerp(속도평활, v, 1f - Mathf.Exp(-8f * dt));

        // ── 죽음 — 한 번 재생하고 마지막 자세에서 멈춘 뒤 땅에 붙는다
        if (!c.Alive)
        {
            바꿈(죽음);
            죽은지 += dt;
            float 길이 = 내클립[죽음] != null ? 내클립[죽음].length : 1f;
            if (죽은지 >= 길이 + 0.15f) { an.speed = 0f; if (죽은지 < 길이 + 1.6f) 접지(); }
            else 접지();
            return;
        }
        죽은지 = 0f;

        // ── 피격 — 맞은 직후 1초
        if (피격남은 > 0f) { 피격남은 -= dt; 바꿈(피격); return; }
        if (c.넘어짐) { 바꿈(피격); return; }

        // ── 공격 — 커밋된 휘두름 동안은 끝까지 (허공이라도)
        if (c.휘두르는중) { 바꿈(공격); return; }

        // ── 이동 — 실측 속도로 가른다 (상태가 아니라 발이 진실이다)
        float 기준 = Mathf.Max(0.5f, c.종.이속);
        if (속도평활 > 기준 * 0.55f) 바꿈(뛰기);
        else if (속도평활 > 0.15f) 바꿈(걷기);
        else 바꿈(대기);
    }
}
