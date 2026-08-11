using System.Collections.Generic;
using UnityEngine;

/// 회색상자 부품 — 색칠한 상자를 만드는 공용 도구.
///
/// ★지금 세상은 전부 상자다. 진짜 모델은 나중에 하나씩 갈아끼운다 (`WorldGen` 의
///   「교체 자리」 참고). 상자 단계에서 **시스템이 먼저 굴러가는지**를 본다.
public static class Grey
{
    static readonly Dictionary<int, Material> mats = new Dictionary<int, Material>();

    /// 같은 색이면 재질 하나를 나눠 쓴다 (드로콜 절약 + GPU 인스턴싱)
    public static Material Mat(Color c)
    {
        int key = Mathf.RoundToInt(c.r * 255) | (Mathf.RoundToInt(c.g * 255) << 8) | (Mathf.RoundToInt(c.b * 255) << 16);
        if (mats.TryGetValue(key, out var m) && m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = c;
        m.SetFloat("_Smoothness", 0.08f);
        m.enableInstancing = true;
        mats[key] = m;
        return m;
    }

    // ★격자 재질 — 동굴의 바닥·벽·덮개가 입는다 (2026-08-11 사용자 "바닥이고 벽이고 모두
    //   격자형식으로"). 땅의 칸 규칙(1.4m·밝기 흔들)을 상자 표면에 그대로 얹은 셰이더.
    //   셰이더가 없으면 조용히 민무늬로 돌아간다 (은퇴는 삭제가 아니라 스위치)
    static readonly Dictionary<int, Material> 격자mats = new Dictionary<int, Material>();

    public static Material 격자Mat(Color c)
    {
        int key = Mathf.RoundToInt(c.r * 255) | (Mathf.RoundToInt(c.g * 255) << 8) | (Mathf.RoundToInt(c.b * 255) << 16);
        if (격자mats.TryGetValue(key, out var m) && m != null) return m;
        var sh = Shader.Find("Toyra/격자상자");
        if (sh == null) return Mat(c);
        m = new Material(sh);
        m.SetColor("_BaseColor", c);
        격자mats[key] = m;
        return m;
    }

    /// 색칠한 상자 하나. `blockR > 0` 이면 뚫고 지나갈 수 없다
    public static GameObject Box(Transform parent, Vector3 center, Vector3 size, Color color,
                                 string name, float blockR = 0f, float yaw = 0f)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent, true);
        g.transform.localScale = size;
        g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        g.transform.position = center;
        Strip(g);
        g.GetComponent<MeshRenderer>().sharedMaterial = Mat(color);
        if (blockR > 0f) Blocker.Add(new Vector3(center.x, 0f, center.z), blockR);
        return g;
    }

    /// 프리미티브에 딸려 오는 콜라이더를 뗀다 (충돌은 Blocker 가 한다)
    public static void Strip(GameObject g)
    {
        var col = g.GetComponent<Collider>();
        if (col == null) return;
        if (Application.isPlaying) Object.Destroy(col); else Object.DestroyImmediate(col);
    }

    /// 이름에서 항상 같은 색을 뽑는다
    public static Color ColorFor(string name)
    {
        int h = 0;
        if (!string.IsNullOrEmpty(name)) foreach (var ch in name) h = h * 31 + ch;
        return Color.HSVToRGB(Mathf.Abs(h % 997) / 997f, 0.55f, 0.8f);
    }
}
