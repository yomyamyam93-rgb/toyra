using System;
using System.Collections.Generic;
using UnityEngine;

/// 땅 사진 — **원본 그대로 쓰고, 자리마다 다른 게 걸리게 한다** (2026-08-05 사용자 확정).
///
/// ★★사진마다 **자기 노이즈 밭을 하나씩** 갖는다. 그 밭이 높은 자리에서 그 사진이 진해진다.
///   칸을 나눠 배정하는 게 아니라 **함수가 정하므로**, 경계선이 없고 장수 제한도 없다
///   (사용자 "5칸으로 제한하면 안 되고 함수 사용해서 어디는 많이쓰고 어디는 적게쓰고 랜덤하게").
///
///   사진 i 의 가중치 =  노이즈(월드좌표 × 주파수_i + 오프셋_i)  +  편향_i
///
///     · **주파수** — 얼룩 하나의 크기 (25~90m). 어떤 사진은 넓게 깔리고 어떤 건 잘게 흩어진다
///     · **오프셋** — 밭끼리 안 겹치게 어긋뜨린다
///     · **편향**   — ★이게 「어디는 많이, 어디는 적게」다. 높으면 지도 전체에서 흔하고,
///                     낮으면 드물게 얼룩으로만 나타난다. 씨앗이 바뀌면 주인이 바뀐다
///
/// ★셰이더가 픽셀마다 **가장 진한 둘만** 뽑아 섞으므로, 사진이 13장이든 30장이든
///   픽셀당 비용은 그대로다. 늘리려면 폴더에 넣기만 하면 된다 — 코드에 장수가 없다.
///
/// ★크기가 제각각(360~1250px)이라 배열로 묶으려면 한 크기로 맞춰야 한다. 이건 **픽셀 변환이
///   아니라 리샘플**이다 — 점 필터로 각지게 깎는 것과 다르다 (`GroundPhotoImport` 참고).
public static class GroundPhotos
{
    /// 셰이더 상수 배열 크기 — 여기를 늘리면 `Ground.shader` 의 배열도 같이 늘려야 한다
    public const int 최대 = 16;

    /// 무리 번호 — 그 자리가 무엇이냐에 따라 후보가 갈린다
    public const int 잔디 = 0, 흙 = 1, 돌 = 2;

    public static Texture2DArray 배열 { get; private set; }
    public static int 개수 { get; private set; }

    /// (얼룩주파수 1/m, 오프셋x, 오프셋z, 편향)
    public static Vector4[] 파라미터 { get; private set; }
    /// (무리번호, uv배율, 0, 0)
    public static Vector4[] 무리 { get; private set; }

    /// ★사진마다 미리 재둔 **평균색 — 선형(linear)이다.**
    ///   풀이 발밑 땅 톤을 따라가는 데 쓴다 (사용자 "잔디 색이 땅 텍스처 색상을 못 따라가네").
    /// ★★반드시 **선형으로 풀어서 평균**을 낸다. sRGB 값을 그대로 평균 내면 실제로 보이는
    ///   평균보다 밝게 나와 풀만 땅에서 뜬다 — 감마는 평균과 자리를 안 바꾼다.
    public static Vector4[] 평균색 { get; private set; }

    static int 만든씨앗 = int.MinValue;
    static int 만든크기;
    static float 만든작게, 만든크게;

    static readonly (string 길, int 무리)[] 폴더 =
    {
        ("ground/사진/잔디", 잔디),
        ("ground/사진/흙",   흙),
        ("ground/사진/돌",   돌),
    };

    /// 사진을 모아 배열 한 장으로 묶고, 사진마다 노이즈 밭을 뽑는다.
    /// 같은 씨앗·같은 값이면 다시 안 만든다 (판을 다시 지어도 재사용).
    public static bool 준비(int seed, int 크기 = 1024, float 얼룩작게 = 75f, float 얼룩크게 = 450f)
    {
        if (배열 != null && 만든씨앗 == seed && 만든크기 == 크기
            && Mathf.Approximately(만든작게, 얼룩작게) && Mathf.Approximately(만든크게, 얼룩크게))
            return 개수 > 0;

        var 원본 = new List<Texture2D>();
        var 무리번호 = new List<int>();

        foreach (var (길, m) in 폴더)
        {
            var 것들 = Resources.LoadAll<Texture2D>(길);
            // ★이름순으로 세운다 — `LoadAll` 의 순서는 보장이 없어서, 안 그러면 씨앗이 같아도
            //   판마다 사진이 뒤바뀐다 (같은 씨앗 = 같은 맵 이라는 약속이 깨진다)
            Array.Sort(것들, (a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (var t in 것들)
            {
                if (원본.Count >= 최대) break;
                원본.Add(t); 무리번호.Add(m);
            }
        }

        if (원본.Count == 0)
        {
            Debug.LogWarning("[땅사진] Resources/ground/사진/{잔디,흙,돌} 에 그림이 없다 — 옛 방식으로 간다");
            개수 = 0; 배열 = null; 만든씨앗 = seed; 만든크기 = 크기;
            return false;
        }

        개수 = 원본.Count;
        배열 = 묶기(원본, 크기);
        밭뽑기(seed, 무리번호, 얼룩작게, 얼룩크게);

        만든씨앗 = seed; 만든크기 = 크기; 만든작게 = 얼룩작게; 만든크게 = 얼룩크게;
        Debug.Log($"[땅사진] {개수}장 · {크기}px · 잔디 {수(무리번호, 잔디)} 흙 {수(무리번호, 흙)} 돌 {수(무리번호, 돌)}" +
                  $" · 얼룩 {얼룩작게:0}~{얼룩크게:0}m");
        return true;
    }

    static int 수(List<int> 무리번호, int m)
    {
        int n = 0;
        foreach (var v in 무리번호) if (v == m) n++;
        return n;
    }

    /// 크기가 제각각인 사진들을 한 크기로 다시 그려 배열에 넣는다.
    /// ★픽셀을 읽는 대신 **GPU 로 옮겨 그린다**(Blit) — 원본이 압축돼 있어도, 읽기 가능이
    ///   꺼져 있어도 된다. 사진을 「읽을 수 있게」 만들면 메모리를 두 벌 쓰게 된다.
    static Texture2DArray 묶기(List<Texture2D> 원본, int 크기)
    {
        var arr = new Texture2DArray(크기, 크기, 원본.Count, TextureFormat.RGBA32, true, false)
        {
            name = "땅사진배열",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 8            // 아이소메트릭이라 땅을 늘 비스듬히 본다 — 이게 있어야 안 흐려진다
        };

        평균색 = new Vector4[최대];
        var 임시 = new Texture2D(크기, 크기, TextureFormat.RGBA32, false);
        var 이전 = RenderTexture.active;

        for (int i = 0; i < 원본.Count; i++)
        {
            var rt = RenderTexture.GetTemporary(크기, 크기, 0, RenderTextureFormat.ARGB32,
                                                RenderTextureReadWrite.sRGB);
            Graphics.Blit(원본[i], rt);
            RenderTexture.active = rt;
            임시.ReadPixels(new Rect(0, 0, 크기, 크기), 0, 0);
            임시.Apply(false);
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            var px = 임시.GetPixels();
            arr.SetPixels(px, i, 0);

            // 평균색 — 선형으로 풀어서 낸다 (위 주석 참고). 16칸 건너 표본이면 충분하다
            double r = 0, g = 0, b = 0; int c = 0;
            for (int k = 0; k < px.Length; k += 16) { var L = px[k].linear; r += L.r; g += L.g; b += L.b; c++; }
            평균색[i] = new Vector4((float)(r / c), (float)(g / c), (float)(b / c), 1f);
        }

        RenderTexture.active = 이전;
        if (Application.isPlaying) UnityEngine.Object.Destroy(임시);
        else UnityEngine.Object.DestroyImmediate(임시);

        arr.Apply(true);              // 밉맵까지 만든다 — 없으면 멀리서 자글자글 끓는다
        return arr;
    }

    /// 사진마다 노이즈 밭 하나. **씨앗만 바꾸면 세상의 주인 사진이 바뀐다.**
    ///
    /// ★★얼룩은 **크게** 잡는다 (2026-08-05 사용자 "너무 자주 바뀌지 않게, 3배 5배까지
    ///   넓게넓게"). 처음 값 25~90m 는 한 화면(가로 약 120m) 안에서 서너 번 바뀌어
    ///   **땅이 얼룩덜룩한 무늬**로 읽혔다. 지금 75~450m 면 한 화면에 한두 갈래만 걸리고,
    ///   걸어가는 동안 **천천히** 바뀐다 — 무늬가 아니라 「지역이 달라진다」로 읽힌다.
    /// ★맵 한 변이 1440m 이므로 450m 짜리는 지도를 서너 덩어리로 나눈다. 그보다 키우면
    ///   그 사진이 사실상 지도 절반을 먹는다.
    static void 밭뽑기(int seed, List<int> 무리번호, float 얼룩작게, float 얼룩크게)
    {
        파라미터 = new Vector4[최대];
        무리 = new Vector4[최대];

        var r = new System.Random(seed);
        float 다음(float a, float b) => a + (float)r.NextDouble() * (b - a);

        for (int i = 0; i < 개수; i++)
        {
            // 얼룩 하나가 몇 미터인가 (기본 75m ~ 450m)
            float 얼룩m = 다음(얼룩작게, 얼룩크게);
            파라미터[i] = new Vector4(
                1f / 얼룩m,
                다음(0f, 1000f),
                다음(0f, 1000f),
                // ★편향 — 「어디는 많이, 어디는 적게」. 노이즈가 0~1 이므로 ±0.35 면
                //   높은 놈은 거의 항상 이기고 낮은 놈은 밭이 솟은 자리에서만 얼굴을 내민다
                다음(-0.35f, 0.35f));

            // uv 배율을 사진마다 흔든다 — 같은 사진이 이어져도 되풀이 무늬가 덜 읽힌다
            무리[i] = new Vector4(무리번호[i], 다음(0.8f, 1.3f), 0f, 0f);
        }
    }
}
