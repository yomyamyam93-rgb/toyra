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
    [Tooltip("발밑 땅색에 곱하는 값 — 1이면 땅과 완전히 같은 색")]
    public float 밝기 = 1.06f;
    [Tooltip("땅에서 살짝 띄운다 (겹치면 지지직거린다)")] public float 띄우기 = 0.03f;

    const int 묶음 = 1000;                 // 한 번에 그리는 최대 개수 (한계 1023)

    // ★[그림][땅톤] 로 재질을 나눈다 (2026-08-04).
    //   인스턴스마다 색을 다르게 주려 했더니 URP 기본 재질이 그걸 안 받아서
    //   색이 제멋대로 튀고 **반짝거렸다.** 재질을 나누면 확실하게 색이 고정된다.
    Material[,] 재질들;
    Material[] 마스크재질;      // 그림마다 하나 (알파 모양이 달라서)
    float[] 비율;
    Texture2D[] 흰그림들;
    Mesh 판;
    float yaw = 45f;

    List<Matrix4x4>[,] 그릴것;
    Vector2Int 지난칸 = new Vector2Int(int.MinValue, int.MinValue);

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

        var sh = Shader.Find("Universal Render Pipeline/Lit");
        int 톤수 = GroundPaint.톤수;
        재질들 = new Material[그림.Length, 톤수];
        그릴것 = new List<Matrix4x4>[그림.Length, 톤수];
        비율 = new float[그림.Length];

        흰그림들 = new Texture2D[그림.Length];
        for (int i = 0; i < 그림.Length; i++)
        {
            비율[i] = 그림[i].width / (float)그림[i].height;
            var 흰그림 = 흰색으로(그림[i]);          // ★색은 땅이 정한다 (아래 참고)
            흰그림들[i] = 흰그림;

            for (int t = 0; t < 톤수; t++)
            {
                그릴것[i, t] = new List<Matrix4x4>();

                var m = new Material(sh) { name = $"잔디{i}_{t}" };
                m.mainTexture = 흰그림;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", 흰그림);

                // ★발밑 땅색에 맞춘다 — 그림이 밝은 연두라 그대로 쓰면 바닥에서 뜬다
                var c = GroundPaint.톤색(t + 1) * 밝기; c.a = 1f;
                m.color = c;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);

                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.5f);
                m.renderQueue = 2450;
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
                if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
                if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0f);
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
                m.enableInstancing = true;
                재질들[i, t] = m;
            }
        }

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

    void Update()
    {
        var hero = Hero.Me;
        if (hero == null || 재질들 == null) return;

        var p = hero.transform.position;
        var 칸 = new Vector2Int(Mathf.FloorToInt(p.x / 간격), Mathf.FloorToInt(p.z / 간격));

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

        // 한 칸이라도 움직였을 때만 목록을 다시 뽑는다 (그 외엔 그리기만)
        if (칸 != 지난칸) { 지난칸 = 칸; 목록뽑기(p); }

        그리기();
    }

    void 목록뽑기(Vector3 p)
    {
        int 그림수 = 재질들.GetLength(0), 톤수 = 재질들.GetLength(1);
        for (int i = 0; i < 그림수; i++)
            for (int t = 0; t < 톤수; t++) 그릴것[i, t].Clear();

        int r = Mathf.CeilToInt(반경 / 간격);
        int cx = Mathf.FloorToInt(p.x / 간격), cz = Mathf.FloorToInt(p.z / 간격);
        var rot = Quaternion.Euler(0f, yaw, 0f);
        var 집 = WorldGrid.Center;
        float r2 = 반경 * 반경;

        var st = Random.state;
        for (int gx = cx - r; gx <= cx + r; gx++)
            for (int gz = cz - r; gz <= cz + r; gz++)
            {
                float dx = (gx + 0.5f) * 간격 - p.x, dz = (gz + 0.5f) * 간격 - p.z;
                if (dx * dx + dz * dz > r2) continue;

                Random.InitState(WorldGrid.TileSeed(0x9e55, gx, gz));
                if (Random.value > 밀도) continue;

                var at = new Vector3((gx + Random.value) * 간격, 띄우기, (gz + Random.value) * 간격);

                // ★덤불 자리인가 — 낮은 주파수 노이즈가 높은 곳에만 난다.
                //   가장자리로 갈수록 성글어져서 덤불의 경계가 선으로 안 보인다.
                if (뭉치크기 > 0.01f && 뭉치비율 < 0.999f)
                {
                    float n = Mathf.PerlinNoise(at.x / 뭉치크기 + 137.1f, at.z / 뭉치크기 + 71.3f);
                    float 문턱 = 1f - 뭉치비율;
                    if (n < 문턱) continue;
                    if (Random.value > Mathf.InverseLerp(문턱, 1f, n)) continue;
                }

                // ★발밑 땅의 톤을 그대로 따른다 — 잔디가 아니면 아예 안 난다
                int 톤 = GroundPaint.톤(at);
                if (톤 <= 0) continue;
                if ((new Vector2(at.x - 집.x, at.z - 집.z)).sqrMagnitude < 9f * 9f) continue;

                // 한 칸에 여러 포기를 몰아 심는다 — 덤불 안이 빽빽해진다
                int 톤칸 = Mathf.Clamp(톤 - 1, 0, 톤수 - 1);
                int 몇 = Mathf.Max(1, 칸당);
                for (int k = 0; k < 몇; k++)
                {
                    var 자리 = k == 0 ? at
                             : new Vector3((gx + Random.value) * 간격, 띄우기, (gz + Random.value) * 간격);
                    int i = Random.Range(0, 그림수);
                    float h = Random.Range(최소키, 최대키);
                    그릴것[i, 톤칸].Add(Matrix4x4.TRS(자리, rot, new Vector3(h * 비율[i], h, 1f)));
                }
            }
        Random.state = st;
    }

    static readonly Matrix4x4[] 버퍼 = new Matrix4x4[묶음];

    void 그리기()
    {
        int 그림수 = 재질들.GetLength(0), 톤수 = 재질들.GetLength(1);
        for (int i = 0; i < 그림수; i++)
            for (int t = 0; t < 톤수; t++)
            {
                var 목록 = 그릴것[i, t];
                for (int s = 0; s < 목록.Count; s += 묶음)
                {
                    int n = Mathf.Min(묶음, 목록.Count - s);
                    목록.CopyTo(s, 버퍼, 0, n);

                    Graphics.DrawMeshInstanced(판, 0, 재질들[i, t], 버퍼, n, null,
                        UnityEngine.Rendering.ShadowCastingMode.Off, false);

                    // 실루엣 마스크에도 같이 (펫 테두리가 잔디 위에 덧그려지지 않게)
                    if (마스크재질 != null && 마스크재질[i] != null)
                        Graphics.DrawMeshInstanced(판, 0, 마스크재질[i], 버퍼, n, null,
                            UnityEngine.Rendering.ShadowCastingMode.Off, false, Outliner.잔디층);
                }
            }
    }
}
