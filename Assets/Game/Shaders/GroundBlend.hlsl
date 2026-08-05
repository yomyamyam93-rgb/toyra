// 땅 사진 배합 — **땅과 풀이 반드시 같은 식을 쓰게** 하는 공용 파일 (2026-08-05).
//
// ★★왜 파일로 뺐나: 사용자 "잔디 색이 땅 텍스처 색상을 못 따라가네".
//   풀은 자기 발밑 땅색을 따라가야 하는데, 땅이 사진 배합으로 바뀌면서 풀만 옛 팔레트를
//   보고 있었다. 고치려면 **풀도 똑같은 배합을 계산**해야 한다 — 그런데 두 셰이더에
//   식을 각각 적어 두면 **한쪽만 고치는 날 조용히 어긋난다.** 그래서 한 군데에 둔다.
//
// ★풀은 사진을 **안 찍는다.** 사진마다 미리 재둔 **평균색**만 섞는다:
//     · 풀잎 하나는 화면에서 몇 픽셀이라 사진 결이 보일 자리가 없다
//     · 텍스처 뽑기가 0번이라 수만 포기를 그려도 공짜다
//     · 풀이 따라가야 하는 건 결이 아니라 **그 자리의 땅 톤**이다

#ifndef TOYRA_GROUND_BLEND_INCLUDED
#define TOYRA_GROUND_BLEND_INCLUDED

#define PHOTO_MAX 16

// ── 노이즈 밭 (값 노이즈 2옥타브)
float ToyraHash21(float2 p)
{
    p = frac(p * float2(127.31, 311.7));
    p += dot(p, p + 34.23);
    return frac(p.x * p.y);
}

float ToyraField1(float2 p)
{
    float2 i = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = ToyraHash21(i), b = ToyraHash21(i + float2(1, 0));
    float c = ToyraHash21(i + float2(0, 1)), d = ToyraHash21(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float ToyraField2(float2 p)
{
    // ★2.3 배 — 정확히 2배로 겹치면 무늬가 되풀이돼 보인다 (길 규칙과 같은 이유)
    // ★잔 겹의 몫은 0.22 — 큰 겹만 넓히고 이걸 두면 잔 겹이 갈아타기를 도맡아
    //   결국 비슷하게 자주 바뀐다 (2026-08-05 "너무 자주 바뀌지 않게").
    return ToyraField1(p) * 0.78 + ToyraField1(p * 2.3 + 19.7) * 0.22;
}

/// 그 무리의 자리 무게 — 0 잔디 · 1 흙 · 2 돌
float ToyraSlot(int g, float3 slots)
{
    return (g == 0) ? slots.x : ((g == 1) ? slots.y : slots.z);
}

/// 사진 한 장의 가중치. prm = (주파수 1/m, 오프셋x, 오프셋z, 편향)
float ToyraWeight(float2 wp, float4 prm, float slot)
{
    if (slot <= 0.001) return 0.0;
    return (ToyraField2(wp * prm.x + prm.yz) + prm.w) * slot;
}

/// 꼭대기 근처만 색에 낀다 — 그 밖은 0.
/// ★「제일 진한 둘만 섞기」로 하면 2등과 3등이 뒤바뀌는 자리에서 색이 뚝 끊긴다.
///   이 방식은 그 자리에서 둘 다 0 에 가까워 끊길 것이 없다.
float ToyraContrib(float w, float peak, float band)
{
    float c = w - (peak - band);
    return (c <= 0.0) ? 0.0 : c * c;      // 제곱 — 꼭대기 쪽에 힘을 실어 준다
}

// ★★배합 **루프**는 공유하지 않는다 — 배열을 함수 인자로 넘기면 유니티 셰이더
//   프리프로세서가 삼킨다 (2026-08-05 "Unexpected directive" 로 컴파일이 깨졌다).
//   그래서 위의 스칼라 함수 넷만 나눠 쓰고, 루프는 각자 일곱 줄씩 적는다.
//   ★중요한 건 루프가 아니라 **식**이다 — 식이 여기 한 군데뿐이면 둘은 안 어긋난다.

#endif
