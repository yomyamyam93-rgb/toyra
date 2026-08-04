using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// 남녀 몸 바꾸기 — **시험용 단축키 하나** (2026-08-04 사용자 "단축키 하나 넣어줘,
/// 누르면 전환될 수 있도록 테스트로 왔다갔다 보게").
///
/// ★두 몸은 이미 캐릭터 밑에 **둘 다 서 있다** (`HeroSetup`). 이건 켜고 끄는 것뿐이다.
///   런타임에 모델을 불러오는 길을 따로 안 내도 되고, 바꾸는 데 한 프레임도 안 든다.
///
/// ★뼈대가 완전히 같아서 컨트롤러를 공유한다 — 그래서 **걷던 도중에 바꿔도 걸음이
///   그대로 이어진다.** 두 몸을 나란히 비교하기에 이만한 게 없다.
[DefaultExecutionOrder(-10)]        // 몸을 먼저 정해 놓고 HeroAnim·HeroHold 가 찾게 한다
public class HeroSwap : MonoBehaviour
{
    // ★F4 는 안 먹었다 (2026-08-04 사용자 "F4로 남자로 전환 안돼고"). 코드로 `적용()` 을
    //   부르면 멀쩡히 바뀌는 걸 확인했으므로 **키가 안 들어오는 것**이다 — 기능키는
    //   편집기·OS·노트북 Fn 에 먼저 걸리는 일이 잦다. 시험용 키는 글자키로 둔다.
    [Tooltip("이 키로 남녀를 바꾼다")] public Key 바꾸는키 = Key.T;
    [Tooltip("지금 남자인가")] public bool 남자;

    Transform 여자몸, 남자몸;

    void Awake() { 찾기(); 적용(); }

    void 찾기()
    {
        여자몸 = transform.Find("사람_여자");
        남자몸 = transform.Find("사람_남자");
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k == null) return;
        if (k[바꾸는키].wasPressedThisFrame) { 남자 = !남자; 적용(); }
#endif
    }

    public void 적용()
    {
        if (여자몸 == null || 남자몸 == null) 찾기();
        if (남자몸 == null) return;                    // 남자 몸이 아직 없으면 그냥 둔다

        if (여자몸 != null) 여자몸.gameObject.SetActive(!남자);
        남자몸.gameObject.SetActive(남자);

        // ★몸을 바꾸면 **뼈를 쥐고 있던 것들에게 알려야** 한다. 안 그러면 꺼진 몸의
        //   뼈를 계속 돌려서 팔이 안 움직인다 (겉으로는 "무기 드는 게 안 된다" 로 보인다).
        var hold = GetComponent<HeroHold>();
        if (hold != null) hold.뼈잡기();
    }
}
