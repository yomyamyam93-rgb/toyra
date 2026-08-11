using UnityEngine;

/// 칸칸이 높이가 다른 **격자 바닥** — 주인공을 따라다니는 판때기.
///
/// ★★윗면 색은 **내가 짓지 않는다** (2026-08-09 사용자 "이 격자에다가 기존에 맵 만드는
///   방식을 적용시킬 순 없는거야?"). 땅 판때기의 UV 가 **월드 좌표에 1:1** 이라
///   (`uv = 월드 ÷ 1440`, 실측 확인) 같은 UV 를 물리고 **땅 재질을 그대로 가져다 쓴다.**
///   잔디·흙길·물가·결이 한 픽셀도 안 달라진다. 격자는 높이와 옆면만 얹는다.
///
/// ★★높이는 `땅격자.높이()` 하나에서만 온다 — 캐릭터·펫도 같은 걸 쓴다. 「그림 = 판정」.
///
/// ★온 세계(1440m)를 한 메시로 만들면 정점이 수천만이라, **주인공 둘레만** 만들고
///   반 판때기만큼 움직이면 다시 짓는다.
[ExecuteAlways]
public class 격자바닥 : MonoBehaviour
{
    [Header("격자")]
    [Tooltip("가로·세로 칸 수 — 화면(가로 78m)보다 넉넉해야 가장자리가 안 보인다")]
    // ★칸 하나가 면 9장(윗면1 + 베벨4 + 홈벽4)이라 칸수를 올리면 정점이 제곱으로 는다.
    //   72칸 × 2.11m = 152m — 화면(제일 넓게 78m)의 두 배라 가장자리가 안 보인다.
    public int 칸수 = 72;

    // ★★칸 사이를 조금 벌리고 모서리를 깎는다 (2026-08-09 사용자 "틈새를 아주 쬐금씩
    //   벌려주면 격자 선이 잘 보일 듯, 모퉁이도 쪼금만 베벨").
    //   틈 = 격자선 · 베벨 = 모서리에 빛이 걸리는 자리. 둘 다 **그림자가 만드는 선**이라
    //   덧그리는 선(장식)이 아니다 — 이펙트 규칙의 잣대를 통과한다.
    [Header("틈과 베벨")]
    // ★★틈은 **0 이 기본**이다 (2026-08-09 사용자 "격자 간격은 없애야할거같다").
    //   칸이 서로 붙고, 칸 경계는 **베벨과 높이차**만으로 읽힌다 — 벌어진 홈이 없다.
    [Tooltip("칸 사이 틈 (m) — 0 이면 칸이 딱 붙는다")] public float 틈 = 0f;
    [Tooltip("모서리를 깎는 폭 (m)")] public float 베벨 = 0.03f;
    [Tooltip("베벨이 내려앉는 깊이 (m)")] public float 베벨깊이 = 0.015f;
    [Tooltip("틈 바닥까지의 깊이 (m) — 얕으면 틈으로 바닥이 비친다")] public float 홈깊이 = 0.08f;

    [Header("옆면")]
    public Color 옆면 = new Color(0.42f, 0.33f, 0.21f);
    [Tooltip("아래로 갈수록 더 어둡게")] [Range(0f, 1f)] public float 옆면어둠 = 0.45f;

    [Header("따라다니기")]
    // ★★판때기 크기를 **카메라에서 뽑는다** (2026-08-09 사용자 "맵 전역으로 이 격자가
    //   적용이 안돼는거같은데"). 고정 칸수로 두면 줌아웃했을 때 화면이 판때기보다 커져
    //   가장자리가 드러난다 — 실측: 줌 22 의 화면 반대각 44.9m 인데 최악일 때 가장자리가
    //   33.7m 였다. 그래서 **화면 반대각 + 흔들림 여유**만큼 자동으로 잡는다.
    [Tooltip("화면 밖으로 더 깔아 두는 여유 (m)")] public float 여유 = 12f;
    // ★★★**이 값이 없으면 매 프레임 다시 짓는다** (2026-08-09 사용자 "렉이 너무심해").
    //   판때기를 화면에 딱 맞는 크기로 잡으면, 주인공이 한 발짝만 옮겨도 곧바로
    //   「모자란다」가 되어 9.3ms 짜리 재건축이 끝없이 돈다.
    //   → 화면보다 이만큼 더 크게 지어 두고, 그 여윳돈을 다 쓸 때까지 안 짓는다.
    [Tooltip("다시 짓기 전까지 걸어다닐 수 있는 거리 (m)")] public float 걸을여유 = 18f;
    [Tooltip("칸수 상한 — 정점이 제곱으로 늘어난다")] public int 칸수상한 = 140;

    Mesh 메시;
    // ★버퍼는 컴포넌트가 들고 있는다 — 다시 지을 때마다 만들면 GC 가 화면을 멈춰 세운다
    System.Collections.Generic.List<Vector3> 정점;
    System.Collections.Generic.List<Vector2> uv;
    System.Collections.Generic.List<Color> 색;
    System.Collections.Generic.List<int> 윗삼각, 옆삼각;
    float[,] H;
    Material 옆면재질, 임시윗면;
    Vector3 지은자리 = new Vector3(1e9f, 0, 1e9f);
    int 지은칸수 = -1;
    Transform 주인공;
    Coroutine 짓는중;

    void OnEnable() { 짓기(); }
    void Start() { 땅재질물리기(); }
    void OnValidate() { if (isActiveAndEnabled) { 지은자리 = new Vector3(1e9f, 0, 1e9f); 짓기(); } }

    /// 화면을 덮으려면 반폭이 얼마여야 하나 — 직교라 대각선이 제일 멀다
    float 필요반폭()
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return 60f;
        float a = Mathf.Max(0.1f, cam.aspect);
        return cam.orthographicSize * Mathf.Sqrt(1f + a * a) + 여유;
    }

    // ★스스로 시간을 잰다 (2026-08-11) — 스파이크에 「Late 24ms」 라고만 찍히면
    //   어느 부품인지 알 수가 없다. 오래 걸린 프레임에 무엇을 했는지 남긴다
    [Tooltip("이보다 오래 걸린 프레임을 콘솔에 남긴다 (ms · 0 이면 안 남김)")]
    public float 스스로재기 = 6f;

    void LateUpdate()
    {
        var 시계 = 스스로재기 > 0f ? System.Diagnostics.Stopwatch.StartNew() : null;
        bool 지었나 = false;
        try { 실제LateUpdate(ref 지었나); }
        finally
        {
            if (시계 != null)
            {
                시계.Stop();
                double ms = 시계.Elapsed.TotalMilliseconds;
                if (ms >= 스스로재기)
                    Debug.LogFormat("[격자바닥-느림] {0:F0}ms · 다시지음={1} · 칸수={2} · 짓는중={3}",
                                    ms, 지었나, 지은칸수, 짓는중 != null);
            }
        }
    }

    void 실제LateUpdate(ref bool 지었나)
    {
        if (주인공 == null)
        {
            var h = FindFirstObjectByType<Hero>();
            if (h != null) 주인공 = h.transform;
        }
        if (주인공 == null || 짓는중 != null) return;

        float 칸 = Mathf.Max(0.01f, 땅격자.칸);
        float 필요 = 필요반폭();
        // ★판때기는 「화면 + 걸을여유」만큼 짓는다. 다시 짓는 건 「화면」이 모자랄 때만
        int N = Mathf.Clamp(Mathf.CeilToInt((필요 + 걸을여유) * 2f / 칸 / 2f) * 2, 8, 칸수상한);

        // 지금 판때기로 화면을 덮고 있나 — 주인공이 가운데에서 밀린 만큼 여유가 깎인다
        var p = 주인공.position;
        float 밀림 = Mathf.Max(Mathf.Abs(p.x - 지은자리.x), Mathf.Abs(p.z - 지은자리.z));
        float 남은반폭 = 지은칸수 * 칸 * 0.5f - 밀림;

        // ★★칸수가 조금 달라졌다고 다시 짓지 않는다 — 줌은 매 프레임 스르르 변하므로
        //   그대로 두면 **프레임마다** 다시 지어 판때기가 쉴 새 없이 흔들린다.
        //   모자랄 때만 늘리고, 한참 남을 때(8칸)만 줄인다.
        if (지은칸수 < N || 지은칸수 > N + 8 || 남은반폭 < 필요)
        {
            칸수 = N;
            지었나 = true;
            짓기();      // ★자리는 `짓기속` 이 주인공 기준으로 잡고 맨 끝에 옮긴다
        }
    }

    // ── 재질 ─────────────────────────────────────────────────────────
    /// 실행 중이면 **진짜 땅 재질**을 윗면에 물린다. 그림을 두 벌 만들지 않는다.
    [ContextMenu("땅 재질 물리기")]
    public void 땅재질물리기()
    {
        var mr = GetComponent<MeshRenderer>(); if (mr == null) return;
        var 세계 = GameObject.Find("세계"); if (세계 == null) return;
        var 판 = 세계.transform.Find("월드/땅"); if (판 == null) return;
        var r = 판.GetComponent<MeshRenderer>(); if (r == null || r.sharedMaterial == null) return;

        var ms = mr.sharedMaterials;
        if (ms.Length >= 1 && ms[0] != r.sharedMaterial) { ms[0] = r.sharedMaterial; mr.sharedMaterials = ms; }
    }

    Material 윗면재질()
    {
        // 에디터에서는 땅이 아직 안 지어져 있다 — 그때만 임시 초록
        if (임시윗면 == null)
        {
            var sh = Shader.Find("토이라/격자바닥");
            if (sh == null) return null;
            임시윗면 = new Material(sh) { name = "격자바닥_임시윗면" };
        }
        return 임시윗면;
    }

    // ── 메시 ─────────────────────────────────────────────────────────
    /// ★★한 번 짓는 데 **19.8ms** 다 (2026-08-09 실측, 80칸). 걷다 보면 몇 초에 한 번씩
    ///   지어야 하는데 그때마다 한 프레임을 통째로 먹으면 뚝뚝 끊긴다.
    ///   → **여러 프레임에 나눠 짓는다.** 다 지을 때까지 옛 판때기가 그대로 보이므로
    ///     화면에는 아무 일도 안 일어난다. (에디터에서는 그냥 한 번에 짓는다)
    [ContextMenu("다시 짓기")]
    public void 짓기()
    {
        if (!Application.isPlaying) { var e = 짓기속(); while (e.MoveNext()) { } return; }
        if (짓는중 != null) StopCoroutine(짓는중);
        짓는중 = StartCoroutine(짓기천천히());
    }

    System.Collections.IEnumerator 짓기천천히()
    {
        var e = 짓기속();
        while (true)
        {
            var 시계 = System.Diagnostics.Stopwatch.StartNew();
            bool 남음 = true;
            // 한 프레임에 3ms 만 쓴다 — 60fps 의 16.7ms 중 5분의 1
            while (시계.Elapsed.TotalMilliseconds < 1.5 && (남음 = e.MoveNext())) { }
            if (!남음) break;
            yield return null;
        }
        짓는중 = null;
    }

    System.Collections.IEnumerator 짓기속()
    {
        var mf = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        var mr = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();

        if (옆면재질 == null)
        {
            var sh = Shader.Find("토이라/격자바닥");
            if (sh == null) { Debug.LogWarning("[격자바닥] 셰이더를 못 찾았다"); yield break; }
            옆면재질 = new Material(sh) { name = "격자바닥_옆면" };
        }

        float 칸 = Mathf.Max(0.01f, 땅격자.칸);
        int N = Mathf.Clamp(칸수, 2, 200);
        float 반 = N * 칸 * 0.5f;

        // 칸 경계를 월드 격자에 올린다 — 어긋나면 색 칸과 높이 칸이 반 칸씩 밀린다
        //   ★자리를 **여기서 옮기지 않는다.** 여러 프레임에 걸쳐 짓는 동안엔 옛 판때기가
        //   보이고 있는데, 먼저 옮기면 옛 메시가 통째로 딸려가 화면이 튄다. 맨 끝에서 옮긴다.
        Vector3 원점 = 주인공 != null && Application.isPlaying ? 주인공.position : transform.position;
        원점.x = Mathf.Round((원점.x - 반) / 칸) * 칸 + 반;
        원점.z = Mathf.Round((원점.z - 반) / 칸) * 칸 + 반;
        원점.y = 0f;

        float 판크기 = WorldGrid.Size;

        // ★★★버퍼를 **다시 만들지 않는다** (2026-08-09 사용자 "두두두둑 뭔가 생성렉이 있어").
        //   실측 `[렉스파이크] 94ms · GC 18,454KB` — 다시 지을 때마다 리스트 다섯 개를
        //   새로 만들어 23만 개씩 채웠다. 그게 통째로 쓰레기가 되어 GC 가 화면을 멈춰 세운다.
        //   → 한 번 만들어 두고 `Clear()` 로 비워 쓴다. 용량이 한 번 커지면 다시 안 늘어난다.
        정점 ??= new System.Collections.Generic.List<Vector3>(1 << 16);
        uv   ??= new System.Collections.Generic.List<Vector2>(1 << 16);
        색   ??= new System.Collections.Generic.List<Color>(1 << 16);
        윗삼각 ??= new System.Collections.Generic.List<int>(1 << 16);
        옆삼각 ??= new System.Collections.Generic.List<int>(1 << 16);
        정점.Clear(); uv.Clear(); 색.Clear(); 윗삼각.Clear(); 옆삼각.Clear();

        void 면(System.Collections.Generic.List<int> tri,
                Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            int i = 정점.Count;
            정점.Add(a); 정점.Add(b); 정점.Add(c); 정점.Add(d);
            // ★UV = 월드 좌표 ÷ 판 크기. 땅 판때기와 똑같은 매핑이라 그림이 이어진다
            uv.Add(new Vector2((a.x + 원점.x) / 판크기, (a.z + 원점.z) / 판크기));
            uv.Add(new Vector2((b.x + 원점.x) / 판크기, (b.z + 원점.z) / 판크기));
            uv.Add(new Vector2((c.x + 원점.x) / 판크기, (c.z + 원점.z) / 판크기));
            uv.Add(new Vector2((d.x + 원점.x) / 판크기, (d.z + 원점.z) / 판크기));
            for (int k = 0; k < 4; k++) 색.Add(col);
            tri.Add(i); tri.Add(i + 1); tri.Add(i + 2);
            tri.Add(i); tri.Add(i + 2); tri.Add(i + 3);
        }

        // ★★★**높이 계산도 나눠서 한다** (2026-08-11 실측 — 이동 중 끊김의 조각 하나).
        //   코루틴은 **첫 `yield` 를 만날 때까지 부른 자리에서 그대로 실행된다.**
        //   그래서 이 N×N 반복문이 첫 yield 앞에 있으면 「여러 프레임에 나눠 짓기」가
        //   여기엔 안 걸리고 **통째로 LateUpdate 에 얹힌다** (실측 N=140 → 19,600번 · 6.1ms).
        //   → 시작하자마자 한 번 숨을 돌리고, 높이도 몇 줄씩 끊어 계산한다.
        yield return null;
        if (H == null || H.GetLength(0) < N) H = new float[N, N];   // 커질 때만 새로
        for (int z = 0; z < N; z++)
        {
            for (int x = 0; x < N; x++)
                H[x, z] = 땅격자.높이(x * 칸 - 반 + 원점.x + 칸 * 0.5f,
                                       z * 칸 - 반 + 원점.z + 칸 * 0.5f);
            if ((z & 15) == 15) yield return null;      // 열여섯 줄마다 숨을 돌린다
        }

        // 칸 하나의 세 겹: 안쪽 윗면(±안) → 베벨 링(±밖, 조금 낮게) → 홈벽(아래로)
        float 밖 = Mathf.Max(0.02f, 칸 * 0.5f - 틈 * 0.5f);
        float 안 = Mathf.Max(0.01f, 밖 - Mathf.Max(0f, 베벨));
        var 베벨색 = Color.white;

        for (int z = 0; z < N; z++)
        {
            yield return null;      // ★한 줄마다 숨을 돌린다 — 바깥이 시간을 재서 모아 돌린다
            for (int x = 0; x < N; x++)
            {
                float h = H[x, z];
                float cx = x * 칸 - 반 + 칸 * 0.5f;
                float cz = z * 칸 - 반 + 칸 * 0.5f;
                float hb = h - 베벨깊이;                       // 베벨 바깥 테두리의 높이

                // ① 안쪽 윗면 — 땅 그림이 붙는 자리
                면(윗삼각, new Vector3(cx - 안, h, cz - 안), new Vector3(cx - 안, h, cz + 안),
                          new Vector3(cx + 안, h, cz + 안), new Vector3(cx + 안, h, cz - 안), Color.white);

                // ② 베벨 링 — 면이 기울어 **빛이 다르게 걸린다.** 색을 칠하는 게 아니라
                //    법선이 달라져서 저절로 모서리가 읽힌다
                면(윗삼각, new Vector3(cx - 안, h, cz + 안), new Vector3(cx - 밖, hb, cz + 밖),
                          new Vector3(cx + 밖, hb, cz + 밖), new Vector3(cx + 안, h, cz + 안), 베벨색);
                면(윗삼각, new Vector3(cx + 안, h, cz - 안), new Vector3(cx + 밖, hb, cz - 밖),
                          new Vector3(cx - 밖, hb, cz - 밖), new Vector3(cx - 안, h, cz - 안), 베벨색);
                면(윗삼각, new Vector3(cx - 안, h, cz - 안), new Vector3(cx - 밖, hb, cz - 밖),
                          new Vector3(cx - 밖, hb, cz + 밖), new Vector3(cx - 안, h, cz + 안), 베벨색);
                면(윗삼각, new Vector3(cx + 안, h, cz + 안), new Vector3(cx + 밖, hb, cz + 밖),
                          new Vector3(cx + 밖, hb, cz - 밖), new Vector3(cx + 안, h, cz - 안), 베벨색);

                // ③ 홈벽. ★틈이 0 이면 **이웃보다 높은 쪽만** 세운다 — 같은 높이끼리
                //    맞붙은 자리엔 벽이 보일 리가 없는데, 다 세우면 정점이 헛되이 갑절 난다
                //    (2026-08-09 렉 잡기). 틈이 있으면 네 면 다 세워야 홈이 생긴다.
                bool 다세움 = 틈 > 0.001f;
                void 벽(int nx, int nz, Vector3 p0, Vector3 p1)
                {
                    float nh = (nx < 0 || nz < 0 || nx >= N || nz >= N) ? h : H[nx, nz];
                    if (!다세움 && nh >= hb - 0.0005f) return;
                    float 바닥 = Mathf.Min(hb - 홈깊이, nh - 홈깊이);
                    float 깊이 = hb - 바닥;
                    var 어둠 = Color.Lerp(옆면, 옆면 * (1f - 옆면어둠),
                                          Mathf.Clamp01(깊이 / Mathf.Max(0.01f, 땅격자.높이폭 + 땅격자.칸흔들)));
                    // ★감김 순서 — 위 두 점 먼저, 그다음 아래 두 점.
                    //   반대로 감으면 법선이 **칸 안쪽**을 봐서 뒷면 컬링에 통째로 먹힌다
                    //   (2026-08-09 사용자 "옆면이 비어있네" — 실제로 그 버그였다)
                    면(옆삼각, new Vector3(p0.x, hb, p0.z), new Vector3(p1.x, hb, p1.z),
                              new Vector3(p1.x, 바닥, p1.z), new Vector3(p0.x, 바닥, p0.z), 어둠);
                }
                벽(x - 1, z, new Vector3(cx - 밖, 0, cz + 밖), new Vector3(cx - 밖, 0, cz - 밖));
                벽(x + 1, z, new Vector3(cx + 밖, 0, cz - 밖), new Vector3(cx + 밖, 0, cz + 밖));
                벽(x, z - 1, new Vector3(cx - 밖, 0, cz - 밖), new Vector3(cx + 밖, 0, cz - 밖));
                벽(x, z + 1, new Vector3(cx + 밖, 0, cz + 밖), new Vector3(cx - 밖, 0, cz + 밖));
            }
        }

        if (메시 == null) 메시 = new Mesh { name = "격자바닥" };
        메시.Clear();
        메시.indexFormat = 정점.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        메시.SetVertices(정점); 메시.SetUVs(0, uv); 메시.SetColors(색);
        메시.subMeshCount = 2;
        메시.SetTriangles(윗삼각, 0);
        메시.SetTriangles(옆삼각, 1);
        메시.RecalculateNormals(); 메시.RecalculateBounds();
        mf.sharedMesh = 메시;

        var 지금 = mr.sharedMaterials;
        Material 위 = (지금 != null && 지금.Length > 0 && 지금[0] != null &&
                       지금[0].shader != null && 지금[0].shader.name != "토이라/격자바닥")
                       ? 지금[0]                       // 이미 땅 재질이 물려 있으면 지킨다
                       : 윗면재질();
        mr.sharedMaterials = new[] { 위, 옆면재질 };
        // ★★땅은 그림자를 **받기만** 한다 (2026-08-09 렉 잡기). 6만 삼각짜리 판때기를
        //   캐스케이드마다 한 번씩 더 그리면서 얻는 건 「1cm 턱이 만드는 제 그림자」뿐이다.
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        땅재질물리기();

        // ★새 메시와 새 자리를 **같은 프레임에** 갈아끼운다 — 하나만 먼저 바뀌면 어긋나 보인다
        transform.position = 원점;
        지은자리 = 원점;
        지은칸수 = N;
    }
}
