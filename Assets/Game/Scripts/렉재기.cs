using System.Text;
using Unity.Profiling;
using UnityEngine;

/// ★렉을 **짐작하지 않고 재는** 자리 (2026-08-06).
///
/// 플레이하면 저절로 붙어서, 켠 지 `기다림` 초 뒤부터 `잴시간` 초 동안 프레임을 모아
/// 콘솔에 한 줄로 찍는다. **F12 로 언제든 다시 잰다.**
///
/// ★왜 파일로 두는가 — 에디터에서 코드를 꽂아 재면 **컴파일이 돌 때마다 탐침이 날아간다.**
///   실제로 그것 때문에 측정을 네 번 놓쳤다. 파일이면 안 죽는다.
///
/// ★읽는 법: 「프레임 평균」과 각 단계의 합이 안 맞으면 나머지는 **GPU 대기**다.
///   · `BehaviourUpdate`·`LateBehaviourUpdate` 가 크면 → 스크립트(우리 코드)가 범인
///   · `Camera.Render` 가 크면 → 그리는 양(물체 수·드로콜)이 범인
///   · `Gfx.WaitFor…` 가 크면 → GPU 가 못 따라온다 (해상도·셰이더·오버드로)
public class 렉재기 : MonoBehaviour
{
    [Tooltip("켜고 이만큼 지난 뒤부터 잰다 (초) — 시작 생성 비용을 안 섞으려고")]
    public float 기다림 = 4f;
    [Tooltip("이만큼 모아서 한 번 찍는다 (초)")]
    public float 잴시간 = 8f;
    [Tooltip("끄면 플레이할 때 저절로 안 잰다 (F12 는 그대로 된다)")]
    public bool 켤때자동 = true;

    static readonly string[] 마커 = {
        "PlayerLoop", "BehaviourUpdate", "LateBehaviourUpdate", "Camera.Render",
        "Gfx.WaitForPresentOnGfxThread", "Gfx.WaitForRenderThread",
        "Physics.Processing", "Animators.Update", "Culling",
    };

    [Tooltip("이보다 오래 걸린 프레임은 그 자리에서 낱개로 찍는다 (ms)")]
    public float 스파이크문턱 = 80f;

    ProfilerRecorder[] 재개; double[] 합; int 센것;
    float 남은, 대기; bool 재는중;
    double 프레임합; float 최대ms; int 튄33, 튄100;

    // ★스파이크는 **평균이 아니라 낱개로** 봐야 정체가 나온다 — 그 프레임에 무엇이
    //   늘었는지(야생 마릿수·오브젝트 수·GC)를 같이 남긴다. 평균에 섞으면 지워진다.
    ProfilerRecorder gc재개;
    int 지난야생 = -1;
    bool 스파이크감시 = true;

    // ★스파이크는 창 밖에서도 오므로 **레코더를 항상 살려 둔다** — 튄 프레임에서
    //   어느 단계가 부풀었는지(스크립트냐 렌더냐)를 그 자리에서 알아야 정체가 나온다
    ProfilerRecorder 감시_행동, 감시_렌더, 감시_컬링;
    // ★★스파이크 줄에 **LateUpdate 와 애니메이터가 빠져 있었다** (2026-08-11). 우리 시스템
    //   상당수가 LateUpdate 에서 돈다(격자바닥·구름그림자·흩날림·IsoCam·대상표시·조준표시…).
    //   그게 튀어도 로그에는 「스크립트」로 안 잡혀서 62ms 의 정체를 못 봤다.
    ProfilerRecorder 감시_늦행동, 감시_애니, 감시_물리;
    // ★★★**CPU 가 일한 게 아닌 스파이크가 있다** (2026-08-11 실측:
    //   `397ms · Update 1 · LateUpdate 23 · 애니 0 · 렌더 0 · 컬링 0 · GC 14KB`
    //   — 합이 24ms 인데 397ms 다. 373ms 가 어디에도 안 잡힌다).
    //   그러면 남는 건 **GPU 대기**와 **셰이더를 그 자리에서 굽는 것**뿐이다.
    //   에디터는 처음 보는 재질 조합을 만나면 그때 셰이더를 굽는데 수백 ms 가 걸린다.
    ProfilerRecorder 감시_gpu1, 감시_gpu2, 감시_셰이더;

    void OnEnable()
    {
        감시_행동 = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "BehaviourUpdate", 1);
        감시_늦행동 = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "LateBehaviourUpdate", 1);
        감시_애니 = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Animators.Update", 1);
        감시_물리 = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Physics.Processing", 1);
        감시_렌더 = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Camera.Render", 1);
        감시_컬링 = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Culling", 1);
        감시_gpu1 = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Gfx.WaitForPresentOnGfxThread", 1);
        감시_gpu2 = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Gfx.WaitForRenderThread", 1);
        감시_셰이더 = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shader.CreateGPUProgram", 1);
        gc재개 = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
        if (켤때자동) 예약(기다림);
    }
    void OnDisable() { 치우기(); 감시끄기(); 스파이크파일쓰기(); }

    // ★스파이크 줄을 파일에 쌓는다 — 콘솔을 찍어 보내는 왕복이 느리고 잘리기 때문
    readonly System.Collections.Generic.List<string> 스파이크줄 = new System.Collections.Generic.List<string>();

    void 스파이크파일쓰기()
    {
        if (스파이크줄.Count == 0) return;
        try
        {
            var 길 = System.IO.Path.Combine(Application.dataPath, "..", "렉_스파이크.txt");
            System.IO.File.AppendAllText(길, string.Join("\n", 스파이크줄) + "\n");
            스파이크줄.Clear();
        }
        catch { }
    }

    void 예약(float 지연) { 대기 = 지연; 남은 = 0f; }

    void 시작()
    {
        치우기();
        재개 = new ProfilerRecorder[마커.Length];
        for (int i = 0; i < 마커.Length; i++)
            재개[i] = ProfilerRecorder.StartNew(ProfilerCategory.Internal, 마커[i], 1);
        합 = new double[마커.Length];
        센것 = 0; 프레임합 = 0; 최대ms = 0f; 튄33 = 0; 튄100 = 0;
        남은 = Mathf.Max(1f, 잴시간);
        재는중 = true;
    }

    // ── F11: 스크립트를 하나씩 꺼 가며 「그것이 없으면 얼마나 싸지나」를 잰다
    bool 범인찾는중;

    System.Collections.IEnumerator 범인찾기()
    {
        범인찾는중 = true;
        var 후보 = new System.Collections.Generic.List<MonoBehaviour>();
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (mb == null || !mb.enabled || !mb.gameObject.activeInHierarchy || mb is 렉재기) continue;
            var t = mb.GetType();
            var F = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                  | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly;
            if (t.GetMethod("Update", F) == null && t.GetMethod("LateUpdate", F) == null) continue;
            if (mb is Critter) continue;              // 야생은 수가 많아 따로 본다(구간 표시)
            후보.Add(mb);
        }

        Debug.Log("[범인찾기] " + 후보.Count + "개를 하나씩 끕니다 — 약 "
                  + (후보.Count * 2.2f + 2f).ToString("F0") + "초");

        double 기준 = 0;
        var 결과 = new System.Collections.Generic.List<string>();

        // 기준
        yield return 잠깐재기(v => { 기준 = v; 결과.Add(string.Format("기준(전부 켬)  {0,6:F2}ms", v)); });

        for (int i = 0; i < 후보.Count; i++)
        {
            var mb = 후보[i];
            if (mb == null) continue;
            mb.enabled = false;
            double 값 = 0;
            yield return 잠깐재기(v => 값 = v);
            mb.enabled = true;
            double 절약 = 기준 - 값;
            if (절약 > 0.3)
                결과.Add(string.Format("{0,-16} 끄면 {1,6:F2}ms  (절약 {2,6:F2}ms = {3,3:F0}%)",
                        mb.GetType().Name, 값, 절약, 100 * 절약 / Mathf.Max(0.01f, (float)기준)));
        }

        Debug.Log("[범인찾기] 결과 (절약 0.3ms 넘는 것만)\n  " + string.Join("\n  ", 결과));
        // ★★결과를 **파일로도 남긴다** (2026-08-11) — 콘솔은 스크롤해서 찍어 보내야 하는데
        //   그 왕복이 느리고 잘린다. 파일이면 그대로 읽을 수 있다.
        try
        {
            var 길 = System.IO.Path.Combine(Application.dataPath, "..", "렉_범인찾기.txt");
            System.IO.File.WriteAllText(길, "[범인찾기] 결과 (절약 0.3ms 넘는 것만)\n"
                + string.Join("\n", 결과) + "\n");
            Debug.Log("[범인찾기] 파일로도 남겼다 → 렉_범인찾기.txt");
        }
        catch (System.Exception e) { Debug.LogWarning("[범인찾기] 파일 못 씀: " + e.Message); }
        범인찾는중 = false;
    }

    /// 2초 동안 `BehaviourUpdate` 평균을 재서 넘겨준다 (앞 0.5초는 전환 여파라 버린다)
    System.Collections.IEnumerator 잠깐재기(System.Action<double> 받기)
    {
        var r = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "BehaviourUpdate", 1);
        float t = 0f; double 합 = 0; int n = 0;
        while (t < 2.0f)
        {
            t += Time.unscaledDeltaTime;
            if (t > 0.5f && r.Valid) { 합 += r.LastValue * 1e-6; n++; }
            yield return null;
        }
        if (r.Valid) r.Dispose();
        받기(n > 0 ? 합 / n : 0);
    }

    void 감시끄기()
    {
        if (감시_행동.Valid) 감시_행동.Dispose();
        if (감시_늦행동.Valid) 감시_늦행동.Dispose();
        if (감시_애니.Valid) 감시_애니.Dispose();
        if (감시_물리.Valid) 감시_물리.Dispose();
        if (감시_gpu1.Valid) 감시_gpu1.Dispose();
        if (감시_gpu2.Valid) 감시_gpu2.Dispose();
        if (감시_셰이더.Valid) 감시_셰이더.Dispose();
        if (감시_렌더.Valid) 감시_렌더.Dispose();
        if (감시_컬링.Valid) 감시_컬링.Dispose();
        if (gc재개.Valid) gc재개.Dispose();
    }

    void 치우기()
    {
        if (재개 == null) { 재는중 = false; return; }
        for (int i = 0; i < 재개.Length; i++) if (재개[i].Valid) 재개[i].Dispose();
        재개 = null; 재는중 = false;
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var k = UnityEngine.InputSystem.Keyboard.current;
        if (k != null && k.f12Key.wasPressedThisFrame) 시작();
        // ★★F11 = **하나씩 끄며 몫 재기.** 에디터 쪽에 탐침을 꽂아 재려 했더니 컴파일·리로드에
        //   자꾸 죽었다(네 번 놓쳤다). 게임 안에서 코루틴으로 돌면 안 죽는다.
        if (k != null && k.f11Key.wasPressedThisFrame && !범인찾는중) StartCoroutine(범인찾기());
#endif
        float ms = Time.unscaledDeltaTime * 1000f;

        // ★튄 프레임은 그 자리에서 낱개로 — 무엇이 늘었나.
        //   ★★**측정 창 밖에서도 항상 본다** — 끊김은 아무 때나 오므로 8초 창에 안 걸린다.
        if (스파이크감시 && ms > 스파이크문턱)
        {
            int 야생 = 0;
            for (int i = 0; i < Critter.All.Count; i++)
            {
                var c = Critter.All[i];
                if (c != null && c.side == Critter.Side.야생) 야생++;
            }
            double gcKB = gc재개.Valid ? gc재개.LastValue / 1024.0 : -1;
            double u = 감시_행동.Valid ? 감시_행동.LastValue * 1e-6 : 0;
            double lu = 감시_늦행동.Valid ? 감시_늦행동.LastValue * 1e-6 : 0;
            double an = 감시_애니.Valid ? 감시_애니.LastValue * 1e-6 : 0;
            double rd = 감시_렌더.Valid ? 감시_렌더.LastValue * 1e-6 : 0;
            double cu = 감시_컬링.Valid ? 감시_컬링.LastValue * 1e-6 : 0;
            double g1 = 감시_gpu1.Valid ? 감시_gpu1.LastValue * 1e-6 : 0;
            double g2 = 감시_gpu2.Valid ? 감시_gpu2.LastValue * 1e-6 : 0;
            double sh = 감시_셰이더.Valid ? 감시_셰이더.LastValue * 1e-6 : 0;
            // ★★설명 안 되는 시간을 **직접 찍는다** — 이게 크면 CPU 가 일한 게 아니다
            double 나머지 = ms - (u + lu + an + rd + cu + g1 + g2 + sh);
            Debug.LogFormat("[렉스파이크] {0:F0}ms · Update {1:F0} · Late {2:F0} · 애니 {3:F0} · 렌더 {4:F0} · 컬링 {5:F0} · GPU대기 {6:F0}/{7:F0} · 셰이더굽기 {8:F0} · ★설명안됨 {9:F0} · 야생 {10}마리({11}) · GC {12:F0}KB · t={13:F1}s",
                ms, u, lu, an, rd, cu, g1, g2, sh, 나머지,
                야생, 지난야생 < 0 ? "?" : (야생 - 지난야생).ToString("+#;-#;0"), gcKB,
                Time.unscaledTime);
            지난야생 = 야생;
            // ★파일에도 쌓는다 — 콘솔을 찍어 보내는 왕복을 없앤다
            스파이크줄.Add(string.Format("{0,6:F0}ms · Update {1,4:F0} · Late {2,4:F0} · 애니 {3,3:F0} · 렌더 {4,3:F0} · 컬링 {5,3:F0} · GPU {6,3:F0}/{7,3:F0} · 셰이더 {8,3:F0} · 설명안됨 {9,4:F0} · 야생 {10,2}({11}) · GC {12,7:F0}KB · t={13:F1}s",
                ms, u, lu, an, rd, cu, g1, g2, sh, 나머지, 야생,
                지난야생 < 0 ? "?" : "0", gcKB, Time.unscaledTime));
            if (스파이크줄.Count >= 40) 스파이크파일쓰기();
        }
        else if (스파이크감시 && Time.frameCount % 30 == 0)
        {
            int 야생 = 0;
            for (int i = 0; i < Critter.All.Count; i++)
            {
                var c = Critter.All[i];
                if (c != null && c.side == Critter.Side.야생) 야생++;
            }
            지난야생 = 야생;
        }

        if (!재는중)
        {
            if (대기 > 0f) { 대기 -= Time.unscaledDeltaTime; if (대기 <= 0f) 시작(); }
            return;
        }

        프레임합 += ms; 센것++;
        if (ms > 최대ms) 최대ms = ms;
        if (ms > 33f) 튄33++;
        if (ms > 100f) 튄100++;
        for (int i = 0; i < 재개.Length; i++)
            if (재개[i].Valid) 합[i] += 재개[i].LastValue * 1e-6;   // ns → ms

        남은 -= Time.unscaledDeltaTime;
        if (남은 > 0f) return;

        var sb = new StringBuilder();
        double 평균 = 센것 > 0 ? 프레임합 / 센것 : 0;
        sb.AppendFormat("[렉재기] {0}프레임 · 평균 {1:F1}ms ({2:F1}fps) · 최대 {3:F1}ms · 33ms↑ {4}회 · 100ms↑ {5}회\n",
                        센것, 평균, 평균 > 0 ? 1000.0 / 평균 : 0, 최대ms, 튄33, 튄100);
        for (int i = 0; i < 마커.Length; i++)
        {
            double v = 센것 > 0 ? 합[i] / 센것 : 0;
            if (v > 0.05) sb.AppendFormat("   {0,-30} {1,7:F2}ms\n", 마커[i], v);
        }
        // ★야생(Critter) 안쪽 구간 — 어느 일이 무거운지까지 내려간다
        foreach (var 이름 in new[]{ "Critter.비켜서기", "Critter.표적찾기", "Critter.판단", "Critter.행동", "Critter.Squash" })
        {
            var r = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, 이름, 1);
            if (r.Valid && r.LastValue > 0) sb.AppendFormat("   {0,-30} {1,7:F2}ms (마지막 프레임)\n", 이름, r.LastValue * 1e-6);
            if (r.Valid) r.Dispose();
        }
        sb.Append("   (F12 = 다시 재기)");
        Debug.Log(sb.ToString());
        치우기();
    }
}
