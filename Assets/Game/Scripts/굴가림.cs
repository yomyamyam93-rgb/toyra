using System.Collections.Generic;
using UnityEngine;

/// 동굴 덮개 걷기 — **밖에서는 막힌 바위, 들어가면 좀보이드처럼 걷혀 안이 보인다**
/// (2026-08-11 사용자 "밖에서 보면 막힌 지형인데, 입구가 있고, 들어가면 좀보이드처럼
///  투시되서 보이는 그런 공간이거야"). `WorldGen.동굴터` 가 동굴 뿌리에 붙인다.
///
/// ★두 가지를 다르게 다룬다 (좀보이드의 컷어웨이 그대로):
///   · 「덮개」(천장) — 통째로 숨긴다. 천장이 있으면 내가 안 보인다
///   · 「덮개산」(바위 살 = 벽) — 숨기지 않고 **낮은 턱으로 깎는다.** 통째로 숨기면
///     동굴의 윤곽이 사라져 어디가 벽인지 모른다. 낮게 남아야 길이 읽힌다
/// ★행동이 아니라 그림이다 — 판단은 「사람이 영역 안인가」 하나뿐.
public class 굴가림 : MonoBehaviour
{
    const float 턱높이 = 0.8f;

    readonly List<Renderer> 천장들 = new List<Renderer>();
    readonly List<(Transform t, Vector3 크기, Vector3 자리)> 살들 = new List<(Transform, Vector3, Vector3)>();
    float xMin, xMax, zMin, zMax;
    bool 걷힘;

    void Start()
    {
        bool 첫 = true;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            var b = r.bounds;
            if (첫) { xMin = b.min.x; xMax = b.max.x; zMin = b.min.z; zMax = b.max.z; 첫 = false; }
            else
            {
                xMin = Mathf.Min(xMin, b.min.x); xMax = Mathf.Max(xMax, b.max.x);
                zMin = Mathf.Min(zMin, b.min.z); zMax = Mathf.Max(zMax, b.max.z);
            }
            var n = r.gameObject.name;
            if (n.StartsWith("덮개산")) 살들.Add((r.transform, r.transform.localScale, r.transform.localPosition));
            else if (n.StartsWith("덮개")) 천장들.Add(r);
        }
    }

    void Update()
    {
        var h = Hero.Me;
        if (h == null || (천장들.Count == 0 && 살들.Count == 0)) return;
        var p = h.transform.position;
        // ★모서리 살짝 안쪽부터 걷는다 — 바깥 벽에 스치기만 해도 걷히면 「막힌 지형」이 안 된다
        bool 안 = p.x > xMin + 1.2f && p.x < xMax - 1.2f && p.z > zMin + 1.2f && p.z < zMax - 1.2f;
        if (안 == 걷힘) return;
        걷힘 = 안;

        foreach (var r in 천장들) if (r != null) r.enabled = !안;
        foreach (var (t, 크기, 자리) in 살들)
        {
            if (t == null) continue;
            if (안)
            {
                t.localScale = new Vector3(크기.x, 턱높이, 크기.z);
                t.localPosition = new Vector3(자리.x, 턱높이 * 0.5f - 0.02f, 자리.z);
            }
            else
            {
                t.localScale = 크기;
                t.localPosition = 자리;
            }
        }
    }
}
