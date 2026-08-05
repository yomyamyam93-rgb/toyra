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

            TEXTURE2D(_BaseMap);   SAMPLER(sampler_BaseMap);
            TEXTURE2D(_GroundMap); SAMPLER(sampler_GroundMap);

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

                half3 albedo = ground * _Tint.rgb * tex.rgb;

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
    }

    FallBack "Universal Render Pipeline/Lit"
}
