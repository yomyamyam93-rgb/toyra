using UnityEngine;

/// 기절한 머리 위에서 도는 **동글뱅이** (2026-08-12 사용자 "머리위에 동글뱅이가 뜨면서
/// 기절한상태로 누워있게하는거지").
///
/// ★규칙 12(이펙트)를 지킨다 — **실제 동작을 따라가는 것만** 넣는다.
///   이건 장식이 아니라 **상태 표시**다: 지우면 「이 놈이 기절했는지」를 알 방법이 없다.
///   ☆초록 테두리는 가까이 가야 뜬다. 동글뱅이는 멀리서도 보여서, 떼 속에서 누가
///     엎어졌는지 한눈에 읽힌다 — 그게 「지금 저놈을 잡으러 갈까」를 고르게 만든다.
///
/// ★9-4(렉): 만들고 부수지 않는다. 한 번 만들어 두고 **껐다 켠다.**
///   알갱이 셋 · 재질 하나(모두 나눠 쓴다) · 매 프레임 하는 일은 각도 세 개 얹기뿐이다.
public class 기절표시 : MonoBehaviour
{
    [Tooltip("머리 위 얼마에 뜨나 — 몸 키에 대한 비율")] public float 높이비 = 1.15f;
    [Tooltip("도는 반지름 — 몸 키에 대한 비율")] public float 반지름비 = 0.30f;
    [Tooltip("한 바퀴 도는 데 걸리는 시간 (초)")] public float 한바퀴 = 1.1f;
    [Tooltip("알갱이 크기 — 몸 키에 대한 비율")] public float 알크기비 = 0.10f;

    static readonly Color C동글 = new Color(1f, 0.92f, 0.35f);   // 노란 별빛

    Transform[] 알;
    float 키, t;
    bool 켜짐;

    /// 붙이거나 찾아서 켠다 — `Critter` 가 기절할 때 부른다
    public static 기절표시 붙이기(Transform 뿌리, float 키)
    {
        var s = 뿌리.GetComponent<기절표시>();
        if (s == null) s = 뿌리.gameObject.AddComponent<기절표시>();
        s.키 = 키;
        s.만들기();
        return s;
    }

    void 만들기()
    {
        if (알 != null) return;
        알 = new Transform[3];
        float 크기 = Mathf.Max(0.05f, 키 * 알크기비);
        for (int i = 0; i < 알.Length; i++)
        {
            // ★납작한 상자 — 아이소에서 위에서 내려다보므로 눕혀야 「동그라미」로 읽힌다
            var g = Grey.Box(transform, Vector3.zero,
                             new Vector3(크기, 크기 * 0.35f, 크기), C동글, "기절_동글");
            var r = g.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            // 외곽선을 안 두른다 — 표시지 물체가 아니다
            if (g.GetComponent<NoOutline>() == null) g.AddComponent<NoOutline>();
            알[i] = g.transform;
        }
        켜기(false);
    }

    /// 보였다 감췄다 — **부수지 않는다** (9-4)
    public void 켜기(bool 켤까)
    {
        켜짐 = 켤까;
        if (알 == null) return;
        for (int i = 0; i < 알.Length; i++)
            if (알[i] != null) 알[i].gameObject.SetActive(켤까);
    }

    void LateUpdate()
    {
        if (!켜짐 || 알 == null) return;
        t += Time.deltaTime;

        float 각0 = t / Mathf.Max(0.05f, 한바퀴) * Mathf.PI * 2f;
        float r = 키 * 반지름비;
        float h = 키 * 높이비;

        for (int i = 0; i < 알.Length; i++)
        {
            if (알[i] == null) continue;
            float a = 각0 + i * Mathf.PI * 2f / 알.Length;
            // 원이 살짝 기울어 있어야 아이소에서 「돈다」로 읽힌다 (정원이면 납작해 보인다)
            알[i].localPosition = new Vector3(Mathf.Cos(a) * r,
                                              h + Mathf.Sin(a) * r * 0.35f,
                                              Mathf.Sin(a) * r * 0.7f);
            알[i].localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
        }
    }
}
