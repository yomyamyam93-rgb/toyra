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

    /// 죽은 자리에 사체를 남긴다 — 죽은 놈의 몸을 그대로 눕혀서 쓴다
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
            body.localPosition = new Vector3(0f, c.종.키 * 0.18f, 0f);
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
