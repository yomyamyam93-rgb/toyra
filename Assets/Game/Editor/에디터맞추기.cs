using System.IO;
using UnityEditor;
using UnityEngine;

/// ★★★**다른 컴퓨터에서 열어도 바로 되게 한다** (2026-08-12 사용자 "에디터설정같은것들도
/// 싹다 그냥 다른 컴퓨터에서 열면 바로 할 수 있게좀해줘 적용안돼는거없이").
///
/// ☆9-1 의 연장이다. 씬·인스펙터 값은 커밋하면 따라가지만, **유니티 Preferences 는
///   깃에 안 들어간다** — 그건 프로젝트가 아니라 **그 컴퓨터**에 붙어 있다.
///   그래서 다른 컴퓨터에서 열면 「코드는 같은데 화면이 다른」 상태가 된다.
///
/// → 프로젝트가 **스스로 맞춘다.** 열 때마다 한 번 확인하고, 다르면 고치고 남긴다.
[InitializeOnLoad]
public static class 에디터맞추기
{
    // 한 세션에 한 번만 말한다 (컴파일마다 도메인이 다시 뜨므로 그때마다 이 함수가 돈다)
    const string 말했나키 = "토이라_에디터맞추기_말했나";

    static 에디터맞추기()
    {
        bool 고친것 = false;

        // ★★**셰이더 비동기 컴파일** (9-4 — 「어디에도 안 잡히는 렉」의 정체).
        //   끄면 처음 보는 재질 조합이 화면에 들 때마다 그 자리에서 굽느라 수백 ms 멈춘다
        //   (실측 397ms · CPU 마커엔 하나도 안 잡힌다). 이 프로젝트는 **미리 굽기**로
        //   해결했으므로(`Wildlife.셰이더예열`) 여기는 켜져 있어야 맞다.
        //   ☆`Wildlife`·`맵보기` 가 굽는 동안 잠깐 끄고 `finally` 로 되돌린다 —
        //     되돌리기가 실패해 꺼진 채 남은 컴퓨터도 이 줄이 되살린다.
        if (!ShaderUtil.allowAsyncCompilation)
        {
            ShaderUtil.allowAsyncCompilation = true;
            고친것 = true;
        }

        if (SessionState.GetBool(말했나키, false)) return;
        SessionState.SetBool(말했나키, true);

        // ★유니티 판이 다르면 말해 준다 — 판이 다르면 씬·프리팹이 조용히 바뀔 수 있다
        string 적힌판 = null;
        var 판파일 = Path.Combine(Directory.GetCurrentDirectory(), "ProjectSettings/ProjectVersion.txt");
        if (File.Exists(판파일))
            foreach (var 줄 in File.ReadAllLines(판파일))
                if (줄.StartsWith("m_EditorVersion:")) { 적힌판 = 줄.Substring(16).Trim(); break; }

        if (!string.IsNullOrEmpty(적힌판) && 적힌판 != Application.unityVersion)
            Debug.LogWarning($"[에디터맞추기] ★유니티 판이 다르다 — 프로젝트 {적힌판} · 이 컴퓨터 {Application.unityVersion}. " +
                             "판이 다르면 씬·프리팹이 조용히 바뀔 수 있다 (9-1).");

        Debug.Log($"[에디터맞추기] 확인 — 비동기 셰이더 컴파일 {(ShaderUtil.allowAsyncCompilation ? "켬" : "끔")}" +
                  $"{(고친것 ? " (내가 켰다)" : "")} · 유니티 {Application.unityVersion}");
    }
}
