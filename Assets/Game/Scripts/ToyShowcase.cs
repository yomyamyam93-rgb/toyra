using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// 장난감 진열 — 옛 프로젝트에서 가져온 펫들을 **인게임에서** 쭉 세워 놓고 본다.
///
/// ★씬 창이 아니라 게임 화면이어야 하는 이유: 픽셀 마감·시야·낮밤이 전부 걸린 상태로
///   봐야 진짜 인상을 알 수 있다. 씬 창에서는 예뻤는데 게임에서는 안 읽히는 일이 흔하다.
///
/// F1 로 세우고 치운다. `Resources/toys` 에 있는 모델을 전부 불러온다.
public class ToyShowcase : MonoBehaviour
{
    [Tooltip("F1 을 누르면 세운다")] public bool 시작할때세우기 = false;
    [Tooltip("한 줄에 몇 마리")] public int 한줄 = 10;
    [Tooltip("좌우 간격 (m)")] public float 간격 = 4f;
    [Tooltip("줄 간격 (m)")] public float 줄간격 = 5f;
    [Tooltip("전부 이 키(m)로 맞춘다 — 원래 크기가 제각각이라 그대로 두면 비교가 안 된다")]
    public float 키 = 2f;
    [Tooltip("캠프에서 이만큼 앞에 세운다 (m)")] public float 앞으로 = 14f;

    Transform 진열;

    void Start() { if (시작할때세우기) 세우기(); }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null && k.f1Key.wasPressedThisFrame)
        {
            if (진열 != null) 치우기(); else 세우기();
        }
#endif
    }

    public void 세우기()
    {
        치우기();
        var 모델 = Resources.LoadAll<GameObject>("toys");
        if (모델 == null || 모델.Length == 0)
        {
            Debug.LogWarning("[진열] Resources/toys 에 모델이 없다.");
            return;
        }

        진열 = new GameObject("장난감_진열").transform;
        var c = WorldGrid.Center;
        진열.position = new Vector3(c.x - 간격 * (한줄 - 1) * 0.5f, 0f, c.z + 앞으로);

        // 사람 1.8m — 옆에 없으면 큰지 작은지 알 수가 없다
        Grey.Box(진열, 진열.position + Vector3.left * 간격 + Vector3.up * 0.9f,
                 new Vector3(0.5f, 1.8f, 0.35f), new Color(0.9f, 0.9f, 0.9f), "사람_1.8m");

        var 이름들 = new List<string>();
        for (int i = 0; i < 모델.Length; i++)
        {
            var pos = 진열.position + new Vector3((i % 한줄) * 간격, 0f, (i / 한줄) * 줄간격);
            var g = Instantiate(모델[i], pos, Quaternion.identity, 진열);
            g.name = 모델[i].name;
            맞춤(g, 키);
            이름들.Add(모델[i].name);
        }
        Debug.Log($"[진열] {모델.Length}마리 — F1 로 치웁니다.");
    }

    public void 치우기()
    {
        if (진열 == null) return;
        Destroy(진열.gameObject);
        진열 = null;
    }

    /// 모델마다 내보낸 크기가 제각각이라 키를 맞춰야 나란히 비교가 된다
    static void 맞춤(GameObject g, float 키)
    {
        g.transform.localScale = Vector3.one;
        var rs = g.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;

        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        if (b.size.y > 0.0001f) g.transform.localScale = Vector3.one * (키 / b.size.y);

        b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        g.transform.position += Vector3.up * (g.transform.position.y - b.min.y);
    }
}
