using UnityEngine;

/// 시작 — 켤 때마다 월드를 새로 만들고, 캐릭터와 동행 펫을 집(정중앙)에 세운다.
///
/// ★씬에는 오브젝트가 몇 개뿐이다. 세상은 전부 여기서 만들어진다 —
///   그래야 「매번 새로운 맵」이 되고, 씬 파일이 커지지도 않는다.
[DefaultExecutionOrder(-100)]
public class Boot : MonoBehaviour
{
    public WorldGen world;
    public Hero hero;

    // ══════════════════════════════════════════════════════════
    //  ★★★**이건 은퇴한 「크기 재던 자」다 — 되살릴 물건이 아니다** (2026-08-10)
    //
    //  아래 `동행들` 은 펫 크기를 눈으로 견주려고 세워 둔 시험용 자였다.
    //  **게임 기능이 아니었다.** 두 가지 이유로 이제 쓰지 않는다:
    //
    //   ① `Resources/toys/` 의 **옛 치비 모델**이다 — 뼈대도 걷기 애니도 없어서
    //      다리를 안 젓고 미끄러진다. 야생은 `Resources/rig/` 의 리깅 모델을 쓴다
    //   ② ★**저절로 주어지는 펫은 9-0(인과와 행위)에 어긋난다.**
    //      펫은 이제 **잡아서 데려와 먹여야** 생긴다 (`HeroCarry` · `Critter.자라기`)
    //
    //  ☆지우지 않고 꺼 둔다 (9장 3조 — 은퇴는 삭제가 아니라 스위치).
    //    크기를 다시 견줘 볼 일이 생기면 그때만 잠깐 켠다.
    // ══════════════════════════════════════════════════════════
    [Header("동행 펫 — ★은퇴한 시험용 자. 평소엔 꺼 둔다")]
    [Tooltip("크기 견주기용 옛 자다. 펫은 잡아서 기르는 것으로 바뀌었다 — 위 주석 참고")]
    public bool 펫데리고시작 = false;

    // ★씬을 안 고쳐도 되게 여기서 붙인다 — F1 을 누르기 전엔 아무 일도 안 한다
    [Header("동작 진열 (F1)")]
    [Tooltip("F1 = 구워 놓은 네발 동작 60개를 격자로 세워 보여준다 (다시 누르면 치운다)")]
    public bool 동작진열키 = true;
    [Header("시험 모드 (F2)")]
    [Tooltip("F2 = 이속 10배 · 지구력 안 닳음. 세상을 둘러볼 때 쓴다")]
    public bool 시험모드키 = true;

    /// ★크기를 눈으로 견주려고 셋을 **다른 덩치**로 데리고 나간다 (사람 1.8m 기준).
    ///   무릎께 · 가슴께 · 올려다보는 것. 이래야 크기 규칙을 실제로 정할 수 있다.
    ///   ★큰 놈일수록 느리게 — 덩치와 속도가 같이 가야 크기가 읽힌다.
    ///
    /// ★★**은퇴함 (2026-08-10).** 위 `펫데리고시작` 의 주석을 볼 것 —
    ///   옛 `toys/` 모델이고, 펫은 이제 잡아서 길러야 생긴다.
    [System.Serializable]
    public class 동행
    {
        public string 이름 = "";
        [Tooltip("Resources/toys 안의 파일 이름")] public string 모델 = "";
        public float 키 = 1.5f, 체력 = 90f, 이속 = 3.8f, 피해 = 10f, 간격 = 1f, 반지름 = 0.5f, 무게 = 2f;
    }

    public 동행[] 동행들 =
    {
        new 동행 { 이름 = "쫑",   모델 = "dino_raptor",   키 = 0.9f, 체력 = 55f,  이속 = 4.4f, 피해 = 7f,  간격 = 0.8f, 반지름 = 0.35f, 무게 = 0.8f },
        new 동행 { 이름 = "돌뿔", 모델 = "dino_trike_1",  키 = 1.6f, 체력 = 110f, 이속 = 3.6f, 피해 = 12f, 간격 = 1.2f, 반지름 = 0.6f,  무게 = 2.5f },
        new 동행 { 이름 = "큰이", 모델 = "dino_tyranno_1", 키 = 2.8f, 체력 = 180f, 이속 = 3.0f, 피해 = 20f, 간격 = 1.8f, 반지름 = 0.9f,  무게 = 5f  },
    };

    void Start()
    {
        if (world == null) world = FindFirstObjectByType<WorldGen>();
        if (hero == null) hero = FindFirstObjectByType<Hero>();

        if (동작진열키 && GetComponent<동작진열>() == null) gameObject.AddComponent<동작진열>();
        // F2 — 시험모드 (이속 10배 · 지구력 안 닳음). 누르기 전엔 아무 일도 안 한다
        if (시험모드키 && GetComponent<시험모드>() == null) gameObject.AddComponent<시험모드>();

        Stock.Clear();
        if (world != null) world.Generate();

        if (hero == null) return;

        // 씬을 안 고쳐도 되게 여기서 붙인다 (C — 제작창 · 배고픔·목마름·피로)
        if (hero.GetComponent<제작창>() == null) hero.gameObject.AddComponent<제작창>();
        if (hero.GetComponent<생존>() == null) hero.gameObject.AddComponent<생존>();
        if (hero.GetComponent<인벤창>() == null) hero.gameObject.AddComponent<인벤창>();   // Tab
        if (hero.GetComponent<대상표시>() == null) hero.gameObject.AddComponent<대상표시>();

        // 집 칸 한가운데 — 부화터 바로 옆에 선다
        var c = WorldGrid.Center;
        hero.transform.position = new Vector3(c.x, 0f, c.z - 12f);

        if (!펫데리고시작) return;

        // 이미 있으면 또 만들지 않는다 (씬에 남은 찌꺼기 방지)
        foreach (var already in Critter.All)
            if (already != null && already.side == Critter.Side.내편) return;

        // ★동행 펫 — 자동으로 싸우고 나를 따라온다. 몇 마리든 자유다 (규칙으로 안 막는다)
        for (int i = 0; i < 동행들.Length; i++)
        {
            var d = 동행들[i];
            var s = new SpeciesDef
            {
                이름 = string.IsNullOrEmpty(d.이름) ? "동행" : d.이름,
                모델 = string.IsNullOrEmpty(d.모델) ? null : Resources.Load<GameObject>("toys/" + d.모델),
                키 = d.키, 반지름 = d.반지름, 무게 = d.무게,
                체력 = d.체력, 이속 = d.이속, 피해 = d.피해, 간격 = d.간격,
                사거리 = Mathf.Max(0.8f, d.키 * 0.7f),
                시야 = 28f, 시야각 = 220f, 청각 = 30f,
                겁 = 0f, 공격성 = 1f, 영역 = 9999f,
                무리최소 = 1, 무리최대 = 1
            };
            if (s.모델 == null && !string.IsNullOrEmpty(d.모델))
                Debug.LogWarning($"[동행] Resources/toys/{d.모델} 을 못 찾았다 — 상자로 나온다");

            // 뒤쪽에 부챗살로 세운다 — 겹치지 않게
            float a = (i - (동행들.Length - 1) * 0.5f) * 1.1f;
            var back = -hero.transform.forward;
            var at = hero.transform.position
                   + (Quaternion.Euler(0f, a * Mathf.Rad2Deg * 0.5f, 0f) * back) * (2.5f + i * 0.6f);

            var pet = Wildlife.Make(s, at, Critter.Side.내편, hero.transform);
            pet.name = s.이름;
        }
    }
}
