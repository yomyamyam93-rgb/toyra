using System.Collections.Generic;
using UnityEngine;

/// 절차 생성 — 칸(`WorldGrid`)마다 랜드마크를 하나씩 앉힌다.
///
/// ★방식: 조각은 손으로, 조립은 기계가. 무작위 지형을 뽑는 게 아니라 **정해진 종류를
///   격자 위에 규칙대로 흩뿌린다.** 품질은 만든 만큼 나오고 배치만 매번 달라진다.
///
/// ★지금은 전부 **색칠한 상자**다. 진짜 모델로 바꾸는 자리는 아래 「교체 자리」 —
///   프리팹을 끌어다 넣으면 그때부터 상자 대신 그게 나온다. 코드는 안 고쳐도 된다.
///
/// ★땅은 지형(Terrain)이 아니라 **판때기 하나**다. 완전 평지라 높이 데이터가 필요 없고,
///   옛 프로젝트를 무겁게 하던 16MB 짜리 높이 파일도 없다.
public class WorldGen : MonoBehaviour
{
    public enum Land { 빈들판, 숲, 바위지대, 물웅덩이, 폐허, 둥지, 캠프 }

    [Header("씨앗")]
    [Tooltip("0 = 켤 때마다 새 맵 · 다른 숫자 = 그 숫자의 맵이 항상 똑같이 나온다")]
    public int worldSeed = 0;

    [Header("칸 종류가 뽑히는 비율")]
    public float w빈들판 = 4f, w숲 = 2.5f, w바위 = 2f, w물 = 1f, w폐허 = 0.8f, w둥지 = 0.8f;

    [Header("★교체 자리 — 프리팹을 넣으면 상자 대신 그게 나온다")]
    public GameObject[] 나무프리팹;
    public GameObject[] 바위프리팹;
    public GameObject[] 폐허프리팹;
    public GameObject 둥지프리팹;
    public GameObject 부화터프리팹;
    public GameObject 물프리팹;

    // 상자 색 — 「무엇인지 색으로 안다」
    public static readonly Color C땅 = new Color(0.24f, 0.30f, 0.19f);
    static readonly Color C잎 = new Color(0.20f, 0.42f, 0.20f);
    static readonly Color C줄기 = new Color(0.30f, 0.22f, 0.14f);
    static readonly Color C바위 = new Color(0.42f, 0.42f, 0.40f);
    static readonly Color C폐허 = new Color(0.33f, 0.32f, 0.30f);
    static readonly Color C물 = new Color(0.18f, 0.38f, 0.50f);
    static readonly Color C둥지 = new Color(0.70f, 0.42f, 0.15f);
    static readonly Color C알 = new Color(0.90f, 0.87f, 0.80f);
    static readonly Color C캠프 = new Color(0.75f, 0.28f, 0.28f);

    Land[,] kinds;
    Transform holder;

    // ══════════════════════════════════════════════════════════
    public void Generate()
    {
        int seed = worldSeed != 0 ? worldSeed : Random.Range(1, int.MaxValue);
        var save = Random.state;

        Blocker.Clear();
        PickKinds(seed);
        Build(seed);

        Random.state = save;
        Debug.Log($"[월드] 씨앗 {seed} · {WorldGrid.N}×{WorldGrid.N}칸 · 한 변 {WorldGrid.Size}m");
    }

    // ── ① 어느 칸에 무엇이 오나
    void PickKinds(int seed)
    {
        int n = WorldGrid.N, home = WorldGrid.Home;
        kinds = new Land[n, n];

        for (int x = 0; x < n; x++)
            for (int z = 0; z < n; z++)
            {
                if (x == home && z == home) { kinds[x, z] = Land.캠프; continue; }
                Random.InitState(WorldGrid.TileSeed(seed, x, z));

                // 집 옆 여덟 칸은 트여 있어야 한다 — 나가자마자 벽이면 답답하다
                if (Mathf.Abs(x - home) <= 1 && Mathf.Abs(z - home) <= 1)
                { kinds[x, z] = Random.value < 0.6f ? Land.빈들판 : Land.숲; continue; }

                kinds[x, z] = Roll();
            }

        // 물은 뭉쳐야 호수로 보인다
        var add = new List<Vector2Int>();
        for (int x = 0; x < n; x++)
            for (int z = 0; z < n; z++)
                if (kinds[x, z] == Land.빈들판 && Neighbors(x, z, Land.물웅덩이) >= 2)
                    add.Add(new Vector2Int(x, z));
        foreach (var p in add) kinds[p.x, p.y] = Land.물웅덩이;

        // 바위가 넉 칸 넘게 뭉치면 벽이 된다 — 하나를 튼다
        for (int x = 0; x < n; x++)
            for (int z = 0; z < n; z++)
                if (kinds[x, z] == Land.바위지대 && Neighbors(x, z, Land.바위지대) >= 3)
                    kinds[x, z] = Land.빈들판;
    }

    int Neighbors(int x, int z, Land k)
    {
        int c = 0;
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0) continue;
                if (WorldGrid.InRange(x + dx, z + dz) && kinds[x + dx, z + dz] == k) c++;
            }
        return c;
    }

    Land Roll()
    {
        float total = w빈들판 + w숲 + w바위 + w물 + w폐허 + w둥지;
        float r = Random.value * total;
        if ((r -= w빈들판) < 0) return Land.빈들판;
        if ((r -= w숲) < 0) return Land.숲;
        if ((r -= w바위) < 0) return Land.바위지대;
        if ((r -= w물) < 0) return Land.물웅덩이;
        if ((r -= w폐허) < 0) return Land.폐허;
        return Land.둥지;
    }

    // ── ② 실제로 세운다
    void Build(int seed)
    {
        Clear();
        holder = new GameObject("월드").transform;
        holder.SetParent(transform, false);

        MakeGround();

        int n = WorldGrid.N;
        for (int x = 0; x < n; x++)
            for (int z = 0; z < n; z++)
            {
                var k = kinds[x, z];
                if (k == Land.빈들판) continue;

                Random.InitState(WorldGrid.TileSeed(seed, x, z) ^ 0x5f3a);

                // 칸 한가운데가 아니라 랜덤하게 밀어서 — 이게 격자를 숨긴다
                var c = WorldGrid.TileCenter(x, z);
                if (k != Land.캠프)
                {
                    c.x += Random.Range(-1f, 1f) * WorldGrid.Tile * 0.25f;
                    c.z += Random.Range(-1f, 1f) * WorldGrid.Tile * 0.25f;
                }

                switch (k)
                {
                    case Land.숲: Forest(c); break;
                    case Land.바위지대: Rocks(c); break;
                    case Land.물웅덩이: Water(c); break;
                    case Land.폐허: Ruin(c); break;
                    case Land.둥지: Nest(c); break;
                    case Land.캠프: Camp(c); break;
                }
            }
    }

    public void Clear()
    {
        var old = transform.Find("월드");
        if (old != null)
        {
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
        }
        holder = null;
    }

    /// 땅 — 지형이 아니라 판때기 하나 (완전 평지)
    void MakeGround()
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
        g.name = "땅";
        g.transform.SetParent(holder, true);
        g.transform.position = new Vector3(WorldGrid.Size * 0.5f, 0f, WorldGrid.Size * 0.5f);
        g.transform.localScale = Vector3.one * (WorldGrid.Size / 10f);   // Plane 은 한 변 10m
        Grey.Strip(g);
        g.GetComponent<MeshRenderer>().sharedMaterial = Grey.Mat(C땅);
    }

    // ══════════════════════════════════════════════════════════
    //  랜드마크 (크기는 전부 사람 1.8m 기준)
    // ══════════════════════════════════════════════════════════

    void Forest(Vector3 c)
    {
        int count = Random.Range(24, 46);
        float spread = Random.Range(35f, 60f);
        for (int i = 0; i < count; i++)
        {
            var p = Scatter(c, spread);
            if (Swap(나무프리팹, p, true, 0.5f)) continue;
            float h = Random.Range(9f, 15f);              // 나무 9~15m — 사람의 5~8배
            float w = Random.Range(4f, 7f);
            float tr = Random.Range(0.4f, 0.7f);
            var trunk = Grey.Box(holder, p + Vector3.up * (h * 0.3f), new Vector3(tr, h * 0.6f, tr), C줄기, "나무_줄기");
            var leaf = Grey.Box(holder, p + Vector3.up * (h * 0.75f), new Vector3(w, h * 0.5f, w), C잎, "나무_잎");
            leaf.transform.SetParent(trunk.transform, true);   // 줄기를 캐면 잎도 같이 사라진다
            Blocker.Add(p, tr * 0.7f);                         // 줄기 굵기만 막는다

            // ★선 나무를 패면 자원이 아니라 **쓰러진다.** 나무는 통나무를 패야 나온다
            var fall = trunk.AddComponent<TreeFall>();
            fall.통나무값 = Mathf.RoundToInt(4f + h * 0.5f);      // 큰 나무일수록 많이
            fall.선자리 = p;

            var hv = trunk.AddComponent<Harvest>();
            hv.kind = Stock.Kind.나무;
            hv.hits = 4; hv.perHit = 0;        // 선 나무를 팬다고 나무가 나오진 않는다
            hv.blockAt = p; hv.장애물치우기 = false;
            hv.쓰러짐 = fall;
        }
    }

    void Rocks(Vector3 c)
    {
        int count = Random.Range(7, 15);
        float spread = Random.Range(15f, 28f);
        for (int i = 0; i < count; i++)
        {
            var p = i == 0 ? c : Scatter(c, spread);
            if (Swap(바위프리팹, p, true, 1.2f)) continue;
            float w = i == 0 ? Random.Range(3.5f, 5.5f) : Random.Range(1.2f, 2.8f);
            float h = w * Random.Range(0.7f, 1.4f);
            var rock = Grey.Box(holder, p + Vector3.up * (h * 0.42f),
                     new Vector3(w, h, w * Random.Range(0.7f, 1.3f)), C바위, "바위",
                     w * 0.5f, Random.value * 360f);

            var hv = rock.AddComponent<Harvest>();
            hv.kind = Stock.Kind.돌; hv.hits = Mathf.RoundToInt(3f + w); hv.perHit = 2; hv.blockAt = p;
        }
    }

    void Ruin(Vector3 c)
    {
        int count = Random.Range(5, 9);
        float r = Random.Range(8f, 14f);
        float a0 = Random.value * Mathf.PI * 2f;
        for (int i = 0; i < count; i++)
        {
            float a = a0 + i * Mathf.PI * 2f / count + Random.Range(-0.16f, 0.16f);
            var p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
            if (Swap(폐허프리팹, p, true, 0.8f)) continue;
            float w = Random.Range(0.8f, 1.4f), h = Random.Range(3.5f, 6f);
            Grey.Box(holder, p + Vector3.up * (h * 0.45f), new Vector3(w, h, w), C폐허, "돌기둥",
                     w * 0.6f, Random.value * 360f);
        }
        // 하나쯤은 쓰러져 있어야 폐허로 읽힌다
        var f = Scatter(c, r * 0.5f);
        Grey.Box(holder, f + Vector3.up * 0.5f, new Vector3(Random.Range(3f, 5f), 1f, 1.2f),
                 C폐허, "쓰러진돌", 1.2f, Random.value * 360f);
    }

    void Nest(Vector3 c)
    {
        if (Swap(둥지프리팹 != null ? new[] { 둥지프리팹 } : null, c, false, 2.5f)) return;
        Grey.Box(holder, c + Vector3.up * 0.4f, new Vector3(8f, 0.8f, 8f), C둥지, "둥지", 3.4f);
        int eggs = Random.Range(2, 5);
        for (int i = 0; i < eggs; i++)
            Grey.Box(holder, Scatter(c, 2.2f) + Vector3.up * 1.1f,
                     new Vector3(0.7f, 0.9f, 0.7f), C알, "알");
    }

    void Camp(Vector3 c)
    {
        if (Swap(부화터프리팹 != null ? new[] { 부화터프리팹 } : null, c, false, 4f)) return;
        Grey.Box(holder, c + Vector3.up * 2f, new Vector3(10f, 4f, 10f), C캠프, "부화터", 5f);
    }

    void Water(Vector3 c)
    {
        if (Swap(물프리팹 != null ? new[] { 물프리팹 } : null, c, false, 0f)) return;
        float r = Random.Range(14f, 26f);
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = "물웅덩이";
        g.transform.SetParent(holder, true);
        g.transform.localScale = new Vector3(r * 2f, 0.04f, r * 2f * Random.Range(0.7f, 1.2f));
        g.transform.rotation = Quaternion.Euler(0f, Random.value * 360f, 0f);
        g.transform.position = c + Vector3.up * 0.03f;
        Grey.Strip(g);
        g.GetComponent<MeshRenderer>().sharedMaterial = Grey.Mat(C물);
    }

    /// 그 자리가 어떤 칸인가 — 야생 구성이 이걸 보고 갈린다
    public Land KindAt(Vector3 p)
    {
        if (kinds == null) return Land.빈들판;
        int x = Mathf.Clamp(Mathf.FloorToInt(p.x / WorldGrid.Tile), 0, WorldGrid.N - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt(p.z / WorldGrid.Tile), 0, WorldGrid.N - 1);
        return kinds[x, z];
    }

    // ── 부품
    Vector3 Scatter(Vector3 c, float radius)
    {
        float a = Random.value * Mathf.PI * 2f;
        return c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * Mathf.Sqrt(Random.value);
    }

    bool Swap(GameObject[] set, Vector3 pos, bool randomYaw, float blockR)
    {
        if (set == null || set.Length == 0) return false;
        var pf = set[Random.Range(0, set.Length)];
        if (pf == null) return false;
        var rot = randomYaw ? Quaternion.Euler(0f, Random.value * 360f, 0f) : Quaternion.identity;
        Instantiate(pf, pos, rot, holder);
        if (blockR > 0f) Blocker.Add(pos, blockR);
        return true;
    }
}
