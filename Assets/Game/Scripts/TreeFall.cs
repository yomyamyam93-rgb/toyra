using UnityEngine;

/// 나무 쓰러뜨리기 — **선 나무를 패면 자원이 아니라 「통나무」가 된다.**
///
/// ★2026-08-03 사용자: *"나무를 부수면 자연스럽게 쓰러지고 통나무가 된다.
///   그걸 다시 패면 작은 나무 조각들로 나오게 하는 거지."* (발헤임 방식)
///   → 「모든 결과에는 인과와 행위가 있어야 한다」의 나무 판.
///
///   선 나무를 팬다 → **쓰러진다** → 통나무를 팬다 → 나무가 나온다
///
/// ★통나무를 통째로 짊어지고 옮기는 건 나중에 (사용자 확정).
public class TreeFall : MonoBehaviour
{
    [Tooltip("통나무를 다 패면 나오는 나무 총량")] public int 통나무값 = 6;
    [Tooltip("쓰러지는 데 걸리는 시간 (초)")] public float 쓰러지는시간 = 1.6f;   // ★"쭈우우욱" — t² 곡선이라 앞 절반이 길게 기운다
    [HideInInspector] public Vector3 선자리;

    bool 쓰러지는중;
    float t;
    Vector3 밑동, 축;
    Transform 잎;

    /// 다 팼다 — 쓰러지기 시작한다. `dir` 은 도끼질이 온 방향(그쪽으로 넘어간다)
    public void 시작(Vector3 dir)
    {
        if (쓰러지는중) return;
        쓰러지는중 = true;
        t = 0f;

        // 밑동을 축으로 돈다 — 가운데를 축으로 돌리면 나무가 공중에 뜬다
        밑동 = transform.position - Vector3.up * (transform.localScale.y * 0.5f);
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
        축 = Vector3.Cross(Vector3.up, dir.normalized);   // 그 방향으로 넘어가는 회전축

        Blocker.Remove(선자리);                            // 서 있던 자리를 튼다

        foreach (Transform c in transform) if (c.name.Contains("잎")) { 잎 = c; break; }

        // ★8/6 에 Update 를 걷어내며 이 시동을 빠뜨려 **나무가 영영 안 쓰러졌다** (2026-08-11).
        //   시작만 하고 아무도 넘어가기를 안 돌리니, 선 나무는 팬 만큼만 닳고 그대로 서 있었다
        StartCoroutine(넘어가기());
    }

    // ★★★**`Update` 를 없앴다** (2026-08-06 — `Harvest` 와 같은 이유).
    //   나무 21,946그루 × `if (!쓰러지는중) return;` = 아무 일도 안 하면서 매 프레임
    //   2만 2천 번. 쓰러지는 나무는 **한 번에 한 그루**이므로 그때만 도는 게 맞다.
    System.Collections.IEnumerator 넘어가기()
    {
        while (true)
        {
            float 이전 = Mathf.Clamp01(t / 쓰러지는시간);
            t += Time.deltaTime;
            float 지금 = Mathf.Clamp01(t / 쓰러지는시간);

            // 처음엔 천천히 기울다 끝에서 확 넘어간다 (실제로 그렇게 쓰러진다)
            float 각이전 = 92f * 이전 * 이전;
            float 각지금 = 92f * 지금 * 지금;
            transform.RotateAround(밑동, 축, 각지금 - 각이전);

            if (지금 >= 1f) break;
            yield return null;
        }
        통나무되기();
    }

    /// 쓰러진 뒤 — 잎은 떨어져 사라지고, 줄기는 통나무가 된다
    void 통나무되기()
    {
        쓰러지는중 = false;
        if (잎 != null) { 잎흩날리기(); Destroy(잎.gameObject); }

        gameObject.name = "통나무";

        var h = gameObject.AddComponent<Harvest>();
        h.kind = Stock.Kind.나무;
        h.hits = 3;
        h.perHit = Mathf.Max(1, Mathf.CeilToInt(통나무값 / 3f));
        h.장애물치우기 = false;

        Destroy(this);
    }

    /// 착지 순간 — 잎이 있던 자리에서 잎 조각이 펄럭이며 가라앉는다 (12장: 몸이 실제로 한 일)
    void 잎흩날리기()
    {
        var 색 = new Color(0.32f, 0.46f, 0.22f);
        var r = 잎.GetComponentInChildren<Renderer>();
        if (r != null && r.sharedMaterial != null) 색 = r.sharedMaterial.color;
        var 중심 = r != null ? r.bounds.center : 잎.position;
        float 반 = r != null ? Mathf.Max(r.bounds.extents.x, r.bounds.extents.z) : 1.5f;

        int n = Random.Range(9, 14);
        for (int i = 0; i < n; i++)
        {
            float s = Random.Range(0.14f, 0.24f);
            var at = 중심 + new Vector3(Random.Range(-반, 반), Random.Range(-0.4f, 0.4f), Random.Range(-반, 반));
            var 조각 = Grey.Box(transform.parent, at, new Vector3(s, 0.02f, s * 0.7f),
                                색, "잎조각", 0f, Random.value * 360f);
            // ★숙주를 조각 자신에 둔다 — TreeFall 은 곧 Destroy 되어 코루틴이 같이 죽는다
            var 숙주 = 조각.AddComponent<잎조각숙주>();
            숙주.StartCoroutine(펄럭(조각.transform));
        }
    }

    /// 흔들리며 가라앉고, 땅에 닿으면 잠시 뒤 사라진다
    static System.Collections.IEnumerator 펄럭(Transform t)
    {
        float 위상 = Random.value * 10f, 돌기 = Random.Range(-160f, 160f);
        float 흔들 = Random.Range(0.6f, 1.2f);
        while (t != null && t.position.y > 0.03f)
        {
            var p = t.position;
            p.y -= 0.55f * Time.deltaTime;
            p.x += Mathf.Sin(Time.time * 3.1f + 위상) * 흔들 * Time.deltaTime;
            t.position = p;
            t.Rotate(0f, 돌기 * Time.deltaTime, 24f * Time.deltaTime);
            yield return null;
        }
        if (t != null) Object.Destroy(t.gameObject, Random.Range(0.6f, 1.4f));
    }
}

/// 잎 조각의 코루틴 숙주 — Update 없음, 코루틴만 든다 (공짜)
public class 잎조각숙주 : MonoBehaviour {}
