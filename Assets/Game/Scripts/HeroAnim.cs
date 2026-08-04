using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

    // ★★의자 (2026-08-04 사용자 "의자같은데 앉기랑 일어서기가 있는데 그건 의자같은데
    //   상호작용하게해주면돼"). **웅크리기(Ctrl)와 다른 것**이다 — 실측으로 확인했다:
    //   웅크린 Hips 는 77~86 인데 앉기는 97 → 56 으로 앉는 높이까지 내려간다.
    // ★아직 씬에 의자가 없다. 그래서 지금은 시험 키로만 켠다 —
    //   의자 물건이 생기면 그쪽이 `의자앉음` 을 켜고 끄면 그대로 돌아간다.
    [Header("의자")]
    [Tooltip("의자에 앉아 있나 — 의자 물건이 생기면 그쪽이 켠다")]
    public bool 의자앉음;
    [Tooltip("시험용 — 이 키로 앉았다 일어난다 (아직 의자가 없어서)")]
    public Key 의자시험키 = Key.Y;      // ★기능키는 안 들어오는 일이 있다 (HeroSwap 주석 참고)

    Hero hero;
    Animator anim;
    float 앞뒤, 좌우, 빠르기;
    /// 마지막으로 향했던 방향 (길이 1). 멈춰도 이걸 들고 있어야 블렌드가 원점을 안 밟는다
    Vector2 지난방향 = Vector2.up;

    void Awake() { hero = GetComponent<Hero>(); }

    void LateUpdate()
    {
        // ★몸이 바뀌면(F4 · `HeroSwap`) 애니메이터도 바뀐다. 꺼진 몸을 계속 쥐고 있으면
        //   화면에 보이는 몸이 첫 프레임에서 굳는다 — 반드시 켜진 쪽을 다시 찾는다.
        if (anim == null || !anim.isActiveAndEnabled)
        {
            anim = GetComponentInChildren<Animator>();   // 켜진 것만 찾는다
            if (anim == null) return;
        }

        의자입력();

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

        // ★★★한 번씩 걸음이 **멈추던** 버그 (2026-08-04 사용자).
        //
        //   걷기 블렌드 트리에는 네 방향 클립만 있고 **가운데(0,0)에는 아무것도 없다.**
        //   그 자리에서는 앞걷기와 뒤로걷기가, 왼걸음과 오른걸음이 서로 상쇄돼
        //   **자세가 통째로 굳는다** — 걷는데 다리가 멈춘 것처럼 보인다.
        //
        //   그런데 앞뒤·좌우를 **따로** 부드럽게 하다 보니, 방향을 뒤집을 때마다
        //   (W→S · A→D · 몸이 16칸으로 끊겨 돌 때) 좌표가 **원점을 관통**한다.
        //   그 찰나가 곧 멈춤이다.
        //
        //   → 움직이는 동안에는 길이를 1 로 되돌린다. 그러면 좌표가 원을 **돌아서** 가고
        //     한가운데를 밟지 않는다. 방향은 그대로라 걸음 자체는 안 달라진다.
        //   ★★★방향은 **언제나 길이 1** 로 보낸다 (2026-08-05).
        //
        //     방향 블렌드는 **길이가 0 이거나 아주 작으면** 가중치가 정의되지 않아
        //     상태의 클립 길이가 0 으로 계산된다. 그러면 `normalizedTime` 이 터지고
        //     (실측 6.57×10³⁷) **한 번 터지면 눌어붙어** 애니메이션이 통째로 굳는다.
        //     Ctrl 로 웅크렸다 펴야 풀렸던 게 이것이다 — 상태를 다시 들어가야 초기화된다.
        //
        //     처음엔 「멈추면 0.02 로 짧게」 로 막았는데, 그것도 결국 작은 값이라 똑같이 터졌다.
        //     → **멈춤/걸음은 오직 `빠르기` 가 정한다.** 방향은 늘 원 위에 있고, 서 있을 땐
        //       그 방향이 어디든 상관없다 (어차피 정지 클립만 재생되므로).
        var 방향 = new Vector2(좌우, 앞뒤);
        if (방향.sqrMagnitude > 1e-6f) 지난방향 = 방향.normalized;
        방향 = 지난방향;                       // 길이는 늘 1

        anim.SetFloat("앞뒤", 방향.y);
        anim.SetFloat("좌우", 방향.x);

        // ★★그래도 터졌으면 **스스로 되살린다.** 원인을 하나 막아도 다른 길로 터질 수 있고,
        //   터진 채로 두면 사용자는 「걷기가 안 된다」로만 겪는다. 상태를 다시 들어가면 낫는다.
        var 상태 = anim.GetCurrentAnimatorStateInfo(0);
        if (상태.length <= 0.001f || !float.IsFinite(상태.normalizedTime) || Mathf.Abs(상태.normalizedTime) > 1e6f)
        {
            anim.Play(상태.fullPathHash, 0, 0f);
            anim.Update(0f);
        }
        anim.SetFloat("빠르기", 빠르기);

        // 웅크리기는 Ctrl 을 **누르는 동안만** (사용자 확정). 앉아 있는 동안은 안 겹치게 끈다
        anim.SetBool("앉기", hero.Sneaking && !의자앉음);
        anim.SetBool("의자", 의자앉음);
    }

    void 의자입력()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null && k[의자시험키].wasPressedThisFrame) 의자앉음 = !의자앉음;
#endif
        hero.묶임 = 의자앉음;      // 앉아 있는 동안은 발이 안 나간다
    }
}
