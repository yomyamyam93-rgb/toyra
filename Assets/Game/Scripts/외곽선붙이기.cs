using System.Collections.Generic;
using UnityEngine;

/// 캐릭터·펫에 **검은 테두리**를 붙인다 — 재질 슬롯을 하나 더 얹는 방식.
///
/// ★★왜 기존 `Outliner` 를 안 쓰나 (2026-08-09 사용자 "아웃라인 좋고, 근데 픽셀은 하지말자").
///   기존 것은 실루엣 복사본을 만들어 두고 **`PixelScreen` 이 칠하는** 구조라,
///   픽셀 화면을 끄면 선이 아예 안 그려진다.
///   → 이건 물체 자신의 재질에 **패스 하나를 더 얹는다.** 후처리에 안 기댄다.
///
/// ★스킨드 메시에도 그대로 먹는다 — 같은 메시를 한 번 더 그리는 것이라 뼈가 이미 적용돼 있다.
/// ★비용은 **드로콜 ×2** 다. 캐릭터·펫처럼 화면에 몇 마리 없는 것에만 붙인다.
///   나무 2만 그루에는 절대 붙이지 말 것.
[DisallowMultipleComponent]
public class 외곽선붙이기 : MonoBehaviour
{
    [Tooltip("선 색")] public Color 색 = new Color(0.08f, 0.07f, 0.10f, 1f);
    // ★0.0045 → 0.0012 (2026-08-09 사용자 "최외곽만 얇게 지금 너무 두껍다").
    //   캐릭터가 화면에서 100픽셀 남짓이라, 굵으면 실루엣을 파먹는다.
    [Tooltip("화면 비례 굵기 — 줌을 바꿔도 같은 굵기로 보인다")] [Range(0f, 0.02f)] public float 굵기 = 0.0012f;
    [Tooltip("아무리 작아도 이만큼은 (m)")] [Range(0f, 0.2f)] public float 최소굵기 = 0.0015f;
    [Tooltip("자식까지 전부")] public bool 자식까지 = true;

    // ★★★재질을 **렌더러마다 새로 만들지 않는다** (2026-08-09 사용자 "두두두둑 생성렉").
    //   야생이 1.5초에 하나씩 생기는데 그때마다 렌더러 수만큼 `new Material` 이 났다.
    //   재질을 새로 만들면 ①할당이 나고 ②재질이 다 달라 **한 번에 못 그린다.**
    //   색·굵기가 같으면 결과가 같으니 **그 조합마다 한 벌만** 만들어 나눠 쓴다.
    static readonly Dictionary<(Color, float, float), Material> 재질칸 =
        new Dictionary<(Color, float, float), Material>();
    readonly List<Renderer> 붙인것 = new List<Renderer>();

    static Material 선재질(Color 색, float 굵기, float 최소굵기)
    {
        var 키 = (색, 굵기, 최소굵기);
        if (재질칸.TryGetValue(키, out var 있던) && 있던 != null) return 있던;
        var sh = Shader.Find("토이라/외곽선");
        if (sh == null) { Debug.LogWarning("[외곽선] 셰이더를 못 찾았다 — Assets/Game/Shaders/외곽선.shader"); return null; }
        var m = new Material(sh) { name = "외곽선(공용)" };
        m.SetColor("_OutlineColor", 색);
        m.SetFloat("_OutlineWidth", 굵기);
        m.SetFloat("_MinWidth", 최소굵기);
        m.enableInstancing = true;
        재질칸[키] = m;
        return m;
    }

    void OnEnable() { 붙이기(); }
    void OnValidate() { if (isActiveAndEnabled) 붙이기(); }

    [ContextMenu("다시 붙이기")]
    public void 붙이기()
    {
        var 선 = 선재질(색, 굵기, 최소굵기);
        if (선 == null) return;

        var 대상 = 자식까지 ? GetComponentsInChildren<Renderer>(true) : GetComponents<Renderer>();
        foreach (var r in 대상)
        {
            if (r == null || r is ParticleSystemRenderer) continue;
            var 지금 = r.sharedMaterials;
            // 이미 붙어 있으면 건너뛴다 (매번 슬롯이 늘어나면 안 된다)
            bool 있음 = false;
            foreach (var m in 지금) if (m != null && m.shader != null && m.shader.name == "토이라/외곽선") { 있음 = true; break; }
            if (있음) continue;

            var 새것 = new Material[지금.Length + 1];
            System.Array.Copy(지금, 새것, 지금.Length);
            새것[지금.Length] = 선;
            r.sharedMaterials = 새것;
            붙인것.Add(r);
        }
    }

    [ContextMenu("떼기")]
    public void 떼기()
    {
        var 대상 = 자식까지 ? GetComponentsInChildren<Renderer>(true) : GetComponents<Renderer>();
        foreach (var r in 대상)
        {
            if (r == null) continue;
            var mats = new List<Material>(r.sharedMaterials);
            mats.RemoveAll(m => m != null && m.shader != null && m.shader.name == "토이라/외곽선");
            r.sharedMaterials = mats.ToArray();
        }
        붙인것.Clear();
    }
}
