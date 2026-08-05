using System.Collections.Generic;
using UnityEngine;

/// 검은 테두리 붙이기 — 모델마다 **부풀린 껍데기**를 하나 더 씌운다.
///
/// ★왜 후처리가 아니라 이 방식인가 (2026-08-04):
///   · 색으로 잡으면 모델 **안쪽 무늬마다** 선이 껴서 그물이 된다
///   · 깊이로 잡으려니 UI 단계에서는 깊이 텍스처가 안 잡혔다
///   → 물체마다 직접 그리는 게 확실하다. 실루엣만 나오고, 반드시 나온다.
///
/// ★두께가 미터인 게 여기선 장점이다: 픽셀 밀도가 고정이라
///   **1 ÷ 픽셀당미터 = 정확히 1픽셀**. 줌해도 선 굵기가 안 변한다.
[DefaultExecutionOrder(310)]
public class Outliner : MonoBehaviour
{
    /// 실루엣 복사본이 사는 층 — 본 카메라는 이 층을 안 찍고, 마스크 카메라만 찍는다
    public const int 층 = 31;

    /// ★잔디는 **다른 층**에 그린다 (2026-08-04). 마스크를 두 장 만들려면 갈라야 한다:
    ///   · 「보이는 것」 = 물체 + 잔디  (잔디가 가린 자리를 알려 준다)
    ///   · 「물체만」   = 물체         (테두리를 그릴 진짜 윤곽을 알려 준다)
    ///   이 둘을 견주면 **잔디가 가린 부분만** 선을 뺄 수 있다.
    public const int 잔디층 = 30;

    [Tooltip("몇 초마다 새로 생긴 것을 훑나 (0 이면 처음 한 번만)")]
    public float 다시훑기 = 1.5f;
    // ★★**부풀리기와 두께는 따로 논다** (2026-08-05 사용자 확정 — "부풀리기 0 까지
    //   할 수 있게 다시 해주고, 두께만 따로 지정 가능한 걸 넣어줘야지").
    //
    //     · `부풀리기`(여기)              = 실루엣을 **바깥으로 미는 픽셀 수**
    //     · `PixelScreen.외곽선두께`      = 그 위에 **실제로 칠하는 픽셀 수**
    //
    //   ★둘이 같으면 선이 몸에 딱 붙고 속이 찬다.
    //   ★부풀리기 > 두께 면 남는 껍데기가 비어 **몸에서 떨어져 뜬 선**이 된다.
    //   ★부풀리기 < 두께 면 모자란 만큼 **몸을 갉아먹는다.**
    //     ☆**부풀리기 0 이 그 극단**이다 — 선이 통째로 몸 안쪽에 그려진다.
    //       이건 고장이 아니라 하나의 그림체라서 쓸 수 있게 남겨 둔다.
    //
    //   → 어느 쪽이 맞다고 코드가 정하지 않는다. 눈으로 보고 고르는 자리다.
    [Tooltip("실루엣을 몇 픽셀 부풀리나 — 0 이면 선이 몸 안쪽에 그려진다")]
    [Range(0f, 6f)] public float 부풀리기 = 1f;
    [Tooltip("이 이름이 들어간 것은 건너뛴다")]
    // ★환경(잔디·나무·땅)은 테두리를 안 두른다 (2026-08-04 사용자 확정).
    //   풀과 잎마다 검은 선이 생기면 화면이 그물처럼 지저분해진다.
    //   테두리는 **살아 있는 것과 사람이 놓은 것**에만 두른다.
    public string[] 건너뛰기 =
    {
        "땅", "물웅덩이", "시야_덮개", "픽셀_",
        "풀", "잔디", "나무", "잎", "줄기", "통나무",
    };

    /// 실루엣을 밖으로 미는 거리 (m) — **부풀리기 픽셀 수를 그대로** 미터로 바꾼 값.
    /// ★두께(`PixelScreen.외곽선두께`)와는 독립이다. 맞추고 어긋내는 건 눈으로 고른다.
    float 밀거리()
    {
        var ps = FindFirstObjectByType<PixelScreen>();
        float 한픽셀 = ps != null && ps.유효픽셀당미터 > 0.01f ? 1f / ps.유효픽셀당미터 : 0.08f;
        return 한픽셀 * 부풀리기;
    }

    const string 이름 = "실루엣";
    static Material mat;
    float cd;
    readonly HashSet<int> 처리됨 = new HashSet<int>();

    void Start() { 훑기(); }

    void Update()
    {
        // 부풀리기 값이 인스펙터에서 바뀌면 바로 반영 (재질 하나를 나눠 쓰므로 한 줄이면 된다)
        if (mat != null)
        {
            mat.SetFloat("_Expand", 밀거리());
        }

        if (다시훑기 <= 0f) return;
        cd -= Time.deltaTime;
        if (cd > 0f) return;
        cd = 다시훑기;
        훑기();
    }

    [Tooltip("한 번에 몇 개까지 테두리를 붙이나 — 켤 때 한꺼번에 만들면 렉이 걸린다")]
    public int 한번에 = 150;

    public void 훑기()
    {
        int 만든수 = 0;
        if (mat == null)
        {
            var sh = Shader.Find("Toyra/Outline");
            if (sh == null) { Debug.LogError("[실루엣] Outline.shader 를 못 찾았다"); enabled = false; return; }
            mat = new Material(sh) { name = "실루엣" };
        }

        // ★마스크에서만 몸을 딱 한 픽셀 부풀린다 → 테두리가 모델 **바깥**에 그려진다.
        //   화면에 그려지는 몸은 그대로라 모델이 깎이지 않는다.
        mat.SetFloat("_Expand", 밀거리());

        foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (만든수 >= 한번에) break;      // 나머지는 다음 훑기에 (렉 방지)
            int id = r.GetInstanceID();
            if (처리됨.Contains(id)) continue;
            처리됨.Add(id);

            if (r.gameObject.name == 이름) continue;
            if (건너뜀(r.transform)) continue;

            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            var go = new GameObject(이름) { layer = 층 };
            go.transform.SetParent(r.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            꾸미기(go.AddComponent<MeshRenderer>(), r.transform);
            만든수++;
        }

        // ★뼈대 있는 몸(캐릭터)도 테두리를 두른다 (2026-08-04 사용자 "캐릭터에는 외곽선이
        //   없음"). 전에는 `MeshRenderer` 만 훑어서 캐릭터가 통째로 빠져 있었다.
        //   같은 메시·같은 뼈를 쓰는 복사본을 만들면 **동작까지 그대로 따라온다.**
        foreach (var r in FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (만든수 >= 한번에) break;
            int id = r.GetInstanceID();
            if (처리됨.Contains(id)) continue;
            처리됨.Add(id);

            if (r.gameObject.name == 이름) continue;
            if (건너뜀(r.transform)) continue;
            if (r.sharedMesh == null) continue;

            var go = new GameObject(이름) { layer = 층 };
            go.transform.SetParent(r.transform.parent, false);   // 형제로 (뼈를 그대로 쓴다)
            go.transform.localPosition = r.transform.localPosition;
            go.transform.localRotation = r.transform.localRotation;
            go.transform.localScale = r.transform.localScale;

            var sk = go.AddComponent<SkinnedMeshRenderer>();
            sk.sharedMesh = r.sharedMesh;
            sk.bones = r.bones;
            sk.rootBone = r.rootBone;
            sk.updateWhenOffscreen = true;
            꾸미기(sk, r.transform);
            만든수++;
        }
    }

    /// 실루엣 복사본의 재질·값 설정
    /// ★같은 물체의 조각들은 **같은 값**, 다른 물체는 **다른 값**을 찍는다.
    ///   같은 값이어야 안쪽에 선이 안 생기고, 달라야 겹쳤을 때 둘이 갈린다.
    void 꾸미기(Renderer mr, Transform 원본)
    {
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

        mpb ??= new MaterialPropertyBlock();
        mpb.SetFloat("_Id", 값(단위(원본)));
        mr.SetPropertyBlock(mpb);
    }

    static MaterialPropertyBlock mpb;

    [Tooltip("여기 적힌 이름은 「담는 그릇」으로 본다 — 그 밑의 것들은 서로 다른 물체다")]
    public string[] 그릇 = { "월드", "장난감_진열", "세계", "펫표본" };

    /// 이 렌더러가 속한 **한 물체**의 뿌리를 찾는다.
    /// 조각(머리·다리)이 아무리 많아도 같은 뿌리면 같은 값을 찍어야 안쪽에 선이 안 생긴다.
    Transform 단위(Transform t)
    {
        var best = t;
        for (var p = t; p != null; p = p.parent)
        {
            if (p.parent == null) break;
            bool 그릇인가 = false;
            foreach (var s in 그릇)
                if (!string.IsNullOrEmpty(s) && p.parent.name.Contains(s)) { 그릇인가 = true; break; }
            best = p;
            if (그릇인가) break;      // 그릇 바로 아래가 「한 물체」다
        }
        return best;
    }

    /// 뿌리마다 고정된 0.1~0.95 사이 값 (0 은 배경이므로 피한다)
    static float 값(Transform t)
    {
        unchecked
        {
            uint h = (uint)t.GetInstanceID() * 2654435761u;
            h ^= h >> 15;
            return 0.1f + (h % 217) / 256f;
        }
    }

    // ★★코드에 박아 두는 제외 목록 (2026-08-04). 인스펙터 목록만 믿었더니, **씬에 이미
    //   저장된 옛 값이 코드 기본값을 이겨서** 잔디에 테두리가 계속 붙었다.
    //   환경은 테두리를 두르지 않는다는 건 취향이 아니라 규칙이므로 코드가 들고 있는다.
    static readonly string[] 늘제외 =
    { "땅", "물웅덩이", "시야_덮개", "픽셀_", "풀", "잔디", "나무", "잎", "줄기", "통나무" };

    bool 건너뜀(Transform t)
    {
        // ★★이름이 아니라 **표식**으로도 거른다 (2026-08-05 사용자 "이 나무들 외곽선
        //   들어가면 안돼고"). 위 목록에 "나무"·"잎"이 있는데도 테두리가 붙어 있었다 —
        //   실제 이름이 `tree_stylized_4(Clone)/Canopy` 라 한 글자도 안 걸린 것이다.
        //   모델을 새로 받을 때마다 이름이 달라지므로 목록으로는 못 따라간다.
        if (t.GetComponentInParent<NoOutline>(true) != null) return true;

        for (var p = t; p != null; p = p.parent)
        {
            foreach (var s in 늘제외)
                if (p.name.Contains(s)) return true;
            if (건너뛰기 != null)
                foreach (var s in 건너뛰기)
                    if (!string.IsNullOrEmpty(s) && p.name.Contains(s)) return true;
            if (p.gameObject.layer == LayerMask.NameToLayer("UI")) return true;
        }
        return false;
    }
}
