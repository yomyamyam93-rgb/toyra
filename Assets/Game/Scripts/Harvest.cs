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

    void Update()
    {
        if (shake <= 0f) return;
        shake = Mathf.Max(0f, shake - Time.deltaTime * 5f);
        float k = 1f + shake * 0.12f;
        transform.localScale = new Vector3(baseScale.x / k, baseScale.y * k, baseScale.z / k);
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
        shake = 1f;
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
