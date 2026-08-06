using UnityEngine;

/// 직소 조각 하나의 **덩치**를 적어 두는 쪽지.
///
/// ★조각 프리팹에 붙인다. 안 붙여도 돌아가지만(그리는 것을 재서 쓴다), 붙이면
///   **의도한 자리**를 정확히 쓸 수 있다 — 지붕이 튀어나오거나 잔가지가 뻗은 조각은
///   그리는 것을 재면 실제보다 크게 나와서 이웃이 붙을 자리가 없어진다.
///
/// ★상자는 **격자(4m) 배수**로 적는 게 좋다. 그래야 이가 딱 맞는다.
public class 직소조각 : MonoBehaviour
{
    [Tooltip("조각 원점에서 상자 가운데까지 (m)")]
    public Vector3 상자중심 = new Vector3(0f, 2f, 0f);

    [Tooltip("조각이 차지하는 상자 크기 (m) — 격자 4m 배수를 권한다")]
    public Vector3 상자크기 = new Vector3(12f, 4f, 12f);

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.35f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(상자중심, 상자크기);

        // 이음 표식의 방향을 보여 준다 — **+Z 가 바깥**이어야 한다
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        Gizmos.matrix = Matrix4x4.identity;
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("이음_")) continue;
            Gizmos.DrawLine(t.position, t.position + t.forward * 2f);
            Gizmos.DrawWireSphere(t.position, 0.4f);
        }
    }
}
