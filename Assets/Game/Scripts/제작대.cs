using System.Collections.Generic;
using UnityEngine;

/// 제작대 — **가죽을 다루는 자리** (2026-08-12 사용자 "가죽이 나오게끔 해서 제작대에서
/// 제작할 수 있게해줄래? 제작대는또 나무나 돌같은 재료로 만들 수 있게해주고,
/// 제작대를 만들려면 또 제작 망치같은게 필요하게해주고").
///
/// ★기획 5-4 의 「작업대가 거점을 만든다」 자리다 — 모닥불(요리·불) 옆에 무두질대가 선다.
///   가방을 만들려면 여기 와야 하므로, 캠프가 **규칙이 아니라 이득으로** 굵어진다.
///
/// ★모닥불과 같은 꼴로 만든다: 정적 목록 + `가까운것`. 제작창이 그걸 보고 목록을 바꾼다.
///   ☆다만 훨씬 단순하다 — 불처럼 재료를 붓고 타는 단계가 없다. 세우면 바로 쓴다.
///
/// ★9-4(렉): 세상에 몇 개 안 생기고(내가 짓는 것뿐) `가까운것` 은 그 목록만 훑는다.
///   자리는 등록할 때 한 번 적어 둔다 — Transform 을 매번 읽지 않는다 (2026-08-11 교훈).
public class 제작대 : MonoBehaviour
{
    public static readonly List<제작대> All = new List<제작대>();

    [HideInInspector] public Vector3 자리;

    void OnEnable() { All.Add(this); 자리 = transform.position; }
    void OnDisable() { All.Remove(this); }

    /// 사람 가까이 있는 제작대 — 제작창이 「가죽 제작법을 보여줄까」를 이걸로 정한다
    public static 제작대 가까운것(Vector3 에서, float 거리)
    {
        제작대 best = null; float bd = 거리 * 거리;
        for (int i = All.Count - 1; i >= 0; i--)
        {
            var t = All[i];
            if (t == null) continue;
            float dx = t.자리.x - 에서.x, dz = t.자리.z - 에서.z;
            float d2 = dx * dx + dz * dz;
            if (d2 > bd) continue;
            bd = d2; best = t;
        }
        return best;
    }

    // ── 세우기 ─────────────────────────────────────────────────
    static readonly Color C상판 = new Color(0.46f, 0.33f, 0.20f);
    static readonly Color C다리 = new Color(0.34f, 0.24f, 0.15f);
    static readonly Color C돌 = new Color(0.45f, 0.44f, 0.42f);

    /// 앞에 하나 세운다 — 상판 + 다리 넷 + 숫돌.
    /// ★표면은 격자다 (11-1) — `Grey.격자Mat` 를 쓴다. 새로 만드는 것은 전부 이 규칙을 입는다
    public static 제작대 세우기(Vector3 at)
    {
        var go = new GameObject("제작대");
        go.transform.position = new Vector3(at.x, 0f, at.z);

        const float 폭 = 1.6f, 깊 = 0.9f, 높 = 0.95f, 다리굵기 = 0.14f;

        // 상판
        var 판 = Grey.Box(go.transform, new Vector3(at.x, 높, at.z),
                          new Vector3(폭, 0.14f, 깊), C상판, "제작대_상판");
        판.GetComponent<MeshRenderer>().sharedMaterial = Grey.격자Mat(C상판);

        // 다리 넷
        for (int i = 0; i < 4; i++)
        {
            float sx = (i & 1) == 0 ? -1f : 1f;
            float sz = (i & 2) == 0 ? -1f : 1f;
            var p = new Vector3(at.x + sx * (폭 * 0.5f - 0.12f), 높 * 0.5f,
                                at.z + sz * (깊 * 0.5f - 0.12f));
            var 다리 = Grey.Box(go.transform, p, new Vector3(다리굵기, 높, 다리굵기), C다리, "제작대_다리");
            다리.GetComponent<MeshRenderer>().sharedMaterial = Grey.격자Mat(C다리);
        }

        // 숫돌 — 「가죽을 다루는 자리」로 읽히게 얹는다
        var 돌 = Grey.Box(go.transform, new Vector3(at.x + 폭 * 0.28f, 높 + 0.13f, at.z),
                          new Vector3(0.34f, 0.12f, 0.26f), C돌, "제작대_숫돌");
        돌.GetComponent<MeshRenderer>().sharedMaterial = Grey.격자Mat(C돌);

        // 몸으로 막는다 — 뚫고 지나가면 물건으로 안 읽힌다
        Blocker.Add(new Vector3(at.x, 0f, at.z), 0.75f);

        return go.AddComponent<제작대>();
    }
}
