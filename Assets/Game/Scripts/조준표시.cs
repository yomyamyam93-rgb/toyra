using UnityEngine;

/// 마우스가 가리키는 곳과 **때리면 닿는 데**를 땅에 그린다 (좀보이드식 조준 표시).
///
/// ★★★**그림 = 판정.** 사거리를 인스펙터에서 베껴 오지 않고 `HeroAttack.닿는거리()` 를
///   그대로 부른다. 무기를 길게 바꾸면 표시도 저절로 따라온다 — 어긋날 방법이 없다.
///   (이 프로젝트에서 「몽둥이가 닿는데 딜이 안 박힌다」로 한 번 크게 당한 자리다)
///
/// ★설명 문구를 안 쓴다 — 부채꼴과 고리가 스스로 말한다 (「UI 문구 규칙」).
/// ★땅 높이를 따라 눕는다 (`땅격자.걷는높이`) — 평평하게 그리면 언덕에서 땅을 뚫는다.
[DefaultExecutionOrder(400)]      // Hero 가 LookDir 를 정한 **뒤에** 그린다
public class 조준표시 : MonoBehaviour
{
    [Header("부채꼴 (때리면 닿는 데)")]
    [Tooltip("안 보이게 하려면 0")] [Range(0f, 1f)] public float 부채진하기 = 0.10f;
    [Tooltip("바깥 테두리 밝기")] [Range(0f, 1f)] public float 테두리진하기 = 0.42f;
    [Tooltip("테두리 굵기 (m)")] public float 테두리 = 0.09f;
    public Color 색 = new Color(1f, 0.95f, 0.75f);
    [Tooltip("적이 부채꼴 안에 있으면 이 색")] public Color 잡힘색 = new Color(1f, 0.45f, 0.35f);

    [Header("커서 고리")]
    public float 고리반지름 = 0.42f;
    public float 고리굵기 = 0.07f;
    [Range(0f, 1f)] public float 고리진하기 = 0.55f;

    [Header("공통")]
    [Tooltip("땅에서 띄우는 높이 (m)")] public float 띄움 = 0.04f;
    [Tooltip("부채꼴을 몇 조각으로 그리나")] [Range(8, 64)] public int 조각 = 28;

    float 보인반지름;
    Hero hero;
    HeroAttack 공격;
    IsoCam 카메라;
    Mesh 메시;
    Material 재질;
    MeshFilter mf;
    MeshRenderer mr;

    // 버퍼는 들고 있는다 — 매 프레임 새로 만들면 GC 가 쌓인다
    readonly System.Collections.Generic.List<Vector3> 정점 = new System.Collections.Generic.List<Vector3>(512);
    readonly System.Collections.Generic.List<Color> 색깔 = new System.Collections.Generic.List<Color>(512);
    readonly System.Collections.Generic.List<int> 삼각 = new System.Collections.Generic.List<int>(1024);

    void Awake()
    {
        hero = FindFirstObjectByType<Hero>();
        공격 = FindFirstObjectByType<HeroAttack>();
        카메라 = FindFirstObjectByType<IsoCam>();

        var sh = Shader.Find("토이라/조준선");
        if (sh == null) { Debug.LogWarning("[조준표시] 셰이더를 못 찾았다"); enabled = false; return; }
        재질 = new Material(sh) { name = "조준선" };

        mf = gameObject.AddComponent<MeshFilter>();
        mr = gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = 재질;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

        메시 = new Mesh { name = "조준표시" };
        메시.MarkDynamic();
        mf.sharedMesh = 메시;
    }

    void LateUpdate()
    {
        if (hero == null) { hero = FindFirstObjectByType<Hero>(); if (hero == null) return; }
        transform.position = Vector3.zero;      // 정점을 월드로 쌓는다

        정점.Clear(); 색깔.Clear(); 삼각.Clear();

        var 발 = hero.transform.position;
        var look = hero.LookDir; look.y = 0f;
        if (look.sqrMagnitude < 1e-5f) look = Vector3.forward; else look.Normalize();

        // ★★사거리·각도를 **판정에서 그대로** 가져온다. 단 「지금 이 순간」 무기 끝이 아니라
        //   **한 판을 휘두르며 나온 제일 먼 거리**를 쓴다 — 그게 실제로 맞는 거리이고,
        //   순간값을 쓰면 팔이 오르내릴 때마다 고리가 출렁인다.
        float 반지름 = 공격 != null
            ? (공격.표시사거리 > 0.01f ? 공격.표시사거리 : 공격.사거리)
            : 2f;
        보인반지름 = 보인반지름 <= 0f ? 반지름
                   : Mathf.MoveTowards(보인반지름, 반지름, Time.deltaTime * 4f);   // 바뀌어도 부드럽게
        반지름 = 보인반지름;
        // ★★인스펙터의 `각도` 가 아니라 **실제로 맞는 각도**를 그린다.
        //   판정은 양쪽으로 `판정각여유`(14°)씩 더 열고 `판정거리여유`(0.12m)만큼 봐준다 —
        //   그걸 빼고 그렸더니 "휘두르는 것보다 영역이 좁다" 가 됐다.
        float 각 = 공격 != null ? 공격.표시각도 : 70f;
        if (공격 != null) 반지름 += 공격.판정거리여유;

        bool 잡힘 = 안에누가있나(발, look, 반지름, 각);
        var c = 잡힘 ? 잡힘색 : 색;

        부채꼴(발, look, 반지름, 각, c);

        // 커서 고리
        if (카메라 != null && 고리진하기 > 0.001f &&
            카메라.MouseGround(발.y, out var 커서))
            고리(커서, 고리반지름, 고리굵기, c, 고리진하기);

        메시.Clear();
        if (정점.Count == 0) return;
        메시.SetVertices(정점); 메시.SetColors(색깔); 메시.SetTriangles(삼각, 0);
        메시.RecalculateBounds();
    }

    /// 부채꼴 — 안쪽은 옅게 채우고 바깥 테두리만 또렷하게
    void 부채꼴(Vector3 발, Vector3 look, float r, float 각, Color c)
    {
        int n = Mathf.Clamp(조각, 8, 64);
        float 시작 = -각 * 0.5f, 폭 = 각 / n;
        float 안 = Mathf.Max(0.05f, r - Mathf.Max(0.01f, 테두리));

        for (int i = 0; i < n; i++)
        {
            var d0 = Quaternion.Euler(0f, 시작 + 폭 * i, 0f) * look;
            var d1 = Quaternion.Euler(0f, 시작 + 폭 * (i + 1), 0f) * look;

            if (부채진하기 > 0.001f)
                띠(발, d0, d1, 0.25f, 안, c, 부채진하기);
            if (테두리진하기 > 0.001f)
                띠(발, d0, d1, 안, r, c, 테두리진하기);
        }
    }

    void 고리(Vector3 가운데, float r, float 굵기, Color c, float 진하기)
    {
        int n = 20;
        for (int i = 0; i < n; i++)
        {
            var d0 = Quaternion.Euler(0f, 360f / n * i, 0f) * Vector3.forward;
            var d1 = Quaternion.Euler(0f, 360f / n * (i + 1), 0f) * Vector3.forward;
            띠(가운데, d0, d1, Mathf.Max(0.01f, r - 굵기), r, c, 진하기);
        }
    }

    /// 두 방향 사이의 사각 조각 하나 — 네 점 다 **땅 높이**에 눕힌다
    void 띠(Vector3 가운데, Vector3 d0, Vector3 d1, float r0, float r1, Color c, float a)
    {
        var p0 = 땅에(가운데 + d0 * r0);
        var p1 = 땅에(가운데 + d0 * r1);
        var p2 = 땅에(가운데 + d1 * r1);
        var p3 = 땅에(가운데 + d1 * r0);

        int i = 정점.Count;
        정점.Add(p0); 정점.Add(p1); 정점.Add(p2); 정점.Add(p3);
        var col = new Color(c.r, c.g, c.b, a);
        for (int k = 0; k < 4; k++) 색깔.Add(col);
        삼각.Add(i); 삼각.Add(i + 1); 삼각.Add(i + 2);
        삼각.Add(i); 삼각.Add(i + 2); 삼각.Add(i + 3);
    }

    Vector3 땅에(Vector3 p) => new Vector3(p.x, 땅격자.걷는높이(p.x, p.z) + 띄움, p.z);

    /// 부채꼴 안에 때릴 게 들어와 있나 — 들어와 있으면 색이 바뀐다
    bool 안에누가있나(Vector3 발, Vector3 look, float r, float 각)
    {
        float 반각 = 각 * 0.5f;
        foreach (var c in Critter.All)
        {
            if (c == null || !c.isActiveAndEnabled || !c.Alive) continue;
            var v = c.transform.position - 발; v.y = 0f;
            float d = v.magnitude - c.Radius;
            if (d > r || d < -r) continue;
            if (Vector3.Angle(look, v.sqrMagnitude < 1e-6f ? look : v.normalized) > 반각) continue;
            return true;
        }
        return false;
    }
}
