using UnityEngine;

/// 종 — **몸과 기질을 한 자리에 모은다.**
///
/// ★좀비와 동물의 차이가 전부 「기질」에 들어 있다 (2026-08-03 사용자 지적).
///   좀비는 겁이 없고 도망 안 가고 무한히 쫓아온다. 동물은 그 셋이 전부 반대다.
///   그래서 `겁` · `공격성` · `영역반경` 이 종 데이터의 1급 시민이다 — 나중에 붙이는
///   깃발이 아니라, 행동이 여기서 나온다.
[System.Serializable]
public class SpeciesDef
{
    public enum 번식식 { 알, 태생 }
    public enum 활동 { 언제나, 낮, 밤 }

    [Header("이름")]
    public string 이름 = "늑대";

    [Header("몸")]
    [Tooltip("★모델 — 비우면 색칠한 상자로 나온다. 넣으면 그게 나온다")]
    public GameObject 모델;
    [Tooltip("모델이 엉뚱한 쪽을 보면 여기서 돌린다 (°)")] public float 모델회전 = 0f;
    [Tooltip("키 (m) — 사람은 1.8")] public float 키 = 1.2f;
    [Tooltip("장애물·서로에게 걸리는 반지름 (m)")] public float 반지름 = 0.4f;
    [Tooltip("★무게 — 밀기·넉백에 얼마나 버티나. 사람은 1. 티라는 8쯤")]
    public float 무게 = 1f;

    [Header("싸움")]
    public float 체력 = 24f;
    public float 이속 = 3.4f;
    public float 피해 = 4f;
    [Tooltip("몇 초에 한 번 때리나")] public float 간격 = 0.9f;
    [Tooltip("몸 표면에서 이만큼 안이면 때린다 (m)")] public float 사거리 = 1.2f;

    [Header("감각")]
    [Tooltip("이 거리 안을 본다 (m)")] public float 시야 = 20f;
    [Tooltip("보는 각도 (°) — 뒤는 못 본다")] public float 시야각 = 200f;
    [Tooltip("뛰는 소리를 이 거리에서 알아챈다 (m)")] public float 청각 = 30f;

    [Header("★기질 — 좀비와 갈리는 자리")]
    [Tooltip("0 = 겁이 없다 · 1 = 겁이 아주 많다. 높을수록 일찍 달아난다")]
    [Range(0f, 1f)] public float 겁 = 0.5f;
    [Tooltip("0 = 먼저 안 덤빈다 · 1 = 보면 무조건 덤빈다")]
    [Range(0f, 1f)] public float 공격성 = 0.5f;
    [Tooltip("★영역 — 제 자리에서 이만큼 벗어나면 포기하고 돌아간다 (m). 좀비는 무한 추격")]
    public float 영역 = 45f;

    [Tooltip("★묶어 놓고 안 먹이면 이 시간(초)에 굶어 죽는다")] public float 굶는시간 = 240f;

    [Header("무리")]
    public int 무리최소 = 4, 무리최대 = 8;
    [Tooltip("이 비율만큼 새끼가 섞인다 (태생만)")] [Range(0f, 0.5f)] public float 새끼비율 = 0.25f;

    [Header("생태")]
    public 번식식 번식 = 번식식.태생;
    public 활동 활동시간 = 활동.언제나;

    /// 새끼는 어미의 축소판 — 작고 약하고 잘 놀란다. 대신 **잘 길든다**
    public SpeciesDef 새끼로()
    {
        return new SpeciesDef
        {
            이름 = 이름 + " 새끼",
            모델 = 모델, 모델회전 = 모델회전,        // 같은 모델을 작게 쓴다
            키 = 키 * 0.45f, 반지름 = 반지름 * 0.55f, 무게 = 무게 * 0.25f,
            체력 = 체력 * 0.35f, 이속 = 이속 * 1.05f, 피해 = 피해 * 0.3f,
            간격 = 간격, 사거리 = 사거리 * 0.6f,
            시야 = 시야, 시야각 = 시야각, 청각 = 청각,
            겁 = Mathf.Clamp01(겁 + 0.45f),          // 새끼는 훨씬 잘 놀란다
            공격성 = 공격성 * 0.2f,
            영역 = 영역,
            무리최소 = 1, 무리최대 = 1,
            번식 = 번식, 활동시간 = 활동시간
        };
    }
}
