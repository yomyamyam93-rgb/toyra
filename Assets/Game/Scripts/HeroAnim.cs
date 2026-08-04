using UnityEngine;

/// 캐릭터 걷기 — **동작을 고르는 게 아니라 섞는다.**
///
/// ★좀보이드에서 앞걷기·옆걷기·뒤로걷기가 유기적으로 이어져 보이는 건, 동작을
///   갈아 끼우는 게 아니라 **블렌드 트리**로 섞기 때문이다. 우리도 같은 방식이다.
///
/// ★몸이 보는 방향(마우스)과 가는 방향(WASD)이 따로 놀므로, **가는 방향을 몸 기준으로
///   풀어서** 앞뒤·좌우 두 값으로 준다. 그러면 뒷걸음질도 옆걸음도 저절로 섞인다.
[RequireComponent(typeof(Hero))]
[DefaultExecutionOrder(60)]
public class HeroAnim : MonoBehaviour
{
    [Tooltip("이 속도면 걷기 동작이 100%가 된다 (m/s)")] public float 걷는속도 = 2.6f;
    [Tooltip("이 속도면 달리기 동작이 100%가 된다 (m/s)")] public float 달리는속도 = 6f;
    [Tooltip("동작이 바뀌는 부드러움 (클수록 빨리 바뀐다)")] public float 따라붙기 = 18f;

    // ★모델이 향한 쪽이 유니티 기준(+Z)과 다르면 여기서 돌린다.
    //   실행 중에 바꿔 보면서 맞추면 된다 (0 / 90 / 180 / 270 중 하나가 맞는다)
    [Tooltip("모델이 마우스 반대쪽을 보면 180 으로, 옆을 보면 90 또는 270 으로")]
    [Range(0f, 360f)] public float 모델회전 = 0f;

    Hero hero;
    Animator anim;
    float 앞뒤, 좌우, 빠르기;

    void Awake() { hero = GetComponent<Hero>(); }

    void LateUpdate()
    {
        if (anim == null)
        {
            anim = GetComponentInChildren<Animator>();
            if (anim == null) return;
        }

        // 모델 방향 보정 — 실행 중에 값을 바꾸면 바로 반영된다
        anim.transform.localRotation = Quaternion.Euler(0f, 모델회전, 0f);

        var v = hero.속도;
        float 크기 = v.magnitude;

        // 가는 방향을 **모델이 향한 쪽 기준**으로 푼다 — 앞뒤(+앞) · 좌우(+오른쪽)
        // ★뿌리(transform)가 아니라 **모델**을 기준으로 삼아야 한다 (2026-08-04 사용자
        //   "뒤로걷기가 되어 있고"). 모델을 180도 돌려 놓았으면 뿌리 기준으로는 앞인 게
        //   눈에는 뒤라서, 앞으로 가는데 뒤로걷기 동작이 나온다.
        // ★★기준은 **끊기 전 방향**이다 (2026-08-04 사용자 "걷는 건 좀 뚝뚝 딜레이돼").
        //   몸은 22.5° 씩 끊어 돌리는데(16칸), 동작 고르는 값까지 그 끊긴 몸을 기준으로
        //   재면 한 칸 넘어갈 때마다 앞뒤·좌우가 최대 0.38 씩 **한 번에 튄다.**
        //   블렌드가 그 자리에서 갈아타서 뚝뚝 끊겨 보인다.
        //   → 보이는 몸은 그대로 끊고, **동작만 매끄러운 방향**으로 고른다. 어긋나 봐야
        //     11.25° 라 눈에 안 띄고, 튐은 완전히 사라진다.
        float f = 0f, s = 0f;
        if (크기 > 0.05f)
        {
            var 기준 = Quaternion.LookRotation(hero.LookDir, Vector3.up)
                     * Quaternion.Euler(0f, 모델회전, 0f);
            var d = v / 크기;
            f = Vector3.Dot(d, 기준 * Vector3.forward);
            s = Vector3.Dot(d, 기준 * Vector3.right);
        }

        // 0 = 멈춤 · 1 = 걷기 · 2 = 달리기
        float 목표빠르기 = 크기 <= 0.05f ? 0f
                        : Mathf.Clamp(Mathf.InverseLerp(0f, 걷는속도, 크기)
                                    + Mathf.InverseLerp(걷는속도, 달리는속도, 크기), 0f, 2f);

        float k = 1f - Mathf.Exp(-따라붙기 * Time.deltaTime);
        앞뒤 = Mathf.Lerp(앞뒤, f, k);
        좌우 = Mathf.Lerp(좌우, s, k);
        빠르기 = Mathf.Lerp(빠르기, 목표빠르기, k);

        anim.SetFloat("앞뒤", 앞뒤);
        anim.SetFloat("좌우", 좌우);
        anim.SetFloat("빠르기", 빠르기);
    }
}
