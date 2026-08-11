// 잔디 포기 셰이더 — **자기 발밑 땅 그림을 직접 본다** (2026-08-05 사용자 —
// "잔디가 왜 묶여있어? 묶지 말아줄래? 그냥 잔디하나당 그아래 땅을 보게해줘").
//
// ★전에는 잔디를 **톤별로 묶어서** 그렸다 (여섯 칸 + 흙 + 모래). 묶으면 한 칸 안의 풀이
//   전부 같은 색이라, 땅색이 스르르 변하는 자리에서 풀만 계단처럼 끊긴다.
//   → 묶지 않는다. 풀이 **자기 세계 좌표로 땅 그림을 찍어** 그 색을 쓴다.
//     칸도, 평균도, 분류도 필요 없다. 땅이 변하면 풀도 그대로 따라간다.
//
// ★풀 그림은 **흰색**이다 (`GrassField.흰색으로`). 색은 100% 땅이 정한다.
// ★빛은 땅과 **같은 방식**으로 받는다 — 그래야 풀과 땅이 같은 밝기로 붙는다.
//   법선을 위쪽으로 고정하는 이유도 그것이다 (판때기가 서 있어도 땅처럼 밝게).
Shader "Toyra/Grass"
{
    Properties
    {
        _BaseMap("풀 그림 (흰색)", 2D) = "white" {}
        _GroundMap("땅 그림", 2D) = "white" {}
        _WorldSize("세계 한 변 (m)", Float) = 1440
        _Tint("밝기", Color) = (1,1,1,1)
        _Cutoff("자르기", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
        LOD 100
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #include "GroundBlend.hlsl"      // ★배합 식은 땅과 **같은 파일**을 쓴다

            TEXTURE2D(_BaseMap);   SAMPLER(sampler_BaseMap);
            TEXTURE2D(_GroundMap); SAMPLER(sampler_GroundMap);

            // ★전역으로 받는다 — `WorldGen.사진꽂기` 가 놓아 준다. 이름 앞의 `_G` 는
            //   흔한 이름을 전역에 놓아 다른 셰이더까지 물들이는 것을 막는 표식이다.
            TEXTURE2D(_GMaskMap);  SAMPLER(sampler_GMaskMap);
            TEXTURE2D(_GRockMap);  SAMPLER(sampler_GRockMap);
            TEXTURE2D_ARRAY(_GPhotoArr); SAMPLER(sampler_GPhotoArr);
            float4 _GPhotoParams[PHOTO_MAX];
            float4 _GPhotoGroup[PHOTO_MAX];
            float4 _GPhotoAvg[PHOTO_MAX];
            float  _GPhotoNum, _GPhotoBand, _GPhotoTiling, _GPhotoMip, _GPhotoShade;

            // ★밑동을 어둡게 (2026-08-05 사용자 "잔디를 아랫부분은 살짝 어둡게 처리해줘도되고").
            //   풀끼리 진짜 그림자를 주고받는 것보다 훨씬 싸면서 **바닥에 박힌 느낌**을 낸다 —
            //   실제로 풀 밑동은 서로 가려서 어둡다. 이펙트가 아니라 몸이 하는 일이다.
            float _GRootDark;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Tint;
                float  _WorldSize;
                float  _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float  fogCoord   : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogCoord = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                clip(tex.a - _Cutoff);

                // ★★발밑 땅을 찍는다 — 세계 좌표를 그대로 땅 그림의 uv 로 쓴다.
                //   땅 그림은 (0,0)~(세계한변,세계한변) 을 0~1 로 덮으므로 이대로 맞는다.
                float2 guv = IN.positionWS.xz / max(1.0, _WorldSize);
                half3 ground = SAMPLE_TEXTURE2D(_GroundMap, sampler_GroundMap, guv).rgb;

                // ★★땅이 사진을 쓰면 풀도 **같은 배합**을 계산한다 (2026-08-05 사용자
                //   "잔디 색이 땅 텍스처 색상을 못 따라가네"). 팔레트로 칠한 `_GroundMap` 은
                //   더 이상 땅색이 아니다 — 물에서만 맞는다.
                // ★사진을 **흐린 밉**으로 찍는다 — 결은 안 따라오고 색만 따라온다.
                if (_GPhotoNum > 0.5)
                {
                    half4 mask = SAMPLE_TEXTURE2D(_GMaskMap, sampler_GMaskMap, guv);
                    half rock  = SAMPLE_TEXTURE2D(_GRockMap, sampler_GRockMap, guv).r;
                    float3 slots = float3(mask.r * (1.0h - rock), mask.g + mask.b, mask.r * rock);

                    // 땅과 같은 식 (`GroundBlend.hlsl`). 루프만 각자 적는다
                    float2 wp = IN.positionWS.xz;
                    int n = (int)_GPhotoNum;
                    float w[PHOTO_MAX];
                    float peak = 0.0;
                    [loop] for (int i = 0; i < n; i++)
                    {
                        w[i] = ToyraWeight(wp, _GPhotoParams[i], ToyraSlot((int)_GPhotoGroup[i].x, slots));
                        peak = max(peak, w[i]);
                    }
                    // ★★사진을 **흐린 밉으로 직접 찍는다.** 평균색 한 덩어리를 쓰면 한 지역이
                    //   통째로 납작한 한 색이 되어, 결이 살아 있는 땅 옆에서 **계단처럼**
                    //   읽힌다 (2026-08-05 사용자 "뚝뚝 끊어지면서 색이 변해서").
                    //   흐린 밉이면 결은 안 보이면서 자리마다 색이 이어진다.
                    float2 puv = wp * _GPhotoTiling;
                    half3 sum3 = 0.0h; float wsum = 0.0;
                    [loop] for (int j = 0; j < n; j++)
                    {
                        float c = ToyraContrib(w[j], peak, _GPhotoBand);
                        if (c <= 0.0) continue;
                        float s = _GPhotoGroup[j].y;
                        sum3 += SAMPLE_TEXTURE2D_ARRAY_LOD(_GPhotoArr, sampler_GPhotoArr,
                                                           puv * s, j, _GPhotoMip).rgb * c;
                        wsum += c;
                    }
                    half3 tone = (wsum > 0.0) ? sum3 / wsum : ground;

                    // ★★큰 명암을 **땅과 똑같이** 얹는다. 이게 빠져 있어서 땅은 그라디언트로
                    //   흐르는데 풀만 평평했다 — 어긋남의 절반은 이것이었다.
                    float shade = ToyraField2(wp * 0.008 + 71.3);
                    tone *= 1.0h + (shade - 0.5h) * 2.0h * _GPhotoShade;

                    // 물 위에는 풀이 안 나지만, 나더라도 칠한 색을 따르게 둔다
                    ground = lerp(ground, tone, mask.a);
                }

                // ★밑동은 어둡게, 끝으로 갈수록 밝게 (uv.y = 0 이 바닥)
                half rootFade = lerp(1.0h - saturate(_GRootDark), 1.0h, saturate(IN.uv.y));

                half3 albedo = ground * _Tint.rgb * tex.rgb * rootFade;

                // ── 빛 (땅과 같은 계산). 법선은 위쪽 고정 — 서 있는 판때기라도 땅처럼 밝게
                float3 n = float3(0, 1, 0);
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndl = saturate(dot(n, mainLight.direction));
                half3 lit = mainLight.color * (ndl * mainLight.shadowAttenuation);
                half3 ambient = SampleSH(n);

                half3 col = albedo * (lit + ambient);
                col = MixFog(col, IN.fogCoord);
                return half4(col, 1.0h);
            }
            ENDHLSL
        }

        // ★그림자 드리우기 (`GrassField.그림자드리우기`) — 이 패스가 없으면 스위치를 켜도
        //   아무 일도 안 일어난다. 풀 모양대로 잘라야 그림자가 네모로 지지 않는다.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Tint;
                float  _WorldSize;
                float  _Cutoff;
            CBUFFER_END

            struct ShIn  { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct ShOut { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            ShOut shadowVert(ShIn IN)
            {
                ShOut OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                float3 wp = TransformObjectToWorld(IN.positionOS.xyz);
                float3 wn = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS = TransformWorldToHClip(ApplyShadowBias(wp, wn, _MainLightPosition.xyz));
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 shadowFrag(ShOut IN) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        // 깊이 — 시야 셰이더가 읽는다. 풀 모양대로 잘라야 네모로 안 찍힌다
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Tint;
                float  _WorldSize;
                float  _Cutoff;
            CBUFFER_END

            struct DepthIn
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct DepthOut { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            DepthOut depthVert(DepthIn IN)
            {
                DepthOut OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 depthFrag(DepthOut IN) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a;
                clip(a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        // ★★★**법선 패스가 없으면 화면에 흰 덩어리가 뜬다** (2026-08-11 사용자 "나무에 하얀
        //   빛나오는버그 또생겼어"). 이 프로젝트의 SSAO 는 `Source = Depth Normals` 라
        //   (실측: PC_Renderer.asset 의 `Source: 1`), 이 패스가 없는 물체는 법선 텍스처에
        //   **아무것도 안 적고** SSAO 가 그 자리에서 쓰레기 값을 읽어 **흰색**으로 터진다.
        //   ☆2026-08-09 에 `단색나무` 만 고쳤는데, 나무 **밑동에 나는 풀**(`GrassField`)이
        //     같은 병을 앓고 있었다 — 그래서 "나무에서" 다시 나온 것이다.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex normVert
            #pragma fragment normFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Tint;
                float  _WorldSize;
                float  _Cutoff;
            CBUFFER_END

            struct NormIn
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct NormOut
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            NormOut normVert(NormIn IN)
            {
                NormOut OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 normFrag(NormOut IN) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a;
                clip(a - _Cutoff);
                return half4(normalize(IN.normalWS), 0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
