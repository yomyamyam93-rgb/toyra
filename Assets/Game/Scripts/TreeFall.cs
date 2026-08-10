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
    [Tooltip("쓰러지는 데 걸리는 시간 (초)")] public float 쓰러지는시간 = 1.1f;
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
        if (잎 != null) Destroy(잎.gameObject);

        gameObject.name = "통나무";

        var h = gameObject.AddComponent<Harvest>();
        h.kind = Stock.Kind.나무;
        h.hits = 3;
        h.perHit = Mathf.Max(1, Mathf.CeilToInt(통나무값 / 3f));
        h.장애물치우기 = false;

        Destroy(this);
    }
}
