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

    // ★★★**사체 같은 「제 자리를 가진 통」** (2026-08-12 — 갈무리한 것이 사체에 쌓인다).
    //   그냥 땅무더기는 비면 사라지는 게 맞지만, 사체는 **아직 갈무리가 남았을 수 있다.**
    //   비었다고 몸이 사라지면 「다 가져갔더니 시체가 증발」이 된다 → 파괴는 `Carcass` 가 정한다.
    [HideInInspector] public bool 안사라짐;

    /// 창에 뭐라고 뜨나 — 비어 있으면 「땅바닥」
    [HideInInspector] public string 이름표;

    // ★★몸집을 재서 넣는다 — 누운 티라는 4.5m 라 **중심 한 점**으로 재면 옆에 붙어 서도
    //   창에 안 잡힌다 (`Harvest.반경` 이 겪은 것과 같은 함정, 2026-08-11).
    [HideInInspector] public float 반경;

    // ★자리를 기억해 둔다 — `Harvest.자리` 와 같은 이유 (2026-08-11).
    //   땅에 떨군 것은 안 움직인다. 세계에 9,882개가 깔려 있어 매번 훑으면 비싸다
    [HideInInspector] public Vector3 자리;

    void OnEnable() { All.Add(this); 자리 = transform.position; }
    void OnDisable() { All.Remove(this); }

    // ★★`Update` 를 안 쓴다 — 줍이가 수백 개 깔리는데, 아무것도 안 하는 `Update` 도
    //   수만 개면 렉이다 (toyrassic 실측 38.9ms). 내용이 바뀌는 순간(넣기·꺼내기·떨구기)에
    //   부르는 쪽이 `갱신()` 을 부른다.
    public void 갱신()
    {
        // 빈 무더기는 사라진다 (다 주워 갔으면 자리를 남길 이유가 없다)
        // ★단 사체처럼 「제 몸을 가진 통」은 제가 정한다 (`안사라짐`)
        if (속.것들.Count == 0) { if (!안사라짐) Destroy(gameObject); return; }
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
    /// ★사체 같은 「제 몸을 가진 통」에는 안 섞는다 — 옆에 내려놓은 짐이 시체 속으로
    ///   들어가 버리면, 시체가 썩을 때 내 짐도 같이 사라진다
    public static 땅무더기 여기(Vector3 자리)
    {
        var 가까운 = 가까운것(자리, 뭉치는거리, true);
        if (가까운 != null) return 가까운;

        var go = new GameObject("땅무더기");
        go.transform.position = new Vector3(자리.x, 0f, 자리.z);
        return go.AddComponent<땅무더기>();
    }

    /// ★몸집을 봐 준다 — 누운 사체는 중심이 멀어도 옆구리가 코앞이다
    public static 땅무더기 가까운것(Vector3 자리, float 거리, bool 땅만 = false)
    {
        땅무더기 best = null; float bd = 거리;
        for (int i = All.Count - 1; i >= 0; i--)
        {
            var p = All[i];
            if (p == null || (땅만 && p.안사라짐)) continue;
            var v = p.자리 - 자리; v.y = 0f;      // ★기억해 둔 자리 (Transform 을 안 읽는다)
            float 여유 = 거리 + p.반경;
            if (v.sqrMagnitude > 여유 * 여유) continue;          // ①싼 거르개 (제곱끼리)
            float d = Mathf.Max(0f, v.magnitude - p.반경);       // ②후보만 제대로 잰다
            if (d > bd) continue;
            bd = d; best = p;
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
