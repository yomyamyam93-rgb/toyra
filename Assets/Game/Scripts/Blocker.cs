using System.Collections.Generic;
using UnityEngine;

/// 장애물 밀어내기 — 물리 없이 원기둥으로만. 나무·바위를 뚫고 지나가지 못하게 한다.
///
/// ★길찾기를 짜지 않는 이유: 직진하다 막히면 **밖으로 밀려나 미끄러져 돌아간다.**
///   밀어내는 반지름에 상한이 있어 좁은 틈에서도 영영 끼지 않는다.
///   (옛 프로젝트에서 검증된 방식 — 여기서 새 길찾기를 만들지 말 것)
///
/// 주변 3×3 칸만 검사하므로 장애물이 수천 개여도 프레임당 몇 개만 본다.
public static class Blocker
{
    const float Cell = 20f;
    static Dictionary<Vector2Int, List<Vector3>> grid;   // (x, z, 반지름)

    public static void Clear() { grid = new Dictionary<Vector2Int, List<Vector3>>(); }

    public static void Add(Vector3 wp, float radius)
    {
        if (grid == null) Clear();
        var k = Key(wp);
        if (!grid.TryGetValue(k, out var list)) grid[k] = list = new List<Vector3>();
        list.Add(new Vector3(wp.x, wp.z, radius));
    }

    /// 그 자리의 장애물을 지운다 (나무를 다 캤을 때 — 안 지우면 보이지 않는 벽이 남는다)
    public static void Remove(Vector3 wp, float tol = 0.6f)
    {
        if (grid == null) return;
        if (!grid.TryGetValue(Key(wp), out var list)) return;
        for (int i = list.Count - 1; i >= 0; i--)
            if (Mathf.Abs(list[i].x - wp.x) < tol && Mathf.Abs(list[i].y - wp.z) < tol)
                list.RemoveAt(i);
    }

    static Vector2Int Key(Vector3 p)
        => new Vector2Int(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.z / Cell));

    // ★★★**몸집이 곧 통행권이다** (2026-08-06 — 헌법 7번을 고치면서 생긴 조항).
    //
    //   *"지형이 무기다 — 다만 「높낮이」가 아니라 「폭」이다. 큰 놈은 빽빽한 숲·좁은
    //     바위틈·굴에 못 들어온다."*
    //
    //   그런데 여기 `Resolve` 는 **모든 몸을 똑같이** 밀어내고 있었다. 게다가 반지름을
    //   2.5m 로 깎아서(끼임 방지) **티라도 다람쥐처럼 나무 사이를 지나갔다.**
    //   ☆밀어내기 자체는 그대로 둔다 — 끼면 게임이 멈추니까. 대신 **「여기 들어갈 수
    //     있나」를 물어볼 수 있게** 하고, 못 들어가는 놈은 **쫓기를 포기**한다(`Critter`).
    //   ★막는 게 아니라 포기하게 하는 이유: 나무에 부딪혀 멈춰 서 있으면 고장처럼 보인다.
    //     가장자리에서 서성이다 돌아가는 게 동물답고, `SpeciesDef.영역` 과도 맞는다.

    /// 이 몸이 그 자리에 **설 수 있나** — 좁아서 못 들어가면 false.
    /// ★`Resolve` 와 달리 반지름을 깎지 않는다. 큰 몸은 정말로 큰 몸이다.
    public static bool 들어가나(Vector3 pos, float radius)
    {
        if (grid == null) return true;
        var k = Key(pos);
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (!grid.TryGetValue(new Vector2Int(k.x + dx, k.y + dz), out var list)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    var e = list[i];
                    float need = e.z + radius;
                    float ddx = pos.x - e.x, ddz = pos.z - e.y;
                    if (ddx * ddx + ddz * ddz < need * need) return false;
                }
            }
        return true;
    }

    /// 그 자리로 가는 길이 이 몸에게 **트여 있나** — 몇 걸음 앞까지 짚어 본다.
    /// ★한 점만 보면 나무 사이 틈을 통과한 뒤의 자리만 보고 "갈 수 있다"고 답한다.
    public static bool 갈수있나(Vector3 여기, Vector3 저기, float radius, float 걸음 = 1.2f)
    {
        var d = 저기 - 여기; d.y = 0f;
        float len = d.magnitude;
        if (len < 0.01f) return 들어가나(저기, radius);
        int n = Mathf.Clamp(Mathf.CeilToInt(len / Mathf.Max(0.3f, 걸음)), 1, 24);
        for (int i = 1; i <= n; i++)
            if (!들어가나(여기 + d * (i / (float)n), radius)) return false;
        return true;
    }

    /// 반경 radius 의 몸이 장애물을 뚫지 않게 밀어낸 위치
    public static Vector3 Resolve(Vector3 pos, float radius)
    {
        if (grid == null) return pos;
        radius = Mathf.Min(radius, 2.5f);          // 큰 몸도 틈을 지나가게 (끼임 방지)
        var k = Key(pos);
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (!grid.TryGetValue(new Vector2Int(k.x + dx, k.y + dz), out var list)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    var e = list[i];
                    float need = e.z + radius;
                    float ddx = pos.x - e.x, ddz = pos.z - e.y;
                    float dist = Mathf.Sqrt(ddx * ddx + ddz * ddz);
                    if (dist < need && dist > 1e-3f)
                    {
                        float push = need / dist;
                        pos.x = e.x + ddx * push;
                        pos.z = e.y + ddz * push;
                    }
                }
            }
        return pos;
    }
}
