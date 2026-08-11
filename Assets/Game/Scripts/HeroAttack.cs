using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// 사람의 손 — **좀보이드식 3막 공격.**
///
/// ★클릭하는 순간 피해가 들어가면 손맛이 없다. 실제 몸이 하는 일을 따라가야 한다:
///   **예비(뒤로 감음) → 휘두름(이때만 판정) → 여운(되돌아옴).**
///   판정은 무기가 **쓸고 지나간 각도**로 나간다 — 그림과 판정이 같아야 읽힌다.
///
/// ★한 번 휘두를 때 한 놈을 두 번 때리지 않는다. 쓸면서 지나간 놈만 한 번씩.
///
/// ★밀기(우클릭)는 좀보이드 기본기다. 피해는 거의 없고 **넘어뜨려 공간을 만든다** —
///   떼에 둘러싸였을 때 빠져나가는 유일한 수단이자, 산 채로 잡을 때 쓰는 손이기도 하다.
///
/// ★**누르면 들고, 떼면 휘두른다** (2026-08-04). 누르는 시간이 곧 예비 동작이다.
///
/// ★실행 순서 300 — 애니메이터(0)·`HeroHold`(200) 가 팔을 다 돌린 **뒤에** 몽둥이를 쥐여 놓는다.
///
/// ★★플레이를 안 켜도 무기가 손에 붙어 있다 (`ExecuteAlways`, 2026-08-09 사용자 "무기위치
///   내가 보면서 조절할 수 있게해달라했는데 사람 레이어 아래 넣어서 그게 없네").
///   전엔 `무기` 를 런타임에 `Grey.Box` 로 만들어서 **플레이 중에만 존재**했고, 그나마
///   `무기자세()` 가 매 프레임 덮어써서 끌어도 안 붙었다. 지금은 셋 다 된다:
///     ①씬에 실물로 있다 (`Tools/토이라/무기 만들기`) — 「손·무기는 씬에 실존」 규칙 그대로
///     ②편집 중에도 손을 따라간다 — 플레이를 안 켜도 자리가 보인다
///     ③`직접조절` 을 켜면 **씬 뷰에서 끌어 옮긴 만큼을 되읽어** `쥔자리`·`기울임` 에 담는다.
///       편집 중에 옮기면 그대로 저장되고, 플레이 중에 옮긴 값은 인스펙터에서 복사해 두면 된다.
[RequireComponent(typeof(Hero))]
[DefaultExecutionOrder(300)]
[ExecuteAlways]
public class HeroAttack : MonoBehaviour
{
    // ★★★★**채집** (2026-08-11 사용자 "갈무리, 쪼그려앉아서 뒤적이는 모션 넣어줄래?
    //   그런다음 게이지 다 차게되면…" · "나무나 돌 캐기도 클릭하면 동작이 있고 게이지로").
    //   옛 방식은 **휘두름 한 번 = 한 칸**이라 캐는 것도 때리는 것과 같은 몸짓이었다.
    //   이제 **누르고 있으면 게이지가 찬다** — 갈무리는 쪼그려 앉아 뒤적이고,
    //   나무·돌은 몸을 숙여 계속 판다.
    enum State { 쉼, 예비, 휘두름, 여운, 채집 }

    [Header("★채집 (누르고 있으면 캔다)")]
    // ★★★**기다리는 시간을 두지 않는다** (2026-08-11 사용자 "뭘 누르라는거야 시발;;
    //   마우스로 좌클릭해서 누르면 캐지게해주면안돼?"). 처음엔 0.16초 누르고 있어야
    //   채집으로 넘어가게 했는데, 그건 **손에 안 잡히는 규칙**이다.
    //   → 4장의 원칙 그대로 간다: **「앞에 무엇이 있느냐」가 정한다.**
    //     앞에 살아 있는 짐승이 있으면 때리고, 없으면 캔다. 누르는 순간 갈린다.
    [Tooltip("0 이면 누르는 즉시 캔다 — 굳이 늦추고 싶을 때만 올린다")]
    [Range(0f, 0.6f)] public float 채집지연 = 0f;
    [Tooltip("채집하는 동안의 걷는 속도 배")] [Range(0f, 1f)] public float 채집이속 = 0.3f;
    [Tooltip("나무·돌을 팰 때 초당 몇 번 내려찍나")] [Range(0.5f, 4f)] public float 패는빠르기 = 1.6f;
    [Tooltip("(은퇴) 좌클릭 평타로도 캐진다 — 캐기는 F 로 갔다")] public bool 평타로도캐기 = false;
    [Tooltip("갈무리할 때 얼마나 앉나 — 1 이면 공격 예비만큼(9cm), 2.6 이면 쪼그려 앉기")]
    [Range(1f, 4f)] public float 앉기굽힘 = 2.6f;
    [Tooltip("걸으면 채집이 풀린다 — 이 속도를 넘으면 끊긴다 (m/s)")]
    [Range(0.05f, 2f)] public float 채집끊는속도 = 0.35f;
    Harvest 채집대상;
    bool 이번칸때림;              // 이번 게이지 한 바퀴에서 이미 때렸나
    /// HUD 가 읽는다 — 상시로 안 띄우고 **캘 때만** 뜬다 (11장 · 기획 5-7)
    public static bool 채집중;
    public static float 채집게이지;

    [Header("때리기 — 3막")]
    [Tooltip("예비 동작 (초) — 뒤로 감는 시간")] public float 예비 = 0.14f;
    [Tooltip("휘두르는 시간 (초) — 이 동안만 판정이 나간다")] public float 휘두름 = 0.16f;
    [Tooltip("여운 (초) — 되돌아오는 시간. 이 동안 다음 공격 못 함")] public float 여운 = 0.22f;
    // ★★한 스윙의 전체 박자 = 예비+휘두름+여운+쿨 (2026-08-07 사용자 "너무 빨리 여러 대
    //   때릴 수 있어서 컨트롤이라는 게 없어"). 여운만으로는 0.5초에 한 대라 난타가 됐다 —
    //   쿨을 더해 「휘두르고 → 자리를 다시 잡고 → 다음 대」의 리듬을 만든다.
    [Tooltip("여운이 끝난 뒤 다음 공격까지 쉬는 시간 (초)")] public float 공격쿨 = 0.35f;

    [Header("위력")]
    public float 피해 = 14f;
    [Tooltip("닿는 거리 (m)")] public float 사거리 = 2.2f;
    // ★★110° → 70° (2026-08-04). 팔이 **머리 위에서 앞으로 내려찍는** 동작이 되면서
    //   판정도 앞쪽으로 좁혀야 그림과 맞는다. 110° 는 옆으로 쓸던 시절의 값이라,
    //   시작 순간 **왼쪽 55°에 있는 놈**부터 맞아서 "앞을 안 때린다" 로 읽혔다.
    [Tooltip("쓸고 지나가는 각도 (°) — 앞을 내려찍는 동작이라 좁다")] public float 각도 = 70f;
    [Tooltip("손목이 젖혔다 눕는 정도 (°) — 몽둥이 끝의 궤적")] public float 손목 = 22f;
    [Tooltip("맞으면 이만큼 밀린다 (m)")] public float 넉백 = 0.6f;
    [Tooltip("맞으면 이만큼 비틀거린다 (초)")] public float 비틀 = 0.35f;
    [Tooltip("한 번 휘두를 때 쓰는 지구력")] public float 지구력소모 = 8f;
    // ★★연타에 숨값을 매긴다 (2026-08-11 사용자 "지구력회복도 멈춰야하고, 지구력도 닳아야지").
    //   전엔 소모 6 에 회복 9/초 — 한 사이클 0.87초당 6 을 쓰고 그 사이 8 이 차서,
    //   **아무리 연타해도 숨이 안 찼다.** 헌법 6("지구력이 전투를 제한한다")이 공격에는
    //   안 걸려 있던 것. 휘두른 뒤 잠깐 회복을 멈추면 연타 = 순수 소모가 된다 —
    //   숨이 바닥나면 도망(달리기)도 못 하니 연타 자체가 도박이 된다.
    // ★★★1.2 → 0.5 (2026-08-11 사용자 "공격할때 손을 한번 내렸다 다시들어서 패는 버그").
    //   실측: 한 판이 0.92초인데 회복정지가 1.2초면 **연속 공격 중 회복이 0** 이라
    //   16방이면 지구력이 소모값 아래로 떨어지고, 그때 공격이 **조용히 안 나간다** —
    //   팔은 공격예약(0.4초) 동안 들려 있다 내려가고 다음 클릭에 다시 든다. 그게 그 버그다.
    //   0.5 면 한 판마다 3.8 이 차서(0.42초 × 9) 순소모 2.2 — 45방쯤 이어 칠 수 있다.
    [Tooltip("휘두르거나 민 뒤 이만큼은 지구력이 안 찬다 (초) — 한 판(0.92초)보다 짧아야 한다")]
    public float 회복정지 = 0.5f;

    // ★★★**뾰족한 것으로는 기절시킬 수 없다** (2026-08-10 사용자 — *"뾰족한 무기가 아니라,
    //   둔기같은 뭉뚱한 무기로만 때렸을때 기절하게 해줘, 말이안돼니까"*).
    //   창으로 찔러서 기절시키는 건 말이 안 된다. **무기 고르기가 획득 방법을 가른다** —
    //   죽이려면 뾰족한 것, 산 채로 잡으려면 뭉툭한 것.
    //   ☆기획 5-1 의 *"어미를 기절시켜 꺼낸다(죽이면 안 된다)"* 가 여기서 열린다.
    [Header("★무기 성질 — 뭉툭하냐 뾰족하냐")]
    [Tooltip("몽둥이·돌 같은 둔기인가. 끄면(창·칼) 기절을 못 시킨다")]
    public bool 둔기 = true;
    [Tooltip("둔기로 한 대 칠 때 쌓이는 기절값")] public float 기절력 = 20f;
    [Tooltip("밀 때 쌓이는 기절값 — 미는 건 언제나 뭉툭한 짓이다")] public float 밀기기절 = 12f;

    [Header("밀기 (우클릭)")]
    public float 밀기피해 = 2f;
    public float 밀기사거리 = 1.9f;
    public float 밀기각도 = 90f;
    [Tooltip("밀려나는 거리 (m)")] public float 밀기넉백 = 2.2f;
    [Tooltip("넘어져 있는 시간 (초)")] public float 넘어짐 = 1.4f;
    public float 밀기소모 = 9f;
    public float 밀기쿨 = 0.7f;

    // ★★몽둥이는 **팔에 맞춰 잡는다** (2026-08-04 사용자 "몽둥이만 한 번 잘 잡아들 수 있게").
    //   전엔 손뼈 밑에 각도 0 · 위치 0 으로 매달아서, 막대 **한가운데**가 손에 박히고
    //   방향은 뼈의 축(리그마다 다르다)이 정했다 — 반쪽이 손등 뒤로 삐져나왔다.
    //   이제 **팔뚝→손 방향**을 축으로 삼는다. 뼈 이름·축이 어떻든 팔이 가리키는 쪽이라
    //   리그에 안 휘둘리고, 팔을 들면 몽둥이도 따라 들린다.
    [Header("몽둥이 (한손)")]
    [Tooltip("길이 (m)")] public float 길이 = 0.8f;
    [Tooltip("굵기 (m)")] public float 굵기 = 0.06f;
    // ★★쥐는 자리를 **3축으로** 연다 (2026-08-05 사용자 "몽둥이도 손에 제대로 안들려있네,
    //   이거는 내가 위치조절이 되나?"). 전엔 「앞으로 얼마나」 한 축뿐이라, 막대가 손바닥을
    //   벗어나 떠 있어도 당겨 넣을 방법이 없었다.
    //   ★기준은 **막대 자신의 축**이다: z = 막대가 뻗는 쪽 · x = 손등 좌우 · y = 위아래.
    //     z 를 줄이면 손잡이 끝을 쥐고, 늘리면 가운데를 쥔다.
    [Tooltip("손에서 얼마나 떨어져 쥐나 (m) — z=막대 방향 · x=좌우 · y=위아래")]
    public Vector3 쥔자리 = new Vector3(0f, 0f, 0.3f);
    [Tooltip("팔 방향에서 더 기울이는 각도 (°) — x 를 음수로 주면 끝이 위로 선다")]
    public Vector3 기울임 = new Vector3(-25f, 0f, 0f);

    // ★★**평소에 메고 있을 때는 잡는 자리가 다르다** (2026-08-09 사용자 "무기를 든 손을
    //   허벅지 뒤쪽으로 애매하게 빼고있는 버그"). 팔이 내려가 있을 때 싸울 때의 각도를
    //   그대로 쓰면 막대가 **허벅지를 가로지른다** — 팔뚝 축으로 0.18m 뻗고 −25° 눕기 때문.
    //   싸우는 자세(`지금`)에 따라 이 둘 사이를 섞는다. 씬에서 끌어 맞추면 그때 켜져 있는
    //   쪽(멘 자세 / 싸우는 자세)에 값이 담긴다.
    [Header("평소 멘 자세")]
    //   ★기본값은 **풀어서 얻었다**: 각도를 훑어 보며 「막대가 아래로 곧게 늘어지고(아래성분
    //     0.98) 다리에 안 겹치는」 짝을 골랐다. 결과 — 손에서 15cm 를 쥐고 (−10, 0, 30)°.
    [Tooltip("팔을 내리고 있을 때 쥐는 자리 (m)")] public Vector3 멘자리 = new Vector3(0f, 0f, 0.15f);
    [Tooltip("팔을 내리고 있을 때 기울임 (°) — 막대가 아래로 늘어진다")]
    public Vector3 멘기울임 = new Vector3(-10f, 0f, 30f);
    // ★★**편집 중에는 언제나 끌린다** (2026-08-09 사용자 "무기가 왜 이동이안돼니..?").
    //   처음엔 이 스위치를 켜야만 끌리게 했는데, 그러면 체크박스를 찾기 전까지는 무기가
    //   끌어도 매 틱 제자리로 튕겨 돌아간다 — 「고장」으로 보인다. 편집 창에서 끌어 맞추는
    //   건 당연한 동작이라 조건을 달지 않는다. 이 스위치는 **플레이 중에도** 끌고 싶을 때만 쓴다.
    [Tooltip("플레이 중에도 무기를 끌어 옮길 수 있게 한다 (편집 중에는 항상 끌린다)")]
    public bool 직접조절;

    // ★★**모델을 끼울 수 있게 한다** (2026-08-06 사용자 "모델링에 붙은 무기나 장비 위치
    //   조절 좀 가능하게 해줄래?"). 전에는 무기가 **회색 상자 고정**이라, 모델을 넣어도
    //   `localScale = (굵기,굵기,길이)` 가 그 모델을 납작하게 눌러 버렸다.
    //   ☆구조: **겉(`무기`)은 코드가** 손에 맞춰 놓고, **안쪽 모델만 오프셋으로** 맞춘다.
    //     소유가 갈려 있어야 둘이 안 싸운다 (CLAUDE.md 「소유권」).
    //   ☆값은 **플레이 중에 만져도 즉시 보인다** — 매 프레임 다시 얹는다.
    [Header("무기 모델 (비우면 회색 상자)")]
    [Tooltip("손에 들 모델 — 넣으면 상자 대신 이게 나온다")] public GameObject 무기모델;
    [Tooltip("모델만 더 밀어 넣기 (m) — 손 안으로 당길 때")] public Vector3 모델위치;
    [Tooltip("모델만 더 돌리기 (°) — 칼날 방향 맞출 때")] public Vector3 모델회전;
    [Tooltip("모델 크기 배수")] public float 모델크기 = 1f;

    // ★★★**주먹은 손가락 뼈로 낸다. 메시를 찌그러뜨리지 않는다** (2026-08-09 사용자
    //   "그냥 찌그러트리면 어떻게해.. 리깅이 필요하면 내가 리깅을 해주면되잖아").
    //   한 번 손뼈 크기를 눌러 실루엣만 닫아 봤는데 그건 땜빵이다 — 걷어냈다.
    //
    //   ☆지금 리그에는 손가락 뼈가 없다 (뼈 30개, 손 밑은 `무기자리` 마커 하나. 셰이프도 0개).
    //     `RightHand` 밑에 손가락 뼈가 들어오면 **아무 이름이든** 이 코드가 알아서 쓴다.
    //
    //   ★★**각도를 코드가 정하지 않는다.** 뼈 축은 리그마다 달라서 "몇 도 접으면 주먹" 을
    //     짐작하면 반드시 틀린다 (이 프로젝트가 여러 번 당한 함정). 대신 사람이 씬에서
    //     손을 주먹 모양으로 잡아 놓고 `Tools/토이라/지금 손 자세를 주먹으로 저장` 을 누르면
    //     그 자세를 그대로 기억하고, 게임에선 거기로 섞어 들어간다.
    [Header("주먹 (손가락 뼈가 있어야 작동)")]
    [Tooltip("무기를 들고 있을 때 손을 주먹으로 쥔다")] public bool 주먹 = true;
    [Tooltip("얼마나 쥐나 — 0 이면 편 손, 1 이면 저장한 주먹 자세 그대로")] [Range(0f, 1f)] public float 주먹세기 = 1f;
    [Tooltip("쥐고 펴는 빠르기")] public float 주먹속도 = 14f;
    [SerializeField, HideInInspector] string[] 주먹뼈이름;
    [SerializeField, HideInInspector] Quaternion[] 주먹뼈자세;

    /// 지금 손 밑 뼈들의 자세를 「주먹」으로 기억한다 (에디터 도구가 부른다)
    public string 주먹으로저장()
    {
        var 뼈 = 손가락뼈();
        if (뼈.Count == 0) return "손 밑에 손가락 뼈가 없다 — 리깅이 먼저다 (RightHand 밑에 뼈를 넣어라)";
        주먹뼈이름 = new string[뼈.Count];
        주먹뼈자세 = new Quaternion[뼈.Count];
        for (int i = 0; i < 뼈.Count; i++) { 주먹뼈이름[i] = 뼈[i].name; 주먹뼈자세[i] = 뼈[i].localRotation; }
        return $"손가락 뼈 {뼈.Count}개의 지금 자세를 주먹으로 기억했다.";
    }

    /// `RightHand` 밑의 모든 뼈 (무기 붙이는 마커는 뺀다)
    List<Transform> 손가락뼈()
    {
        var 목록 = new List<Transform>();
        if (손뼈 == null) return 목록;
        foreach (var t in 손뼈.GetComponentsInChildren<Transform>(true))
        {
            if (t == 손뼈) continue;
            if (t.name.Contains("자리") || t.GetComponent<Renderer>() != null) continue;   // 무기자리·장비 모델은 뼈가 아니다
            목록.Add(t);
        }
        return 목록;
    }

    // ★★★**클립을 넣으면 코드 대신 그것이 동작을 그린다** (2026-08-09 사용자 "떄리는 모션은
    //   왜 애니메이션 수정이 안돼?"). 여태 때리는 동작은 전부 절차 모션이라 애니메이션 창에
    //   고칠 키프레임이 없었다. `Tools/토이라/공격 모션을 클립으로 굽기` 로 지금 동작을
    //   그대로 구워 넣고, 여기에 꽂으면 그때부터는 **클립이 정본**이다.
    //
    //   ☆비우면 예전대로 절차 모션이 돈다 — 클립을 고치다 망쳐도 되돌릴 길이 남는다.
    //   ☆판정은 **여기 상태 기계가 계속 쥔다** (`휘두름` 구간이 곧 쓸고 지나가는 구간).
    //     클립의 3막 비율을 크게 바꾸면 그림과 판정이 어긋나니 `예비·휘두름·여운` 도 같이 맞춰라.
    //   ☆클립이 안 건드리는 뼈(팔·척추 말고 나머지)는 걷기 클립이 계속 갖는다.
    [Header("공격 클립 (비우면 코드가 그린다)")]
    [Tooltip("때리는 동작 클립 — 넣으면 HeroHold·몸통스윙 대신 이게 그린다")]
    public AnimationClip 공격클립;

    [Header("느낌")]
    [Tooltip("맞히는 순간 아주 짧게 멈춘다 (초) — 0 이면 안 씀")] public float 히트스톱 = 0.045f;

    Hero hero;
    HeroHold 드는자세;
    Transform 무기, 몸, 손뼈, 팔뚝뼈, 모델인스턴스;
    float 스윙yaw, 스윙pitch;
    State state = State.쉼;
    float t, cd, 밀기cd, sweptFrom, 공격예약;
    readonly List<Critter> 맞은것 = new List<Critter>();
    float stopUntil;
    Vector3 놓은자리; Quaternion 놓은회전; bool 놓은적있나;

    void Awake()
    {
        hero = GetComponent<Hero>();
        몸 = transform.Find("몸");
        MakeWeapon();
    }

    void OnEnable()
    {
        놓은적있나 = false;
#if UNITY_EDITOR
        // ★`ExecuteAlways` 만으로는 편집 중에 매 프레임 돌지 않는다 (씬이 바뀔 때만 온다).
        //   무기를 끌면서 실시간으로 손에 붙는 걸 보려면 에디터 틱에 직접 물려야 한다.
        UnityEditor.EditorApplication.update -= 편집중갱신;
        UnityEditor.EditorApplication.update += 편집중갱신;
#endif
    }

#if UNITY_EDITOR
    void OnDestroy() { UnityEditor.EditorApplication.update -= 편집중갱신; }

    void 편집중갱신()
    {
        if (Application.isPlaying || this == null || !isActiveAndEnabled) return;
        if (무기 == null || 손뼈 == null || !손뼈.gameObject.activeInHierarchy) MakeWeapon();
        주먹쥐기();
        무기자세();
    }
#endif

    /// 손에 든 것 — 지금은 막대 하나.
    /// ★뼈 밑에 매달지 않는다. 자세는 매 프레임 `무기자세()` 가 손 위치에 맞춰 놓는다 —
    ///   뼈에 붙이면 뼈의 축과 크기(비균등일 수 있다)에 막대가 휘둘린다.
    void MakeWeapon()
    {
        // ★몸을 바꾸면(남↔여) 쥐고 있던 뼈가 꺼진 몸의 것이 된다 — 그때마다 다시 잡는다
        손뼈 = 뼈찾기("hand", "wrist");
        팔뚝뼈 = 뼈찾기("forearm", "lowerarm");

        // ★★★**손뼈 밑의 `무기자리` 가 정본이다** (2026-08-09 사용자 "무기자리,하고 밖에
        //   무기 따로 있는데 뭐가맞는거냐?"). 씬에 손뼈 자식으로 이미 있고, 저절로 손을
        //   따라가고, 끌어 맞춘 게 그대로 저장되고, 남녀 몸에 각각 있다.
        //   전에는 코드가 `캐릭터/무기` 를 따로 만들어 매 프레임 손 위치로 옮겼는데,
        //   그러면 **둘 다 그려져서 무기가 두 개 보이고 따로 논다.**
        //   → 자리가 있으면 그 밑의 모델을 쓰고, 코드는 자세에 손을 안 댄다.
        자리모드 = false;
        if (손뼈 != null)
        {
            var 자리 = 손뼈.Find("무기자리");
            if (자리 != null)
            {
                무기 = 자리.childCount > 0 ? 자리.GetChild(0) : 자리;
                무기자리 = 자리;
                자리모드 = true;
                var 군더더기 = transform.Find("무기");        // 코드가 만들었던 것은 치운다
                if (군더더기 != null && Application.isPlaying) Destroy(군더더기.gameObject);
                return;
            }
        }

        무기 = transform.Find("무기");
        if (무기 != null) return;
        // ★편집 중엔 **찾기만 한다** — 여기서 만들면 씬을 열 때마다 오브젝트가 생겨 지저분해진다.
        //   씬에 실물로 두려면 `Tools/토이라/무기 만들기` 를 한 번 누른다.
        if (!Application.isPlaying) return;

        var g = Grey.Box(transform, Vector3.zero, new Vector3(굵기, 굵기, 길이),
                         new Color(0.62f, 0.5f, 0.35f), "무기");
        무기 = g.transform;
    }

    /// 무기 모델 손질 — 모델이 꽂혀 있으면 상자를 감추고 모델을 얹는다.
    /// ★매 프레임 부른다. 인스펙터 값을 만지면 **플레이 중에도 즉시** 보이게 하려는 것이고,
    ///   비용은 트랜스폼 세 줄이라 사실상 0 이다.
    void 모델손질()
    {
        if (무기 == null) return;

        if (무기모델 == null)
        {
            if (모델인스턴스 != null) { Destroy(모델인스턴스.gameObject); 모델인스턴스 = null; }
            var mr0 = 무기.GetComponent<MeshRenderer>();
            if (mr0 != null && !mr0.enabled) mr0.enabled = true;      // 상자를 되살린다
            return;
        }

        if (모델인스턴스 == null || 모델인스턴스.gameObject == null)
        {
            var inst = Instantiate(무기모델, 무기);
            inst.name = "무기모델";
            모델인스턴스 = inst.transform;
            // ★상자는 **지우지 않고 감춘다** — 길이·굵기가 판정(`쓸고 지나간 각도`)의 기준이다
            var mr = 무기.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
        }

        // 겉(무기)이 손에 맞춰 놓이고, 안쪽 모델은 여기서만 움직인다.
        // ★겉의 크기가 (굵기,굵기,길이)로 눌려 있으므로 그걸 되돌려 곱한다 —
        //   안 그러면 모델이 납작해진다 (그게 옛 버그였다).
        모델인스턴스.localPosition = new Vector3(모델위치.x / Mathf.Max(1e-4f, 굵기),
                                             모델위치.y / Mathf.Max(1e-4f, 굵기),
                                             모델위치.z / Mathf.Max(1e-4f, 길이));
        모델인스턴스.localRotation = Quaternion.Euler(모델회전);
        모델인스턴스.localScale = new Vector3(모델크기 / Mathf.Max(1e-4f, 굵기),
                                          모델크기 / Mathf.Max(1e-4f, 굵기),
                                          모델크기 / Mathf.Max(1e-4f, 길이));
    }

    /// 무기를 들고 있으면 손가락을 저장해 둔 주먹 자세로 접는다.
    /// ★애니메이터가 손가락을 돌려도 이긴다 — 실행 순서 300 의 `LateUpdate` 에서 쓴다.
    /// ★손가락 뼈가 없으면 **아무 일도 안 한다.** 메시를 대신 찌그러뜨리지 않는다.
    void 주먹쥐기()
    {
        if (손뼈 == null || 주먹뼈이름 == null || 주먹뼈이름.Length == 0) return;
        if (손가락 == null || 손가락.Count != 주먹뼈이름.Length) 손가락잡기();

        bool 들었나 = 주먹 && 무기 != null && 무기.gameObject.activeInHierarchy;
        float 목표 = 들었나 ? Mathf.Clamp01(주먹세기) : 0f;
        쥔정도 = Application.isPlaying
            ? Mathf.Lerp(쥔정도, 목표, 1f - Mathf.Exp(-주먹속도 * Time.deltaTime))
            : 목표;
        if (쥔정도 < 0.001f) return;

        for (int i = 0; i < 손가락.Count; i++)
            if (손가락[i] != null)
                손가락[i].localRotation = Quaternion.Slerp(손가락[i].localRotation, 주먹뼈자세[i], 쥔정도);
    }

    void 손가락잡기()
    {
        손가락 = new List<Transform>();
        var 있는것 = 손가락뼈();
        foreach (var 이름 in 주먹뼈이름) 손가락.Add(있는것.Find(t => t.name == 이름));
    }
    List<Transform> 손가락; float 쥔정도;

    /// `eulerAngles` 는 0~360 으로 나와 −25° 가 335° 로 보인다 — 인스펙터에서 읽으라고 접어 준다
    static Vector3 읽기좋게(Vector3 e)
    {
        for (int i = 0; i < 3; i++) if (e[i] > 180f) e[i] -= 360f;
        return e;
    }

    /// 이름에 낱말이 든 **오른쪽** 뼈를 찾는다 (없으면 null)
    ///
    /// ★★**켜져 있는 몸에서만 찾는다** (2026-08-09 사용자 "무기를 든 손을 허벅지 뒤쪽으로
    ///   애매하게 빼고있는 버그"). 몸이 둘(남/여)인데 꺼진 것까지 훑으면 **안 보이는 몸의
    ///   손뼈**를 쥔다. 그 손은 딴 자세로 서 있으니 몽둥이가 허벅지 옆 허공에 뜬다.
    ///   `HeroHold.뼈잡기` 는 같은 이유로 이미 `false` 를 쓰고 있었는데 여기만 빠져 있었다.
    Transform 뼈찾기(params string[] 낱말)
    {
        Transform 후보 = null;
        foreach (var t in GetComponentsInChildren<Transform>(false))   // 켜진 것만
        {
            var n = t.name.ToLower();
            bool 맞나 = false;
            foreach (var w in 낱말) if (n.Contains(w)) { 맞나 = true; break; }
            if (!맞나) continue;
            if (n.Contains("left") || n.EndsWith(".l") || n.Contains("_l")) continue;
            후보 = t;
            if (n.Contains("right") || n.EndsWith(".r") || n.Contains("_r")) break;
        }
        return 후보;
    }

    /// 몽둥이를 손에 쥐여 놓는다 — **팔뚝에서 손으로 뻗는 방향**이 막대의 축이다.
    /// 애니메이터와 `HeroHold` 가 팔을 다 돌린 뒤에 부른다 (실행 순서 300).
    Transform 무기자리; bool 자리모드;

    void 무기자세()
    {
        if (무기 == null) return;

        // ★★★자리모드 — **코드는 `무기자리` 에 손을 대지 않는다** (2026-08-09 사용자
        //   "무기자리를 수정해도 안바뀌는데..?").
        //   잠깐 손목 젖힘(`스윙pitch`)을 여기 얹어 봤는데, 그러려면 기준 회전을 캐시해서
        //   **매 틱 다시 씌워야** 한다 — 씬에서 돌리는 족족 코드가 되돌려 버린다.
        //   손목 젖힘은 공격 클립이 갖게 하고, 이 자리는 통째로 사람 몫으로 넘긴다.
        //   그래야 끌어 맞춘 것이 그대로 남는다 (「소유권을 가른다」).
        if (자리모드) { 모델손질(); return; }

        Vector3 축 = 손뼈 != null && 팔뚝뼈 != null ? 손뼈.position - 팔뚝뼈.position : transform.forward;
        if (축.sqrMagnitude < 1e-6f) 축 = transform.forward;

        // ★★`LookRotation(축, Vector3.up)` 을 쓰면 안 된다 (2026-08-04 사용자 "무기가
        //   팔랑거리기도 하고"). 팔을 들면 축이 **거의 수직**이 되는데, 그때 위쪽 기준이
        //   축과 나란해져 회전이 정의되지 않는다 — 매 프레임 롤이 홱홱 뒤집히고,
        //   그 롤 위에 얹힌 `기울임` 이 같이 돌아 몽둥이가 팔랑거렸다.
        //   FromToRotation 은 **몸이 선 자세에서 최소로 비틀어** 축에 맞추므로 뒤집힘이 없다.
        var 팔기준 = Quaternion.FromToRotation(transform.forward, 축.normalized) * transform.rotation;
        var 손 = 손뼈 != null ? 손뼈.position
                             : transform.TransformPoint(new Vector3(0.3f, 1.15f, 0.15f));

        // 팔이 얼마나 들렸나 — 0 이면 멘 자세, 1 이면 싸우는 자세
        float 들림 = 드는자세 != null ? Mathf.Clamp01(드는자세.지금) : 0f;
        bool 싸움쪽 = 들림 > 0.5f;
        var 쓸자리 = Vector3.Lerp(멘자리, 쥔자리, 들림);
        var 쓸기울임 = Vector3.Lerp(멘기울임, 기울임, 들림);

        // ★★끌어 옮긴 것을 **되읽는다** — 우리가 지난 프레임에 놓은 자리와 다르면 사람이
        //   씬 뷰에서 옮긴 것이다. 그 차이를 `쥔자리`·`기울임` 으로 환산해 담고, 그다음부터는
        //   다시 코드가 손을 따라 놓는다. 그래서 **끌어도 손을 안 벗어나고, 값으로 남는다.**
        if ((직접조절 || !Application.isPlaying) && 놓은적있나 &&
            (Vector3.Distance(무기.position, 놓은자리) > 1e-4f || Quaternion.Angle(무기.rotation, 놓은회전) > 0.01f))
        {
            var 기본이었던 = Quaternion.AngleAxis(-스윙yaw, Vector3.up) * 무기.rotation
                           * Quaternion.Inverse(Quaternion.Euler(스윙pitch, 0f, 0f));
            var 읽은기울임 = 읽기좋게((Quaternion.Inverse(팔기준) * 기본이었던).eulerAngles);
            var 읽은자리 = Quaternion.Inverse(무기.rotation) * (무기.position - 손);
            // 지금 켜져 있는 쪽에 담는다 — 내리고 있으면 「멘 자세」, 들고 있으면 「싸우는 자세」
            if (싸움쪽) { 기울임 = 읽은기울임; 쥔자리 = 읽은자리; }
            else { 멘기울임 = 읽은기울임; 멘자리 = 읽은자리; }
            쓸자리 = 읽은자리; 쓸기울임 = 읽은기울임;
#if UNITY_EDITOR
            // 끌어 맞춘 값이 씬 저장에 남게 한다 (편집 중에는 더럽힘 표시가 있어야 저장된다)
            if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        var 기본 = 팔기준 * Quaternion.Euler(쓸기울임);
        // 휘두름 — 가로로 쓸고(세상 기준 yaw) 앞뒤로 눕는다(막대 자기 축 기준 pitch)
        var 회전 = Quaternion.AngleAxis(스윙yaw, Vector3.up) * 기본 * Quaternion.Euler(스윙pitch, 0f, 0f);

        무기.SetPositionAndRotation(손 + 회전 * 쓸자리, 회전);
        무기.localScale = new Vector3(굵기, 굵기, 길이);
        놓은자리 = 무기.position; 놓은회전 = 무기.rotation; 놓은적있나 = true;
        모델손질();          // 모델이 꽂혀 있으면 그 안쪽 자리를 맞춘다 (플레이 중 조절 가능)
    }

    void Update()
    {
        if (!Application.isPlaying) return;   // 편집 중엔 무기 자리만 보여준다 (LateUpdate)
        float dt = Time.deltaTime;
        cd -= dt; 밀기cd -= dt;

        if (Time.unscaledTime < stopUntil) return;   // 히트스톱

        if (!hero.Alive) { hero.MoveMul = 1f; return; }

        ReadClick(out bool 좌, out bool 우, out bool 뗌);

        // ★★들었다가 **놓는 순간** 휘두른다 (2026-08-04 사용자 "마우스 떼면 휘두르는 것까지").
        //   누르는 동안은 팔이 올라가 있고(예비), 떼면 내려친다. 누르는 시간이 곧
        //   예비 동작이라 「얼마나 감았나」가 손에 남는다 — 연타로는 안 되는 무게가 생긴다.
        //   ※휘두르는 동안은 팔이 **내려가야** 한다. 그 내려감 자체가 내려치는 동작이다.
        if (드는자세 == null) 드는자세 = GetComponent<HeroHold>();
        if (드는자세 != null)
        {
            // ★★★**쿨 동안 팔을 내리지 않는다** (2026-08-09 사용자 "클릭 연타했을떄 뚝뚝
            //   모션이 끊기면서 애매한 동작을 취하는데, 쿨일때는 모션이 아예 안들어가게").
            //
            //   전엔 여운·쿨에 `목표 = 0` 이라 팔이 **끝까지 내려갔다가 다시 올라왔다** —
            //   한 대 칠 때마다 0.57초(여운 0.22 + 쿨 0.35) 동안 내렸다 올리니 연타하면
            //   위아래로 덜컹거린다. 2026-08-07 에 「쿨엔 안 든다」로 고친 것도 같은 증상을
            //   반대편에서 만진 것이라 덜컹임 자체는 남아 있었다.
            //
            //   → **싸우는 동안은 계속 들고 있는다.** 내려치고 나면 팔이 평소 자세가 아니라
            //     **든 자세로** 돌아온다(`침` 이 쿨에 걸쳐 1→0). 그래서 다음 타가 이미 감긴
            //     자리에서 나가고(예비 건너뜀), 팔이 오르내리는 구간이 아예 사라진다.
            //   ★★**다음 타가 없으면 다시 안 든다** (2026-08-09 사용자 "때리고 나서 왜 또
            //     한번 손을 들었다가 내리냐"). `침 = 0` 이 곧 「든 자세」라서, 쿨 동안 침을
            //     0으로 되돌리면 때린 뒤에 팔이 **한 번 더 올라갔다가** 내려간다.
            //     → 되감는 건 **또 칠 사람에게만** 필요하다. 손을 떼면 내려친 자세(침=1)를
            //       유지한 채 그대로 평소 자세로 풀린다 — 올라가는 구간이 없다.
            // ★★★**누르는 동안 「드는 동작」이 실제로 흐른다** (2026-08-09 사용자 "드는모션을
            //   한 다음 멈춰있게해야하는데 바로 들고잇는 모션으로 가고, 떼면 다시 들었다가 때리는데").
            //   전에는 누르자마자 클립의 예비 끝으로 **순간이동**했고, 떼면 상태 기계가 예비를
            //   **또 한 번** 돌아서 두 번 들었다.
            //   → 누르는 동안 `감은시간` 이 0 → `예비` 로 흐르고 거기서 멈춘다. 떼면 이미
            //     감겨 있으니 예비를 건너뛰고 곧장 휘두름으로 간다.
            누름중 = 좌;
            if (state == State.쉼 && cd <= 0f)
                감은시간 = 좌 ? Mathf.Min(예비, 감은시간 + dt)
                             : Mathf.Max(0f, 감은시간 - dt * 2f);   // 놓으면 스르륵 풀린다
            bool 또칠건가 = (좌 || Time.time <= 공격예약) && state != State.채집;
            // ★채집 중엔 팔을 들지 않는다 — 쪼그려 앉아 뒤적이는 것이라 손이 아래에 있다.
            //   단 **나무·돌을 팰 때는 든다** — 도끼를 들어야 내려찍는 게 보인다
            bool 싸우는중 = 또칠건가 || (state != State.쉼 && state != State.채집) || 채집팸;
            // ★클립이 꽂혀 있으면 **팔 자세 코드는 아예 안 돈다** — 같은 뼈를 놓고 다투면
            //   HeroHold 가 들어 올린 것을 클립이 다시 덮어써서 헛돌기만 한다.
            드는자세.목표 = 공격클립 != null ? 0f : (싸우는중 ? 1f : 0f);
            // 내려치는 것 — 휘두르는 동안 위 → 앞아래로. **가속**해야 내려치는 힘이 보인다
            float u = state == State.휘두름 ? Mathf.Clamp01(t / Mathf.Max(0.01f, 휘두름)) : 0f;
            // ★★★★**쿨이 끝나도 「또 칠 사람」에게만 든 자세를 준다** (2026-08-11 사용자
            //   "한번짧게 클릭해서 공격하면 손을 두번 휘두르는 버그").
            //
            //   `침 = 0` 은 **든 자세**다. 옛 순서는 `cd <= 0f ? 0f` 가 먼저라, 한 대 치고
            //   쿨이 풀리는 순간 침이 **1(내려친 자세) → 0(든 자세)** 로 튀었다.
            //   그 사이 `목표` 는 이미 0 으로 빠지는 중이라, 팔이 **한 번 더 올라갔다가**
            //   스르르 내려간다 — 눈에는 「두 번 휘두른다」로 보인다.
            //   ☆이 의도는 이미 적혀 있었다 (466행 "다음 타가 없으면 다시 안 든다",
            //     2026-08-09 사용자 "때리고 나서 왜 또 한번 손을 들었다가 내리냐").
            //     맨 아래 `1f` 가 그걸 하는데, **`cd <= 0f` 가지에만 그 조건이 빠져 있었다.**
            드는자세.침 = state == State.휘두름 ? u * u
                        : state == State.여운 ? 1f
                        : 채집팸 ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(채집게이지))   // 게이지 = 도끼질 진행도
                        : state == State.채집 ? 1f                // 뒤적임 — 손이 아래에
                        : state == State.예비 ? 0f                // 감는 중 — 머리 위로
                        : !또칠건가 ? 1f                          // ★안 칠 거면 내려친 자세 그대로 풀린다
                        : cd <= 0f ? 0f                           // 또 칠 사람에게만 = 든 자세
                        : Mathf.Clamp01(cd / Mathf.Max(0.01f, 공격쿨));   // 쿨에 걸쳐 되감는다
        }

        // ── 밀기 (우클릭) — 언제든 끼어들 수 있다. 이게 탈출 수단이라서
        if (우 && 밀기cd <= 0f && hero.stamina >= 밀기소모)
        {
            밀기cd = 밀기쿨;
            hero.stamina -= 밀기소모;
            hero.회복정지끝 = Time.time + 회복정지;
            Shove();
        }

        // ── 상태 진행
        switch (state)
        {
            case State.채집:
            {
                t += dt;
                채집중 = true;
                // 다 캤거나 · F 를 다시 눌렀거나 · 멀어지면 끝난다 (놓는다고 안 멈춘다)
                if (채집대상 == null || F방금눌림) { 채집끝(); break; }
                // ★★**걸으면 풀린다** (2026-08-11 사용자 "갈무리도중에 이동하면 풀려야하는데
                //   계속 캐지는 버그 이동하면서"). 쪼그려 앉아 뒤적이면서 걸어갈 수는 없다.
                //   ☆`채집이속` 으로 느려지긴 해도 0 은 아니라, 걸으면 실제로 움직인다.
                if (hero.속도.magnitude > 채집끊는속도) { 채집끝(); break; }
                // ★멀어졌나도 **실제 그림**까지의 거리로 잰다 (잡을 때와 같은 자)
                if (채집대상.수평거리(transform.position) > 사거리 + 1.2f) { 채집끝(); break; }
                var v채 = 채집대상.transform.position - transform.position; v채.y = 0f;
                hero.MoveMul = 채집이속;              // 쪼그려 앉아 뒤적이는 동안은 느리다
                // ★일하는 동안은 대상을 본다 — 패는 곳과 보는 곳이 같아야 한다
                var 볼방향 = 채집대상.경계중심 - transform.position; 볼방향.y = 0f;
                if (볼방향.sqrMagnitude > 0.01f) { hero.시선고정 = true; hero.고정시선 = 볼방향; }
                채집게이지 += 채집대상.캐는속도() * dt;

                // ★★★★**도끼가 닿는 순간에 나무가 흔들린다** (2026-08-11 사용자 "패는것과
                //   나무가 피격되는 동작을 딱 맞추라는거야").
                //   게이지 1.0 에서 때리면 **스윙이 다 끝난 뒤**에 나무가 흔들린다 —
                //   클립의 타격은 중간(예비 끝 + 휘두름의 58%)에 있기 때문이다.
                //   → 게이지가 그 몫을 지나는 순간 때린다. 나머지 게이지는 여운이다.
                //   ☆「그림 = 판정」 — 도끼가 닿는 프레임과 자원이 깎이는 프레임이 같아진다.
                float 타격몫 = 채집팸
                    ? (예비 + 휘두름 * 0.58f) / Mathf.Max(0.01f, 예비 + 휘두름 + 여운)
                    : 1f;                                   // 뒤적임은 다 차면
                if (!이번칸때림 && 채집게이지 >= 타격몫)
                {
                    이번칸때림 = true;
                    var 방향 = v채.sqrMagnitude > 1e-4f ? v채.normalized : transform.forward;
                    if (!채집대상.한칸(방향)) { 채집끝(); break; }   // 다 캐서 사라졌다
                }
                if (채집게이지 >= 1f) { 채집게이지 = 0f; 이번칸때림 = false; }
                break;
            }

            case State.쉼:
                hero.MoveMul = 1f;
                채집중 = false; 채집게이지 = 0f;
                // ★★★★**누르고 있으면 캔다** — 「앞에 무엇이 있느냐가 정한다」(4장).
                //   짧게 딸깍하면 공격, 대상을 두고 **길게 누르면 채집**이다.
                //   ☆지연을 두는 이유: 없으면 나무 옆에서 짐승을 때릴 수가 없다.
                //     `감은시간` 은 누르고 있는 동안 흐르므로 그대로 자로 쓴다.
                // ★★★★**캐기·갈무리는 F 다** (2026-08-11 사용자 "클릭 말고, F 상호작용으로
                //   캐거나 갈무리하게 할까?"). 그게 맞다 — 4장에 F 가 이미
                //   *"앞에 무엇이 있느냐가 정한다"* 는 상호작용 키로 적혀 있다.
                //   ☆이렇게 가르면 **좌클릭 = 싸움 · F = 일** 이 되어 서로 안 싸운다.
                //     좌클릭에 캐기를 얹었더니 "나무 옆에서 짐승을 못 때린다" 가 났었다.
                if (F방금눌림 && cd <= 0f && !모닥불.F먹음)
                {
                    var 대상 = Harvest.찾기(transform.position, hero.LookDir, 사거리 + 0.6f);
                    if (대상 != null)
                    {
                        채집대상 = 대상; 채집게이지 = 0f; 이번칸때림 = false; 공격예약 = 0f;
                        state = State.채집; t = 0f; 채집중 = true; F먹음 = true;
                        break;
                    }
                }
                // ★입력 버퍼 (2026-08-07 사용자 "짧게 클릭하면 공격이 안 나가네") —
                //   쿨 중에 떼면 입력이 그냥 버려져서 손만 들다 말았다. 0.4초 안에
                //   쿨이 풀리면 예약된 공격이 나간다. 격투 게임의 표준 수법이다.
                if (뗌) 공격예약 = Time.time + 0.4f;
                // ★★숨이 모자라면 **예약을 지운다** (2026-08-11). 안 지우면 예약이 살아 있는
                //   0.4초 동안 팔이 들린 채 기다렸다가 스르르 내려가고, 다음 클릭에 또 든다 —
                //   "손을 한번 내렸다 다시 들어서 팬다" 로 보인다. 못 칠 거면 팔부터 내린다.
                if (Time.time <= 공격예약 && cd <= 0f && hero.stamina < 지구력소모) 공격예약 = 0f;
                if (Time.time <= 공격예약 && cd <= 0f && hero.stamina >= 지구력소모)
                {
                    공격예약 = 0f;
                    hero.stamina -= 지구력소모;
                    hero.회복정지끝 = Time.time + 회복정지;
                    맞은것.Clear();
                    캤나 = false;              // 이번 휘두름에서 아직 안 캤다
                    t = 0f;
                    // ★이미 팔이 올라가 있으면 예비를 건너뛴다 — 감아 둔 사람에게
                    //   또 0.14초를 기다리게 하면 "떼면 나간다" 가 아니라 "떼면 늦게 나간다" 가 된다.
                    //   딸깍(누르자마자 떼기)은 아직 안 올라갔으니 예비를 거친다.
                    // ★감았는지는 이제 **`감은시간`** 이 안다 (팔 자세 코드는 클립이 있으면 안 돈다)
                    bool 감았나 = 감은시간 >= 예비 * 0.7f
                                || (공격클립 == null && 드는자세 != null && 드는자세.지금 > 0.7f);
                    float 감긴만큼 = 감은시간;
                    감은시간 = 0f;
                    if (감았나) { state = State.휘두름; sweptFrom = -각도 * 0.5f; }
                    else
                    {
                        // ★★★★**이어서 감는다 — 처음부터 다시 감지 않는다** (2026-08-11 사용자
                        //   "한번짧게 클릭해서 공격하면 손을 두번 위로들어").
                        //
                        //   짧게 딸깍해도 누른 1~2프레임 동안 `감은시간` 이 0.02초쯤 쌓이고,
                        //   클립은 그만큼 **이미 감기 시작한다**(964행 — 누르는 동안 클립이 그린다).
                        //   그런데 0.02 는 `예비 × 0.7` 에 못 미쳐 `감았나` 가 false 라,
                        //   `t = 0` 으로 예비를 **처음부터 다시** 돌았다 → 팔이 두 번 올라갔다.
                        //   ☆앞선 수정(`침` 순서)은 헛다리였다 — 클립이 있으면 988행에서
                        //     절차 자세를 재우므로 `침` 은 아무 영향이 없다.
                        //   → 감긴 만큼에서 **이어서** 간다. 딸깍이든 길게든 들기는 한 번뿐이다.
                        state = State.예비;
                        t = Mathf.Clamp(감긴만큼, 0f, 예비 * 0.95f);
                    }
                }
                break;

            // ★휘두르는 동안 발을 묶지 않는다 (2026-08-04 사용자 "느려지지도 말고,
            //   그냥 정상적인 걷기이면서 동작만"). 예전엔 세 상태가 모두 이속을 35% 로
            //   깎았는데, 좌클릭을 **누르고 있으면 이 세 상태가 계속 돌아** 드는 내내
            //   35% 였다. 그러면 실제 속도가 2.6 → 0.91m/s 로 떨어지고 `HeroAnim` 의
            //   빠르기가 0.35 가 되어 **정지와 걷기를 섞은 어정쩡한 반걸음**이 나온다.
            //   "손을 들면 걸음걸이가 이상해진다" 의 정체가 이것이었다 — 팔 자세(HeroHold)
            //   는 다리를 건드린 적이 없다.
            case State.예비:
                t += dt;
                Pose(-1f, Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, 예비)));
                if (t >= 예비) { state = State.휘두름; t = 0f; sweptFrom = -각도 * 0.5f; }
                break;

            case State.휘두름:
                t += dt;
                {
                    float u = Mathf.Clamp01(t / Mathf.Max(0.01f, 휘두름));
                    float now = Mathf.Lerp(-각도 * 0.5f, 각도 * 0.5f, u);
                    Sweep(sweptFrom, now);       // 지난 프레임부터 지금까지 쓸고 간 구간
                    sweptFrom = now;
                    Pose(1f, u);
                    // ★★휘두르는 **동안의 제일 먼 거리**가 곧 이 무기의 사거리다.
                    //   `닿는거리()` 는 「지금 이 순간」 무기 끝이라 평소엔 팔이 내려가 있어
                    //   짧게 나온다 — 그걸 그대로 그리면 표시가 매 프레임 출렁인다
                    //   (2026-08-09 사용자 "계속 변하는 고리가 ;; 좀 피격 거리도 아닌거같고").
                    이번최대 = Mathf.Max(이번최대, 닿는거리());
                }
                if (t >= 휘두름)
                {
                    if (이번최대 > 0.01f) 표시사거리 = 이번최대;   // 한 판이 끝나야 값이 확정된다
                    이번최대 = 0f;
                    state = State.여운; t = 0f;
                }
                break;

            case State.여운:
                t += dt;
                Pose(1f, 1f - Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, 여운)));
                if (t >= 여운) { state = State.쉼; cd = 공격쿨; Pose(0f, 0f); }   // 쿨이 리듬을 만든다
                break;
        }
    }

    /// ★★**F 는 한 번 누르면 다 캘 때까지 간다** (2026-08-11 사용자 "F를 누르면, 다캘때까지
    ///   패는 모션이 나오는거야 누르고 있는게 아니라"). 누르고 있게 하면 손가락이 아프고,
    ///   무엇보다 **다 캤는지 보려고 게이지를 계속 쳐다보게** 된다.
    ///   ☆끝나는 조건: 다 캤다 / F 를 다시 눌렀다 / 멀어졌다 / 대상이 사라졌다.
    bool F방금눌림
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            return k != null && k.fKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F);
#endif
        }
    }

    /// 지금 캐는 것이 **패는 것**인가 (나무·돌) — 사체는 쪼그려 뒤적인다
    bool 채집팸 => state == State.채집 && 채집대상 != null && 채집대상.kind != Stock.Kind.고기;

    /// ★F 를 채집이 가져갔나 — `HeroCarry`(붙잡기)가 같은 F 로 끼어들지 않게 알려 준다.
    ///   `모닥불.F먹음` 과 같은 방식이다.
    public static bool F먹음;

    /// ★★앞에 **살아 있는** 짐승이 있나 — 있으면 캐는 게 아니라 때리는 것이다 (4장).
    ///   ☆사체는 `Critter` 가 아니므로 안 걸린다 — 사체 옆에서는 그냥 갈무리가 된다.
    ///   ☆판정은 때리는 것과 **같은 자**로 잰다 (실루엣·부채꼴) — 안 그러면
    ///     "때릴 수 있는데 캐진다" 가 난다.
    bool 앞에적있나()
    {
        var p = transform.position;
        var look = hero.LookDir;
        float 닿음 = Mathf.Max(닿는거리(), 표시사거리);
        float 반각 = 표시각도 * 0.5f;
        for (int i = Critter.All.Count - 1; i >= 0; i--)
        {
            var c = Critter.All[i];
            if (c == null || !c.Alive || c.side != Critter.Side.야생) continue;
            var v = c.transform.position - p; v.y = 0f;
            if (v.magnitude > 닿음 + c.Radius + 0.6f) continue;
            if (실루엣판정.부채꼴에맞나(c.transform, p, look, 닿음, 표시각도, 판정거리여유)) return true;
        }
        return false;
    }

    /// 채집을 끝낸다 — 놓았거나, 멀어졌거나, 다 캤거나
    /// 상체·하체 공격 레이어를 부드럽게 내린다 — 걷기(0층)가 다시 몸을 갖는다
    void 상체층내리기()
    {
        if (애니 == null || !애니.isActiveAndEnabled) { 애니 = GetComponentInChildren<Animator>(); }
        if (애니 == null) return;
        if (공격층 < 0) 공격층 = 층찾기("공격층");
        if (하체층 < 0) 하체층 = 층찾기("공격하체층");
        if (공격층 > 0)
            애니.SetLayerWeight(공격층, Mathf.MoveTowards(애니.GetLayerWeight(공격층), 0f, Time.deltaTime * 8f));
        if (하체층 > 0)
        {
            하체무게 = Mathf.MoveTowards(하체무게, 0f, Time.deltaTime * 10f);
            애니.SetLayerWeight(하체층, 하체무게);
        }
        하체정함 = false;
        클립돌던중 = false;
    }

    void 채집끝()
    {
        채집대상 = null; 채집게이지 = 0f; 채집중 = false; F먹음 = false;
        if (hero != null) hero.시선고정 = false;      // 마우스로 되돌린다
        state = State.쉼; t = 0f; 감은시간 = 0f;
        공격예약 = 0f;                   // F 로 캐다 놓았다고 공격이 튀어나가면 안 된다
    }

    /// 무기와 몸의 자세 — 예비엔 뒤로 감고, 휘두르면 앞으로 지나간다
    /// dir: -1 = 감음 · +1 = 휘두름 / k: 0~1 진행도
    void Pose(float dir, float k)
    {
        // ★자세를 여기서 쓰지 않고 값만 남긴다 — 실제로 놓는 것은 `무기자세()` 다.
        //   팔이 다 돌아간 뒤에 놓아야 손을 안 벗어난다
        // ★dir 0 = 원위치. 이걸 안 갈라내면 휘두름 시작값(-55°)이 그대로 남아,
        //   평소에도 몽둥이가 옆으로 55° 틀어진 채 걸어다닌다
        if (dir == 0f)
        {
            스윙yaw = 0f; 스윙pitch = 0f;
            if (몸 != null) 몸.localRotation = Quaternion.identity;
            return;
        }
        // ★★★몽둥이를 **따로** 휘두르지 않는다 (2026-08-04 사용자 "때리는 모션이 맞긴 한데
        //   정확히 앞부분을 때리질 않네, 몽둥이 궤적이 좀..").
        //
        //   전엔 팔이 안 움직여서 **몽둥이만** 세상 기준 yaw 로 ±55° 옆으로 쓸었다.
        //   그런데 이제 `HeroHold` 가 팔을 오른쪽 머리 위 → 앞아래로 실제로 내려친다.
        //   그 위에 옆쓸기까지 얹으니 **두 동작이 겹쳐** 궤적이 옆으로 밀리고 앞을 못 때렸다.
        //   → 몽둥이는 이제 **손을 따라가기만** 한다. 휘두르는 것은 팔의 몫이다.
        //   ★남긴 pitch 는 손목이다 — 감을 때 끝을 세우고 칠 때 눕힌다. 옆으로는 안 민다.
        스윙yaw = 0f;
        스윙pitch = dir < 0f ? -손목 * k : Mathf.Lerp(-손목, 손목 * 0.6f, k);

        // 몸은 `HeroHold` 가 골반·척추로 돌린다 (여기서 또 돌리면 두 번 돈다)
    }

    /// ★★★**판정 거리는 무기 끝에서 나온다** (2026-08-09 사용자 "실제와 다르게 멀리서 맞추고 떄려").
    ///   실측: `사거리` 가 1.90m 로 박혀 있었는데 몽둥이 끝이 실제로 닿는 곳은 **0.81m** 였다 —
    ///   여유 0.2 까지 더하면 **그림의 2.6배** 되는 거리에서 맞았다.
    ///   → 매번 무기 끝까지의 거리를 재서 쓴다. 씬에서 무기를 길게/짧게 바꾸면 판정이 저절로 따라온다.
    ///   ☆무기를 못 찾으면 인스펙터의 `사거리` 로 떨어진다.
    // ★★★**판정의 여유를 상수로 꺼내 둔다** (2026-08-09 사용자 "휘두르는것보다 영역이
    //   더 좁은데?"). 전엔 이 숫자들이 `Sweep` 안에 박혀 있어서 조준 표시가 알 수가 없었고,
    //   그래서 표시가 실제 판정보다 **양쪽 14°씩 좁게** 그려졌다.
    //   → 판정과 표시가 **같은 상수**를 본다. 한쪽만 고쳐서 어긋날 방법이 없다.
    [Tooltip("판정이 부채꼴을 양쪽으로 더 여는 각 (°) — 빨리 지나가는 부채꼴이라 딱 자르면 헛방이 난다")]
    public float 판정각여유 = 14f;
    [Tooltip("실루엣 판정이 봐주는 거리 (m)")] public float 판정거리여유 = 0.12f;
    // ★★★**가로폭** (2026-08-11 사용자 "무기에만 피격판정을 넣어서 그런지 잘 안맞네..
    //   가로폭 범위가 없어서그런가"). 정확한 진단이었다 — 판정이 **각도로만** 재고 있었다.
    //   각도는 멀수록 넓고 가까울수록 좁다: 실측으로 앞 0.4m 에선 좌우 ±0.2m 밖에 안 맞았다.
    //   그런데 몽둥이는 **굵기가 있는 막대**라 코앞의 옆도 쓸고 지나간다.
    //   → 거리에 따라 각도를 늘려 **미터로 일정한 옆폭**을 만든다 (atan(옆폭/거리)).
    //     멀리서는 거의 안 변하고, 코앞에서만 넓어진다 — 그림과 맞는다.
    [Tooltip("좌우로 더 쳐주는 폭 (m) — 각도가 아니라 미터라 코앞에서도 옆이 안 좁아진다")]
    public float 가로폭 = 0.45f;

    /// 실제로 맞는 부채꼴 각도 — 조준 표시가 이걸 그린다
    public float 표시각도 => 각도 + 판정각여유 * 2f;

    float 이번최대;
    /// **판정이 실제로 쓴 사거리** — 조준 표시가 이걸 그린다.
    /// 아직 한 번도 안 휘둘렀으면 0 이고, 그때는 인스펙터 `사거리` 로 떨어진다.
    public float 표시사거리 { get; private set; }

    /// 지금 이 순간 무기 끝까지의 거리. **판정이 쓰는 값**이지만 프레임마다 변한다
    // ★★★**끝을 공식으로 짐작하지 않고 보이는 몸체를 잰다** (2026-08-11 사용자 "안맞는
    //   경우가 많아서 마우스 방향은 맞는데"). 옛 공식 `lossyScale.z*0.5` 는 회색 상자
    //   (크기 = (굵기,굵기,길이) · 중심축) 전용이다. 지금은 `무기자리` 에 진짜 몽둥이
    //   모델이 들어가 있어 그 공식이 절반을 깎았다 — 실측: 공식 끝 0.40m vs 실제 메시 끝
    //   0.91m. **그림의 절반 거리에서만 맞고 있었다.**
    //   → 렌더러 bounds 의 여덟 꼭짓점 중 제일 먼 수평거리를 쓴다. 상자든 모델이든,
    //     무기를 갈아끼우든 저절로 맞는다 (기울면 살짝 후해지는데, 후한 쪽이 맞다).
    Renderer[] 무기렌더러; Transform 무기렌더러주인;
    public float 닿는거리()
    {
        if (무기 == null) return 사거리;
        if (무기렌더러 == null || 무기렌더러주인 != 무기)
        { 무기렌더러 = 무기.GetComponentsInChildren<Renderer>(true); 무기렌더러주인 = 무기; }

        float far = 0f;
        foreach (var r in 무기렌더러)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            var mn = r.bounds.min; var mx = r.bounds.max;
            for (int i = 0; i < 8; i++)
            {
                var v = new Vector3((i & 1) == 0 ? mn.x : mx.x,
                                    (i & 2) == 0 ? mn.y : mx.y,
                                    (i & 4) == 0 ? mn.z : mx.z) - transform.position;
                v.y = 0f;
                far = Mathf.Max(far, v.magnitude);
            }
        }
        if (far <= 0.01f)      // 렌더러가 하나도 없으면 옛 공식으로 떨어진다 (상자 축)
        {
            var 축 = (무기.rotation * Vector3.forward).normalized;
            var 끝 = 무기.position + 축 * (무기.lossyScale.z * 0.5f);
            var w = 끝 - transform.position; w.y = 0f;
            far = w.magnitude;
        }
        return Mathf.Max(far, 0.2f) + 사거리여유;
    }
    // ★★0.10 → 0.30 (2026-08-09 사용자 "몸둥이가 닿는데 딜이 안 박히니까 이상한데").
    //   무기 끝은 **한 점**인데 판정은 그 점까지의 거리로만 봤다. 눈으로는 막대의 옆면이
    //   스치는데 끝점은 아직 안 닿은 순간이 있고, 그때 헛방이 난다.
    //   ☆판정을 그림보다 조금 **후하게** 주는 게 맞다 — 반대는 "닿았는데 안 맞았다" 가 된다.
    [Tooltip("무기 끝에서 더 주는 여유 (m) — 0 이면 그림 그대로")] public float 사거리여유 = 0.30f;

    /// a→b 각도 구간을 쓸면서, 그 안에 든 놈을 한 번씩 맞힌다
    void Sweep(float a, float b)
    {
        float lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
        var p = transform.position;
        var look = hero.LookDir;
        // ★★휘두르는 내내 **이 스윙이 닿을 최대 거리**로 판정한다 (2026-08-11 사용자
        //   "안맞는 경우가 많아서"). 순간값만 쓰면 몽둥이가 위로 들린 프레임엔 수평
        //   닿는거리가 짧아서, 하필 그때 각도가 쓸린 놈이 억울하게 빗나간다.
        //   `표시사거리` 는 지난 스윙의 최대 = **조준 고리가 그리는 바로 그 값** —
        //   고리 안에 있으면 맞는다. 판정과 표시가 같은 값을 본다 (이 파일의 원칙).
        float 닿음 = Mathf.Max(닿는거리(), 표시사거리);
        bool hit = false;

        for (int i = Critter.All.Count - 1; i >= 0; i--)
        {
            var c = Critter.All[i];
            if (c == null || !c.Alive || c.side != Critter.Side.야생) continue;
            if (맞은것.Contains(c)) continue;

            var v = c.transform.position - p; v.y = 0f;
            float d = v.magnitude;
            if (d > 닿음 + c.Radius + 1f) continue;        // 겉가지치기(싼 검사)만 남긴다

            // ★실루엣 판정 (2026-08-07) — 중심원이 아니라 **뼈 점들**로 본다.
            //   티라노의 내민 머리·사슴의 목도 화면에 보이는 그대로 맞는다.
            //   ★각도도 넉넉히 연다 — 빠르게 지나가는 부채꼴이라 딱 맞춰 자르면 헛방이 잦다
            // ★가로폭을 각도로 환산해 더한다 — 가까울수록 크게 열린다 (미터로 일정한 폭)
            float 옆각 = 가로폭 > 0.001f
                ? Mathf.Atan2(가로폭, Mathf.Max(0.35f, d)) * Mathf.Rad2Deg : 0f;
            if (!실루엣판정.쓸기에맞나(c.transform, p, look, 닿음,
                                       lo - 판정각여유 - 옆각, hi + 판정각여유 + 옆각, 판정거리여유)) continue;

            맞은것.Add(c);
            // ★★수치는 **쥔 무기**에서 나온다 (`인벤.쥔것`) — 코드에 안 박는다.
            //   맨손이면 인스펙터 기본값을 쓴다
            var 쥠 = 인벤.쥔것;
            float 낼피해 = (쥠 != null && 쥠.종.무기 ? 쥠.종.무기피해 : 피해)
                          * Mathf.Clamp(hero.생존힘, 0.2f, 1f);       // 굶으면 힘이 빠진다
            float 낼기절 = 쥠 != null && 쥠.종.무기 ? 쥠.종.무기기절 : (둔기 ? 기절력 : 0f);

            c.TakeDamage(낼피해, true);   // ★버티기를 굴린다 — 무거운 놈은 맞아도 하던 일을 계속한다 (밀기는 안 굴린다)
            if (c.Alive)
            {
                c.Knock(v, 넉백, 비틀);
                if (낼기절 > 0f) c.기절값먹임(낼기절);   // ★뾰족한 무기(창)는 0 이라 안 돈다
            }
            if (쥠 != null) 인벤.도구닳음쥔것();
            hit = true;
        }

        if (hit && 히트스톱 > 0f) stopUntil = Time.unscaledTime + 히트스톱;
    }

    /// 밀기 — 넘어뜨려 공간을 만든다. 피해는 거의 없다
    void Shove()
    {
        var p = transform.position;
        var look = hero.LookDir;
        float cos = Mathf.Cos(Mathf.Deg2Rad * 밀기각도 * 0.5f);

        for (int i = Critter.All.Count - 1; i >= 0; i--)
        {
            var c = Critter.All[i];
            if (c == null || !c.Alive || c.side != Critter.Side.야생) continue;
            var v = c.transform.position - p; v.y = 0f;
            float d = v.magnitude;
            if (d > 밀기사거리 + c.Radius + 3f) continue;
            if (!실루엣판정.부채꼴에맞나(c.transform, p, look, 밀기사거리, 밀기각도)) continue;
            if (d > 0.01f && Vector3.Dot(v / d, look) < cos) continue;

            c.TakeDamage(밀기피해);
            if (c.Alive)
            {
                c.Knock(v, 밀기넉백, 0.3f, 넘어짐);
                c.기절값먹임(밀기기절);              // 미는 건 언제나 뭉툭한 짓이다
            }
        }

        // 미는 몸짓 — 무기를 앞으로 쭉
        스윙yaw = 0f; 스윙pitch = 0f;
    }

    void ReadClick(out bool 좌, out bool 우, out bool 뗌)
    {
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        좌 = m != null && m.leftButton.isPressed;
        우 = m != null && m.rightButton.wasPressedThisFrame;
        뗌 = m != null && m.leftButton.wasReleasedThisFrame;
#else
        좌 = Input.GetMouseButton(0);
        우 = Input.GetMouseButtonDown(1);
        뗌 = Input.GetMouseButtonUp(0);
#endif
    }

    /// 캐기는 휘두름이 끝나는 순간 한 번 — 공격과 같은 동작을 쓴다
    // ★★★**몸통 스윙** (2026-08-07 사용자 "손만 움직이고 몸뚱이가 통째로 회전하는데
    //   때리는 동작도 만들어줘야 하지 않을까?").
    //
    //   팔은 절차(HeroHold)로 들리는데 몸통이 3막 없이 통짜로만 돌아서 어색했다.
    //   클립을 새로 저작하는 대신 **공격 상태 기계의 타이밍에 정확히 동기화된
    //   척추 회전**을 애니메이터 뒤(실행 순서 300)에 얹는다 — 모션 3막 그대로:
    //   ①예비: 반대로 감는다(-34°) ②휘두름: 가속하며 돌려친다(+26°)
    //   ③여운: 오버슈트로 지나쳤다 잦아든다. 머리는 60% 반대로 돌아 시선을 지킨다.
    [Header("몸통 스윙")]
    [Tooltip("몸통이 감기는 최대 각 (°)")] public float 몸감기 = 34f;
    [Tooltip("몸통이 돌려치는 최대 각 (°)")] public float 몸치기 = 26f;

    Transform 등뼈2, 등뼈1, 가슴뼈, 머리뼈;
    Transform 골반뼈, 왼허벅, 오른허벅, 왼정강, 오른정강, 왼발목, 오른발목;
    float 몸yaw, 몸pitch, 웅크림, 무게, 내딛음;
    Vector3 지난자리; float 이동평활;
    [Tooltip("예비 때 몸을 낮추는 깊이 (m) — 하체가 시동을 거는 만큼")] public float 웅크림깊이 = 0.09f;
    [Tooltip("앞뒤로 벌리는 발 간격 각 (°) — 0 이면 두 발이 나란히 선다")] public float 스탠스 = 11f;
    // ★★**발이 실제로 나가야 「친다」로 읽힌다** (2026-08-09 사용자 "공격하는 모습도 바뀐게
    //   거의없어 그냥 서서치는거같아"). 척추만 돌리면 실루엣이 거의 안 바뀐다 — 사람이
    //   후릴 때 눈에 띄는 건 **앞발이 한 걸음 나가고 몸이 그 위로 실리는 것**이다.
    //   실측: 허벅 1° 당 발이 약 0.019m 움직인다 (−16° → 0.303m) → 14° ≈ 26cm 걸음.
    [Tooltip("휘두를 때 앞발이 내딛는 각 (°) — 0 이면 제자리에서 친다")] public float 내딛기각 = 22f;
    [Tooltip("내딛는 동안 발이 뜨는 정도 (°) — 0 이면 바닥을 끈다")] public float 내딛기들림 = 10f;

    void 몸통스윙(float dt)
    {
        // 몸이 갈릴 수 있으니(남↔여) 놓치면 다시 잡는다
        if (등뼈2 == null || !등뼈2.gameObject.activeInHierarchy)
        {
            등뼈2 = 등뼈1 = 가슴뼈 = 머리뼈 = 골반뼈 = 왼허벅 = 오른허벅 = 왼정강 = 오른정강 = 왼발목 = 오른발목 = null;
            foreach (var tr in GetComponentsInChildren<Transform>(false))
                switch (tr.name)
                {
                    case "Spine02": 등뼈2 = tr; break;
                    case "Spine01": 등뼈1 = tr; break;
                    case "Spine":   가슴뼈 = tr; break;
                    case "Head":    머리뼈 = tr; break;
                    case "Hips":        골반뼈 = tr; break;
                    case "LeftUpLeg":   왼허벅 = tr; break;
                    case "RightUpLeg":  오른허벅 = tr; break;
                    case "LeftLeg":     왼정강 = tr; break;
                    case "RightLeg":    오른정강 = tr; break;
                    case "LeftFoot":    왼발목 = tr; break;
                    case "RightFoot":   오른발목 = tr; break;
                }
            if (등뼈2 == null) return;
        }

        // 상태별 목표 각 (경계값이 이어져 있어 매끄럽다: -감기 → -감기 → +치기 → 0)
        // ★무게: 0 = 뒷발에 실림(감는 중) · 1 = 앞발로 다 넘어감(친 뒤).
        //   한손 스윙의 하체는 결국 **무게를 뒤에서 앞으로 옮기는 일**이라, 각도가 아니라
        //   이 한 값이 앞뒤 다리를 갈라 굽힌다.
        // ★걸음은 **무게와 따로** 둔다. 무게를 그대로 걸음으로 쓰면 가만히 들고만 있어도
        //   (무게 0.3) 앞발이 나가고 떠 있게 된다 — 실제로 6cm 떠 있었다.
        float 목표yaw = 0f, 목표pitch = 0f, 빠르기 = 16f, 목표웅크림 = 0f, 목표무게 = 0.5f, 목표내딛음 = 0f;
        switch (state)
        {
            case State.쉼:
                if (드는자세 != null && 드는자세.목표 > 0.5f)
                { 목표yaw = -몸감기 * 0.4f; 목표pitch = -2.5f; 목표웅크림 = 0.35f; 목표무게 = 0.3f; }   // 감아 든 채 무게 낮춤
                break;
            case State.예비:
            {
                float w = Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, 예비));
                목표yaw = -몸감기 * w; 목표pitch = -6f * w; 빠르기 = 22f;
                목표웅크림 = w;                              // ★하체가 시동 — 무릎 굽혀 힘 모으기
                목표무게 = Mathf.Lerp(0.5f, 0f, w);          // 뒷발로 실린다
                break;
            }
            case State.휘두름:
            {
                float u = Mathf.Clamp01(t / Mathf.Max(0.01f, 휘두름));
                float k = u * u;                         // 폭발 가속 (Ease In)
                목표yaw = Mathf.Lerp(-몸감기, 몸치기, k);
                목표pitch = Mathf.Lerp(-6f, 12f, k);
                목표웅크림 = Mathf.Lerp(1f, 0.3f, k);        // ★다리를 펴며 지면을 민다 — 힘의 근원
                목표무게 = k;                                // ★뒷발이 밀고 앞발이 받는다
                목표내딛음 = k;                              // ★앞발이 한 걸음 나간다
                빠르기 = 40f;                             // 이 구간만은 즉각 따라붙는다
                break;
            }
            // ★★채집 — **무엇을 캐느냐로 몸짓이 갈린다** (2026-08-11 사용자
            //   "다캘때까지 **패는** 모션" · "갈무리, 쪼그려앉아서 **뒤적이는** 모션").
            case State.채집:
            {
                if (채집팸)
                {
                    // 나무·돌 — 크게 내려찍는다. **게이지가 곧 스윙 진행도**다
                    float 팸 = Mathf.Clamp01(채집게이지);
                    float e = Mathf.SmoothStep(0f, 1f, 팸);
                    목표웅크림 = 0.25f + 0.35f * e;
                    목표pitch = Mathf.Lerp(-8f, 24f, e);      // 젖혔다 내려찍는다
                    목표yaw = Mathf.Lerp(-몸감기 * 0.5f, 몸치기 * 0.4f, e);
                    목표무게 = Mathf.Lerp(0.3f, 0.85f, e);
                    빠르기 = 24f;
                }
                else
                {
                    // ★★★★**진짜로 앉는다** (2026-08-11 사용자 "앉는게 아니라니까 지금?").
                    //   `웅크림 = 1` 은 골반을 **9cm** 내릴 뿐이다 — 공격 예비 동작에 맞춰
                    //   놓은 값이라 「살짝 굽힘」이다. 앉으려면 그 몇 배가 필요하다.
                    //   ☆`웅크림` 은 곱해지는 값이라 1 을 넘겨도 된다 — 골반 내림과 무릎
                    //     접힘이 **같은 비율로** 같이 커진다(다리굽히기가 이 값을 쓴다).
                    //     실측 기준: 2.6 이면 골반 약 23cm · 허벅 −42° · 정강 +62° = 쪼그려 앉기.
                    목표웅크림 = 앉기굽힘;
                    목표무게 = 0.5f;
                    목표pitch = 22f + Mathf.Sin(t * 7f) * 5f;    // 앞으로 숙이고 상하로 뒤적인다
                    목표yaw = Mathf.Sin(t * 3.1f) * 7f;          // 좌우로 헤집는다
                    빠르기 = 9f;                                  // 앉는 데는 시간이 걸린다
                }
                break;
            }

            case State.여운:
            {
                float sm = Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, 여운));
                목표yaw = Mathf.Lerp(몸치기, 0f, sm) - 6f * Mathf.Sin(sm * Mathf.PI);   // 살짝 지나쳤다 돌아온다
                목표pitch = Mathf.Lerp(12f, 0f, sm);
                목표웅크림 = Mathf.Lerp(0.3f, 0f, sm);       // 남은 웅크림이 풀린다
                목표무게 = Mathf.Lerp(1f, 0.5f, sm);         // 앞으로 넘어간 무게가 가운데로 돌아온다
                목표내딛음 = Mathf.Lerp(1f, 0f, sm);         // 내딛은 발이 제자리로 끌려 돌아온다
                빠르기 = 14f;
                break;
            }
        }

        // ★걷는 중엔 다리 간섭을 죽인다 — 걷기 클립과 싸우면 다리가 덜덜거린다
        var 지금p = transform.position;
        float v2 = dt > 1e-5f ? (지금p - 지난자리).magnitude / dt : 0f;
        지난자리 = 지금p;
        이동평활 = Mathf.Lerp(이동평활, v2, 1f - Mathf.Exp(-8f * dt));
        // ★앉아서 뒤적일 때는 이 억제를 안 건다 — 앉자마자 살짝 미끄러지기만 해도 벌떡 선다.
        //   (움직이면 채집 자체가 끊기므로 여기서 또 막을 이유가 없다)
        if (!(state == State.채집 && !채집팸))
            목표웅크림 *= Mathf.Clamp01(1f - 이동평활 / 1.6f);

        float k2 = 1f - Mathf.Exp(-빠르기 * dt);
        몸yaw = Mathf.Lerp(몸yaw, 목표yaw, k2);
        몸pitch = Mathf.Lerp(몸pitch, 목표pitch, k2);
        웅크림 = Mathf.Lerp(웅크림, 목표웅크림, k2);
        무게 = Mathf.Lerp(무게, 목표무게, k2);
        내딛음 = Mathf.Lerp(내딛음, 목표내딛음, k2);
        if (Mathf.Abs(몸yaw) < 0.05f && Mathf.Abs(몸pitch) < 0.05f && 웅크림 < 0.01f) return;

        // ── 하체: 골반을 내리고 무릎을 굽힌다 (IK 없이 — 허벅·정강·발목 절차 회전).
        //
        // ★★★**부호가 갈려야 무릎이다** (2026-08-09 실측 — 사용자 "두 다리가 둘 다 뒤로
        //   빠지는게 맞아?"). 전엔 허벅 +16 · 정강 +24 로 **부호가 같았다.** 이 리그에서
        //   허벅 +X 는 발을 **뒤로 0.303m**, 정강 +X 는 **뒤로 0.243m** 보낸다 — 둘이 더해져
        //   발이 **뒤로 0.516m · 위로 0.211m** 날아갔다. 골반은 9cm 만 내려가니 발이 뜬 채
        //   뒤로 빠진다. 웅크린 게 아니라 다리를 통째로 뒤로 뻗고 있었던 것이다.
        //   → 엉덩이는 굽고(허벅 −) 무릎은 접혀야(정강 +) 발이 제자리에 남는다.
        //     실측: 허벅 −16 · 정강 +24 → 발 이동 (앞 0.057, 위 0.022) = 사실상 제자리.
        //
        // ★★**한손 스윙은 좌우가 다르다.** 두 다리에 같은 값을 먹이면 아무리 부호가 맞아도
        //   「양발 스쿼트」다. 오른손으로 후리는 몸은 **왼발을 앞에 딛고 오른발로 민다** —
        //   앞발은 무게를 받아 굽고, 뒷발은 감을 때 눌렸다가 칠 때 펴진다.
        //
        //   ★골반 로컬 1단위 = **0.0143m** (2026-08-09 실측). 코드에 0.011 로 적혀 있어
        //     웅크림이 인스펙터 값보다 30% 깊게 먹고 있었다.
        if (골반뼈 != null && 웅크림 > 0.01f)
        {
            var lp = 골반뼈.localPosition;
            lp.y -= 웅크림 * (웅크림깊이 / 0.0143f);
            골반뼈.localPosition = lp;

            // ★굽는 양의 **바탕은 골반이 내려간 만큼**이다 — 좌우가 여기서 갈리면 한쪽 발이
            //   반드시 뜬다. 무게 이동은 그 위에 얹는 **작은 치우침**으로만 낸다
            //   (실제 무게 이동은 골반 회전·스탠스가 눈에 보이게 만든다).
            float 치우침 = (무게 - 0.5f) * 2f;      // -1 = 뒷발에 실림 · +1 = 앞발로 넘어감
            float 벌림 = 스탠스 * 웅크림;
            // ★한 걸음 — 무게가 앞으로 넘어가는 만큼 앞발이 나간다. 넘어가는 도중(무게 0.5 부근)에
            //   가장 높이 뜨고 다 넘어가면 땅에 닿는다. 여운에 무게가 돌아오면 발도 같이 돌아온다.
            float 걸음 = Mathf.Clamp01(내딛음);
            float 들림 = Mathf.Sin(걸음 * Mathf.PI) * 내딛기들림;

            다리굽히기(왼허벅, 왼정강, 왼발목, -(벌림 + 내딛기각 * 걸음), 웅크림, +0.18f * 치우침, 들림);
            다리굽히기(오른허벅, 오른정강, 오른발목, +벌림, 웅크림, -0.18f * 치우침, 0f);
        }

        // 세 마디에 나눠 얹고, 머리는 반대로 — 시선이 표적에 남는다
        if (등뼈2 != null) 등뼈2.localRotation = 등뼈2.localRotation * Quaternion.Euler(몸pitch * 0.45f, 몸yaw * 0.45f, 0f);
        if (등뼈1 != null) 등뼈1.localRotation = 등뼈1.localRotation * Quaternion.Euler(몸pitch * 0.35f, 몸yaw * 0.35f, 0f);
        if (가슴뼈 != null) 가슴뼈.localRotation = 가슴뼈.localRotation * Quaternion.Euler(몸pitch * 0.20f, 몸yaw * 0.20f, 0f);
        if (머리뼈 != null) 머리뼈.localRotation = 머리뼈.localRotation * Quaternion.Euler(0f, -몸yaw * 0.6f, 0f);
    }

    /// 클립이 꽂혀 있으면 그것으로 자세를 그린다 — 코드(`HeroHold`·`몸통스윙`)는 손을 뗀다.
    ///
    /// ★애니메이터가 다 돌린 **뒤에** 얹는다(실행 순서 300). 클립이 건드리는 뼈만 덮어쓰므로
    ///   다리 걷기 같은 건 그대로 살아 있다.
    /// ★`쉼` 이고 쿨도 없으면 아무것도 안 한다 — 평소엔 걷기 클립이 온전히 몸을 갖는다.
    void 클립으로그리기()
    {
        float 총 = 예비 + 휘두름 + 여운;
        float 초;
        switch (state)
        {
            case State.예비: 초 = t; break;
            case State.휘두름: 초 = 예비 + t; break;
            case State.여운: 초 = 예비 + 휘두름 + t; break;
            // ★★★★**나무·돌을 팰 때는 공격 클립을 돌린다** (2026-08-11 사용자 "갈무리할때
            //   때리는 모션 넣어달라했는데 안넣었음"). 절차 자세(몸 숙임)만으로는
            //   **도끼질로 안 읽힌다** — 저작된 클립이 팔과 도구를 실제로 휘둘러야 한다.
            //   ☆클립의 예비~여운 구간만 되풀이한다. 꼬리(대기로 풀리는 구간)는 건너뛴다.
            // ★★★★**게이지 한 칸 = 스윙 한 번** (2026-08-11 사용자 "게이지가 총 3번차는데
            //   나무 팰때, 딱 그 팰때에 맞춰서 공격모션이 한번 들어가야하지않을까..?
            //   계속 휘두르는게아니라").
            //   맞다. 클립을 제 속도로 돌리면 **게이지와 따로 놀아서** 언제 한 칸이 들어가는지
            //   손에 안 잡힌다. 게이지를 그대로 클립 시간으로 쓰면 **다 찬 순간이 곧 타격**이다.
            //   ☆「그림 = 판정」의 자리다. 도끼가 닿는 순간과 자원이 깎이는 순간이 같아진다.
            case State.채집 when 채집팸:
                초 = Mathf.Clamp01(채집게이지) * (예비 + 휘두름 + 여운);
                break;
            default:
                // ★★★**누르고 있는 동안(감는 중)도 클립이 그린다** (2026-08-09 사용자 "클릭하고
                //   유지하고있을때의 모션이 안들어가네").
                //   누르고 있으면 `state` 는 아직 `쉼` 이라 여기서 그냥 빠져나갔고, 그 사이
                //   `HeroHold`(200) 가 옛 절차 자세로 팔을 들어 올렸다 — 떼는 순간에야 클립이
                //   들어오니 **감는 자세만 딴판**이었다.
                //   → 누르고 있으면 클립의 **예비 끝(감은 정점)** 에 물려 둔다.
                if (cd <= 0f && 감은시간 > 0.0001f) { 초 = 감은시간; break; }   // 드는 중 · 감은 채 대기

                // 쿨 동안은 클립의 **꼬리(여운 뒤)** 를 따라 평소 자세로 풀린다
                if (cd <= 0f)
                {
                    // 공격이 끝났으면 상체 레이어를 부드럽게 내린다 — 0층 걷기가 다시 상체를 갖는다
                    하체정함 = false;                       // 다음 스윙에서 다시 정한다
                    if (애니 != null && 공격층 > 0)
                    {
                        float w = Mathf.MoveTowards(애니.GetLayerWeight(공격층), 0f, Time.deltaTime * 8f);
                        애니.SetLayerWeight(공격층, w);
                        if (하체층 > 0)
                        {
                            하체무게 = Mathf.MoveTowards(하체무게, 0f, Time.deltaTime * 10f);
                            애니.SetLayerWeight(하체층, 하체무게);
                        }
                    }
                    클립돌던중 = false; return;
                }
                float 남은 = Mathf.Clamp01(1f - cd / Mathf.Max(0.01f, 공격쿨));
                초 = Mathf.Lerp(총, 공격클립.length, 남은);
                break;
        }
        // 든 자세 코드가 같이 돌면 두 번 그려져 떨린다 — 클립이 쥐는 동안은 재운다
        if (드는자세 != null) { 드는자세.목표 = 0f; 드는자세.침 = 0f; }

        if (애니 == null || !애니.isActiveAndEnabled) { 애니 = GetComponentInChildren<Animator>(); 몸뿌리 = 애니 != null ? 애니.gameObject : gameObject; }
        if (애니 == null) return;

        float n = Mathf.Clamp01(초 / Mathf.Max(0.01f, 공격클립.length));

        // ★★★**애니메이터한테 직접 재생시킨다. `SampleAnimation` 은 안 먹는다** (2026-08-09
        //   사용자 "아직 내가 넣은 모션이 아니라").
        //   애니메이터가 돌고 있는 몸에 `SampleAnimation` 을 찍으면, 다음 애니메이션 갱신에
        //   자기 상태(걷기)로 다시 덮어써서 **플레이에서는 아무 일도 안 난다.**
        //   편집 모드는 애니메이터가 멈춰 있어서 되는 것처럼 보였다 — 그래서 내가 두 번
        //   "된다"고 잘못 말했다.
        //   → 컨트롤러의 `공격` 상태를 **진행도를 직접 찍어서** 재생한다. 시계는 여전히
        //     이 상태 기계가 쥐므로 판정과 그림이 안 어긋난다.
        // ★★★**공격은 상체 레이어에만 얹는다** (2026-08-09 사용자 "들고있을떄 걷는건 또 왜없어져").
        //   전에는 0층(전신)에 `Play` 하고 `애니.speed = 0` 까지 세웠다 — 그러면 **다리까지
        //   공격 클립으로 덮이고 애니메이터가 통째로 멈춰서 걷기가 사라진다.**
        //   → 상체 마스크를 씌운 `공격층` 을 따로 두고 그 층만 켠다. 골반·다리는 0층의
        //     걷기가 계속 갖는다. 속도는 절대 안 건드린다.
        //   ☆진행도는 매 프레임 직접 찍으므로 클립이 저 혼자 흐르지 않는다.
        if (공격층 < 0) 공격층 = 층찾기("공격층");
        if (하체층 < 0) 하체층 = 층찾기("공격하체층");
        if (공격층 > 0)
        {
            애니.SetLayerWeight(공격층, 1f);
            애니.Play(공격해시, 공격층, n);

            // ★★★**서 있을 때는 다리까지 공격 모션이 나온다** (2026-08-09 사용자 "걷거나 다른
            //   동작이 아닐때는, 공격모션이 다리에있는 모션까지 다 나와야지").
            //   걷는 중에만 하체를 걷기에 양보한다 — 안 그러면 다리가 얼어붙는다.
            if (하체층 > 0)
            {
                // ★★★**하체를 쓸지는 공격 시작 때 한 번만 정한다** (2026-08-09 사용자
                //   "공격모션할때 두둑 다른동작이 섞이는 버그").
                //   전엔 매 프레임 속도로 가중치를 다시 계산해서, 휘두르는 도중에 걷다 서면
                //   다리가 **공격 ↔ 걷기 사이를 오가며 섞였다.** 그게 「두둑」의 정체다.
                //   → 한 번 정하면 그 스윙이 끝날 때까지 안 바꾼다.
                if (!하체정함)
                {
                    float 속 = hero != null ? hero.속도.magnitude : 0f;
                    하체목표 = 속 <= 걷기문턱 ? 1f : 0f;      // 섞지 않는다 — 쓰거나 안 쓰거나
                    하체정함 = true;
                }
                // ★★★★**걷기 시작하면 다리를 즉시 놓아준다** (2026-08-11 사용자 "가만히 서서
                //   공격을 하고 바로 걷기 시작하면 다리가 움직이지 않고 서서공격모션이 끝난뒤에
                //   걷기모션이 나오는데").
                //
                //   위에서 「한 번 정하면 안 바꾼다」로 못박은 것은 **섞임(두둑)** 을 막으려던
                //   것이었다(2026-08-09). 그런데 그 대가로 **서서 치기 시작하면 스윙이 끝날
                //   때까지 다리가 얼어붙었다.**
                //   → **한쪽으로만** 바꾼다: 놓아주기(1→0)는 하되 **되뺏지는 않는다**(0↛1).
                //     한 방향뿐이라 오갈 수가 없으니 「두둑」은 그대로 막힌다.
                else if (하체목표 > 0.5f && hero != null && hero.속도.magnitude > 걷기문턱)
                    하체목표 = 0f;
                하체무게 = Mathf.MoveTowards(하체무게, 하체목표, Time.deltaTime * 12f);
                애니.SetLayerWeight(하체층, 하체무게);
                if (하체무게 > 0.001f) 애니.Play(공격해시, 하체층, n);
            }
        }
        else 공격클립.SampleAnimation(몸뿌리, Mathf.Clamp(초, 0f, 공격클립.length));  // 층이 없으면 옛길
        클립돌던중 = true;
    }
    bool 클립돌던중; GameObject 몸뿌리; Quaternion? 자리기본; Animator 애니; bool 누름중; float 감은시간;
    static readonly int 공격해시 = Animator.StringToHash("공격");
    int 공격층 = -1, 하체층 = -1; float 하체무게, 하체목표; bool 하체정함;

    [Header("공격 클립 — 하체")]
    [Tooltip("이 속도(m/s)부터 다리를 걷기에 넘긴다")] public float 걷기문턱 = 0.15f;
    [Tooltip("넘기는 데 걸리는 속도 폭 (m/s)")] public float 걷기폭 = 0.6f;

    /// 이름으로 레이어 번호를 찾는다 — 컨트롤러가 갈려도(남↔여) 따라간다.
    /// ★못 찾으면 **−1 을 돌려준다.** 0 을 돌려주면 `< 0` 이 거짓이 되어 다시 안 찾고
    ///   영영 옛길로 떨어진다 (실측: 층 가중치가 0.00 에서 안 올라갔다).
    int 층찾기(string 이름)
    {
        if (애니 == null) return -1;
        for (int i = 1; i < 애니.layerCount; i++)
            if (애니.GetLayerName(i) == 이름) return i;
        return -1;
    }

    /// 다리 하나를 굽힌다 — **엉덩이는 앞으로(−), 무릎은 뒤로(+)** 접어야 발이 제자리에 남는다.
    ///
    /// ★★−10 / +23 은 **푼 값이다** (2026-08-09). 골반이 `웅크림깊이`(0.09m)만큼 내려갈 때
    ///   발이 뜨지도 묻히지도 않는 짝을 유니티에서 탐색해 얻었다 — 발 높이 오차 0.002m.
    ///   깊이를 바꾸면 이 짝도 다시 풀어야 한다 (짐작으로 비율을 맞추지 말 것).
    /// ★발목 회전은 **발 위치를 못 바꾼다** (발목이 곧 그 관절이라서). 발바닥 수평만 맡는다 —
    ///   위쪽 두 관절이 돌린 총합 `벌림 + 13*굽` 을 그대로 되돌리면 밑창이 땅과 나란해진다.
    ///
    /// `벌림`: 음수면 발을 앞에 딛고, 양수면 뒤에 남긴다 / `굽`: 0~1 / `치우침`: 굽는 양을 ±로 기울인다
    /// `들림`: 걸음 도중 무릎을 더 접어 발을 띄운다 (바닥을 끌지 않게)
    static void 다리굽히기(Transform 허벅, Transform 정강, Transform 발목, float 벌림, float 굽, float 치우침, float 들림)
    {
        float g = 굽 * (1f + 치우침);
        float 정강각 = 23f * g + 들림;
        if (허벅 != null) 허벅.localRotation = 허벅.localRotation * Quaternion.Euler(벌림 - 10f * g, 0f, 0f);
        if (정강 != null) 정강.localRotation = 정강.localRotation * Quaternion.Euler(정강각, 0f, 0f);
        if (발목 != null) 발목.localRotation = 발목.localRotation * Quaternion.Euler(-(벌림 + 정강각 - 10f * g), 0f, 0f);
    }

    void LateUpdate()
    {
        if (!Application.isPlaying)
        {   // 편집 중 — 뼈만 찾아 무기를 손에 얹어 본다. 자리를 눈으로 맞추라고 있는 길이다
            if (무기 == null || 손뼈 == null || !손뼈.gameObject.activeInHierarchy) MakeWeapon();
            주먹쥐기();
            무기자세();
            return;
        }
        // ★★★★**사체를 뒤적일 때는 클립을 내려놓고 절차 자세로 그린다** (2026-08-11 사용자
        //   "갈무리 앉아서 하는 동작 왜 없어..?").
        //   쪼그려 앉기는 `몸통스윙` 안에 있는데, **클립이 꽂혀 있으면 `몸통스윙` 이 아예
        //   안 돌아서** 내가 넣은 자세가 한 번도 실행된 적이 없었다.
        //   ☆나무·돌을 팰 때는 클립이 맞다 — 도끼질은 저작된 동작이라야 읽힌다.
        //     사체 뒤적임은 저작된 클립이 없으므로 절차 쪽이 그린다.
        bool 뒤적임 = state == State.채집 && !채집팸;
        if (공격클립 != null && !뒤적임) 클립으로그리기();
        else
        {
            if (뒤적임) 상체층내리기();       // 클립이 상체를 쥔 채면 쪼그림과 다툰다
            몸통스윙(Time.deltaTime);        // 척추를 먼저 돌리고 — 무기는 그 팔을 따라간다
        }
        주먹쥐기();
        무기자세();
        if (state != State.휘두름 || t < 휘두름 * 0.5f || 캤나) return;
        캤나 = true;
        // ★★(은퇴) 평타로 캐던 길 — **캐기는 이제 F 다** (2026-08-11 사용자 "평타고 나무가
        //   캐지는버그"). 좌클릭 = 싸움 · F = 일 로 갈랐는데 여기가 남아 있어서
        //   허공을 치면 옆 나무가 캐졌다. 지우지 않고 스위치로 끈다.
        if (평타로도캐기 && 맞은것.Count == 0)
            Harvest.TryHarvest(transform.position, hero.LookDir, 사거리 + 0.4f);
    }
    bool 캤나;
    void OnDisable()
    {
        캤나 = false;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= 편집중갱신;
#endif
    }
}
