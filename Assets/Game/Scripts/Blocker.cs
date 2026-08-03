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
