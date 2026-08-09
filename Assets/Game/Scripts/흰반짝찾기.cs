using UnityEngine;

/// 화면에 **하얗게 타오르는 자리**가 생기면 그게 무엇인지 콘솔에 적는다 (진단 전용).
///
/// ★★왜 만드나 (2026-08-09 사용자 "하얗게 반짝 거리는 곳이 있어, 버그같은데").
///   불빛·입자·발광 재질을 씬에서 전부 뒤졌는데 **하나도 없었다.** 그러면 남은 건
///   「무엇이 그려지느냐」이고, 그건 **그 순간 화면을 봐야** 안다.
///   → 화면을 읽어 흰 덩어리를 찾고, 그 자리로 광선을 쏴서 무엇이 있는지 적는다.
///
/// ★범인을 잡으면 이 파일은 지운다. 게임 기능이 아니다.
public class 흰반짝찾기 : MonoBehaviour
{
    [Tooltip("몇 초마다 볼까")] public float 주기 = 1.0f;
    [Tooltip("이 값(0~255)을 넘으면 「하얗다」")] public int 문턱 = 215;
    [Tooltip("이만큼 모여야 덩어리로 친다")] public int 최소픽셀 = 30;

    float 다음;
    Texture2D 사진;

    void Update()
    {
        if (Time.unscaledTime < 다음) return;
        다음 = Time.unscaledTime + Mathf.Max(0.2f, 주기);
        StartCoroutine(살피기());
    }

    System.Collections.IEnumerator 살피기()
    {
        yield return new WaitForEndOfFrame();

        int W = Screen.width, H = Screen.height;
        if (사진 == null || 사진.width != W || 사진.height != H)
        {
            if (사진 != null) Destroy(사진);
            사진 = new Texture2D(W, H, TextureFormat.RGB24, false);
        }
        사진.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        사진.Apply(false);

        // ★★HUD 를 빼고 본다 (2026-08-09). 처음엔 위·아래 띠를 안 뺐더니 **좌상단 흰 글씨**를
        //   200픽셀짜리 덩어리로 꾸준히 잡아 놓고, 그 광선이 우연히 닿은 땅 좌표를 범인이라고
        //   적었다. 흰 것을 찾을 땐 **글자부터 빼야** 한다.
        int 위아래 = Mathf.RoundToInt(H * 0.14f);
        int 좌우 = Mathf.RoundToInt(W * 0.03f);

        var px = 사진.GetPixels32();
        long sx = 0, sy = 0; int n = 0; int 최대 = 0;
        int x0 = W, x1 = 0, y0 = H, y1 = 0;
        for (int y = 위아래; y < H - 위아래; y++)
            for (int x = 좌우; x < W - 좌우; x++)
            {
                var c = px[y * W + x];
                // ★세 채널이 **모두** 밝아야 흰색이다 — 노란 땅이 걸리지 않게
                int v = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                if (v > 최대) 최대 = v;
                if (v < 문턱) continue;
                n++; sx += x; sy += y;
                if (x < x0) x0 = x; if (x > x1) x1 = x;
                if (y < y0) y0 = y; if (y > y1) y1 = y;
            }
        if (n < 최소픽셀) yield break;

        // ★글자는 **가로로 길고 성기다.** 덩어리는 네모지고 빽빽하다 — 그걸로 한 번 더 거른다
        int bw = x1 - x0 + 1, bh = y1 - y0 + 1;
        float 채움 = n / (float)(bw * bh);
        float 비율 = bw / (float)Mathf.Max(1, bh);
        // ★글자는 가로로 길다 — 그것만 거른다. 채움률로는 안 거른다(덩어리가 흩어져 있을 수 있다)
        if (비율 > 3.5f)
        {
            Debug.Log($"[흰반짝] 흰 픽셀 {n}개 — 가로로 길어서(글자로 보고) 넘긴다 ({bw}x{bh})");
            yield break;
        }

        int cx = (int)(sx / n), cy = (int)(sy / n);
        var cam = Camera.main; if (cam == null) yield break;
        var ray = cam.ScreenPointToRay(new Vector3(cx, cy, 0));

        string 무엇 = "(콜라이더 없음)";
        Vector3 자리 = Vector3.zero;
        if (Physics.Raycast(ray, out var hit, 600f))
        {
            무엇 = $"'{hit.collider.name}' 부모 '{(hit.collider.transform.parent ? hit.collider.transform.parent.name : "-")}'";
            자리 = hit.point;
        }
        else
        {
            var pl = new Plane(Vector3.up, Vector3.zero);
            if (pl.Raycast(ray, out float d)) 자리 = ray.GetPoint(d);
        }

        // 그 자리 둘레에 무엇이 있나 — 렌더러 이름·재질·셰이더를 그대로 적는다
        var 곁 = new System.Text.StringBuilder();
        int 셈 = 0;
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            float dd = Vector2.Distance(new Vector2(자리.x, 자리.z),
                                        new Vector2(r.bounds.center.x, r.bounds.center.z));
            if (dd > 4f) continue;
            if (++셈 > 8) break;
            var mt = r.sharedMaterial;
            곁.Append($"\n    {dd:0.0}m '{r.name}' mat '{(mt ? mt.name : "-")}' / {(mt ? mt.shader.name : "-")} y={r.bounds.center.y:0.00} 크기={r.bounds.size}");
        }

        Debug.Log($"[흰반짝] 흰 픽셀 {n}개 · 덩어리 {bw}x{bh} 채움 {채움:0.00} · 화면({cx},{cy}) → 월드 {자리}\n  맞은 것: {무엇}  곁에:{곁}");
    }
}
