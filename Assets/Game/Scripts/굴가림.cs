using System.Collections.Generic;
using UnityEngine;

/// 동굴 덮개 걷기 — **밖에서는 막힌 바위, 들어가면 좀보이드처럼 걷혀 안이 보인다**
/// (2026-08-11 사용자 "밖에서 보면 막힌 지형인데, 입구가 있고, 들어가면 좀보이드처럼
///  투시되서 보이는 그런 공간이거야"). `WorldGen.동굴터` 가 동굴 뿌리에 붙인다.
///
/// ★★★**언제 걷히나 — 「파인 바닥을 밟았을 때」다** (2026-08-11 사용자 "들어가지도
///   않았는데 뚜껑이 열리니까 좀.."). 처음엔 **바위 덩어리 전체 범위**로 판정해서,
///   아직 바깥 바위 옆에 서 있는데도 열렸다. 동굴 하나가 54×76m 라 한참 밖이었다.
///   → 실제로 **파낸 바닥 칸** 가까이 있을 때만 연다. 바깥 범위는 **싼 1차 거르개**로만 쓴다.
///
/// ★★두 가지를 다르게 다룬다 (좀보이드의 컷어웨이):
///   · 「덮개」(천장) — 통째로 숨긴다. 천장이 있으면 내가 안 보인다
///   · 「덮개산」(바위 살 = 벽) — 숨기지 않고 **낮춘다.** 통째로 숨기면 동굴의 윤곽이
///     사라져 어디가 벽인지 모른다. ★높이는 `벽높이` 로 정한다 — 낮으면 안에 있다는
///     느낌이 안 난다 (2026-08-11 사용자 "벽이 너무 낮다해야하나, 안에있다는 느낌도 안나고").
public class 굴가림 : MonoBehaviour
{
    [Tooltip("안에 들어갔을 때 바위 벽을 이 높이로 낮춘다 (m) — 사람 키가 1.8m")]
    public float 벽높이 = 2.4f;
    [Tooltip("파인 바닥에서 이만큼 안이면 「들어왔다」로 본다 (m)")]
    public float 들어온거리 = 3f;

    readonly List<Renderer> 천장들 = new List<Renderer>();
    readonly List<(Transform t, Vector3 크기, Vector3 자리)> 살들 = new List<(Transform, Vector3, Vector3)>();
    // ★파인 바닥 칸의 자리 — 「들어왔나」는 이걸로 판단한다 (Transform 을 매번 읽지 않는다)
    Vector3[] 바닥자리;
    float xMin, xMax, zMin, zMax;
    bool 걷힘;

    void Start()
    {
        var 바닥 = new List<Vector3>();
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
            // ★이름을 **부모까지 거슬러** 본다 — 뚜껑 위의 나무는 「덮개장식」 아래 있는데
            //   나무 자신의 이름은 「나무1(Clone)」 이라 제 이름만 봐서는 못 고른다
            string 갈래 = null;
            for (var t = r.transform; t != null && t != transform; t = t.parent)
            {
                var nm = t.name;
                if (nm.StartsWith("덮개산")) { 갈래 = "살"; break; }
                if (nm.StartsWith("덮개")) { 갈래 = "천장"; break; }   // 덮개·덮개혹·덮개장식·덮개풀
                if (nm.StartsWith("바닥")) { 갈래 = "바닥"; break; }
            }
            if (갈래 == "살") 살들.Add((r.transform, r.transform.localScale, r.transform.localPosition));
            else if (갈래 == "천장") 천장들.Add(r);
            else if (갈래 == "바닥") 바닥.Add(r.transform.position);
        }
        바닥자리 = 바닥.ToArray();
    }

    void Update()
    {
        var h = Hero.Me;
        if (h == null || 바닥자리 == null) return;
        var p = h.transform.position;

        // ① 싼 거르개 — 바위 덩어리 범위 밖이면 볼 것도 없다
        bool 안 = false;
        if (p.x > xMin && p.x < xMax && p.z > zMin && p.z < zMax)
        {
            // ② 진짜 판정 — 파인 바닥 가까이 있나
            float r2 = 들어온거리 * 들어온거리;
            for (int i = 0; i < 바닥자리.Length; i++)
            {
                float dx = 바닥자리[i].x - p.x, dz = 바닥자리[i].z - p.z;
                if (dx * dx + dz * dz <= r2) { 안 = true; break; }
            }
        }

        // ★굴 안에 있는 동안 시야에게 「실내」라고 알린다 — 낮이어도 어두워지고 시야가 줄어든다
        //   (2026-08-11 사용자 "동굴안에 들어가면 다른곳 시야가 막혀서 안보이지않나..?")
        if (안) VisionCone.실내목표 = 1f;

        if (안 == 걷힘) return;
        걷힘 = 안;

        foreach (var r in 천장들) if (r != null) r.enabled = !안;
        foreach (var (t, 크기, 자리) in 살들)
        {
            if (t == null) continue;
            if (안)
            {
                t.localScale = new Vector3(크기.x, 벽높이, 크기.z);
                t.localPosition = new Vector3(자리.x, 벽높이 * 0.5f - 0.02f, 자리.z);
            }
            else
            {
                t.localScale = 크기;
                t.localPosition = 자리;
            }
        }
    }
}
