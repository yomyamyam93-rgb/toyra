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
        // ★★**몸집을 재서 넣는다** — 갈무리 거리가 중심 한 점만 보면 큰 사체는
        //   옆에 붙어 서도 손이 안 닿는다 (`Harvest.반경` 참고).
        //   ☆종의 반지름이 아니라 **누운 몸의 실제 폭**을 쓴다 — 눕히면 키만큼 길어진다.
        h.반경 = Mathf.Max(c.Radius, c.종.키 * 0.45f);
        // ★★★**사체는 방향을 안 본다** (2026-08-11 사용자 "시체 주변에 조금만 닿아있어도
        //   갈무리가 가능하게해줘야하는데 아예 선택조차도안돼 방향도 맞춰야하고").
        //   ☆앞서 `방향무관` 을 만들고 검사까지 넣어 놓고 **켜는 줄을 안 썼다** — 내 실수다.
        //     갈무리는 쪼그려 앉아 뒤적이는 일이라 어느 쪽을 보는지가 상관없다.
        h.방향무관 = true;
        // ★★갈무리한 것은 **여기 쌓인다** — 인벤으로 순간이동하지 않는다 (2026-08-12).
        //   뒤적여서 꺼내 놓는 것과, 그것을 챙기는 것은 다른 행위다 (9-0).
        h.그자리에쌓기 = true;

        // 통을 미리 달아 둔다 — 갈무리 전에 Tab 을 열어도 「사슴 사체 (비어 있음)」이 보인다.
        // 뒤적여야 뭔가 생긴다는 것이 창에서 그대로 읽힌다
        var 통 = go.AddComponent<땅무더기>();
        통.프롭모양 = true;                    // 누운 몸이 곧 모양이다 — 상자를 얹지 않는다
        통.안사라짐 = true;                    // 파괴는 아래 `Update` 가 정한다
        통.이름표 = go.name;
        통.반경 = h.반경;

        var car = go.AddComponent<Carcass>();
        car.몸 = go.transform.childCount > 0 ? go.transform.GetChild(0) : null;
        car.처음자리 = go.transform.position;
        car.갈무리 = h; car.통 = 통;
    }

    Harvest 갈무리; 땅무더기 통;

    /// ★★★★**움직였으면 기억한 자리를 다시 알린다** (2026-08-12 — 「사체가 안 사라진다」의 정체).
    ///
    ///   `땅무더기`·`Harvest` 는 값이 비싸서 **자리를 한 번 재서 기억해 둔다**(`OnEnable`).
    ///   그런데 사체는 그 **직후에 죽으면서 튕겨 나간다**(`튕겨내기`) — 그러면 눈에 보이는
    ///   곳과 등록된 자리가 어긋난다. 인벤창은 등록된 자리로 2.6m 안을 찾으므로
    ///   **Tab 을 열어도 사체가 안 뜨고 → 못 가져가니 → 영영 안 사라진다.**
    ///
    ///   ☆`Harvest.툭()` 이 돌맹이에서 똑같이 겪고 *"같은 함정이 또 있을 수 있다"* 고
    ///     적어 둔 바로 그것이다. **「자리를 기억하는 것」은 움직이면 다시 기억시켜야 한다.**
    void 자리알리기()
    {
        if (통 != null) 통.자리 = transform.position;
        if (갈무리 != null) 갈무리.자리갱신();
    }

    /// 다 뒤적였나 — ★`hits <= 0` 도 같이 본다.
    /// ☆실측에서 **`hits = 0` 인데 `Harvest` 가 살아 있는** 사체가 나왔다. 왜 안 지워졌는지는
    ///   못 짚었지만, 그때도 「다 뒤적였다」인 것은 분명하다 — 한쪽만 보면 영영 안 사라진다.
    bool 다뒤적임 => 갈무리 == null || 갈무리.hits <= 0;

    /// 꺼낸 것을 그 자리 땅에 쏟는다 — 몸이 사라져도 **결과는 남는다** (9-0)
    void 땅으로쏟기()
    {
        if (통 == null || 통.속.것들.Count == 0) return;
        var 땅 = 땅무더기.여기(transform.position);       // ☆사체 통에는 안 섞인다 (「땅만」)
        for (int i = 통.속.것들.Count - 1; i >= 0; i--)
        {
            var it = 통.속.것들[i];
            if (통.속.빼기(it)) 땅.속.받기(it);
        }
        땅.갱신();
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
        h.방향무관 = true;
        h.반경 = Mathf.Max(c.Radius, c.종.키 * 0.45f);

        var car = go.AddComponent<Carcass>();
        car.몸 = go.transform.childCount > 0 ? go.transform.GetChild(0) : null;
        car.처음자리 = go.transform.position;
    }

    // ★★**사라질 때 면이 지워진다** (2026-08-12 사용자 "마자 메시면이 지워지면서 사라지게").
    //   옛 프로젝트에서 쓰던 그 연출이다 — `디졸브` 가 그린다.
    [Tooltip("사라질 때 면이 지워지는 데 걸리는 시간 (초) — 0 이면 그냥 사라진다")]
    public float 지워지는데 = 0.9f;

    // ☆(은퇴) 끝물에 땅으로 가라앉던 것 — 「사라지는 게 눈에 보이게」 하려던 것인데
    //   이제 디졸브가 그 일을 한다. 지우지 않고 스위치로 남긴다 (9장 3조)
    [Tooltip("끝물에 땅으로 가라앉는다 — 디졸브가 대신하므로 꺼 둔다")]
    public bool 가라앉기 = false;

    bool 지우는중;

    /// 사라진다 — 면이 지워지고 나서 없어진다
    void 사라져(float 시간)
    {
        if (지우는중) return;
        지우는중 = true;
        if (갈무리 != null) Destroy(갈무리);            // 더는 뒤적일 수 없다
        if (통 != null) 통.enabled = false;             // 창·외곽선이 더는 안 잡는다
        if (시간 <= 0f) { Destroy(gameObject); return; }
        디졸브.시작(gameObject, 시간);
    }

    void Update()
    {
        if (지우는중) return;
        t += Time.deltaTime;

        // ★튕겨나가는 중 — 처음엔 훅, 착지로 갈수록 잦아든다 (남은 비율의 제곱)
        if (튕김남은 > 0f)
        {
            float 남음 = 튕김남은 / 튕김총;
            transform.position += 튕김 * (남음 * 남음 * Time.deltaTime / 튕김총 * 3f);
            튕김남은 -= Time.deltaTime;
            처음자리 = transform.position;          // 썩어 가라앉는 기준도 따라 옮긴다
            자리알리기();                            // ★★아래 설명 — 움직였으면 반드시 다시 알린다
        }

        // ★★★★**다 뒤적였으면 몸은 사라지고, 꺼낸 것은 땅에 남는다** (2026-08-12 사용자
        //   "갈무리 끝났는데도 여전히 안사라짐" — 두 번 말했다).
        //
        //   ☆무엇을 쟀나: 돌아가는 판에서 사체 두 구를 까 보니 **통에 6개·8개가 그대로**였다.
        //     갈무리는 끝났는데 **꺼낸 것을 안 가져가서** 몸이 남아 있던 것이다.
        //   ☆앞 규칙("다 가져가야 사라진다")이 틀렸다. 갈무리는 **몸에서 꺼내는 행위**이고,
        //     다 꺼냈으면 몸은 빈 껍데기다 — 껍데기가 남아 있을 이유가 없다.
        //     인과는 그대로 산다: 꺼낸 것은 **그 자리 땅에 쌓여** 있고, 그걸 주워야 내 것이다.
        //     ★덤으로 눈에 보인다 — 땅무더기는 더미를 그리므로 「고기가 나왔다」가 읽힌다.
        if (다뒤적임) { 땅으로쏟기(); 사라져(지워지는데); return; }

        float u = t / Mathf.Max(1f, 썩는시간);
        if (u >= 1f) { 사라져(지워지는데); return; }

        // 끝물에는 땅으로 가라앉는다 — 사라지는 게 눈에 보이게
        if (가라앉기 && 몸 != null && u > 0.85f)
        {
            float k = (u - 0.85f) / 0.15f;
            transform.position = 처음자리 - Vector3.up * k * 1.2f;
        }
    }
}
