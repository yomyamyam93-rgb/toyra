using UnityEngine;

/// 물체도 픽셀 격자 위를 걷게 한다.
///
/// ★왜 (2026-08-04 사용자 — "펫이 움직일 때 음영진 부분이 계속 색이 바뀌면서 자글자글"):
///   카메라만 격자에 맞춰 놨더니, 펫은 여전히 **픽셀 사이를 미끄러지듯** 움직인다.
///   그러면 매 프레임 같은 픽셀이 모델의 **다른 부분**을 집어 와서 색이 끓는다.
///   → 물체의 자리도 **1/픽셀당미터** 단위로 끊어 주면, 픽셀이 통째로 옮겨 다녀
///     같은 자리는 같은 색을 유지한다. 픽셀아트 게임이 원래 이렇게 한다.
///
/// ★방향도 끊는다. 옛 도트 게임의 캐릭터가 여덟 방향·열여섯 방향만 보던 이유가 이거다 —
///   조금씩 돌면 음영이 끊임없이 변해서 그림이 안 선다.
///
/// 자리를 끊는 것은 **보이는 몸**이 아니라 실제 위치다. 0.1m 단위라 이동·충돌에 지장이 없고,
/// 오히려 걸음이 또박또박해진다.
[DefaultExecutionOrder(320)]     // 카메라 스냅(200) 다음
public class PixelSnapper : MonoBehaviour
{
    [Tooltip("물체의 자리를 픽셀 격자에 맞춘다")] public bool 자리끊기 = true;
    // ★방향 끊기는 여기서 하지 않는다. 다 돌아간 결과를 밀면 로직과 싸워서 진자처럼
    //   흔들린다 (헤드뱅잉). 방향은 `Critter.Face` / `Hero.FaceQuantized` 가
    //   **목표 단계에서** 끊는다. 이 값은 그쪽 규칙을 정하는 데만 쓴다.
    [Tooltip("바라보는 방향을 몇 갈래로 끊나 (0 이면 안 끊는다). 16 = 22.5도씩")]
    [Range(0, 64)] public int 방향수 = 16;
    [Tooltip("사람도 같이 끊는다")] public bool 사람도 = true;

    PixelScreen 화면;
    Camera cam;

    void Start()
    {
        cam = Camera.main;
        화면 = cam != null ? cam.GetComponent<PixelScreen>() : null;
        Critter.방향수 = 방향수;      // 방향 끊기 규칙을 알려 준다
    }

    void LateUpdate()
    {
        if (cam == null) return;
        if (화면 == null) 화면 = cam.GetComponent<PixelScreen>();
        if (화면 == null || !화면.켬) return;

        // ★줌을 얹은 값을 쓴다 — 줌이 연속으로 바뀌므로 격자도 같이 따라와야 한다
        float ppu = Mathf.Max(0.5f, 화면.유효픽셀당미터);
        var right = cam.transform.right;
        var up = cam.transform.up;

        for (int i = 0; i < Critter.All.Count; i++)
        {
            var c = Critter.All[i];
            if (c == null) continue;
            끊기(c.transform, ppu, right, up);
        }

        if (사람도 && Hero.Me != null) 끊기(Hero.Me.transform, ppu, right, up);
    }

    // ★★남는 거리를 다음 프레임으로 넘긴다 (2026-08-04 — 이게 없으면 이동이 아예 안 된다).
    //   걷기 2.6m/s 면 한 프레임에 0.043m 인데 격자가 0.1m 라, 그냥 반올림하면
    //   **매 프레임 제자리로 되돌아가** 영영 못 간다. 잘라낸 만큼을 들고 있다가
    //   다음 프레임에 도로 더해 주면, 실제 이동은 그대로면서 그림만 격자 위에 선다.
    class 나머지 { public Vector3 자리; }
    readonly System.Collections.Generic.Dictionary<int, 나머지> 남은것
        = new System.Collections.Generic.Dictionary<int, 나머지>();

    /// ★그 물체의 **끊기 전 진짜 자리** (2026-08-04). 카메라가 이걸 따라가야 한다 —
    ///   끊긴 자리를 따라가면 한 칸(=1픽셀)씩 튀는 걸 그대로 좇아서 **두두두둑 끊긴다.**
    public Vector3 진짜자리(Transform t)
    {
        int id = t.GetInstanceID();
        return 남은것.TryGetValue(id, out var n) ? t.position + n.자리 : t.position;
    }

    void 끊기(Transform t, float ppu, Vector3 right, Vector3 up)
    {
        int id = t.GetInstanceID();
        if (!남은것.TryGetValue(id, out var n)) 남은것[id] = n = new 나머지();

        if (자리끊기)
        {
            var p = t.position + n.자리;                 // 지난번에 잘라낸 몫을 돌려준다
            float a = Vector3.Dot(p, right), b = Vector3.Dot(p, up);
            float sa = Mathf.Round(a * ppu) / ppu;
            float sb = Mathf.Round(b * ppu) / ppu;

            // ★★위아래로는 **절대 안 민다** (2026-08-04 사용자 "가만히 서있을때 위아래로
            //   엄청나게 떨리는 버그"). 예전엔 `up` 을 그대로 밀었는데, 내려보는 각도가 40°라
            //   `up` 의 월드 Y 성분이 0.77 이나 된다 → 물체를 **실제로 공중에 띄운다.**
            //   그런데 `Hero.Update` 는 매 프레임 `pos.y = 0` 으로 되돌린다. 띄운 만큼이
            //   나머지에 남았는데 자리는 0 으로 돌아가니, 나머지가 영영 안 줄고 매 프레임
            //   다시 띄웠다 내렸다 한다 — 그게 떨림의 정체다. 0.05m 를 60번/초 왕복했다.
            //
            // ★그래도 화면에서는 정확히 픽셀 위에 선다. 화면 세로 = `dot(자리, up)` 인데,
            //   높이가 고정이면 **땅 위에서 옆으로 밀어도 같은 값을 만들 수 있기** 때문이다.
            //   `up` 을 땅에 눕힌 방향으로 밀되, 눕히면서 짧아진 만큼(= sin(내려보는각))
            //   나눠서 보정한다. 세로 픽셀 정렬은 그대로, 몸은 땅에 붙어 있다.
            var 눕힌up = new Vector3(up.x, 0f, up.z);
            float 길이 = 눕힌up.magnitude;
            var 세로밀기 = Vector3.zero;
            if (길이 > 1e-4f)
            {
                눕힌up /= 길이;
                float 몫 = Vector3.Dot(눕힌up, up);      // = sin(내려보는각). 40°면 0.643
                if (몫 > 1e-3f) 세로밀기 = 눕힌up * ((sb - b) / 몫);
            }
            // `right` 는 카메라에 기울임(roll)이 없어 이미 수평이다 — 그대로 써도 안 뜬다
            var snapped = p + right * (sa - a) + 세로밀기;

            n.자리 = p - snapped;                        // 이번에 잘라낸 몫을 들고 있는다
            t.position = snapped;
        }

        // ★방향은 **나머지를 넘기지 않는다.** 자리는 쌓여야 앞으로 가지만, 방향은
        //   쌓을 것이 없다. 넘겼더니 두 칸 사이를 계속 오가며 **헤드뱅잉**을 했다
        //   (2026-08-04 사용자).
        //
        // ★대신 **버티기**를 넣는다: 지금 보고 있는 칸에서 한 칸의 60% 넘게 벗어나야
        //   다음 칸으로 넘어간다. 이게 없으면 두 칸 경계에서 덜덜 떤다.
        // 방향은 여기서 안 건드린다 (위 주석 참고) — 목표 단계에서 이미 끊겨 있다
    }
}
