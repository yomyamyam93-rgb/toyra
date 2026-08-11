using System.Collections.Generic;
using UnityEngine;

/// 직소 시험용 **상자 조각** — 모델이 하나도 없어도 조립기가 도는지 눈으로 본다.
///
/// ★프로젝트 규칙 그대로다: **상자 먼저, 모델 나중**. 진짜 모델이 생기면
///   `WorldGen` 의 조각 칸에 프리팹을 꽂기만 하면 이 파일은 안 쓰인다 (지우지 않고 둔다 —
///   은퇴는 삭제가 아니라 스위치).
///
/// ★★두 벌이 있다 (2026-08-06 사용자 — "복도와 방이라는게 있나 우리게임에..?").
///   맞는 지적이었다. **방·복도는 실내 던전의 말**인데 우리 폐허는 **야외**다 —
///   지붕도 문도 없고 무너진 담과 기단만 남은 자리다.
///
///     ①`폐허주머니` — **야외**. 터·돌담길·꺾인담·무너진끝·제단.
///                      담이 **낮아서 넘어다보인다** (아이소 시점에서 안이 보여야 한다)
///     ②`굴주머니`   — **실내**. 굴방·통로. 여기서는 방과 통로가 진짜로 맞는 말이다.
///                      전에 만든 실내 세트를 버리지 않고 이쪽으로 옮겼다.
public static class 직소상자
{
    static Transform 창고;

    public static readonly Color C담 = new Color(0.50f, 0.49f, 0.44f);
    public static readonly Color C기단 = new Color(0.42f, 0.41f, 0.37f);
    public static readonly Color C기둥 = new Color(0.56f, 0.54f, 0.48f);
    public static readonly Color C잡동 = new Color(0.58f, 0.53f, 0.40f);
    public static readonly Color C이끼 = new Color(0.30f, 0.38f, 0.24f);
    // ★★바닥과 벽을 **색으로 갈라 놓는다** (2026-08-11) — 셋이 다 0.26~0.33 이라 들어가서
    //   보면 바닥인지 벽인지 구분이 안 됐다. 벽을 밝게 올려 길이 읽히게 한다
    public static readonly Color C굴벽 = new Color(0.46f, 0.44f, 0.42f);
    public static readonly Color C굴바닥 = new Color(0.21f, 0.20f, 0.20f);
    public static readonly Color C굴덮개 = new Color(0.44f, 0.41f, 0.39f);

    // ───────────────────────────────── ① 야외 폐허

    /// ★야외라 담이 낮다(1.1~1.6m). 사람이 넘어다보고, 카메라가 안을 들여다본다.
    ///   높은 담을 두르면 아이소 시점에서 **안이 통째로 안 보인다** — 그게 실내 세트의 죄였다.
    public static List<직소.주머니> 폐허주머니()
    {
        창고정리();
        return new List<직소.주머니>
        {
            // ★무게 — **큰 것이 드물어야 특별해진다** (MC 템플릿 풀의 weight 자리)
            new 직소.주머니 { 이름 = "폐허터",
                               조각들 = new[] { 터(2), 터(3), 큰터(), 이끼터() },
                               무게   = new[] { 5f,    4f,    1f,     2f },
                               막음   = new[] { 무너진끝() } },
            new 직소.주머니 { 이름 = "돌담길",
                               조각들 = new[] { 담길(8f), 담길(12f), 꺾인담(), 계단길() },
                               무게   = new[] { 5f,       3f,        4f,       2f },
                               막음   = new[] { 무너진끝() } },
            new 직소.주머니 { 이름 = "잡동사니",
                               조각들 = new[] { 잡동("깨진항아리", 0.7f, 0.8f, C잡동),
                                                잡동("돌더미", 1.3f, 0.5f, C담),
                                                잡동("쓰러진기둥", 2.6f, 0.7f, C기둥),
                                                잡동("뼈무더기", 1.0f, 0.35f, C잡동),
                                                잡동("이끼바위", 1.5f, 0.9f, C이끼) } },
        };
    }

    /// 기단만 남은 네모난 터 — 가장자리에 담 밑동이 토막토막 남아 있다
    static GameObject 터(int 길수)
    {
        var g = 새조각("폐허_터_" + 길수, new Vector3(0f, 0.8f, 0f), new Vector3(12f, 1.6f, 12f));
        기단깔기(g, 12f, 12f);
        for (int i = 0; i < 4; i++)
        {
            var 밖 = 네방향[i];
            바깥담(g, 밖 * 6f, 밖, 12f, 1.3f, i < 길수);
            if (i < 길수) 이음달기(g, "돌담길", 밖 * 6f, 밖);
        }
        슬롯달기(g, "잡동사니", new Vector3(-3.2f, 0f, 2.6f));
        슬롯달기(g, "잡동사니", new Vector3(3.0f, 0f, -2.2f));
        return g;
    }

    /// 큰 터 — 부러진 기둥이 서 있다. 폐허의 「광장」
    static GameObject 큰터()
    {
        var g = 새조각("폐허_큰터", new Vector3(0f, 1.6f, 0f), new Vector3(20f, 3.2f, 20f));
        기단깔기(g, 20f, 20f);
        for (int i = 0; i < 4; i++)
        {
            바깥담(g, 네방향[i] * 10f, 네방향[i], 20f, 1.1f, true);
            이음달기(g, "돌담길", 네방향[i] * 10f, 네방향[i]);
        }
        // ★기둥은 **높이를 제각각** 부러뜨린다 — 다 같은 높이면 울타리로 읽힌다
        float[] 키 = { 3.4f, 1.8f, 2.6f, 1.2f };
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * 0.5f + 0.4f;
            상자(g, new Vector3(Mathf.Cos(a) * 5.5f, 키[i] * 0.5f, Mathf.Sin(a) * 5.5f),
                 new Vector3(1.1f, 키[i], 1.1f), C기둥, "부러진기둥");
        }
        슬롯달기(g, "잡동사니", Vector3.zero);
        슬롯달기(g, "잡동사니", new Vector3(-6f, 0f, 4f));
        슬롯달기(g, "잡동사니", new Vector3(5f, 0f, -5f));
        return g;
    }

    /// 이끼 낀 터 — 기단이 반쯤 흙에 묻혔다. **드물게** 나와야 특별해진다
    static GameObject 이끼터()
    {
        var g = 새조각("폐허_이끼터", new Vector3(0f, 0.7f, 0f), new Vector3(12f, 1.4f, 12f));
        기단깔기(g, 12f, 12f);
        상자(g, new Vector3(0f, 0.12f, 0f), new Vector3(9f, 0.24f, 9f), C이끼, "이끼");
        for (int i = 0; i < 4; i++)
        {
            바깥담(g, 네방향[i] * 6f, 네방향[i], 12f, 0.8f, i < 2);
            if (i < 2) 이음달기(g, "돌담길", 네방향[i] * 6f, 네방향[i]);
        }
        슬롯달기(g, "잡동사니", new Vector3(2.4f, 0f, 2.4f));
        return g;
    }

    /// 돌담길 — 양쪽에 낮은 담이 남은 길
    static GameObject 담길(float 길이)
    {
        var g = 새조각("폐허_담길_" + 길이, new Vector3(0f, 0.6f, 0f), new Vector3(4f, 1.2f, 길이));
        기단깔기(g, 4f, 길이);
        상자(g, new Vector3(-2.1f, 0.6f, 0f), new Vector3(0.7f, 1.2f, 길이), C담, "담");
        상자(g, new Vector3(2.1f, 0.6f, 0f), new Vector3(0.7f, 1.2f, 길이), C담, "담");
        이음달기(g, "폐허터", new Vector3(0f, 0f, 길이 * 0.5f), 네방향[0]);
        이음달기(g, "폐허터", new Vector3(0f, 0f, -길이 * 0.5f), 네방향[2]);
        슬롯달기(g, "잡동사니", new Vector3(0f, 0f, 길이 * 0.2f));
        return g;
    }

    /// 꺾인 담 — 이게 있어야 폐허가 한 줄로 안 뻗는다
    static GameObject 꺾인담()
    {
        var g = 새조각("폐허_꺾임", new Vector3(2f, 0.6f, 2f), new Vector3(12f, 1.2f, 12f));
        기단깔기(g, 4f, 12f, new Vector3(0f, 0f, 2f));
        기단깔기(g, 12f, 4f, new Vector3(2f, 0f, 0f));
        상자(g, new Vector3(-2.1f, 0.6f, 2f), new Vector3(0.7f, 1.2f, 12f), C담, "담");
        상자(g, new Vector3(2f, 0.6f, -2.1f), new Vector3(12f, 1.2f, 0.7f), C담, "담");
        이음달기(g, "폐허터", new Vector3(0f, 0f, 8f), 네방향[0]);
        이음달기(g, "폐허터", new Vector3(8f, 0f, 0f), 네방향[1]);
        return g;
    }

    /// 계단길 — 기단 높이가 한 단 오른다. 폐허가 평평하기만 하면 심심하다
    static GameObject 계단길()
    {
        var g = 새조각("폐허_계단", new Vector3(0f, 0.8f, 0f), new Vector3(4f, 1.6f, 8f));
        for (int i = 0; i < 4; i++)
            상자(g, new Vector3(0f, 0.12f + i * 0.22f, -3f + i * 2f), new Vector3(4f, 0.24f, 2f), C기단, "계단");
        이음달기(g, "폐허터", new Vector3(0f, 0f, 4f), 네방향[0]);
        이음달기(g, "폐허터", new Vector3(0f, 0f, -4f), 네방향[2]);
        return g;
    }

    /// 무너진 끝 — 담이 무너져 흙에 묻힌 자리. 이어 붙일 데가 없을 때 여기서 끝난다
    static GameObject 무너진끝()
    {
        var g = 새조각("폐허_무너짐", new Vector3(0f, 0.5f, 1.2f), new Vector3(4f, 1f, 4f));
        상자(g, new Vector3(0f, 0.35f, 1.0f), new Vector3(3.4f, 0.7f, 1.6f), C담, "무너진담");
        상자(g, new Vector3(-0.9f, 0.22f, 2.0f), new Vector3(1.2f, 0.45f, 1.2f), C담, "돌더미");
        상자(g, new Vector3(1.0f, 0.18f, 2.2f), new Vector3(0.9f, 0.36f, 0.9f), C담, "돌더미");
        이음달기(g, "폐허터", Vector3.zero, 네방향[2]);
        return g;
    }

    // ───────────────────────────────── ② 굴 (실내 — 방·통로가 진짜로 맞는 곳)

    public static List<직소.주머니> 굴주머니()
    {
        창고정리();
        return new List<직소.주머니>
        {
            // ★★입구가 첫 조각이다 (2026-08-11 사용자 "밖에서 보면 막힌 지형인데, 입구가
            //   있고, 들어가면 좀보이드처럼 투시되서 보이는"). 남쪽 담의 틈에는 **이음이
            //   없어서 막음 조각이 영영 안 온다** — 그 3.2m 가 밖으로 나가는 유일한 틈새다
            new 직소.주머니 { 이름 = "굴입구",
                               조각들 = new[] { 굴입구방() } },
            new 직소.주머니 { 이름 = "굴방",
                               조각들 = new[] { 굴방(2), 굴방(3), 큰굴방() },
                               무게   = new[] { 5f,      4f,      1f },
                               막음   = new[] { 굴막힘() } },
            new 직소.주머니 { 이름 = "굴통로",
                               조각들 = new[] { 굴통로(8f), 굴통로(12f), 꺾인통로() },
                               무게   = new[] { 5f,         3f,          4f },
                               막음   = new[] { 굴막힘() } },
            new 직소.주머니 { 이름 = "잡동사니",
                               조각들 = new[] { 잡동("종유석", 0.6f, 1.6f, C굴벽),
                                                잡동("돌더미", 1.3f, 0.6f, C굴벽),
                                                잡동("뼈무더기", 1.0f, 0.35f, C잡동) } },
        };
    }

    static GameObject 굴방(int 길수)
    {
        var g = 새조각("굴_방_" + 길수, new Vector3(0f, 2f, 0f), new Vector3(12f, 4f, 12f));
        기단깔기(g, 12f, 12f, default, C굴바닥);
        for (int i = 0; i < 4; i++)
        {
            바깥담(g, 네방향[i] * 6f, 네방향[i], 12f, 3.6f, i < 길수, C굴벽);
            if (i < 길수) 이음달기(g, "굴통로", 네방향[i] * 6f, 네방향[i]);
        }
        지붕덮기(g, 12f, 12f);
        슬롯달기(g, "잡동사니", new Vector3(-3.2f, 0f, 2.6f));
        슬롯달기(g, "잡동사니", new Vector3(3.0f, 0f, -2.2f));
        return g;
    }

    /// ★입구방 — 남쪽 담의 틈에 **이음을 안 단다.** 이음이 없으니 직소가 막음 조각을 못
    ///   붙이고, 그 틈이 밖으로 나가는 유일한 「틈새」로 영영 남는다 (2026-08-11)
    static GameObject 굴입구방()
    {
        var g = 새조각("굴_입구", new Vector3(0f, 2f, 0f), new Vector3(12f, 4f, 12f));
        기단깔기(g, 12f, 12f, default, C굴바닥);
        바깥담(g, 네방향[2] * 6f, 네방향[2], 12f, 3.6f, true, C굴벽);   // 열려 있되 이음 없음 = 입구
        for (int i = 0; i < 4; i++)
        {
            if (i == 2) continue;
            바깥담(g, 네방향[i] * 6f, 네방향[i], 12f, 3.6f, i < 2, C굴벽);
            if (i < 2) 이음달기(g, "굴통로", 네방향[i] * 6f, 네방향[i]);
        }
        지붕덮기(g, 12f, 12f);
        return g;
    }

    static GameObject 큰굴방()
    {
        var g = 새조각("굴_큰방", new Vector3(0f, 2.5f, 0f), new Vector3(20f, 5f, 20f));
        기단깔기(g, 20f, 20f, default, C굴바닥);
        for (int i = 0; i < 4; i++)
        {
            바깥담(g, 네방향[i] * 10f, 네방향[i], 20f, 4.4f, true, C굴벽);
            이음달기(g, "굴통로", 네방향[i] * 10f, 네방향[i]);
        }
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * 0.5f + 0.4f;
            상자(g, new Vector3(Mathf.Cos(a) * 5f, 2.4f, Mathf.Sin(a) * 5f), new Vector3(1.2f, 4.8f, 1.2f), C굴벽, "돌기둥");
        }
        지붕덮기(g, 20f, 20f, default, 4.4f);
        슬롯달기(g, "잡동사니", Vector3.zero);
        슬롯달기(g, "잡동사니", new Vector3(-6f, 0f, 4f));
        return g;
    }

    static GameObject 굴통로(float 길이)
    {
        var g = 새조각("굴_통로_" + 길이, new Vector3(0f, 1.6f, 0f), new Vector3(4f, 3.2f, 길이));
        기단깔기(g, 4f, 길이, default, C굴바닥);
        상자(g, new Vector3(-2.2f, 1.6f, 0f), new Vector3(0.6f, 3.2f, 길이), C굴벽, "벽");
        상자(g, new Vector3(2.2f, 1.6f, 0f), new Vector3(0.6f, 3.2f, 길이), C굴벽, "벽");
        이음달기(g, "굴방", new Vector3(0f, 0f, 길이 * 0.5f), 네방향[0]);
        이음달기(g, "굴방", new Vector3(0f, 0f, -길이 * 0.5f), 네방향[2]);
        지붕덮기(g, 4f, 길이, default, 3.2f);
        return g;
    }

    static GameObject 꺾인통로()
    {
        var g = 새조각("굴_꺾임", new Vector3(2f, 1.6f, 2f), new Vector3(12f, 3.2f, 12f));
        기단깔기(g, 4f, 12f, new Vector3(0f, 0f, 2f), C굴바닥);
        기단깔기(g, 12f, 4f, new Vector3(2f, 0f, 0f), C굴바닥);
        상자(g, new Vector3(-2.2f, 1.6f, 2f), new Vector3(0.6f, 3.2f, 12f), C굴벽, "벽");
        상자(g, new Vector3(2f, 1.6f, -2.2f), new Vector3(12f, 3.2f, 0.6f), C굴벽, "벽");
        이음달기(g, "굴방", new Vector3(0f, 0f, 8f), 네방향[0]);
        이음달기(g, "굴방", new Vector3(8f, 0f, 0f), 네방향[1]);
        지붕덮기(g, 4f, 12f, new Vector3(0f, 0f, 2f), 3.2f);
        지붕덮기(g, 12f, 4f, new Vector3(2f, 0f, 0f), 3.2f);
        return g;
    }

    static GameObject 굴막힘()
    {
        var g = 새조각("굴_막힘", new Vector3(0f, 1.6f, 0.6f), new Vector3(4f, 3.2f, 2f));
        상자(g, new Vector3(0f, 1.6f, 0.6f), new Vector3(4f, 3.2f, 1.0f), C굴벽, "막힌벽");
        이음달기(g, "굴방", Vector3.zero, 네방향[2]);
        return g;
    }

    // ───────────────────────────────── 부품

    static readonly Vector3[] 네방향 =
    { new Vector3(0,0,1), new Vector3(1,0,0), new Vector3(0,0,-1), new Vector3(-1,0,0) };

    static GameObject 잡동(string 이름, float 폭, float 높이, Color 색)
    {
        var g = 새조각("잡_" + 이름, new Vector3(0f, 높이 * 0.5f, 0f), new Vector3(폭, 높이, 폭));
        상자(g, new Vector3(0f, 높이 * 0.5f, 0f), new Vector3(폭, 높이, 폭 * 0.8f), 색, 이름);
        return g;
    }

    static GameObject 새조각(string 이름, Vector3 상자중심, Vector3 상자크기)
    {
        var g = new GameObject(이름);
        g.transform.SetParent(창고, false);
        var d = g.AddComponent<직소조각>();
        d.상자중심 = 상자중심; d.상자크기 = 상자크기;
        g.SetActive(false);                       // 원본은 화면에 안 나온다
        return g;
    }

    static void 기단깔기(GameObject g, float 가로, float 세로, Vector3 자리 = default, Color? 색 = null)
        => 상자(g, 자리 + new Vector3(0f, 0.08f, 0f), new Vector3(가로, 0.16f, 세로), 색 ?? C기단, "바닥");

    /// ★동굴 덮개 — 밖에서는 막힌 바위 더미로 보이고, 들어가면 `굴가림` 이 걷는다 (2026-08-11
    ///   사용자 "밖에서 보면 막힌 지형인데, 입구가 있고, 들어가면 좀보이드처럼 투시되서 보이는").
    ///   이름이 「덮개」로 시작해야 한다 — `직소` 가 장애물을 안 깔고, `굴가림` 이 이 이름만 걷는다.
    ///   판때기 하나면 뚜껑으로 읽혀서, 위에 바위 혹을 얹어 「막힌 지형」으로 만든다
    static void 지붕덮기(GameObject g, float 가로, float 세로, Vector3 자리 = default, float 높이 = 3.6f)
    {
        상자(g, 자리 + new Vector3(0f, 높이 + 0.25f, 0f), new Vector3(가로 + 1.4f, 0.5f, 세로 + 1.4f), C굴덮개, "덮개");
        상자(g, 자리 + new Vector3(가로 * 0.18f, 높이 + 1.1f, -세로 * 0.12f),
             new Vector3(가로 * 0.5f, 1.8f, 세로 * 0.45f), C굴덮개, "덮개혹");
        상자(g, 자리 + new Vector3(-가로 * 0.2f, 높이 + 0.8f, 세로 * 0.15f),
             new Vector3(가로 * 0.36f, 1.2f, 세로 * 0.3f), C굴덮개, "덮개혹2");
    }

    /// 한 면의 담. `열림` 이면 가운데를 비워 두 토막으로 세운다.
    /// ★야외는 `높이` 를 낮게 준다 — 담이 높으면 아이소 시점에서 안이 안 보인다
    static void 바깥담(GameObject g, Vector3 자리, Vector3 밖, float 길이, float 높이, bool 열림, Color? 색 = null)
    {
        var c = 색 ?? C담;
        var 옆 = new Vector3(-밖.z, 0f, 밖.x);
        float 두께 = 0.7f;
        if (!열림)
        {
            상자(g, 자리 + Vector3.up * (높이 * 0.5f), 면크기(밖, 옆, 두께, 높이, 길이), c, "담");
            return;
        }
        float 토막 = (길이 - 3.2f) * 0.5f;              // 지나다닐 폭 3.2m
        var t = 면크기(밖, 옆, 두께, 높이, 토막);
        상자(g, 자리 + Vector3.up * (높이 * 0.5f) + 옆 * (토막 * 0.5f + 1.6f), t, c, "담");
        상자(g, 자리 + Vector3.up * (높이 * 0.5f) - 옆 * (토막 * 0.5f + 1.6f), t, c, "담");
    }

    static Vector3 면크기(Vector3 밖, Vector3 옆, float 두께, float 높이, float 길이)
        => new Vector3(Mathf.Abs(밖.x) * 두께 + Mathf.Abs(옆.x) * 길이,
                       높이,
                       Mathf.Abs(밖.z) * 두께 + Mathf.Abs(옆.z) * 길이);

    static void 상자(GameObject 부모, Vector3 자리, Vector3 크기, Color 색, string 이름)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.name = 이름;
        b.transform.SetParent(부모.transform, false);
        b.transform.localPosition = 자리;
        b.transform.localScale = 크기;
        Grey.Strip(b);
        // ★격자 재질 (2026-08-11 사용자 "바닥이고 벽이고 모두 격자형식으로") —
        //   땅의 칸 규칙이 동굴의 바닥·벽·덮개에도 그대로 얹힌다
        b.GetComponent<MeshRenderer>().sharedMaterial = Grey.격자Mat(색);
    }

    /// ★이음 표식은 **+Z 가 바깥**을 봐야 한다 — 조립기가 그걸로 방향을 맞춘다
    static void 이음달기(GameObject 부모, string 주머니, Vector3 자리, Vector3 밖)
    {
        var t = new GameObject("이음_" + 주머니).transform;
        t.SetParent(부모.transform, false);
        t.localPosition = 자리;
        t.localRotation = Quaternion.LookRotation(밖, Vector3.up);
    }

    static void 슬롯달기(GameObject 부모, string 주머니, Vector3 자리)
    {
        var t = new GameObject("슬롯_" + 주머니).transform;
        t.SetParent(부모.transform, false);
        t.localPosition = 자리;
    }

    static void 창고정리()
    {
        if (창고 != null) return;
        var g = new GameObject("직소_상자창고");
        g.SetActive(false);                       // 통째로 꺼 둔다
        창고 = g.transform;
        // 편집 중에는 `DontDestroyOnLoad` 가 경고를 낸다 — 플레이 중일 때만
        if (Application.isPlaying) Object.DontDestroyOnLoad(g);
    }
}
