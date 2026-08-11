using System.Collections.Generic;
using UnityEngine;

/// 직소 조립 — **조각을 서로 이어 붙여** 매번 다른 장면을 만든다.
///
/// ★마인크래프트의 마을·전초기지·고대도시가 쓰는 방식이다 (2026-08-06 사용자
///   "이 방식으로 가고싶어"). 조각마다 **「이음」**을 심어 두고, 그 이음에
///   *"여기 붙을 것은 「복도」 주머니에서 뽑아라"* 를 적어 둔다. 붙인 조각에 또 이음이
///   있으면 거기서 또 뽑는다 — 다 채울 때까지.
///
/// ★★**타일을 크게 만들지 않는다.** 160m 짜리 지형을 통째로 만들어 두면 열 장만 돌아도
///   밑천이 드러나고 칸 경계에서 이가 안 맞는다. 작은 조각을 이어 붙이면 **조각 8개로도
///   수백 가지 폐허**가 나온다.
///
/// ─── 조각 하나의 약속 (프리팹 안에 **빈 오브젝트**로 심는다) ───
///
///   이음_복도    …  여기 붙일 것을 「복도」 주머니에서 뽑는다.
///                   ★이 표식의 **+Z(파란 화살표)가 바깥을 향해야** 한다.
///   슬롯_잡동사니 …  조립이 끝난 뒤 「잡동사니」 주머니에서 하나 뽑아 꽂는다.
///   비움          …  나중에 흩뿌리기가 이 근처를 피한다 (길·마당)
///
/// ─── 쉽게 만드는 규칙 셋 ───
///   ①조각 크기를 **격자 배수**로 (4m) — 안 맞추면 틈이 생겨 지옥이 된다
///   ②회전은 **90° 네 방향만** — 임의 각도는 겹침 검사가 몇 배로 복잡해진다
///   ③**씨앗 고정** — 같은 씨앗이면 같은 세계 (발하임처럼)
public static class 직소
{
    /// 조각 크기·이음 위치가 맞아떨어지는 기본 눈금 (m)
    public const float 격자 = 4f;

    /// 주머니 — 「복도」 라고 부르면 이 안의 조각 중 하나가 뽑힌다
    [System.Serializable]
    public class 주머니
    {
        public string 이름;
        public GameObject[] 조각들;
        /// ★조각마다의 **가중치** — 비우면 전부 같은 확률 (MC 템플릿 풀의 weight 자리).
        ///   큰 조각을 작은 조각과 같은 확률로 뽑으면 **「큰 것」이 특별하지 않게 된다.**
        public float[] 무게;
        /// 못 이어 붙일 때 끝을 막는 조각들 (없으면 그냥 열어 둔다)
        public GameObject[] 막음;
    }

    public class 설정
    {
        [Tooltip("조각을 최대 몇 개까지 놓나")] public int 조각한도 = 14;
        [Tooltip("시작 조각에서 몇 다리까지 뻗나")] public int 깊이한도 = 5;
        [Tooltip("이 반경(m) 밖으로는 안 짓는다 — 칸을 넘어가지 않게")] public float 반경 = 50f;
        [Tooltip("이음 하나마다 몇 번 찔러 보나")] public int 시도 = 10;
        [Tooltip("놓은 조각의 상자 부품을 못 지나가게 막나")] public bool 막기 = true;

        // ★★★**줄기 먼저, 곁가지 나중** (2026-08-06 사용자 — 로그라이크의 「시작→끝」 기법).
        //   이음을 **먼저 넣은 순서대로**(선입선출) 꺼내면 사방으로 고르게 퍼져 **덩어리**가 된다.
        //   굴이 덩어리면 「들어간다」는 감각이 없다 — 어디가 안쪽인지 모른다.
        //   ☆**마지막에 넣은 것부터**(후입선출) 꺼내면 한 방향으로 계속 파고들어 **긴 줄기**가 된다.
        //     그게 곧 입구→가장 깊은 방이다. 줄기를 다 뻗은 뒤에 나머지를 곁가지로 붙인다.
        //   ★막다른 곁가지가 곧 「안쪽 보상」의 자리다 (광석·둥지·자는 짐승).
        [Tooltip("이 개수까지는 한 줄기로 파고든다 (0 이면 옛 방식 — 사방으로 퍼진 덩어리)")]
        public int 줄기 = 0;
    }

    // ───────────────────────────────── 짓기

    /// `시작` 주머니에서 첫 조각을 뽑아 `자리` 에 놓고, 이음을 따라 뻗어 나간다.
    /// 놓은 조각 수를 돌려준다.
    public static int 짓기(Transform 부모, IList<주머니> 주머니들, string 시작,
                           Vector3 자리, int 씨앗, 설정 s = null)
    {
        s ??= new 설정();
        var 표 = new Dictionary<string, 주머니>();
        foreach (var p in 주머니들) if (p != null && !string.IsNullOrEmpty(p.이름)) 표[p.이름] = p;
        if (!표.ContainsKey(시작)) { Debug.LogWarning("[직소] 시작 주머니 없음: " + 시작); return 0; }

        var 주사위 = new System.Random(씨앗);
        var 놓인상자 = new List<Bounds>();
        var 놓인것 = new List<GameObject>();
        // ★줄기(후입선출) 구간과 곁가지(선입선출) 구간을 한 목록으로 다룬다
        var 대기 = new List<(Transform 이음, int 깊이)>();

        // ── 첫 조각
        var 첫조각 = 뽑기(표[시작].조각들, 표[시작].무게, 주사위);
        if (첫조각 == null) return 0;
        var 첫 = 놓기(부모, 첫조각, 자리, 90f * 주사위.Next(4));
        놓인상자.Add(상자재기(첫));
        놓인것.Add(첫);
        foreach (var m in 이음들(첫.transform)) 대기.Add((m, 1));

        // ── 이음을 따라 뻗는다
        while (대기.Count > 0 && 놓인것.Count < s.조각한도)
        {
            // ★줄기 구간이면 **맨 뒤**에서 꺼낸다 (한 방향으로 파고든다),
            //   줄기를 다 뻗었으면 **맨 앞**에서 꺼낸다 (남은 문에 곁가지를 고르게 붙인다)
            bool 줄기중 = 놓인것.Count < s.줄기;
            int 뽑을자리 = 줄기중 ? 대기.Count - 1 : 0;
            var (이음, 깊이) = 대기[뽑을자리];
            대기.RemoveAt(뽑을자리);
            if (이음 == null) continue;
            string 주머니이름 = 이름떼기(이음.name);
            if (!표.TryGetValue(주머니이름, out var 풀)) continue;

            // 깊이 한도에 닿았으면 **막음 조각으로 마감**한다 — 벽이 뻥 뚫린 채 끝나지 않게
            bool 붙였다 = 깊이 <= s.깊이한도 && 붙이기(부모, 풀.조각들, 풀.무게, 이음, 주사위, s,
                                                     놓인상자, 놓인것, 대기, 깊이 + 1);
            if (!붙였다)
                붙이기(부모, 풀.막음, null, 이음, 주사위, s, 놓인상자, 놓인것, null, 깊이 + 1);
        }

        // ── 슬롯 채우기 + 막기
        foreach (var g in 놓인것)
        {
            슬롯채우기(g, 표, 주사위);
            if (s.막기) 막기(g);
        }
        return 놓인것.Count;
    }

    // ───────────────────────────────── 한 조각 붙이기

    static bool 붙이기(Transform 부모, GameObject[] 후보들, float[] 무게, Transform 이음, System.Random 주사위,
                       설정 s, List<Bounds> 놓인상자, List<GameObject> 놓인것,
                       List<(Transform, int)> 대기, int 다음깊이)
    {
        if (후보들 == null || 후보들.Length == 0) return false;

        var 밖 = 이음.forward; 밖.y = 0f; 밖.Normalize();

        for (int 회 = 0; 회 < s.시도; 회++)
        {
            var 조각 = 뽑기(후보들, 무게, 주사위);
            if (조각 == null) continue;

            // 이 조각이 가진 이음 중 하나를 **맞댈 짝**으로 고른다
            var 짝들 = 이음들(조각.transform);
            if (짝들.Count == 0) continue;
            var 짝 = 짝들[주사위.Next(짝들.Count)];

            // ★90° 네 방향 중, 짝의 앞이 정확히 **반대쪽**을 보게 되는 각을 찾는다
            var 짝앞 = 조각.transform.InverseTransformDirection(짝.forward); 짝앞.y = 0f;
            if (짝앞.sqrMagnitude < 1e-4f) continue;
            짝앞.Normalize();

            float yaw = float.NaN;
            for (int k = 0; k < 4; k++)
            {
                var q = Quaternion.Euler(0f, 90f * k, 0f);
                if (Vector3.Dot(q * 짝앞, -밖) > 0.9f) { yaw = 90f * k; break; }
            }
            if (float.IsNaN(yaw)) continue;

            var rot = Quaternion.Euler(0f, yaw, 0f);
            var 짝로컬 = 조각.transform.InverseTransformPoint(짝.position);
            var 자리 = 이음.position - rot * 짝로컬;

            var 상자 = 상자재기(조각, 자리, yaw);
            if (반경밖(상자, 놓인상자, s)) continue;
            if (겹침(상자, 놓인상자)) continue;

            var go = 놓기(부모, 조각, 자리, yaw);
            놓인상자.Add(상자);
            놓인것.Add(go);

            // 맞댄 짝 말고 나머지 이음만 대기줄로 (이미 쓴 문은 다시 안 쓴다)
            if (대기 != null)
            {
                var 새이음 = 이음들(go.transform);
                int 짝차례 = 짝들.IndexOf(짝);
                for (int i = 0; i < 새이음.Count; i++)
                    if (i != 짝차례) 대기.Add((새이음[i], 다음깊이));
            }
            // 맞댄 문 표식은 지운다 — 남겨 두면 나중에 또 쓴다
            Object.Destroy(이음.gameObject);
            return true;
        }
        return false;
    }

    // ───────────────────────────────── 잔손

    /// 가중치가 있으면 그대로, 없으면 균등하게 하나 뽑는다
    static GameObject 뽑기(GameObject[] 들, float[] 무게, System.Random 주사위)
    {
        if (들 == null || 들.Length == 0) return null;
        for (int i = 0; i < 8; i++)
        {
            int k = 절차.가중치로((float)주사위.NextDouble(), 무게, 들.Length);
            if (k >= 0 && 들[k] != null) return 들[k];
        }
        return null;
    }

    static GameObject 놓기(Transform 부모, GameObject 조각, Vector3 자리, float yaw)
    {
        var go = Object.Instantiate(조각, 자리, Quaternion.Euler(0f, yaw, 0f), 부모);
        go.name = 조각.name;
        go.SetActive(true);
        return go;
    }

    /// 이 조각이 가진 「이음」 표식들 (자식 이름이 `이음_…`)
    static List<Transform> 이음들(Transform 뿌리)
    {
        var 목록 = new List<Transform>();
        foreach (var t in 뿌리.GetComponentsInChildren<Transform>(true))
            if (t != 뿌리 && t.name.StartsWith("이음_")) 목록.Add(t);
        return 목록;
    }

    static string 이름떼기(string 표식이름)
    {
        int i = 표식이름.IndexOf('_');
        return i < 0 ? 표식이름 : 표식이름.Substring(i + 1);
    }

    /// 조각의 **덩치 상자** — 겹침 검사에 쓴다.
    /// `직소조각` 이 붙어 있으면 그 값을, 없으면 그리는 것들을 재서 쓴다.
    static Bounds 상자재기(GameObject 조각, Vector3 자리, float yaw)
    {
        Vector3 중심, 크기;
        var d = 조각.GetComponent<직소조각>();
        if (d != null) { 중심 = d.상자중심; 크기 = d.상자크기; }
        else
        {
            var b = 그린것상자(조각);
            중심 = b.center - 조각.transform.position; 크기 = b.size;
        }
        // 90°·270° 면 가로세로가 바뀐다
        int k = Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) / 90f) & 3;
        if (k == 1 || k == 3) { 크기 = new Vector3(크기.z, 크기.y, 크기.x); 중심 = new Vector3(중심.z, 중심.y, -중심.x); }
        else if (k == 2) 중심 = new Vector3(-중심.x, 중심.y, -중심.z);
        return new Bounds(자리 + 중심, 크기);
    }

    static Bounds 상자재기(GameObject 놓인것)
    {
        var d = 놓인것.GetComponent<직소조각>();
        if (d != null) return new Bounds(놓인것.transform.position + 놓인것.transform.rotation * d.상자중심, d.상자크기);
        return 그린것상자(놓인것);
    }

    static Bounds 그린것상자(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.one * 격자);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    /// ★살짝 줄여서 견준다 — 딱 맞닿은 조각을 「겹쳤다」고 보면 아무것도 못 붙인다
    static bool 겹침(Bounds 새것, List<Bounds> 놓인)
    {
        var a = new Bounds(새것.center, 새것.size * 0.92f);
        foreach (var b in 놓인)
        {
            var c = new Bounds(b.center, b.size * 0.92f);
            if (a.min.x < c.max.x && c.min.x < a.max.x && a.min.z < c.max.z && c.min.z < a.max.z) return true;
        }
        return false;
    }

    static bool 반경밖(Bounds 새것, List<Bounds> 놓인, 설정 s)
    {
        if (놓인.Count == 0) return false;
        var 가운데 = 놓인[0].center;
        var d = 새것.center - 가운데; d.y = 0f;
        return d.magnitude > s.반경;
    }

    /// `슬롯_…` 표식마다 그 주머니에서 하나 뽑아 꽂는다 — **회전도 랜덤**
    static void 슬롯채우기(GameObject 놓인것, Dictionary<string, 주머니> 표, System.Random 주사위)
    {
        var 슬롯 = new List<Transform>();
        foreach (var t in 놓인것.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("슬롯_")) 슬롯.Add(t);

        foreach (var t in 슬롯)
        {
            if (!표.TryGetValue(이름떼기(t.name), out var 풀)) continue;
            var 것 = 뽑기(풀.조각들, 풀.무게, 주사위);
            if (것 == null) continue;
            var go = Object.Instantiate(것, t.position, Quaternion.Euler(0f, (float)(주사위.NextDouble() * 360.0), 0f), 놓인것.transform);
            go.name = 것.name;
            go.SetActive(true);
            float s = 0.85f + (float)주사위.NextDouble() * 0.35f;
            go.transform.localScale *= s;
        }
    }

    /// 조각 안의 덩어리들을 못 지나가게 막는다 (길찾기 없이 밀어내는 방식)
    static void 막기(GameObject 놓인것)
    {
        foreach (var r in 놓인것.GetComponentsInChildren<MeshRenderer>(true))
        {
            var n = r.gameObject.name;
            // ★「덮개」는 동굴 지붕이다 (2026-08-11) — 공중에 떠 있으니 발은 안 걸려야 한다
            if (n.StartsWith("바닥") || n.StartsWith("이음") || n.StartsWith("슬롯") || n.StartsWith("덮개")) continue;
            var b = r.bounds;
            if (b.size.y < 0.6f) continue;                       // 납작한 건 안 막는다
            Blocker.Add(new Vector3(b.center.x, 0f, b.center.z), Mathf.Max(b.size.x, b.size.z) * 0.45f);
        }
    }
}
