using System.Collections.Generic;
using UnityEngine;

/// 사라질 때 **메시 면이 지워진다** — 아래에서 위로 훑고, 잘리는 경계가 빛난다.
///
/// ★2026-08-12 사용자 확정: *"마자 메시면이 지워지면서 사라지게"*.
///   옛 프로젝트(`toyrassic/PetUnit.Dissolve`)에서 한 번 고쳐 잡은 그 방식이다 —
///   몸을 줄이지 않는다. 크기는 끝까지 그대로고, 문턱을 넘은 면이 잘려 나갈 뿐이다.
///
/// ★★9-4(렉): **재질을 사체마다 새로 만들지 않는다.** 원본 재질 하나당 디졸브 재질도
///   하나다 — 같은 종은 같은 재질을 나눠 쓴다. 진행도만 `MaterialPropertyBlock` 으로 준다.
///   ☆`외곽선붙이기` 가 재질 슬롯을 하나 더 얹어 두므로 **먼저 뗀다** —
///     안 떼면 몸이 지워진 자리에 **검은 실루엣만** 덩그러니 남는다.
[DisallowMultipleComponent]
public class 디졸브 : MonoBehaviour
{
    [Tooltip("다 지워지는 데 걸리는 시간 (초)")] public float 시간 = 0.9f;

    /// 이 물체를 지우기 시작한다 — 다 지워지면 스스로 사라진다
    public static void 시작(GameObject go, float 시간 = 0.9f)
    {
        if (go == null || go.GetComponent<디졸브>() != null) return;
        go.AddComponent<디졸브>().시간 = 시간;
    }

    // ── 재질 (원본 하나당 한 벌)
    static readonly Dictionary<Material, Material> 짝 = new Dictionary<Material, Material>();

    static Material 디졸브재질(Material 원본)
    {
        if (원본 == null) return null;
        if (짝.TryGetValue(원본, out var 있던) && 있던 != null) return 있던;

        var sh = Shader.Find("Toyra/디졸브");
        if (sh == null) { Debug.LogWarning("[디졸브] 셰이더를 못 찾았다 — Assets/Game/Shaders/디졸브.shader"); return 원본; }

        var m = new Material(sh) { name = 원본.name + " (디졸브)" };

        // ★그림을 그대로 옮겨 온다 — 펫은 glTF 셰이더그래프라 이름이 다르다
        foreach (var nm in new[] { "baseColorTexture", "_BaseMap", "_MainTex" })
            if (원본.HasProperty(nm) && 원본.GetTexture(nm) != null)
            {
                m.SetTexture("_BaseMap", 원본.GetTexture(nm));
                m.SetTextureScale("_BaseMap", 원본.GetTextureScale(nm));
                m.SetTextureOffset("_BaseMap", 원본.GetTextureOffset(nm));
                break;
            }
        foreach (var nm in new[] { "baseColorFactor", "_BaseColor", "_Color" })
            if (원본.HasProperty(nm)) { m.SetColor("_BaseColor", 원본.GetColor(nm)); break; }

        m.enableInstancing = true;
        짝[원본] = m;
        return m;
    }

    // ── 진행
    Renderer[] 렌들;
    MaterialPropertyBlock 칠;
    float t, y0, y1;

    void Start()
    {
        // ★외곽선을 먼저 뗀다 (위 설명)
        var 선 = GetComponent<외곽선붙이기>();
        if (선 != null) { 선.떼기(); 선.enabled = false; }

        var 목록 = new List<Renderer>();
        var 통 = new Bounds(transform.position, Vector3.one * 0.1f);
        bool 첫 = true;

        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r is ParticleSystemRenderer || !r.enabled) continue;

            var 지금 = r.sharedMaterials;
            var 새것 = new Material[지금.Length];
            for (int i = 0; i < 지금.Length; i++) 새것[i] = 디졸브재질(지금[i]);
            r.sharedMaterials = 새것;

            목록.Add(r);
            if (첫) { 통 = r.bounds; 첫 = false; } else 통.Encapsulate(r.bounds);
        }

        렌들 = 목록.ToArray();
        y0 = 통.min.y - 0.05f;
        y1 = 통.max.y + 0.05f;
        칠 = new MaterialPropertyBlock();
        밀기(0f);
    }

    void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / Mathf.Max(0.05f, 시간));
        밀기(k);
        if (k >= 1f) Destroy(gameObject);
    }

    void 밀기(float k)
    {
        if (렌들 == null) return;
        for (int i = 0; i < 렌들.Length; i++)
        {
            var r = 렌들[i];
            if (r == null) continue;
            r.GetPropertyBlock(칠);
            칠.SetFloat("_Dissolve", k);
            칠.SetFloat("_Y0", y0);
            칠.SetFloat("_Y1", y1);
            r.SetPropertyBlock(칠);
        }
    }
}
