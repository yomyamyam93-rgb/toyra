using System.Collections.Generic;
using UnityEngine;

/// 잔디 — **오브젝트를 만들지 않고 직접 그린다** (`Graphics.DrawMeshInstanced`).
///
/// ★왜 이렇게 (2026-08-04): 포기마다 GameObject 로 만들었더니
///   ①`Outliner` 가 자꾸 테두리를 붙였고 ②빽빽하게 하려니 수만 개가 되어 무거웠다.
///   그리는 것만 하면 **씬에 존재하지 않으므로 테두리가 붙을 대상 자체가 없고**,
///   한 번에 1023포기씩 묶여 나가 수만 포기도 그리는 횟수가 수십 번뿐이다.
///
/// ★잔디는 **잔디 자리에만** 난다 (`GroundPaint.잔디인가`) — 길·물·모래 위에는 안 난다.
/// ★자리는 칸 좌표에서 뽑은 고정 난수라, 다시 와도 같은 자리에 같은 풀이 있다.
[DefaultExecutionOrder(120)]
public class GrassField : MonoBehaviour
{
    [Header("얼마나")]
    // ★화면 밖까지 깔아야 한다 — 범위가 짧으면 **잔디가 끝나는 선**이 화면에 보인다
    //   (2026-08-04 사용자 "외곽에 생성 안 된 잔디들이 보여")
    [Tooltip("이 반경 안에 깐다 (m) — 「화면 따라가기」가 켜져 있으면 자동으로 정해진다")]
    public float 반경 = 36f;

    // ★★반경을 **화면에서 뽑는다** (2026-08-05 사용자 "잔디생성 범위도 넓혀야할듯?").
    //   줌아웃 칸을 하나 열자마자 화면이 27m → 54m 이 되면서 고정 반경 36m 로는
    //   **잔디가 끝나는 선**이 화면에 들어왔다. 숫자를 손으로 다시 맞추면 줌 범위를
    //   건드릴 때마다 또 어긋난다 — 화면 대각선에서 계산하면 저절로 따라온다.
    //   ★대가: 줌아웃하면 훑는 칸이 늘어난다. 그래서 상한을 둔다 (렉의 손잡이는 여전히 간격).
    [Tooltip("화면 크기에 맞춰 반경을 자동으로 정한다")] public bool 화면따라가기 = true;
    [Tooltip("자동으로 정할 때의 상한 (m) — 넘으면 렉이 온다")] public float 반경상한 = 70f;
    // ★★★렉의 정체는 포기 수가 아니라 **다시 까는 횟수**였다 (2026-08-04 사용자 "렉 심하네").
    //   다시 까는 주기가 `간격` 에 묶여 있어서, 간격을 촘촘하게 할수록
    //   ①한 번에 훑는 칸이 제곱으로 늘고 ②**그걸 더 자주** 한다. 간격 0.127m 이면
    //   0.127m 갈 때마다 324,000칸 — 걸어 다니면 초당 스무 번이다.
    //   → 간격을 넉넉히 두고, 빽빽함은 아래 「뭉치기」로 낸다.
    [Tooltip("포기 사이 간격 (m) — 작을수록 빽빽하고 **훨씬** 무겁다")] public float 간격 = 0.5f;

    // ★★목록을 다시 뽑는 주기를 **간격에서 떼어냈다** (2026-08-05 사용자 "렉 안 먹으면서
    //   빽빽히"). 전에는 주인공이 한 「간격」 칸을 움직일 때마다 다시 뽑았다 — 간격이
    //   3m 일 땐 3m 마다라 티가 안 났지만, 빽빽하게 하려고 0.5m 로 낮추면 **반 미터마다
    //   수만 개를 다시 만든다.** 무거운 건 그리기가 아니라 이 재구성이다.
    //   → 다시 뽑는 칸은 따로 둔다. 반경에 여유(`여유`)를 두어 가장자리가 안 비게 한다.
    [Tooltip("몇 미터를 움직여야 목록을 다시 뽑나 — 클수록 덜 버벅이고 그만큼 여유가 필요하다")]
    [Range(1f, 16f)] public float 다시뽑기칸 = 6f;
    [Tooltip("반경에 더해 두는 여유 (m) — 「다시뽑기칸」보다 커야 가장자리가 안 빈다")]
    public float 여유 = 8f;

    [Tooltip("그림자를 받는다 (남이 드리운 그림자가 풀에 진다)")]
    public bool 그림자받기 = true;
    // ★★풀끼리 그림자를 주고받으려면 **드리우는 쪽**을 켜야 한다 (2026-08-05 사용자
    //   "지들끼리는 그림자 안받아?"). 대가가 크다 — 그림자맵에 들어가는 순간
    //   **풀을 한 번 더 그리는 셈**이고, 캐스케이드가 둘이면 두 번 더다.
    //   ☆먼저 `밑동어둡게` 로 해 보라. 풀 밑동이 어두운 건 실제로 서로 가려서인데,
    //     그 결과만 흉내 내면 공짜다. 그림자맵은 그래도 부족할 때 켠다.
    [Tooltip("풀도 그림자를 드리운다 — 풀끼리 그늘이 진다. **비싸다**")]
    public bool 그림자드리우기 = false;
    [Tooltip("밑동을 얼마나 어둡게 — 바닥에 박힌 느낌. 0 이면 안 씀")]
    [Range(0f, 0.8f)] public float 밑동어둡게 = 0.35f;
    [Tooltip("한 자리에 풀이 있을 확률")] [Range(0f, 1f)] public float 밀도 = 0.9f;

    // ★★균등하게 깔면 「카펫」이 되고, 뭉쳐서 깔면 「덤불」이 된다 (2026-08-04 사용자
    //   "그냥 띄엄띄엄 뭉쳐서 나게끔만"). 낮은 주파수 노이즈로 덤불 자리를 정하고,
    //   그 안에서만 풀이 난다. 덤불 가장자리는 성글어져서 경계가 안 보인다.
    [Header("뭉치기")]
    [Tooltip("덤불 하나의 크기 (m)")] public float 뭉치크기 = 4f;
    [Tooltip("땅의 몇 쯤에 덤불이 나나 — 낮출수록 띄엄띄엄")] [Range(0.02f, 1f)] public float 뭉치비율 = 0.25f;
    // ★★빽빽함은 **간격이 아니라 여기서** 낸다 (2026-08-04 사용자 "진짜 군데군데 밀집해서").
    //   간격을 줄이면 훑는 칸이 제곱으로 늘어 렉이 온다. 대신 덤불에 든 칸 하나에
    //   여러 포기를 몰아 심는다 — 훑는 비용은 그대로인데 덤불 안은 빽빽해진다.
    [Tooltip("덤불에 든 한 칸에 몇 포기를 몰아 심나")] [Range(1, 12)] public int 칸당 = 6;

    [Header("생김새")]
    public float 최소키 = 0.45f, 최대키 = 0.95f;
    // ★1 이면 발밑 땅과 **완전히 같은 색**이다. 1.06 은 땅이 아직 팔레트 색이던 시절
    //   풀이 묻혀 보여서 올려 둔 것인데, 이제 풀이 땅을 직접 찍으므로 올릴 이유가 없다
    //   (2026-08-05 "잔디 색이 튀는데?").
    [Tooltip("발밑 땅색에 곱하는 값 — 1이면 땅과 완전히 같은 색")]
    public float 밝기 = 1f;
    [Tooltip("땅에서 살짝 띄운다 (겹치면 지지직거린다)")] public float 띄우기 = 0.03f;

    const int 묶음 = 1000;                 // 한 번에 그리는 최대 개수 (한계 1023)

    // ★★★**묶지 않는다** (2026-08-05 사용자 — "잔디가 왜 묶여있어? … 그냥 잔디하나당
    //   그아래 땅을 보게해줘"). 전에는 [그림][땅톤] 으로 재질을 나눠 묶었는데, 묶으면 한 칸
    //   안의 풀이 전부 같은 색이라 **땅색이 스르르 변하는 자리에서 풀만 계단처럼 끊긴다.**
    //   ☆2026-08-04 에 묶은 이유는 "인스턴스마다 색을 주려니 URP 기본 재질이 안 받아서" 였다.
    //     이제 **풀 전용 셰이더**(`Toyra/Grass`)가 있으니 그 제약이 없어졌다 —
    //     풀이 자기 세계 좌표로 땅 그림을 찍어 색을 가져온다. 재질은 그림당 하나면 된다.
    Material[] 재질들;
    Material[] 마스크재질;      // 그림마다 하나 (알파 모양이 달라서)
    float[] 비율;
    Texture2D[] 흰그림들;
    Mesh 판;
    float yaw = 45f;

    List<Matrix4x4>[] 그릴것;      // 지금 그리고 있는 것
    List<Matrix4x4>[] 뒷것;        // 뒤에서 몇 프레임에 걸쳐 짓는 것
    Vector2Int 지난칸 = new Vector2Int(int.MinValue, int.MinValue);

    // ★★★**한 프레임에 다 짓지 않는다** (2026-08-06 사용자 "이동할때마다 조금씩 끊기는데").
    //   반경 70m·간격 0.5m 면 한 번에 약 **10만 칸**을 훑는다. 실측으로 펄린 6ms +
    //   해시 1ms + 땅 찍기·행렬 만들기까지 합치면 **한 프레임에 20ms 넘게** 몰린다.
    //   60프레임의 예산이 16ms 니까 그 프레임이 통째로 늦고, 그게 걸을 때마다 툭 끊기는 정체다.
    //   ☆무엇을 줄여도 「한 번에 몰아서」인 한 끊긴다 — 계산을 **여러 프레임에 나눈다.**
    //   ☆다 지을 때까지는 **옛 목록을 계속 그린다.** `여유` 가 그 사이 걸어 나간 몫을 덮는다.
    [Tooltip("한 프레임에 몇 줄씩 짓나 — 작을수록 안 끊기고 그만큼 늦게 채워진다")]
    [Range(1, 64)] public int 한프레임줄 = 8;

    bool 짓는중; int 짓는줄; Vector3 짓는p;
    int 짓는cx, 짓는cz, 짓는r; float 짓는r2; Quaternion 짓는rot;

    void Start()
    {
        var cam = Camera.main;
        if (cam != null) { var iso = cam.GetComponent<IsoCam>(); if (iso != null) yaw = iso.yaw; }
        판만들기();
        재질만들기();
    }

    /// 세워 놓을 판 하나 — 모두가 이 메시를 나눠 쓴다 (인스턴싱의 조건)
    void 판만들기()
    {
        판 = new Mesh { name = "풀판" };
        판.SetVertices(new List<Vector3> {
            new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f),
            new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0f) });
        판.SetUVs(0, new List<Vector2> {
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) });
        판.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
        판.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
        판.RecalculateBounds();
    }

    /// ★그림의 색을 지우고 **모양만** 남긴다 (2026-08-04 사용자 "바닥이랑 잔디가 매치가 안 돼").
    ///   원본 그림이 밝은 연두라, 거기에 땅색을 곱하면 **두 색이 섞여** 엉뚱한 초록이 된다.
    ///   흰 실루엣으로 만들면 색을 **온전히 땅색이 정한다** — 그래야 바닥과 딱 맞는다.
    static Texture2D 흰색으로(Texture2D 원본)
    {
        var t = new Texture2D(원본.width, 원본.height, TextureFormat.RGBA32, true)
        { name = 원본.name + "_흰", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };

        var px = 원본.GetPixels32();
        for (int i = 0; i < px.Length; i++) { px[i].r = 255; px[i].g = 255; px[i].b = 255; }
        t.SetPixels32(px);
        t.Apply(true);
        return t;
    }

    void 재질만들기()
    {
        var 그림 = Resources.LoadAll<Texture2D>("grass");
        if (그림 == null || 그림.Length == 0)
        {
            Debug.LogWarning("[잔디] Resources/grass 에 그림이 없다");
            enabled = false; return;
        }

        var sh = Shader.Find("Toyra/Grass");
        if (sh == null)
        {
            Debug.LogError("[잔디] Toyra/Grass 셰이더를 못 찾았다");
            enabled = false; return;
        }

        재질들 = new Material[그림.Length];
        그릴것 = new List<Matrix4x4>[그림.Length];
        뒷것 = new List<Matrix4x4>[그림.Length];
        비율 = new float[그림.Length];

        흰그림들 = new Texture2D[그림.Length];
        for (int i = 0; i < 그림.Length; i++)
        {
            비율[i] = 그림[i].width / (float)그림[i].height;
            var 흰그림 = 흰색으로(그림[i]);          // ★색은 100% 땅이 정한다
            흰그림들[i] = 흰그림;
            그릴것[i] = new List<Matrix4x4>();
            뒷것[i] = new List<Matrix4x4>();

            var m = new Material(sh) { name = "잔디" + i };
            m.SetTexture("_BaseMap", 흰그림);
            m.SetFloat("_WorldSize", WorldGrid.Size);
            m.SetFloat("_Cutoff", 0.5f);
            var c = new Color(밝기, 밝기, 밝기, 1f);
            m.SetColor("_Tint", c);
            m.renderQueue = 2450;
            m.enableInstancing = true;
            재질들[i] = m;
        }
        땅그림물리기();

        // ★실루엣 마스크에도 그린다 — 안 그리면 펫의 테두리가 **잔디 위에 덧그려진다**
        //   (2026-08-04 사용자 "펫들 발 부분 보면 실루엣이 잔디 위로 그려지는 버그").
        //   잔디는 전용 값(0.05)으로 찍고, 화면 셰이더가 그 값에는 선을 안 긋는다.
        var osh = Shader.Find("Toyra/Outline");
        if (osh != null)
        {
            마스크재질 = new Material[그림.Length];
            for (int i = 0; i < 그림.Length; i++)
            {
                var mm = new Material(osh) { name = "잔디마스크" + i };
                mm.SetFloat("_Id", 0.05f);
                mm.SetFloat("_UseAlpha", 1f);        // 판 모양이 아니라 풀잎 모양으로 가린다
                mm.SetFloat("_Cutoff", 0.5f);
                mm.SetTexture("_BaseMap", 흰그림들[i]);
                mm.enableInstancing = true;
                마스크재질[i] = mm;
            }
        }
    }

    /// 땅 그림을 재질에 물린다. 땅이 아직 안 만들어졌으면 다음 프레임에 다시 시도한다
    bool 땅물림;
    void 땅그림물리기()
    {
        if (땅물림 || 재질들 == null) return;
        var 땅 = GroundPaint.땅그림;
        if (땅 == null) return;
        foreach (var m in 재질들) if (m != null) m.SetTexture("_GroundMap", 땅);
        땅물림 = true;
        // ★확인용 — 이게 안 뜨면 풀이 **흰 땅**을 보고 있다는 뜻이고, 그러면 햇빛을 받아
        //   누르스름한 색으로 떠 보인다 (색이 튀는 첫째 후보다)
        Debug.Log("[잔디] 땅 그림 물림 — 이제 발밑 색을 따라간다");
    }

    void Update()
    {
        var hero = Hero.Me;
        if (hero == null || 재질들 == null) return;
        땅그림물리기();

        var p = hero.transform.position;
        // ★다시 뽑는 칸은 「간격」이 아니라 「다시뽑기칸」이다 (위 주석 참고)
        float 뽑기칸 = Mathf.Max(간격, 다시뽑기칸);
        var 칸 = new Vector2Int(Mathf.FloorToInt(p.x / 뽑기칸), Mathf.FloorToInt(p.z / 뽑기칸));

        // ★화면 대각선의 절반을 덮어야 「잔디가 끝나는 선」이 안 보인다
        if (화면따라가기)
        {
            var cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                float 반높이 = cam.orthographicSize;
                float 반너비 = 반높이 * cam.aspect;
                // 카메라가 기울어 있어 세로는 화면에서 더 멀리 뻗는다 — 넉넉히 잡는다
                float 필요 = Mathf.Sqrt(반너비 * 반너비 + 반높이 * 반높이) / Mathf.Sin(Mathf.Deg2Rad * 40f) * 0.75f + 6f;
                float 새반경 = Mathf.Min(필요, 반경상한);
                if (Mathf.Abs(새반경 - 반경) > 1f) { 반경 = 새반경; 지난칸 = new Vector2Int(int.MinValue, int.MinValue); }
            }
        }

        // 한 칸이라도 움직였을 때만 다시 짓기 시작한다 (짓는 중이면 그게 끝나고 나서)
        if (칸 != 지난칸 && !짓는중) { 지난칸 = 칸; 짓기시작(p); }
        if (짓는중) 조금짓기();

        그리기();
    }

    /// 새 목록을 **뒤에서** 짓기 시작한다 — 앞의 것은 계속 그린다
    void 짓기시작(Vector3 p)
    {
        for (int i = 0; i < 뒷것.Length; i++) 뒷것[i].Clear();

        float 깔반경 = 반경 + Mathf.Max(여유, 다시뽑기칸);
        짓는r = Mathf.CeilToInt(깔반경 / 간격);
        짓는r2 = 깔반경 * 깔반경;
        짓는cx = Mathf.FloorToInt(p.x / 간격);
        짓는cz = Mathf.FloorToInt(p.z / 간격);
        짓는rot = Quaternion.Euler(0f, yaw, 0f);
        짓는p = p;
        짓는줄 = -짓는r;
        짓는중 = true;

        // ★맨 처음 한 번은 통째로 짓는다 — 안 그러면 시작하자마자 땅이 민둥산이다
        bool 비었다 = true;
        for (int i = 0; i < 그릴것.Length; i++) if (그릴것[i].Count > 0) { 비었다 = false; break; }
        if (비었다) while (짓는중) 조금짓기(int.MaxValue);
    }

    void 조금짓기(int 줄수 = -1)
    {
        if (줄수 < 0) 줄수 = Mathf.Max(1, 한프레임줄);
        int 끝 = (줄수 == int.MaxValue) ? 짓는r : Mathf.Min(짓는r, 짓는줄 + 줄수 - 1);
        for (; 짓는줄 <= 끝; 짓는줄++) 한줄뽑기(짓는cx + 짓는줄);

        if (짓는줄 > 짓는r)
        {
            // 다 지었다 — 앞뒤를 맞바꾼다 (복사하지 않는다)
            var t = 그릴것; 그릴것 = 뒷것; 뒷것 = t;
            짓는중 = false;
        }
    }

    /// 세로 한 줄(gx 하나)을 훑어 `뒷것` 에 채운다.
    ///
    /// ★★고친 것 둘 (2026-08-06):
    ///   ①`Random.InitState` 대신 **칸 좌표에서 바로 뽑는 해시** — 난수기를 안 건드려서 싸다
    ///   ②**제일 싼 검사를 앞으로** — 덤불 노이즈로 먼저 거르면 대부분의 칸이 거기서 끝나고,
    ///     뒤의 땅 그림 찍기·행렬 만들기까지 안 간다
    ///   ☆값(자리·밀도·뭉침)의 뜻은 그대로다. **무엇이 나는지가 아니라 어떻게 뽑는지**만 바꿨다.
    void 한줄뽑기(int gx)
    {
        int 그림수 = 재질들.Length;
        var p = 짓는p;
        var rot = 짓는rot;
        var 집 = WorldGrid.Center;
        float r2 = 짓는r2;
        int cz = 짓는cz, r = 짓는r;

        float 문턱 = 1f - 뭉치비율;
        bool 뭉침씀 = 뭉치크기 > 0.01f && 뭉치비율 < 0.999f;
        float 집r2 = 9f * 9f;

        {
            for (int gz = cz - r; gz <= cz + r; gz++)
            {
                float 칸x = (gx + 0.5f) * 간격, 칸z = (gz + 0.5f) * 간격;
                float dx = 칸x - p.x, dz = 칸z - p.z;
                if (dx * dx + dz * dz > r2) continue;

                // ① 덤불 판정 — 제일 싸고 제일 많이 걸러 낸다 (뭉치비율 0.25 면 4분의 3이 여기서 끝)
                float 성글 = 1f;
                if (뭉침씀)
                {
                    float n = Mathf.PerlinNoise(칸x / 뭉치크기 + 137.1f, 칸z / 뭉치크기 + 71.3f);
                    if (n < 문턱) continue;
                    성글 = Mathf.InverseLerp(문턱, 1f, n);
                }

                // ② 밀도
                if (해시(gx, gz, 1) > 밀도) continue;
                if (뭉침씀 && 해시(gx, gz, 2) > 성글) continue;

                var at = new Vector3((gx + 해시(gx, gz, 3)) * 간격, 띄우기, (gz + 해시(gx, gz, 4)) * 간격);

                // ③ 발밑 땅의 톤 — 잔디가 아니면 아예 안 난다 (텍스처를 찍으므로 제일 비싸다)
                if (GroundPaint.톤(at) <= 0) continue;
                float hx = at.x - 집.x, hz = at.z - 집.z;
                if (hx * hx + hz * hz < 집r2) continue;

                int 몇 = Mathf.Max(1, 칸당);
                for (int k = 0; k < 몇; k++)
                {
                    var 자리 = k == 0 ? at
                             : new Vector3((gx + 해시(gx, gz, 10 + k * 3)) * 간격, 띄우기,
                                           (gz + 해시(gx, gz, 11 + k * 3)) * 간격);
                    int i = (int)(해시(gx, gz, 12 + k * 3) * 그림수) % 그림수;
                    float h = Mathf.Lerp(최소키, 최대키, 해시(gx, gz, 200 + k));
                    뒷것[i].Add(Matrix4x4.TRS(자리, rot, new Vector3(h * 비율[i], h, 1f)));
                }
            }
        }
    }

    /// 칸 좌표에서 **바로** 0~1 난수를 뽑는다 — 난수기를 건드리지 않으므로 백만 번 불러도 싸다.
    /// ★같은 칸·같은 갈래면 언제나 같은 값이다. 다시 와도 같은 자리에 같은 풀이 있다.
    static float 해시(int gx, int gz, int 갈래)
    {
        unchecked
        {
            uint h = (uint)(gx * 73856093) ^ (uint)(gz * 19349663) ^ (uint)(갈래 * 83492791);
            h ^= h >> 13; h *= 0x85ebca6b; h ^= h >> 16; h *= 0xc2b2ae35; h ^= h >> 15;
            return (h & 0xFFFFFF) * (1f / 0x1000000);
        }
    }

    static readonly Matrix4x4[] 버퍼 = new Matrix4x4[묶음];

    void 그리기()
    {
        Shader.SetGlobalFloat("_GRootDark", 밑동어둡게);

        int 그림수 = 재질들.Length;
        for (int i = 0; i < 그림수; i++)
        {
            var 목록 = 그릴것[i];
            for (int s = 0; s < 목록.Count; s += 묶음)
            {
                int n = Mathf.Min(묶음, 목록.Count - s);
                목록.CopyTo(s, 버퍼, 0, n);

                // ★그림자를 **받는다** (2026-08-05 사용자 "잔디도 그림자 받을 수 있도록").
                //   드리우는 쪽(캐스팅)은 계속 끈다 — 수만 포기가 그림자맵에 들어가면
                //   그림자 그리기가 한 번 더 도는 셈이라 값이 제일 비싸다.
                //   셰이더는 이미 그림자를 계산하고 있었다 — 여기 `false` 가 막고 있었을 뿐이다.
                Graphics.DrawMeshInstanced(판, 0, 재질들[i], 버퍼, n, null,
                    그림자드리우기 ? UnityEngine.Rendering.ShadowCastingMode.TwoSided
                                : UnityEngine.Rendering.ShadowCastingMode.Off,
                    그림자받기);

                // 실루엣 마스크에도 같이 (펫 테두리가 잔디 위에 덧그려지지 않게)
                if (마스크재질 != null && 마스크재질[i] != null)
                    Graphics.DrawMeshInstanced(판, 0, 마스크재질[i], 버퍼, n, null,
                        UnityEngine.Rendering.ShadowCastingMode.Off, false, Outliner.잔디층);
            }
        }
    }
}
