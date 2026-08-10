using System.Collections.Generic;
using UnityEngine;

/// 땅에 쌓인 것 — **컨테이너 창을 따로 만들지 않는다.**
///
/// ★모닥불 옆에 그냥 쌓아 둔다. 무더기가 **눈에 보이고**, 가까이 가면 `인벤창`(Tab) 의
///   오른쪽에 그 속이 뜬다. 상자·창고 UI 를 만들 이유가 없다.
/// ★9-0(인과와 행위): 내려놓은 것은 **거기 그대로 있다.** 저절로 사라지지 않는다.
public class 땅무더기 : MonoBehaviour
{
    public static readonly List<땅무더기> All = new List<땅무더기>();

    /// 무더기는 무게 한도가 없다 (땅이 다 받는다)
    public readonly 인벤 속 = new 인벤 { 한도 = 0f };

    [Tooltip("이 거리 안이면 같은 무더기로 친다 (m)")] public static float 뭉치는거리 = 1.6f;

    Transform 더미;
    int 지난개수 = -1;

    /// ★프롭(막대·돌멩이)이 이미 모양이면 더미 상자를 안 얹는다
    [HideInInspector] public bool 프롭모양;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    // ★★`Update` 를 안 쓴다 — 줍이가 수백 개 깔리는데, 아무것도 안 하는 `Update` 도
    //   수만 개면 렉이다 (toyrassic 실측 38.9ms). 내용이 바뀌는 순간(넣기·꺼내기·떨구기)에
    //   부르는 쪽이 `갱신()` 을 부른다.
    public void 갱신()
    {
        // 빈 무더기는 사라진다 (다 주워 갔으면 자리를 남길 이유가 없다)
        if (속.것들.Count == 0) { Destroy(gameObject); return; }
        if (프롭모양) return;
        if (속.것들.Count != 지난개수) { 지난개수 = 속.것들.Count; 모양갱신(); }
    }

    /// 색칠한 상자 몇 개로 「쌓여 있다」를 보여 준다 (1장 — 상자 먼저, 모델 나중)
    void 모양갱신()
    {
        if (더미 == null)
        {
            더미 = new GameObject("더미").transform;
            더미.SetParent(transform, false);
        }
        for (int i = 더미.childCount - 1; i >= 0; i--) Destroy(더미.GetChild(i).gameObject);

        int 보일수 = Mathf.Min(5, 속.것들.Count);
        for (int i = 0; i < 보일수; i++)
        {
            var it = 속.것들[i];
            float a = i / (float)Mathf.Max(1, 보일수) * Mathf.PI * 2f;
            var g = Grey.Box(더미, Vector3.zero, new Vector3(0.26f, 0.14f, 0.26f),
                             it.종 != null ? it.종.색 : Color.gray, "짐", 0f, a * Mathf.Rad2Deg);
            g.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.16f, 0.07f + i * 0.05f,
                                                    Mathf.Sin(a) * 0.16f);
        }
    }

    // ══════════════════════════════════════════════════════════

    /// 그 자리의 무더기를 찾거나 새로 만든다
    public static 땅무더기 여기(Vector3 자리)
    {
        var 가까운 = 가까운것(자리, 뭉치는거리);
        if (가까운 != null) return 가까운;

        var go = new GameObject("땅무더기");
        go.transform.position = new Vector3(자리.x, 0f, 자리.z);
        return go.AddComponent<땅무더기>();
    }

    public static 땅무더기 가까운것(Vector3 자리, float 거리)
    {
        땅무더기 best = null; float bd = 거리 * 거리;
        for (int i = All.Count - 1; i >= 0; i--)
        {
            var p = All[i];
            if (p == null) continue;
            var v = p.transform.position - 자리; v.y = 0f;
            float d2 = v.sqrMagnitude;
            if (d2 > bd) continue;
            bd = d2; best = p;
        }
        return best;
    }

    /// 못 든 것을 발밑에 떨군다 (`Stock.Add` 가 부른다)
    public static void 떨구기(아이템종 종, int 개수, Vector3 자리)
    {
        if (종 == null || 개수 <= 0) return;
        var 무 = 여기(자리); 무.속.넣기(종, 개수); 무.갱신();
    }

    /// 덩이 하나를 내려놓는다
    public static void 내려놓기(아이템 it, Vector3 자리)
    {
        if (it == null) return;
        var 무 = 여기(자리); 무.속.받기(it); 무.갱신();
    }

    /// ★줍이 — 프롭(막대·돌멩이) 하나가 곧 무더기 하나다. 다 집으면 프롭째 사라진다
    public static 땅무더기 줍이(아이템종 종, int 개수, GameObject 프롭)
    {
        if (종 == null || 프롭 == null) return null;
        var p = 프롭.AddComponent<땅무더기>();
        p.프롭모양 = true;
        p.속.넣기(종, 개수);
        return p;
    }
}
