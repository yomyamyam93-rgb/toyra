// 픽셀 화면 — 저해상도로 그린 화면을 확대하면서 픽셀아트로 마감한다.
//
// ★여기서 하는 일 (업계에서 쓰는 층을 순서대로):
//   ② 서브픽셀 보정 — 카메라를 픽셀 격자에 스냅하고 남은 소수점만큼 여기서 밀어준다.
//      이걸 안 하면 걸어다닐 때 화면이 지글거린다(픽셀 크롤). 아마추어와 갈리는 자리.
//   ⑤ 외곽선 — 이웃 픽셀과 밝기·색이 크게 다르면 어둡게. **저해상도 단계**라 1픽셀로 나온다
//   ⑥ 명암 끊기 — 밝기를 몇 단으로 계단지게 (셀 셰이딩 흉내)
//   ③ 색 양자화 — 색을 단계로 제한한다. 「저해상도」보다 이쪽이 픽셀 인상을 더 만든다
//   ④ 디더링 — 색을 줄이면 생기는 띠를 베이어 점무늬로 메운다
//
// ①(저해상도 렌더)과 ②의 스냅 계산은 `PixelScreen.cs` 가 한다.
Shader "Toyra/PixelDisplay"
{
    Properties
    {
        _MainTex ("화면", 2D) = "white" {}
        _Mask ("보이는 것 마스크", 2D) = "black" {}
        _ObjMask ("물체만 마스크", 2D) = "black" {}
        _Offset ("서브픽셀 밀기 (uv)", Vector) = (0,0,0,0)
        _Levels ("색 단계 (채널당)", Float) = 16
        _Dither ("디더링", Range(0,1)) = 0.6
        _Bands ("명암 단계 (0 = 안 씀)", Float) = 0
        _OutlineW ("외곽선 두께 (픽셀)", Float) = 1
        _OutlineT ("외곽선 문턱", Range(0.01,1)) = 0.18
        _OutlineS ("외곽선 세기", Range(0,1)) = 0.65
        // 두 물체를 「다른 것」으로 칠 최소 값 차이. 한 물체 안에서는 차이가 정확히 0 이라
        // 아주 작아도 된다 — 크게 잡으면 값이 비슷한 이웃끼리 도로 합쳐진다
        _IdGap ("물체 구분 문턱", Range(0.002,0.2)) = 0.01
        _Sat ("채도", Range(0,2)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" "RenderPipeline"="UniversalPipeline" }
        Cull Off  ZWrite Off  ZTest Always
        Blend One Zero

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
            TEXTURE2D(_Mask);     SAMPLER(sampler_Mask);      // 보이는 것 (물체+잔디)
            TEXTURE2D(_ObjMask);  SAMPLER(sampler_ObjMask);   // 물체만
            float4 _MainTex_TexelSize;
            float4 _Offset;
            float _Levels, _Dither, _Bands, _OutlineW, _OutlineT, _OutlineS, _Sat, _IdGap;
            float _ShowMask;    // 1 = 마스크를 그대로 보여준다 (어긋남 진단용)
            float _SilT;        // (안 씀 — 마스크 방식으로 바뀜)
            float _UseDepth;    // 1 = 마스크로 실루엣만 · 0 = 색으로 모든 경계
            float _OutlineIn;   // 0 = 물체 바깥에 두른다 · 1 = 안쪽에 그린다

            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; float4 col : COLOR; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            V vert(A i)
            {
                V o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.uv = i.uv;
                return o;
            }

            float3 Grab(float2 uv) { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb; }
            float Lum(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

            // 베이어 4×4 — 색을 줄일 때 생기는 띠를 점무늬로 흩는다
            float Bayer(float2 p)
            {
                const float m[16] = {
                     0, 8, 2,10,
                    12, 4,14, 6,
                     3,11, 1, 9,
                    15, 7,13, 5
                };
                int2 q = int2(fmod(p, 4.0));
                return m[q.y * 4 + q.x] / 16.0 - 0.5;
            }

            half4 frag(V i) : SV_Target
            {
                // ② 서브픽셀 — 카메라가 버린 소수점만큼 화면을 되민다
                float2 uv = i.uv + _Offset.xy;
                float2 px = uv * _MainTex_TexelSize.zw;      // 저해상도 픽셀 좌표
                float3 c = Grab(uv);

                // 진단 — 마스크를 그대로 본다. 이걸 켜고 물체 모양과 견주면
                // 「어긋난 것인지 그늘인지」가 한눈에 갈린다
                if (_ShowMask > 0.5)
                {
                    float mm = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).r;
                    // 배경=검정 · 잔디=초록 · 물체=흰색
                    float3 dbg = mm < 0.02 ? float3(0,0,0)
                              : (mm < 0.07 ? float3(0.2,0.6,0.2) : float3(1,1,1));
                    return half4(dbg, 1);
                }

                // ⑤ 외곽선 — **한 칸 두께의 새까만 선.** 판정만 먼저 하고, 칠하는 건
                //    색 양자화가 끝난 **맨 마지막**에 한다. 중간에 칠하면 양자화가
                //    검정을 도로 다른 색으로 밀어 테두리에 엉뚱한 색이 낀다.
                float edge = 0.0;
                if (_OutlineS > 0.001)
                {
                    float2 t = _MainTex_TexelSize.xy * max(1.0, _OutlineW);

                    if (_UseDepth > 0.5)
                    {
                        // ★★실루엣만 — **마스크**로 잡는다 (2026-08-04 사용자 "테두리만").
                        //   물체를 전부 흰색 한 덩어리로 따로 찍은 그림이라, 안쪽 경계가
                        //   **아예 존재하지 않는다.** 그 덩어리의 가장자리만 선이 된다.
                        float m  = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).r;
                        float ml = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv + float2(-t.x, 0)).r;
                        float mr = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv + float2( t.x, 0)).r;
                        float md = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv + float2(0, -t.y)).r;
                        float mu = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv + float2(0,  t.y)).r;
                        // ★테두리는 **물체 바깥**에 두른다 (2026-08-04 사용자).
                        //   안쪽에 그리면 물체의 한 칸을 먹어서 모양이 갉인다.
                        //   바깥: 나는 배경인데 이웃이 물체면 → 여기가 테두리
                        //   안쪽: 나는 물체인데 이웃이 배경이면 → 여기가 테두리
                        // ★HLSL 은 한글 변수 이름을 못 쓴다 — 여기 넣었다가 셰이더가 통째로
                        //   깨져 화면이 자홍색이 됐다. 주석에만 한글을 쓴다.
                        // 배경은 0, 잔디는 0.05, 물체는 저마다 다른 값(0.1 이상).
                        //
                        // ★★잔디는 「가려 주는 것」이지 「테두리를 두를 것」이 아니다.
                        //   그래서 잔디를 **아예 셈에서 뺀다**: 잔디 픽셀에는 선을 안 긋고,
                        //   잔디를 물체로도 배경으로도 치지 않는다.
                        //   → 잔디가 앞을 지나면 그 자리엔 선이 없다 = 가려진 것처럼 보인다.
                        //
                        // ★이웃까지 「잔디면 선 금지」로 했더니, 잔디가 빽빽해지자 펫 테두리가
                        //   **통째로 사라졌다** (2026-08-04). 검사는 **그 픽셀 자신**에 대해서만.
                        const float eps = 0.02;
                        #define ISGRASS(v) (step(0.03, v) * step(v, 0.07))
                        #define ISOBJ(v)   (step(eps, v) * (1.0 - ISGRASS(v)))

                        float mObj = ISOBJ(m);
                        float nObj = max(max(ISOBJ(ml), ISOBJ(mr)), max(ISOBJ(md), ISOBJ(mu)));

                        // ★★규칙은 하나뿐이다: **물체의 보이는 가장자리 한 칸을 검게.**
                        //
                        //   나는 물체인가? 그리고 이웃 넷 중 하나라도 「나와 다른 것」인가?
                        //   → 그렇다면 여기가 가장자리다.
                        //
                        //   이 한 규칙이 세 가지를 한꺼번에 푼다 (2026-08-04):
                        //   ①배경과 만나는 자리 ②다른 물체와 겹치는 자리 ③**잔디에 가려진 자리**
                        //     — 가려진 곳은 마스크에서 이미 내가 지워져 있으니(깊이가 처리한다)
                        //       나 자신이 아니라 잔디다 → 애초에 그을 것이 없다.
                        //
                        //   ★두께가 언제나 정확히 한 칸이다. 배경 쪽에 그리던 방식은
                        //     배경이 잔디냐 아니냐에 따라 있다 없다 해서 들쭉날쭉했다.
                        //   ★단, **값이 큰 쪽만** 긋는다. 서로 다르다는 것만 보면 겹친 자리에서
                        //     양쪽이 각자 그어 **선이 두 겹**이 된다 (2026-08-04 사용자).
                        //     배경(0)·잔디(0.05)는 물체보다 값이 작으므로 물체가 긋고,
                        //     물체끼리는 값이 큰 쪽 하나만 긋는다 → 언제나 한 겹.
                        //   ★★마스크 두 장을 견준다 (2026-08-04):
                        //     · `_Mask`   = 보이는 것 (물체 + 잔디)
                        //     · `_ObjMask`= 물체만
                        //
                        //     ① **윤곽은 「물체만」으로** 찾는다 → 잔디가 옆에 있어도
                        //        테두리가 멀쩡히 나온다
                        //     ② **긋기는 「보이는 것」이 나일 때만** 한다 → 잔디가 앞을
                        //        가린 자리는 내가 안 보이므로 선이 안 그어진다
                        //
                        //     한 장으로는 「가린 것」과 「옆에 있는 것」을 구분할 수 없어서,
                        //     빼면 테두리가 통째로 사라지고 넣으면 발밑이 새까매졌다.
                        float b  = SAMPLE_TEXTURE2D(_ObjMask, sampler_ObjMask, uv).r;
                        float bl = SAMPLE_TEXTURE2D(_ObjMask, sampler_ObjMask, uv + float2(-t.x, 0)).r;
                        float br = SAMPLE_TEXTURE2D(_ObjMask, sampler_ObjMask, uv + float2( t.x, 0)).r;
                        float bd = SAMPLE_TEXTURE2D(_ObjMask, sampler_ObjMask, uv + float2(0, -t.y)).r;
                        float bu = SAMPLE_TEXTURE2D(_ObjMask, sampler_ObjMask, uv + float2(0,  t.y)).r;

                        // ★★테두리는 **물체 바깥 한 칸**에 그린다 (2026-08-04 사용자
                        //   "니가 모델 안쪽까지 테두리를 넣으니까 그런 거잖아").
                        //
                        //   기하를 부풀리는 방식은 뼈대 있는 몸에서 껍질이 안쪽으로 삐져나와
                        //   몸에 검은 점이 잔뜩 박혔다. 화면에서 한 칸 밖에 그리면 그런 일이 없다.
                        //
                        //   조건: 나는 물체가 아니고(바깥), 이웃 중에 **지금 보이는** 물체가 있다.
                        //   「보이는」을 이웃마다 따지는 게 요점이다 — 잔디가 앞을 가린 이웃은
                        //   빼야 발밑에 선이 안 생긴다.
                        #define VIS(bn, mn) (step(eps, bn) * step(abs((mn) - (bn)), eps))
                        float nearVisible = max(max(VIS(bl, ml), VIS(br, mr)),
                                                max(VIS(bd, md), VIS(bu, mu)));
                        float outEdge = step(b, eps) * nearVisible;

                        // ★★물체끼리 맞닿은 자리 (2026-08-04 사용자 "팻들 겹칠때 아웃라인
                        //   합쳐지는 것"). 위 「바깥선」은 **내가 배경일 때만** 그리므로,
                        //   펫 둘이 딱 붙으면 사이에 배경 픽셀이 없어 선이 아예 안 생긴다 —
                        //   그래서 둘이 한 덩어리로 보였다.
                        //
                        //   마스크에는 물체마다 다른 값(`Outliner._Id`)이 이미 찍혀 있었는데
                        //   **그 값을 서로 견주는 곳이 없었다.** 값이 이웃과 다르면 거기가
                        //   두 물체의 경계다. 한 물체 안에서는 값이 정확히 같으므로 안쪽에는
                        //   선이 안 생긴다 — 예전에 문제였던 「모델 안쪽까지 선이 낀다」가
                        //   구조적으로 일어날 수 없다.
                        //
                        //   ★값이 **더 큰** 이웃일 때만 그린다. 양쪽에서 다 그리면 경계에
                        //     선이 두 겹 앉아 두 배로 굵어진다. 한쪽만 그려야 딱 한 칸이다.
                        // ★매크로는 **한 줄로** 쓴다. `\` 로 줄을 이었더니 파일이 CRLF 라
                        //   "Unexpected directive" 로 셰이더가 통째로 안 컴파일됐다 (분홍 화면).
                        #define OTHER(bn, mn) (step(eps, bn) * step(abs((mn) - (bn)), eps) * step(_IdGap, (bn) - b))
                        float otherNear = max(max(OTHER(bl, ml), OTHER(br, mr)),
                                              max(OTHER(bd, md), OTHER(bu, mu)));
                        // 나도 물체이고, 잔디에 가려지지 않았을 때만
                        float gapEdge = step(eps, b) * step(abs(m - b), eps) * otherNear;

                        edge = max(outEdge, gapEdge);
                    }
                    else
                    {
                        // 색으로 잡기 — 모든 경계에 선이 생긴다 (원하면 쓰는 길)
                        float3 l = Grab(uv + float2(-t.x, 0));
                        float3 d = Grab(uv + float2(0, -t.y));
                        float e = max(abs(Lum(c) - Lum(l)), abs(Lum(c) - Lum(d)));
                        float h = max(length(c - l), length(c - d));
                        edge = step(_OutlineT, max(e, h * 0.7));
                    }
                }

                // ⑥ 명암 끊기 — 밝기만 계단지게 하고 색조는 지킨다
                if (_Bands >= 2.0)
                {
                    // ★칸의 **가운데** 값을 쓴다. 반올림하면 제일 어두운 칸이 0 이 되어
                    //   어두운 면이 **순수 검정**으로 떨어진다 (2026-08-04 — 상자 윗면이
                    //   새까맣게 나왔다). 가운데를 쓰면 가장 어두운 칸도 0 이 아니다.
                    float L = Lum(c);
                    float q = (floor(L * _Bands) + 0.5) / _Bands;
                    c *= (L > 1e-4) ? (q / L) : 1.0;
                }

                // 채도 — 「환경은 가라앉고 생물은 산다」를 화면 단에서도 한 번 더
                if (abs(_Sat - 1.0) > 0.001)
                {
                    float g = Lum(c);
                    c = lerp(float3(g, g, g), c, _Sat);
                }

                // ③④ 색 양자화 + 디더링
                if (_Levels >= 2.0)
                {
                    float d = Bayer(px) * _Dither / _Levels;
                    c = floor(saturate(c + d) * _Levels + 0.5) / _Levels;
                }

                // 맨 마지막에 새까맣게 — 양자화가 다시 손대지 못하게
                c = lerp(c, float3(0, 0, 0), edge * _OutlineS);

                return half4(saturate(c), 1.0);
            }
            ENDHLSL
        }
    }
}
