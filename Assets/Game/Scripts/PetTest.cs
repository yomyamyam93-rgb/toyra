using UnityEngine;

/// 펫 모델 보기 — **크기를 눈으로 정하려고 세워 두는 것.**
///
/// ★옛 프로젝트의 크기 규칙(등급별 목표 키 표)은 **가져오지 않았다** (2026-08-03 사용자
///   "기존 사이즈 규칙은 전부 제거, 새롭게 하자"). 여기서는 사람 1.8m 옆에 세워 놓고
///   눈으로 보고 정한다 — 숫자를 물려받으면 그 숫자가 왜 그런지 아무도 모르게 된다.
///
/// 실행 중에 `키` 값을 바꾸면 바로 반영된다. 정해지면 그 숫자를 종 데이터로 옮긴다.
public class PetTest : MonoBehaviour
{
    [System.Serializable]
    public class 표본
    {
        public string 이름 = "";
        public GameObject 모델;
        [Tooltip("이 키(m)에 맞춘다. 사람은 1.8m")] public float 키 = 1.5f;
    }

    public 표본[] 표본들;

    [Tooltip("캠프에서 이만큼 옆에 세운다 (m)")] public float 간격 = 3.5f;
    [Tooltip("사람 크기 비교용 기둥도 같이 세운다")] public bool 사람기둥 = true;

    Transform holder;
    float[] 지난키;

    void Start() { Build(); }

    void Build()
    {
        if (holder != null) Destroy(holder.gameObject);
        if (표본들 == null || 표본들.Length == 0) return;

        holder = new GameObject("펫표본").transform;
        var c = WorldGrid.Center;
        holder.position = new Vector3(c.x, 0f, c.z + 8f);

        지난키 = new float[표본들.Length];

        // 사람 1.8m — 옆에 세워 두지 않으면 큰지 작은지 알 수가 없다
        if (사람기둥)
            Grey.Box(holder, holder.position + Vector3.left * 간격 + Vector3.up * 0.9f,
                     new Vector3(0.5f, 1.8f, 0.35f), new Color(0.9f, 0.9f, 0.9f), "사람_1.8m");

        for (int i = 0; i < 표본들.Length; i++)
        {
            var s = 표본들[i];
            if (s.모델 == null) continue;
            var pos = holder.position + Vector3.right * (간격 * i);
            var go = Instantiate(s.모델, pos, Quaternion.identity, holder);
            go.name = string.IsNullOrEmpty(s.이름) ? s.모델.name : s.이름;
            Fit(go, s.키);
            지난키[i] = s.키;
        }
    }

    void Update()
    {
        // 실행 중에 키를 바꾸면 바로 반영 — 눈으로 보면서 정한다
        if (holder == null || 표본들 == null) return;
        for (int i = 0; i < 표본들.Length && i < 지난키.Length; i++)
        {
            if (Mathf.Approximately(지난키[i], 표본들[i].키)) continue;
            지난키[i] = 표본들[i].키;
            var t = FindChild(표본들[i]);
            if (t != null) Fit(t.gameObject, 표본들[i].키);
        }
    }

    Transform FindChild(표본 s)
    {
        string n = string.IsNullOrEmpty(s.이름) ? (s.모델 != null ? s.모델.name : "") : s.이름;
        foreach (Transform t in holder) if (t.name == n) return t;
        return null;
    }

    /// 모델의 실제 높이를 재서 원하는 키에 맞춘다 (모델마다 내보낸 크기가 제각각이다)
    static void Fit(GameObject go, float 키)
    {
        go.transform.localScale = Vector3.one;
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;

        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        if (b.size.y < 0.0001f) return;

        float k = 키 / b.size.y;
        go.transform.localScale = Vector3.one * k;

        // 발이 땅에 닿게 — 크기를 바꾼 뒤 다시 재서 그만큼 들어 올린다
        b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        go.transform.position += Vector3.up * (go.transform.position.y - b.min.y);
    }
}
