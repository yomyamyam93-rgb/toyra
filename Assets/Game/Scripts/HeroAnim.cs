using UnityEngine;

/// 주인공 모션 — **실제로 움직인 거리**를 재서 애니메이터에 넘긴다.
///
/// ★속도를 `Hero` 에서 가져오지 않고 위치 변화로 잰다. 밀림(`Blocker`)·경사 때문에
///  「가려던 속도」와 「실제 간 속도」가 다르기 때문이다. 벽에 붙어 밀면 제자리인데
///  걷는 모션이 나오는 꼴을 막는다.
///
/// ★컨트롤러는 성별마다 다르다 — 남녀 뼈 기본 자세가 최대 14.7° 달라 클립을 따로 굽는다.
[RequireComponent(typeof(HeroBody))]
public class HeroAnim : MonoBehaviour
{
    [Tooltip("속도가 붙고 떨어지는 빠르기 — 크면 반응이 즉각적이다")]
    public float 반응 = 12f;

    HeroBody 몸;
    Vector3 지난자리;
    float 속도;

    void Awake() { 몸 = GetComponent<HeroBody>(); 지난자리 = transform.position; }

    void Update()
    {
        // 컨트롤러는 `HeroBody` 가 문다 (에디터에서도 물려 있어야 키프레임이 보인다)
        var a = 몸 != null ? 몸.Anim : null;
        if (a == null || a.runtimeAnimatorController == null) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        var d = transform.position - 지난자리;
        d.y = 0f;
        지난자리 = transform.position;

        // ★「속도」에 부호를 준다 — 뒤로 가면 음수. 이 게임은 마우스를 보면서
        //   뒷걸음질 치는 게 조작의 핵심이라, 앞뒤가 한 축에 있어야 한다.
        // ★옆걸음은 앞걸음으로 친다 (문턱 −0.25). 옆 클립이 없어서인데,
        //   0 으로 두면 옆으로 미끄러지면서 가만히 서 있는 꼴이 된다.
        float 빠르기 = d.magnitude / dt;
        float 앞쪽 = 빠르기 > 0.01f ? Vector3.Dot(d.normalized, transform.forward) : 1f;
        float 부호 = 앞쪽 < -0.25f ? -1f : 1f;

        속도 = Mathf.Lerp(속도, 빠르기 * 부호, 반응 * dt);
        a.SetFloat("속도", 속도);
    }
}
