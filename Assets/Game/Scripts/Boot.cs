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

    [Header("동행 펫")]
    public bool 펫데리고시작 = true;

    // ★씬을 안 고쳐도 되게 여기서 붙인다 — F1 을 누르기 전엔 아무 일도 안 한다
    [Header("동작 진열 (F1)")]
    [Tooltip("F1 = 구워 놓은 네발 동작 60개를 격자로 세워 보여준다 (다시 누르면 치운다)")]
    public bool 동작진열키 = true;

    /// ★크기를 눈으로 견주려고 셋을 **다른 덩치**로 데리고 나간다 (사람 1.8m 기준).
    ///   무릎께 · 가슴께 · 올려다보는 것. 이래야 크기 규칙을 실제로 정할 수 있다.
    ///   ★큰 놈일수록 느리게 — 덩치와 속도가 같이 가야 크기가 읽힌다.
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

        Stock.Clear();
        if (world != null) world.Generate();

        if (hero == null) return;

        // 씬을 안 고쳐도 되게 여기서 붙인다 (C — 제작창)
        if (hero.GetComponent<제작창>() == null) hero.gameObject.AddComponent<제작창>();

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
