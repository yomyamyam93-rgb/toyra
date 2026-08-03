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

    [Header("동행 펫 — 한 마리만 데리고 다닌다")]
    public bool 펫데리고시작 = true;

    void Start()
    {
        if (world == null) world = FindFirstObjectByType<WorldGen>();
        if (hero == null) hero = FindFirstObjectByType<Hero>();

        Stock.Clear();
        if (world != null) world.Generate();

        if (hero == null) return;

        // 집 칸 한가운데 — 부화터 바로 옆에 선다
        var c = WorldGrid.Center;
        hero.transform.position = new Vector3(c.x, 0f, c.z - 12f);

        if (!펫데리고시작) return;

        // 이미 있으면 또 만들지 않는다 — 동행 펫은 **한 마리**다
        foreach (var already in Critter.All)
            if (already != null && already.side == Critter.Side.내편) return;

        // ★영웅 펫 — 지금은 하나. 자동으로 싸우고 나를 따라온다.
        //   종 데이터는 `Wildlife.내펫` 에 있다 (인스펙터에서 모델을 끼울 수 있게)
        var wl = FindFirstObjectByType<Wildlife>();
        if (wl == null) return;
        var pet = Wildlife.Make(wl.내펫, hero.transform.position + Vector3.right * 3f,
                                Critter.Side.내편, hero.transform);
        pet.name = "내펫";
    }
}
