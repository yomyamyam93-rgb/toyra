using UnityEngine;

/// 사체 — **죽는다고 고기가 생기지 않는다. 갈무리해야 생긴다.**
///
/// ★원칙 (2026-08-03 사용자): *"갈무리가 있어야지. 모든 행동에는 인과관계와 행동이
///   있어야 한다."* 전에는 죽는 순간 고기가 인벤토리로 순간이동했다 — 행위가 없었다.
///
/// ★그래서 생기는 것들:
///   · 갈무리하는 동안 **무방비**다 → 떼 한복판에서는 못 챙긴다
///   · 시간이 지나면 **썩는다** → 잡아만 놓고 다니면 헛수고
///   · 나중에 **청소부**가 시체에 모이게 되면, 사체가 곧 미끼가 된다
[RequireComponent(typeof(Harvest))]
public class Carcass : MonoBehaviour
{
    [Tooltip("이 시간이 지나면 썩어 사라진다 (초)")] public float 썩는시간 = 180f;

    float t;
    Transform 몸;
    Vector3 처음자리;

    /// ★★죽는 순간 **튕겨나가는 이동** — `Critter` 는 죽자마자 꺼져서 `Update` 가 안 돈다.
    ///   저작된 죽음 클립이 포물선을 그리므로, 자리도 같이 움직여야 「날아가 퍽 쓰러진다」가 된다
    ///   (2026-08-09). 처음엔 훅, 착지로 갈수록 잦아든다.
    Vector3 튕김; float 튕김남은, 튕김총;
    public void 튕겨내기(Vector3 거리, float 시간)
    {
        튕김 = 거리; 튕김총 = Mathf.Max(0.01f, 시간); 튕김남은 = 튕김총;
        처음자리 = transform.position;
    }

    /// ★★★**몸을 그 자리에서 사체로 전환한다** (2026-08-07 사용자 "공중에 붕 떠서 뒤지는데
    ///   뒤지는 모션도 적용 안 한 듯").
    ///
    ///   옛길(`남기다`)은 몸을 뜯어다 코드로 86° 눕히고 **정지 자세 바운즈**로 높이를 쟀다 —
    ///   리깅 모델에선 그 바운즈가 거짓말이라 시체가 떴고, 저작된 `죽음` 모션은 버려졌다.
    ///   → 이제 **오브젝트를 그대로 사체로 바꾼다.** 눕히는 건 `죽음` 클립이 하고
    ///     (`몸짓` 이 재생 후 마지막 자세에서 멈춘다), 발은 이미 `진짜맞춤` 이 심어 놓았다.
    ///     재부착도 높이 계산도 없다 — 계산이 없으면 틀릴 것도 없다.
    public static void 전환(Critter c, int 고기량)
    {
        var go = c.gameObject;
        go.name = c.종.이름 + " 사체";

        var h = go.GetComponent<Harvest>();
        if (h == null) h = go.AddComponent<Harvest>();
        h.kind = Stock.Kind.고기;
        h.hits = 3;                                        // 세 번 갈라야 다 챙긴다
        h.perHit = Mathf.Max(1, Mathf.CeilToInt(고기량 / 3f));
        h.장애물치우기 = false;

        var car = go.AddComponent<Carcass>();
        car.몸 = go.transform.childCount > 0 ? go.transform.GetChild(0) : null;
        car.처음자리 = go.transform.position;
    }

    /// (은퇴) 몸을 뜯어다 코드로 눕히던 옛길 — 상자 몸일 때만 맞았다. 지금은 `전환` 이 정본.
    public static void 남기다(Critter c, int 고기량)
    {
        var go = new GameObject(c.종.이름 + " 사체");
        go.transform.position = new Vector3(c.transform.position.x, 0f, c.transform.position.z);
        go.transform.rotation = c.transform.rotation;

        // 몸을 통째로 옮겨 옆으로 눕힌다 (모델을 새로 만들지 않는다)
        if (c.transform.childCount > 0)
        {
            var body = c.transform.GetChild(0);
            body.SetParent(go.transform, false);
            body.localRotation = Quaternion.Euler(0f, 0f, 86f * (Random.value < 0.5f ? 1f : -1f));

            // ★★띄우는 높이를 **재서** 정한다 (2026-08-05 사용자 "공중에서 뒤지는애들도있네").
            //   전엔 `키 × 0.18` 로 짐작해 띄웠다. 상자 모델일 땐 맞았지만 지금 모델은
            //   **원점이 이미 발밑**이라 그 띄움이 고스란히 공중으로 뜬다 (티라 4.5m 면 0.81m).
            //   눕힌 **뒤에** 몸의 제일 낮은 점을 재서 딱 그만큼만 올린다.
            body.localPosition = Vector3.zero;
            Physics.SyncTransforms();
            float 바닥 = float.MaxValue;
            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
                바닥 = Mathf.Min(바닥, r.bounds.min.y);
            body.localPosition = 바닥 < float.MaxValue * 0.5f
                ? new Vector3(0f, go.transform.position.y - 바닥, 0f)
                : new Vector3(0f, c.종.키 * 0.18f, 0f);       // 잴 게 없으면 옛 방식
        }

        int 몫 = Mathf.Max(1, Mathf.CeilToInt(고기량 / 3f));
        var h = go.AddComponent<Harvest>();
        h.kind = Stock.Kind.고기;
        h.hits = 3;                     // 세 번 갈라야 다 챙긴다
        h.perHit = 몫;
        h.장애물치우기 = false;          // 사체는 길을 막지 않는다

        var car = go.AddComponent<Carcass>();
        car.몸 = go.transform.childCount > 0 ? go.transform.GetChild(0) : null;
        car.처음자리 = go.transform.position;
    }

    void Update()
    {
        t += Time.deltaTime;

        // ★튕겨나가는 중 — 처음엔 훅, 착지로 갈수록 잦아든다 (남은 비율의 제곱)
        if (튕김남은 > 0f)
        {
            float 남음 = 튕김남은 / 튕김총;
            transform.position += 튕김 * (남음 * 남음 * Time.deltaTime / 튕김총 * 3f);
            튕김남은 -= Time.deltaTime;
            처음자리 = transform.position;          // 썩어 가라앉는 기준도 따라 옮긴다
        }

        float u = t / Mathf.Max(1f, 썩는시간);
        if (u >= 1f) { Destroy(gameObject); return; }

        // 끝물에는 땅으로 가라앉는다 — 사라지는 게 눈에 보이게
        if (몸 != null && u > 0.85f)
        {
            float k = (u - 0.85f) / 0.15f;
            transform.position = 처음자리 - Vector3.up * k * 1.2f;
        }
    }
}
