using System.Collections.Generic;
using UnityEngine;

/// **실루엣 타격 판정** (2026-08-07 사용자 "머리통도 맞는 영역이어야 하지 않나?
/// 바운더리가 아니라 실제 팻 실루엣으로는 못 하나?").
///
/// ★전에는 「중심점 + 반지름」 원기둥이었다 — 티라노의 내민 머리, 사슴의 목은
///   원 밖이라 **보이는 몸을 때렸는데 빗나갔다.** 그림과 판정이 어긋나면 전투를 못 읽는다.
///
/// ★완전한 메시 판정은 과하다. 업계 실용해는 **뼈 = 실루엣의 골격**이다:
///   스킨드메시의 `bones` 배열(마리당 20~40개)이 이미 몸 전체에 퍼져 있으니,
///   그 위치들을 판정점으로 쓰면 머리·목·꼬리까지 몸을 따라간다.
///   검사는 **휘두르는 순간에만** 도니 비용은 사실상 0 이다.
public static class 실루엣판정
{
    static readonly List<Vector3> 점들 = new List<Vector3>(64);

    /// 이 몸의 판정점들 — 뼈 위치 전부. 뼈가 없으면(상자 몸) 중심 하나.
    /// ★돌려주는 리스트는 공용 버퍼다 — 다음 호출 전까지만 유효하다.
    public static List<Vector3> 몸점들(Transform 뿌리)
    {
        점들.Clear();
        foreach (var smr in 뿌리.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var bs = smr.bones;
            if (bs == null) continue;
            for (int i = 0; i < bs.Length; i++)
                if (bs[i] != null) 점들.Add(bs[i].position);
        }
        if (점들.Count == 0) 점들.Add(뿌리.position);
        return 점들;
    }

    /// 부채꼴 안에 이 몸의 **아무 점이라도** 드는가 (수평 기준).
    /// `여유` = 뼈는 선이고 살은 두께가 있으니 그만큼 더 봐 준다 (0.15~0.3m 권장)
    public static bool 부채꼴에맞나(Transform 몸, Vector3 from, Vector3 시선,
                                    float 사거리, float 각도, float 여유 = 0.2f)
    {
        float cos = Mathf.Cos(Mathf.Deg2Rad * 각도 * 0.5f);
        foreach (var pt in 몸점들(몸))
        {
            var v = pt - from; v.y = 0f;
            float d = v.magnitude;
            if (d > 사거리 + 여유) continue;
            if (d > 0.01f && Vector3.Dot(v / d, 시선) < cos) continue;
            return true;
        }
        return false;
    }

    /// 쓸고 지나간 각도 구간(lo~hi°, 시선 기준 부호 각)에 드는가 — 휘두름 판정용
    public static bool 쓸기에맞나(Transform 몸, Vector3 from, Vector3 시선,
                                  float 사거리, float lo, float hi, float 여유 = 0.2f)
    {
        foreach (var pt in 몸점들(몸))
        {
            var v = pt - from; v.y = 0f;
            float d = v.magnitude;
            if (d > 사거리 + 여유) continue;
            float ang = Vector3.SignedAngle(시선, d > 0.01f ? v / d : 시선, Vector3.up);
            if (ang < lo || ang > hi) continue;
            return true;
        }
        return false;
    }

    /// 거리만 보는 판정 (펫의 물기용) — 아무 점이라도 reach 안이면 문다
    public static bool 닿나(Transform 몸, Vector3 from, float reach, float 여유 = 0.2f)
    {
        foreach (var pt in 몸점들(몸))
        {
            var v = pt - from; v.y = 0f;
            if (v.magnitude <= reach + 여유) return true;
        }
        return false;
    }
}
