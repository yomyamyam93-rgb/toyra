using UnityEngine;

/// 길 표식 — **내가 박아서 길을 낸다.**
///
/// ★왜 필요한가 (2026-08-10): 맵이 1440m 인데 화면에 보이는 건 **가로 128m** 뿐이다
///   (최대로 당겨도 213m). 카메라가 안 돌아가고 평지라 높은 데도 없다.
///   → **모닥불로 돌아갈 방법이 기억밖에 없었다.**
///
/// ☆연기 기둥을 세우자는 안은 폐기했다 — 사용자 지적: *"멀리 볼 수 없는 카메라 구도인데?"*
///   맞다. 100m 밖은 애초에 화면에 안 들어온다. 세계 안의 표지는 **화면 안에서만** 값을 한다.
///
/// ★그래서 길은 **저절로 생기지 않고 내가 낸다** (9-0 인과와 행위).
///   나무 두 개면 박을 수 있어서, 자주 다니는 길에 하나씩 꽂아 두게 된다.
///   ☆막지 않는다 — `Blocker` 를 안 건다. 길에 세우는 것이 길을 막으면 안 된다.
public class 표식 : MonoBehaviour
{
    [Tooltip("기둥 높이 (m) — 사람 1.8m 보다 커야 풀·덤불에 안 묻힌다")] public float 높이 = 2.4f;

    /// 세운 것들 — 나중에 지도가 생기면 여기 것을 찍는다
    public static readonly System.Collections.Generic.List<표식> All =
        new System.Collections.Generic.List<표식>();

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Awake() { 짓기(); }

    void 짓기()
    {
        // 기둥 — 나무빛
        var 기둥 = Grey.Box(transform, Vector3.zero, new Vector3(0.12f, 높이, 0.12f),
                            new Color(0.40f, 0.28f, 0.17f), "기둥");
        기둥.transform.localPosition = new Vector3(0f, 높이 * 0.5f, 0f);

        // ★꼭대기에 밝은 조각 — 어두운 숲에서도 눈에 걸리게 (6장: 생물만 채도가 높고
        //   환경은 낮다. 표식은 「내가 만든 것」이라 조금 튀어도 된다)
        var 머리 = Grey.Box(transform, Vector3.zero, new Vector3(0.34f, 0.16f, 0.34f),
                            new Color(0.92f, 0.78f, 0.30f), "머리");
        머리.transform.localPosition = new Vector3(0f, 높이 + 0.05f, 0f);

        // 밑동에 돌 두 개 — 「박아 놓았다」로 읽히게
        for (int i = 0; i < 2; i++)
        {
            var d = Grey.Box(transform, Vector3.zero, new Vector3(0.2f, 0.12f, 0.2f),
                             new Color(0.46f, 0.45f, 0.43f), "밑돌", 0f, i * 55f);
            d.transform.localPosition = new Vector3(i == 0 ? 0.14f : -0.12f, 0.06f,
                                                    i == 0 ? -0.1f : 0.13f);
        }
    }

    /// 앞에 하나 박는다 — `제작창` 이 부른다
    public static 표식 박기(Vector3 자리)
    {
        var g = new GameObject("표식");
        g.transform.position = new Vector3(자리.x, 0f, 자리.z);
        return g.AddComponent<표식>();
    }
}
